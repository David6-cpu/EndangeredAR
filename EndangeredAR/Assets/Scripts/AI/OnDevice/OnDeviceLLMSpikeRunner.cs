using System;

namespace EndangeredAR.AI.OnDevice
{
    public sealed class OnDeviceLLMSpikeRunner : IDisposable
    {
        public const string FixedPrompt = "用一句中文介绍你自己。";
        public const string GeneratorId = "on_device_llm";
        public const string ModelFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";

        private readonly IOnDeviceLLMNativeBackend backend;
        private readonly int contextSize;
        private readonly int threadCount;
        private readonly int maxTokens;
        private bool completionConsumed;

        public OnDeviceLLMSpikeRunner(
            IOnDeviceLLMNativeBackend backend,
            int contextSize,
            int threadCount,
            int maxTokens)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (contextSize < 256 || threadCount < 1 || maxTokens < 1 || maxTokens > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(contextSize));
            }

            this.contextSize = contextSize;
            this.threadCount = threadCount;
            this.maxTokens = maxTokens;
            Status = backend.IsSupported ? "ready_to_load" : "unsupported";
        }

        public string Status { get; private set; }
        public string Generator { get; private set; } = string.Empty;
        public string Output { get; private set; } = string.Empty;
        public string Error { get; private set; } = string.Empty;
        public OnDeviceLLMMetrics Metrics { get; private set; } = OnDeviceLLMMetrics.Empty;

        public bool StartLoad(string modelPath)
        {
            ClearResult();
            if (!backend.IsSupported || !backend.StartLoad(
                    modelPath,
                    contextSize,
                    threadCount,
                    256,
                    256))
            {
                Status = "load_rejected";
                Error = backend.ReadError();
                return false;
            }

            Status = "loading";
            return true;
        }

        public bool StartFixedPrompt()
        {
            ClearResult();
            if (backend.State != OnDeviceLLMNativeState.Ready &&
                backend.State != OnDeviceLLMNativeState.Completed &&
                backend.State != OnDeviceLLMNativeState.Cancelled)
            {
                Status = "generation_rejected";
                return false;
            }

            var request = new OnDeviceLLMRequest(
                "spike_fixed_prompt",
                new[]
                {
                    new OnDeviceChatMessage("system", "你是森森，请用简短、友好的中文回答。"),
                    new OnDeviceChatMessage("user", FixedPrompt)
                },
                maxTokens,
                0.7f,
                0.8f,
                1.0f,
                0xC0DEC0DEu);
            if (!backend.StartGenerate(
                    request.SerializeMessages(),
                    request.MaxTokens,
                    request.Temperature,
                    request.TopP,
                    request.RepeatPenalty,
                    request.Seed))
            {
                Status = "generation_rejected";
                Error = backend.ReadError();
                return false;
            }

            completionConsumed = false;
            Status = "generating";
            return true;
        }

        public void Poll()
        {
            switch (backend.State)
            {
                case OnDeviceLLMNativeState.Unsupported:
                    Status = "unsupported";
                    Error = backend.ReadError();
                    break;
                case OnDeviceLLMNativeState.Uninitialized:
                    Status = "ready_to_load";
                    break;
                case OnDeviceLLMNativeState.Loading:
                    Status = "loading";
                    break;
                case OnDeviceLLMNativeState.Ready:
                    Status = "ready";
                    Metrics = backend.ReadMetrics();
                    break;
                case OnDeviceLLMNativeState.Generating:
                    Status = "generating";
                    break;
                case OnDeviceLLMNativeState.Completed:
                    CompleteOnce();
                    break;
                case OnDeviceLLMNativeState.Cancelled:
                    Status = "cancelled";
                    Generator = string.Empty;
                    Output = string.Empty;
                    break;
                case OnDeviceLLMNativeState.Error:
                    Status = "error";
                    Generator = string.Empty;
                    Output = string.Empty;
                    Error = backend.ReadError();
                    break;
            }
        }

        public void Cancel()
        {
            backend.Cancel();
        }

        public void OnApplicationPause(bool paused)
        {
            if (paused &&
                (backend.State == OnDeviceLLMNativeState.Loading ||
                 backend.State == OnDeviceLLMNativeState.Generating))
            {
                backend.Cancel();
            }
        }

        public void Dispose()
        {
            backend.Dispose();
        }

        private void CompleteOnce()
        {
            if (completionConsumed)
            {
                return;
            }

            completionConsumed = true;
            var output = backend.ReadOutput();
            Metrics = backend.ReadMetrics();
            if (string.IsNullOrWhiteSpace(output))
            {
                Status = "error";
                Error = "empty_native_completion";
                Generator = string.Empty;
                Output = string.Empty;
                return;
            }

            Status = "completed";
            Error = string.Empty;
            Output = output;
            Generator = GeneratorId;
        }

        private void ClearResult()
        {
            Generator = string.Empty;
            Output = string.Empty;
            Error = string.Empty;
            Metrics = OnDeviceLLMMetrics.Empty;
            completionConsumed = false;
        }
    }
}
