using System;
using System.Collections;
using UnityEngine;

namespace EndangeredAR.AI
{
    public sealed class AIRouter
    {
        private const string FinalErrorCode = "all_providers_failed";
        private const string FinalErrorMessage = "No AI provider could answer the request.";

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

            switch (mode)
            {
                case AIRouteMode.LocalOnly:
                    var localOnlyAttempt = new ProviderAttempt();
                    yield return TryProvider(local, request, localTimeout, localOnlyAttempt);
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

                    route.CompleteError(FinalError());
                    yield break;

                case AIRouteMode.LocalFirstCloudFallback:
                    var startTime = Now();
                    var initialLocalTimeout = Mathf.Min(localTimeout, totalTimeout);
                    var localFirstAttempt = new ProviderAttempt();
                    yield return TryProvider(local, request, initialLocalTimeout, localFirstAttempt);
                    if (TryCompleteSuccess(route, local, localFirstAttempt, "local_first"))
                    {
                        yield break;
                    }

                    var remainingBudget = RemainingBudget(startTime, totalTimeout);
                    if (remainingBudget > 0f)
                    {
                        var cloudAttempt = new ProviderAttempt();
                        yield return TryProvider(cloud, request, remainingBudget, cloudAttempt);
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

                    route.CompleteError(FinalError());
                    yield break;

                case AIRouteMode.CloudOnly:
                default:
                    var cloudOnlyAttempt = new ProviderAttempt();
                    yield return TryProvider(cloud, request, totalTimeout, cloudOnlyAttempt);
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

                    route.CompleteError(FinalError());
                    yield break;
            }
        }

        private IEnumerator TryKnowledge(AIRequest request, ProviderAttempt attempt)
        {
            yield return TryProvider(knowledge, request, 0f, attempt);
        }

        private IEnumerator TryProvider(IAIProvider provider, AIRequest request, float timeoutSeconds, ProviderAttempt result)
        {
            if (provider == null)
            {
                result.CompleteError(new AIProviderError("provider_unavailable", "Provider is unavailable.", false));
                yield break;
            }

            IEnumerator routine;
            try
            {
                routine = provider.Send(
                    request,
                    timeoutSeconds,
                    response => result.CompleteSuccess(response),
                    error => result.CompleteError(error));
            }
            catch (Exception)
            {
                result.CompleteError(new AIProviderError("provider_exception", "Provider request failed.", false));
                yield break;
            }

            if (result.IsComplete)
            {
                yield break;
            }

            if (routine == null)
            {
                result.CompleteError(new AIProviderError("provider_no_response", "Provider did not respond.", false));
                yield break;
            }

            yield return routine;
            if (!result.IsComplete)
            {
                result.CompleteError(new AIProviderError("provider_no_response", "Provider did not respond.", false));
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

        private float RemainingBudget(float startTime, float totalTimeout)
        {
            return Mathf.Max(0f, totalTimeout - Mathf.Max(0f, Now() - startTime));
        }

        private float Now()
        {
            return realtimeClock();
        }

        private static float ClampTimeout(float timeoutSeconds)
        {
            return Mathf.Max(0f, timeoutSeconds);
        }

        private static AIProviderError FinalError()
        {
            return new AIProviderError(FinalErrorCode, FinalErrorMessage, false);
        }

        private sealed class ProviderAttempt
        {
            public AIResponse Response { get; private set; }
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
