using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndangeredAR.AI
{
    public sealed class AIRouter
    {
        private const string ProviderTimeoutCode = "provider_timeout";
        private const string ProviderTimeoutMessage = "Provider request timed out.";

        private readonly IAIProvider local;
        private readonly IAIProvider cloud;
        private readonly IAIProvider knowledge;
        private readonly Func<float> realtimeClock;

        public AIRouter(
            IAIProvider local,
            IAIProvider cloud,
            IAIProvider knowledge,
            Func<float> realtimeClock = null)
        {
            this.local = local;
            this.cloud = cloud;
            this.knowledge = knowledge;
            this.realtimeClock = realtimeClock ?? (() => Time.realtimeSinceStartup);
        }

        public IEnumerator Route(
            AIRequest request,
            AIRouteMode mode,
            float localTimeoutSeconds,
            float totalTimeoutSeconds,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            var route = new RouteCompletion(onSuccess, onError);
            var localTimeout = ClampTimeout(localTimeoutSeconds);
            var totalTimeout = ClampTimeout(totalTimeoutSeconds);
            var routeStartedAt = Now();
            var routeDeadline = routeStartedAt + totalTimeout;

            switch (mode)
            {
                case AIRouteMode.LocalOnly:
                case AIRouteMode.LocalFirstCloudFallback:
                    var localAttempt = new ProviderAttempt();
                    var localDeadline = Mathf.Min(Now() + localTimeout, routeDeadline);
                    yield return TryProvider(local, request, localDeadline, true, localAttempt);
                    if (TryCompleteSuccess(route, local, localAttempt, "local_only", mode, routeStartedAt, localAttempt))
                    {
                        yield break;
                    }

                    route.CompleteError(AttachErrorProvenance(
                        UnavailableError("local_model_unavailable", localAttempt),
                        mode,
                        routeStartedAt,
                        localAttempt));
                    yield break;

                case AIRouteMode.CloudOnly:
                default:
                    var cloudOnlyAttempt = new ProviderAttempt();
                    yield return TryProvider(cloud, request, routeDeadline, true, cloudOnlyAttempt);
                    if (TryCompleteSuccess(route, cloud, cloudOnlyAttempt, "cloud_only", mode, routeStartedAt, cloudOnlyAttempt))
                    {
                        yield break;
                    }

                    route.CompleteError(AttachErrorProvenance(
                        UnavailableError("cloud_model_unavailable", cloudOnlyAttempt),
                        mode,
                        routeStartedAt,
                        cloudOnlyAttempt));
                    yield break;
            }
        }

        private IEnumerator TryProvider(
            IAIProvider provider,
            AIRequest request,
            float deadline,
            bool enforceDeadline,
            ProviderAttempt result)
        {
            result.ProviderId = provider == null ? string.Empty : provider.ProviderId;
            if (provider == null)
            {
                result.CompleteError(new AIProviderError("provider_unavailable", "Provider is unavailable.", false));
                yield break;
            }

            if (enforceDeadline && HasExpired(deadline))
            {
                result.CompleteError(TimeoutError());
                yield break;
            }

            IEnumerator routine;
            try
            {
                routine = provider.Send(
                    request,
                    enforceDeadline ? Mathf.Max(0f, deadline - Now()) : 0f,
                    response => CompleteProviderSuccess(result, response, deadline, enforceDeadline),
                    error => CompleteProviderError(result, error, deadline, enforceDeadline));
            }
            catch (Exception)
            {
                result.CompleteError(new AIProviderError("provider_exception", "Provider request failed.", false));
                yield break;
            }

            if (result.IsComplete)
            {
                Dispose(routine);
                yield break;
            }

            if (routine == null)
            {
                result.CompleteError(new AIProviderError("provider_no_response", "Provider did not respond.", false));
                yield break;
            }

            while (true)
            {
                if (enforceDeadline && HasExpired(deadline))
                {
                    result.CompleteError(TimeoutError());
                    Dispose(routine);
                    yield break;
                }

                bool hasNext;
                try
                {
                    hasNext = routine.MoveNext();
                }
                catch (Exception)
                {
                    result.CompleteError(new AIProviderError("provider_exception", "Provider request failed.", false));
                    Dispose(routine);
                    yield break;
                }

                if (enforceDeadline && HasExpired(deadline))
                {
                    result.CompleteError(TimeoutError());
                    Dispose(routine);
                    yield break;
                }

                if (result.IsComplete)
                {
                    Dispose(routine);
                    yield break;
                }

                if (!hasNext)
                {
                    result.CompleteError(new AIProviderError("provider_no_response", "Provider did not respond.", false));
                    Dispose(routine);
                    yield break;
                }

                if (routine.Current != null)
                {
                    result.CompleteError(new AIProviderError("provider_unobservable_yield", "Provider yielded unsupported work.", false));
                    Dispose(routine);
                    yield break;
                }

                yield return null;
            }
        }

        private bool TryCompleteSuccess(
            RouteCompletion route,
            IAIProvider provider,
            ProviderAttempt attempt,
            string reason,
            AIRouteMode mode,
            float routeStartedAt,
            params ProviderAttempt[] attempts)
        {
            if (attempt.Response == null || string.IsNullOrWhiteSpace(attempt.Response.reply))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(attempt.Response.source) &&
                AIFinalSourceProtocol.TryParseExact(provider.ProviderId, out _))
            {
                attempt.Response.source = provider.ProviderId;
            }

            if (!AIFinalSourceProtocol.TryParseExact(attempt.Response.source, out var finalSource) ||
                !MatchesProvider(finalSource, provider?.ProviderId) ||
                !MatchesLanguageGenerator(finalSource, attempt.Response.LanguageGenerator))
            {
                attempt.RejectResponse(new AIProviderError(
                    "invalid_final_source",
                    "Provider returned an unsupported final source.",
                    false));
                return false;
            }

            attempt.Response.routeReason = reason;
            ApplyRouteProvenance(attempt.Response, mode, routeStartedAt, attempts);
            route.CompleteSuccess(attempt.Response);
            return true;
        }

        private void ApplyRouteProvenance(
            AIResponse response,
            AIRouteMode mode,
            float routeStartedAt,
            ProviderAttempt[] attempts)
        {
            response.RouteMode = mode;
            var providerAttempts = new List<string>();
            string lastErrorCode = null;
            foreach (var attempt in attempts ?? Array.Empty<ProviderAttempt>())
            {
                if (attempt == null)
                {
                    continue;
                }

                if (attempt.Error != null)
                {
                    AddAttempt(providerAttempts, attempt.ProviderId);
                    lastErrorCode = attempt.Error.Code;
                    continue;
                }

                if (attempt.Response == response)
                {
                    foreach (var providerAttempt in response.ProviderAttempts ?? Array.Empty<string>())
                    {
                        AddAttempt(providerAttempts, providerAttempt);
                    }

                    if (response.ProviderAttempts == null || response.ProviderAttempts.Length == 0)
                    {
                        AddAttempt(providerAttempts, attempt.ProviderId);
                    }
                }
            }

            response.ProviderAttempts = providerAttempts.ToArray();
            response.FallbackUsed = false;
            if (string.IsNullOrEmpty(response.FallbackReasonCode))
            {
                response.FallbackReasonCode = AIProvenanceProtocol.SanitizeReasonCode(lastErrorCode);
            }

            var routeElapsed = Math.Max(0L, (long)Math.Round((Now() - routeStartedAt) * 1000f));
            response.ElapsedMilliseconds = Math.Max(response.ElapsedMilliseconds, routeElapsed);
        }

        private static void AddAttempt(List<string> attempts, string providerId)
        {
            if (string.IsNullOrEmpty(providerId) ||
                (attempts.Count > 0 && string.Equals(attempts[attempts.Count - 1], providerId, StringComparison.Ordinal)))
            {
                return;
            }

            attempts.Add(providerId);
        }

        private AIProviderError AttachErrorProvenance(
            AIProviderError error,
            AIRouteMode mode,
            float routeStartedAt,
            params ProviderAttempt[] attempts)
        {
            var providers = new List<string>();
            foreach (var attempt in attempts ?? Array.Empty<ProviderAttempt>())
            {
                AddAttempt(providers, attempt?.ProviderId);
            }

            error.RouteMode = mode;
            error.ProviderAttempts = providers.ToArray();
            error.ElapsedMilliseconds = Math.Max(0L, (long)Math.Round((Now() - routeStartedAt) * 1000f));
            return error;
        }

        private void CompleteProviderSuccess(ProviderAttempt result, AIResponse response, float deadline, bool enforceDeadline)
        {
            if (enforceDeadline && HasExpired(deadline))
            {
                result.CompleteError(TimeoutError());
                return;
            }

            result.CompleteSuccess(response);
        }

        private void CompleteProviderError(ProviderAttempt result, AIProviderError error, float deadline, bool enforceDeadline)
        {
            if (enforceDeadline && HasExpired(deadline))
            {
                result.CompleteError(TimeoutError());
                return;
            }

            result.CompleteError(error ?? new AIProviderError("provider_failed", "Provider request failed.", false));
        }

        private bool HasExpired(float deadline)
        {
            return Now() >= deadline;
        }

        private float Now()
        {
            return realtimeClock();
        }

        private static float ClampTimeout(float timeoutSeconds)
        {
            return Mathf.Max(0f, timeoutSeconds);
        }

        private static void Dispose(IEnumerator routine)
        {
            var disposable = routine as IDisposable;
            disposable?.Dispose();
        }

        private static AIProviderError TimeoutError()
        {
            return new AIProviderError(ProviderTimeoutCode, ProviderTimeoutMessage, true);
        }

        private static AIProviderError UnavailableError(string code, ProviderAttempt attempt)
        {
            if (attempt?.Error?.Code == "ai_response_validation_failed")
            {
                return new AIProviderError(
                    "ai_response_validation_failed",
                    "The generated response did not pass trusted-context validation.",
                    false);
            }

            return new AIProviderError(
                code,
                "The selected AI language generator is unavailable.",
                attempt?.Error?.IsTimeout == true);
        }

        private static bool MatchesProvider(AIFinalSource source, string providerId)
        {
            return (source == AIFinalSource.LocalLlm && providerId == "local_llm") ||
                   (source == AIFinalSource.CloudLlm && providerId == "cloud_llm");
        }

        private static bool MatchesLanguageGenerator(
            AIFinalSource source,
            LanguageGenerator generator)
        {
            return (source == AIFinalSource.LocalLlm && generator == LanguageGenerator.LocalLlm) ||
                   (source == AIFinalSource.CloudLlm && generator == LanguageGenerator.CloudLlm);
        }

        private sealed class ProviderAttempt
        {
            public string ProviderId { get; set; }
            public AIResponse Response { get; private set; }
            public AIProviderError Error { get; private set; }
            public bool IsComplete { get; private set; }

            public void RejectResponse(AIProviderError error)
            {
                Response = null;
                Error = error;
            }

            public void CompleteSuccess(AIResponse response)
            {
                if (IsComplete)
                {
                    return;
                }

                Response = response;
                IsComplete = true;
            }

            public void CompleteError(AIProviderError error)
            {
                if (IsComplete)
                {
                    return;
                }

                Error = error;
                IsComplete = true;
            }
        }

        private sealed class RouteCompletion
        {
            private readonly Action<AIResponse> onSuccess;
            private readonly Action<AIProviderError> onError;
            private bool isComplete;

            public RouteCompletion(Action<AIResponse> onSuccess, Action<AIProviderError> onError)
            {
                this.onSuccess = onSuccess;
                this.onError = onError;
            }

            public void CompleteSuccess(AIResponse response)
            {
                if (isComplete)
                {
                    return;
                }

                isComplete = true;
                onSuccess?.Invoke(response);
            }

            public void CompleteError(AIProviderError error)
            {
                if (isComplete)
                {
                    return;
                }

                isComplete = true;
                onError?.Invoke(error);
            }
        }
    }
}
