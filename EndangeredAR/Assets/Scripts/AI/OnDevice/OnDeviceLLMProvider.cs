using System;
using System.Collections;
using UnityEngine;

namespace EndangeredAR.AI.OnDevice
{
    public sealed class OnDeviceLLMProvider : IOnDeviceLLMProvider
    {
        public const string OnDeviceGeneratorId = "on_device_llm";

        private readonly IOnDeviceLLMNativeBackend backend;
        private readonly string modelPath;
        private readonly OnDeviceLLMRuntimeConfig runtimeConfig;
        private bool disposed;
        private bool preparing;
        private string activeGenerationId = string.Empty;

        public OnDeviceLLMProvider(
            IOnDeviceLLMNativeBackend backend,
            string modelPath,
            OnDeviceLLMRuntimeConfig runtimeConfig)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException("A bundled model path is required.", nameof(modelPath));
            }

            this.modelPath = modelPath;
            this.runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        }

        public string GeneratorId => OnDeviceGeneratorId;

        public int CountTokens(System.Collections.Generic.IReadOnlyList<OnDeviceChatMessage> messages)
        {
            if (disposed || preparing || !string.IsNullOrEmpty(activeGenerationId) ||
                messages == null || messages.Count == 0 || !backend.IsSupported ||
                !CanGenerate(backend.State))
            {
                return -1;
            }

            try
            {
                return backend.CountTokens(OnDeviceLLMRequest.SerializeMessages(messages));
            }
            catch (ArgumentException)
            {
                return -1;
            }
        }

        public IEnumerator Prepare(
            float timeoutSeconds,
            Action onReady,
            Action<OnDeviceLLMError> onError)
        {
            if (disposed)
            {
                onError?.Invoke(Error("on_device_provider_disposed"));
                yield break;
            }

            if (preparing || !string.IsNullOrEmpty(activeGenerationId))
            {
                onError?.Invoke(Error("on_device_generation_busy"));
                yield break;
            }

            if (!backend.IsSupported)
            {
                onError?.Invoke(Error("on_device_llm_unsupported"));
                yield break;
            }

            preparing = true;
            var startedAt = Time.realtimeSinceStartup;
            var safeTimeout = SafeTimeout(timeoutSeconds);
            try
            {
                if (backend.State == OnDeviceLLMNativeState.Uninitialized &&
                    !backend.StartLoad(
                        modelPath,
                        runtimeConfig.ContextSize,
                        runtimeConfig.ThreadCount,
                        runtimeConfig.BatchSize,
                        runtimeConfig.MicroBatchSize))
                {
                    onError?.Invoke(Error("on_device_model_load_rejected"));
                    yield break;
                }

                while (backend.State == OnDeviceLLMNativeState.Loading)
                {
                    if (HasTimedOut(startedAt, safeTimeout))
                    {
                        backend.Cancel();
                        onError?.Invoke(Error("on_device_model_load_timeout", true));
                        yield break;
                    }

                    yield return null;
                }

                if (!CanGenerate(backend.State))
                {
                    onError?.Invoke(ErrorForNativeState(backend.State, backend.ReadError()));
                    yield break;
                }

                onReady?.Invoke();
            }
            finally
            {
                preparing = false;
            }
        }

        public IEnumerator Send(
            OnDeviceLLMRequest request,
            float timeoutSeconds,
            Action<OnDeviceLLMResult> onSuccess,
            Action<OnDeviceLLMError> onError)
        {
            if (disposed)
            {
                onError?.Invoke(Error("on_device_provider_disposed"));
                yield break;
            }

            if (request == null)
            {
                onError?.Invoke(Error("on_device_request_invalid"));
                yield break;
            }

            if (!string.IsNullOrEmpty(activeGenerationId))
            {
                onError?.Invoke(Error("on_device_generation_busy"));
                yield break;
            }

            if (!backend.IsSupported)
            {
                onError?.Invoke(Error("on_device_llm_unsupported"));
                yield break;
            }

            activeGenerationId = request.GenerationId;
            var startedAt = Time.realtimeSinceStartup;
            var safeTimeout = SafeTimeout(timeoutSeconds);
            try
            {
                if (backend.State == OnDeviceLLMNativeState.Uninitialized)
                {
                    if (!backend.StartLoad(
                            modelPath,
                            runtimeConfig.ContextSize,
                            runtimeConfig.ThreadCount,
                            runtimeConfig.BatchSize,
                            runtimeConfig.MicroBatchSize))
                    {
                        onError?.Invoke(Error("on_device_model_load_rejected"));
                        yield break;
                    }
                }

                while (backend.State == OnDeviceLLMNativeState.Loading)
                {
                    if (HasTimedOut(startedAt, safeTimeout))
                    {
                        backend.Cancel();
                        onError?.Invoke(Error("on_device_model_load_timeout", true));
                        yield break;
                    }

                    yield return null;
                }

                if (!CanGenerate(backend.State))
                {
                    onError?.Invoke(ErrorForNativeState(backend.State, backend.ReadError()));
                    yield break;
                }

                if (!backend.StartGenerate(
                        request.SerializeMessages(),
                        request.MaxTokens,
                        request.Temperature,
                        request.TopP,
                        request.RepeatPenalty,
                        request.Seed))
                {
                    onError?.Invoke(Error("on_device_generation_rejected"));
                    yield break;
                }

                while (backend.State == OnDeviceLLMNativeState.Generating)
                {
                    if (HasTimedOut(startedAt, safeTimeout))
                    {
                        backend.Cancel();
                        onError?.Invoke(Error("on_device_generation_timeout", true));
                        yield break;
                    }

                    yield return null;
                }

                if (backend.State != OnDeviceLLMNativeState.Completed)
                {
                    onError?.Invoke(ErrorForNativeState(backend.State, backend.ReadError()));
                    yield break;
                }

                var output = backend.ReadOutput();
                if (string.IsNullOrWhiteSpace(output))
                {
                    onError?.Invoke(Error("on_device_empty_completion"));
                    yield break;
                }

                onSuccess?.Invoke(new OnDeviceLLMResult(
                    request.GenerationId,
                    output.Trim(),
                    backend.ReadMetrics()));
            }
            finally
            {
                activeGenerationId = string.Empty;
            }
        }

        public void Cancel(string generationId)
        {
            if (!disposed && !string.IsNullOrEmpty(generationId) &&
                string.Equals(activeGenerationId, generationId, StringComparison.Ordinal))
            {
                backend.Cancel();
            }
        }

        public void OnApplicationPause(bool paused)
        {
            if (paused && !disposed && !string.IsNullOrEmpty(activeGenerationId))
            {
                backend.Cancel();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (!string.IsNullOrEmpty(activeGenerationId))
            {
                backend.Cancel();
            }

            activeGenerationId = string.Empty;
            backend.Dispose();
        }

        private static bool CanGenerate(OnDeviceLLMNativeState state)
        {
            return state == OnDeviceLLMNativeState.Ready ||
                   state == OnDeviceLLMNativeState.Completed ||
                   state == OnDeviceLLMNativeState.Cancelled;
        }

        private static bool HasTimedOut(float startedAt, float timeoutSeconds)
        {
            return Time.realtimeSinceStartup - startedAt >= timeoutSeconds;
        }

        private static float SafeTimeout(float timeoutSeconds)
        {
            return float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0f
                ? 1f
                : timeoutSeconds;
        }

        private static OnDeviceLLMError ErrorForNativeState(
            OnDeviceLLMNativeState state,
            string nativeError)
        {
            if (state == OnDeviceLLMNativeState.Cancelled)
            {
                return Error("on_device_generation_cancelled");
            }

            return Error(SanitizeNativeError(nativeError));
        }

        private static string SanitizeNativeError(string value)
        {
            switch (value)
            {
                case "model_load_failed":
                case "context_create_failed":
                case "prompt_prepare_failed":
                case "context_budget_exceeded":
                case "prompt_batch_exceeded":
                case "prompt_decode_failed":
                case "token_decode_failed":
                case "generation_decode_failed":
                    return "on_device_" + value;
                default:
                    return "on_device_native_error";
            }
        }

        private static OnDeviceLLMError Error(string code, bool isTimeout = false)
        {
            return new OnDeviceLLMError(code, "On-device generation failed.", isTimeout);
        }
    }
}
