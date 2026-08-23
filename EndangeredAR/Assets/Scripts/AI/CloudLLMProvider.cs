using System;
using System.Collections;
using EndangeredAR.API;

namespace EndangeredAR.AI
{
    public sealed class CloudLLMProvider : IAIProvider
    {
        private readonly ChatApiClient chatApiClient;

        public CloudLLMProvider(ChatApiClient chatApiClient)
        {
            this.chatApiClient = chatApiClient;
        }

        public string ProviderId => "cloud_llm";

        public IEnumerator Send(
            AIRequest request,
            float timeoutSeconds,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            if (chatApiClient == null)
            {
                onError?.Invoke(new AIProviderError("cloud_unavailable", "Cloud AI is unavailable.", false));
                yield break;
            }

            var callbackCompleted = false;
            IEnumerator routine;
            try
            {
                routine = chatApiClient.SendMessage(
                    request == null ? string.Empty : request.animalId,
                    request == null ? string.Empty : request.message,
                    request == null ? Array.Empty<ChatMessage>() : request.history,
                    timeoutSeconds,
                    response =>
                    {
                        if (callbackCompleted)
                        {
                            return;
                        }

                        callbackCompleted = true;
                        onSuccess?.Invoke(ToAIResponse(response));
                    },
                    error =>
                    {
                        if (callbackCompleted)
                        {
                            return;
                        }

                        callbackCompleted = true;
                        onError?.Invoke(ToProviderError(error));
                    });

            }
            catch (Exception)
            {
                onError?.Invoke(new AIProviderError("cloud_request_failed", "Cloud AI request failed.", false));
                yield break;
            }

            if (routine == null)
            {
                onError?.Invoke(new AIProviderError("cloud_request_failed", "Cloud AI request failed.", false));
                yield break;
            }

            try
            {
                while (true)
                {
                    bool hasNext;
                    object yielded;
                    if (!TryMoveNext(routine, out hasNext, out yielded))
                    {
                        if (!callbackCompleted)
                        {
                            onError?.Invoke(new AIProviderError("cloud_request_failed", "Cloud AI request failed.", false));
                        }

                        yield break;
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    if (yielded != null)
                    {
                        onError?.Invoke(new AIProviderError("cloud_request_failed", "Cloud AI request failed.", false));
                        yield break;
                    }

                    yield return null;
                }

                if (!callbackCompleted)
                {
                    onError?.Invoke(new AIProviderError("cloud_request_failed", "Cloud AI request failed.", false));
                }
            }
            finally
            {
                var disposable = routine as IDisposable;
                disposable?.Dispose();
            }
        }

        internal static AIResponse ToAIResponse(ChatResponse response)
        {
            return new AIResponse
            {
                animalId = response == null ? null : response.animalId,
                reply = response == null ? null : response.reply,
                source = response == null ? null : response.source,
                suggestedQuestions = response == null ? null : response.suggestedQuestions,
                missionHint = response == null ? null : response.missionHint,
                answerMode = response == null ? null : response.answerMode,
                evidenceStatus = response == null ? null : response.evidenceStatus,
                ActionSuggestion = AIActionProtocol.Parse(response == null ? null : response.actionSuggestion),
                citations = MapCitations(response == null ? null : response.citations)
            };
        }

        internal static AICitation[] MapCitations(ChatCitation[] citations)
        {
            if (citations == null || citations.Length == 0)
            {
                return Array.Empty<AICitation>();
            }

            var mapped = new AICitation[citations.Length];
            for (var index = 0; index < citations.Length; index++)
            {
                var citation = citations[index];
                mapped[index] = citation == null
                    ? new AICitation()
                    : new AICitation
                    {
                        sourceId = citation.sourceId,
                        title = citation.title,
                        organization = citation.organization,
                        url = citation.url
                    };
            }

            return mapped;
        }

        private static AIProviderError ToProviderError(string error)
        {
            var isTimeout = !string.IsNullOrWhiteSpace(error) &&
                error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
            return new AIProviderError(
                isTimeout ? "cloud_timeout" : "cloud_request_failed",
                isTimeout ? "Cloud AI request timed out." : "Cloud AI request failed.",
                isTimeout);
        }

        private static bool TryMoveNext(IEnumerator routine, out bool hasNext, out object yielded)
        {
            hasNext = false;
            yielded = null;
            try
            {
                hasNext = routine.MoveNext();
                yielded = hasNext ? routine.Current : null;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
