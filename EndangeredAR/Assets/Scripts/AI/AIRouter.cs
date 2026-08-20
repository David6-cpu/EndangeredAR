using System;
using System.Collections;
using UnityEngine;

namespace EndangeredAR.AI
{
    public sealed class AIRouter
    {
        private const string FinalErrorCode = "all_providers_failed";
        private const string FinalErrorMessage = "No AI provider could answer the request.";
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
            var routeDeadline = Now() + totalTimeout;

            switch (mode)
            {
                case AIRouteMode.LocalOnly:
                    var localOnlyAttempt = new ProviderAttempt();
                    var localOnlyDeadline = Mathf.Min(Now() + localTimeout, routeDeadline);
                    yield return TryProvider(local, request, localOnlyDeadline, true, localOnlyAttempt);
                    if (TryCompleteSuccess(route, local, localOnlyAttempt, "local_only"))
                    {
                        yield break;
                    }

                    var localOnlyKnowledgeAttempt = new ProviderAttempt();
                    yield return TryKnowledge(request, localOnlyKnowledgeAttempt);
                    if (TryCompleteSuccess(route, knowledge, localOnlyKnowledgeAttempt, "local_only_knowledge_fallback"))
                    {
                        yield break;
                    }

                    route.CompleteError(FinalError(localOnlyAttempt, localOnlyKnowledgeAttempt));
                    yield break;

                case AIRouteMode.LocalFirstCloudFallback:
                    var initialLocalDeadline = Mathf.Min(Now() + localTimeout, routeDeadline);
                    var localFirstAttempt = new ProviderAttempt();
                    yield return TryProvider(local, request, initialLocalDeadline, true, localFirstAttempt);
                    if (TryCompleteSuccess(route, local, localFirstAttempt, "local_first"))
                    {
                        yield break;
                    }

                    var cloudAttempt = new ProviderAttempt();
                    if (!HasExpired(routeDeadline))
                    {
                        yield return TryProvider(cloud, request, routeDeadline, true, cloudAttempt);
                        if (TryCompleteSuccess(route, cloud, cloudAttempt, "local_first_cloud_fallback"))
                        {
                            yield break;
                        }
                    }

                    var localFirstKnowledgeAttempt = new ProviderAttempt();
                    yield return TryKnowledge(request, localFirstKnowledgeAttempt);
                    if (TryCompleteSuccess(route, knowledge, localFirstKnowledgeAttempt, "local_first_knowledge_fallback"))
                    {
                        yield break;
                    }

                    route.CompleteError(FinalError(localFirstAttempt, cloudAttempt, localFirstKnowledgeAttempt));
                    yield break;

                case AIRouteMode.CloudOnly:
                default:
                    var cloudOnlyAttempt = new ProviderAttempt();
                    yield return TryProvider(cloud, request, routeDeadline, true, cloudOnlyAttempt);
                    if (TryCompleteSuccess(route, cloud, cloudOnlyAttempt, "cloud_only"))
                    {
                        yield break;
                    }

                    var cloudOnlyKnowledgeAttempt = new ProviderAttempt();
                    yield return TryKnowledge(request, cloudOnlyKnowledgeAttempt);
                    if (TryCompleteSuccess(route, knowledge, cloudOnlyKnowledgeAttempt, "cloud_only_knowledge_fallback"))
                    {
                        yield break;
                    }

                    route.CompleteError(FinalError(cloudOnlyAttempt, cloudOnlyKnowledgeAttempt));
                    yield break;
            }
        }

        private IEnumerator TryKnowledge(AIRequest request, ProviderAttempt attempt)
        {
            yield return TryProvider(knowledge, request, 0f, false, attempt);
        }

        private IEnumerator TryProvider(
            IAIProvider provider,
            AIRequest request,
            float deadline,
            bool enforceDeadline,
            ProviderAttempt result)
        {
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

                if (!hasNext)
                {
                    if (!result.IsComplete)
                    {
                        result.CompleteError(new AIProviderError("provider_no_response", "Provider did not respond.", false));
                    }

                    Dispose(routine);
                    yield break;
                }

                if (routine.Current is IEnumerator)
                {
                    result.CompleteError(new AIProviderError("provider_unobservable_yield", "Provider yielded unsupported nested work.", false));
                    Dispose(routine);
                    yield break;
                }

                yield return null;
            }
        }

        private static bool TryCompleteSuccess(RouteCompletion route, IAIProvider provider, ProviderAttempt attempt, string reason)
        {
            if (attempt.Response == null || string.IsNullOrWhiteSpace(attempt.Response.reply))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(attempt.Response.source))
            {
                attempt.Response.source = string.IsNullOrWhiteSpace(provider.ProviderId) ? "unknown_provider" : provider.ProviderId;
            }

            attempt.Response.routeReason = reason;
            route.CompleteSuccess(attempt.Response);
            return true;
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

        private static AIProviderError FinalError(params ProviderAttempt[] attempts)
        {
            foreach (var attempt in attempts)
            {
                if (attempt != null && attempt.Error != null && attempt.Error.IsTimeout)
                {
                    return new AIProviderError(FinalErrorCode, FinalErrorMessage, true);
                }
            }

            return new AIProviderError(FinalErrorCode, FinalErrorMessage, false);
        }

        private sealed class ProviderAttempt
        {
            public AIResponse Response { get; private set; }
            public AIProviderError Error { get; private set; }
            public bool IsComplete { get; private set; }

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
