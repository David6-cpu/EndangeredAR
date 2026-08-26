using System;
using System.Collections;
using System.Collections.Generic;
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
                var animalId = request == null ? string.Empty : request.animalId;
                var message = request == null ? string.Empty : request.message;
                var history = request == null ? Array.Empty<ChatMessage>() : request.history;
                var context = request == null ? ReadOnlyCharacterContext.Empty : request.Context;
                Action<ChatResponse> success = response =>
                {
                    if (callbackCompleted)
                    {
                        return;
                    }

                    callbackCompleted = true;
                    var mapped = ToAIResponse(response, request);
                    if (mapped == null)
                    {
                        onError?.Invoke(new AIProviderError(
                            "cloud_invalid_response",
                            "Cloud AI returned an invalid response.",
                            false));
                        return;
                    }

                    onSuccess?.Invoke(mapped);
                };
                Action<string> failure = error =>
                {
                    if (callbackCompleted)
                    {
                        return;
                    }

                    callbackCompleted = true;
                    onError?.Invoke(ToProviderError(error));
                };
                routine = chatApiClient.SendMessage(
                    animalId,
                    message,
                    history,
                    context,
                    request?.MemoryContext,
                    request == null ? MemoryUseMode.None : request.MemoryUseMode,
                    request == null ? ContentAuthority.None : request.ContentAuthority,
                    timeoutSeconds,
                    success,
                    failure);

            }
            catch (Exception exception)
            {
                onError?.Invoke(RoutineError(exception));
                yield break;
            }

            if (routine == null)
            {
                onError?.Invoke(new AIProviderError("cloud_routine_missing", "Cloud AI request failed.", false));
                yield break;
            }

            try
            {
                while (true)
                {
                    bool hasNext;
                    object yielded;
                    Exception routineException;
                    if (!TryMoveNext(routine, out hasNext, out yielded, out routineException))
                    {
                        if (!callbackCompleted)
                        {
                            onError?.Invoke(RoutineError(routineException));
                        }

                        yield break;
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    if (yielded != null)
                    {
                        onError?.Invoke(new AIProviderError("cloud_opaque_yield", "Cloud AI request failed.", false));
                        yield break;
                    }

                    yield return null;
                }

                if (!callbackCompleted)
                {
                    onError?.Invoke(new AIProviderError("cloud_no_callback", "Cloud AI request failed.", false));
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
            return ToAIResponse(response, null);
        }

        internal static AIResponse ToAIResponse(ChatResponse response, AIRequest request)
        {
            if (response == null ||
                string.IsNullOrWhiteSpace(response.reply) ||
                !AIFinalSourceProtocol.TryParseExact(response.source, out var finalSource) ||
                finalSource != AIFinalSource.CloudLlm ||
                !LanguageGeneratorProtocol.TryParseExact(response.languageGenerator, out var generator) ||
                generator != LanguageGenerator.CloudLlm ||
                !ContentAuthorityProtocol.TryParseExact(response.contentAuthority, out var authority) ||
                (request != null && authority != request.ContentAuthority))
            {
                return null;
            }

            var mapped = new AIResponse
            {
                animalId = response.animalId,
                reply = response.reply,
                source = response.source,
                suggestedQuestions = response.suggestedQuestions,
                missionHint = response.missionHint,
                answerMode = CharacterMemoryTransport.SanitizeExternalAnswerMode(
                    response.answerMode,
                    request == null ? MemoryUseMode.None : request.MemoryUseMode),
                evidenceStatus = response.evidenceStatus,
                GroundingTopic = GroundingTopicProtocol.Parse(response.groundingTopic),
                GroundedFactIds = Copy(response.groundedFactIds),
                ActionSuggestion = AIActionProtocol.Parse(response.actionSuggestion),
                citations = MapCitations(response.citations)
            };
            mapped.ContentAuthority = authority;
            mapped.LanguageGenerator = generator;
            mapped.ProviderAttempts = AIProvenanceProtocol.ParseProviderAttempt(response.providerAttempt);
            mapped.FallbackUsed = response.fallbackUsed;
            mapped.FallbackReasonCode = AIProvenanceProtocol.SanitizeReasonCode(response.fallbackReason);
            mapped.ElapsedMilliseconds = Math.Max(0L, response.elapsedMs);
            return mapped;
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

        internal static string[] Copy(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                {
                    copy.Add(value);
                }
            }

            return copy.ToArray();
        }

        private static AIProviderError ToProviderError(string error)
        {
            var isTimeout = !string.IsNullOrWhiteSpace(error) &&
                error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
            var sanitized = AIProvenanceProtocol.SanitizeReasonCode(error);
            var hasStructuredCode = !string.IsNullOrWhiteSpace(error) &&
                error.StartsWith("cloud_", StringComparison.Ordinal) &&
                string.Equals(error, sanitized, StringComparison.Ordinal);
            var code = isTimeout
                ? "cloud_timeout"
                : hasStructuredCode ? sanitized : "cloud_request_failed";
            return new AIProviderError(
                code,
                isTimeout ? "Cloud AI request timed out." : "Cloud AI request failed.",
                isTimeout);
        }

        private static AIProviderError RoutineError(Exception exception)
        {
            string code;
            if (exception is NullReferenceException)
            {
                code = "cloud_routine_null_reference";
            }
            else if (exception is ArgumentException)
            {
                code = "cloud_routine_argument";
            }
            else if (exception is InvalidOperationException)
            {
                code = "cloud_routine_invalid_operation";
            }
            else if (exception is UnityEngine.UnityException)
            {
                code = "cloud_routine_unity_exception";
            }
            else
            {
                code = "cloud_routine_exception";
            }

            return new AIProviderError(code, "Cloud AI request failed.", false);
        }

        private static bool TryMoveNext(
            IEnumerator routine,
            out bool hasNext,
            out object yielded,
            out Exception exception)
        {
            hasNext = false;
            yielded = null;
            exception = null;
            try
            {
                hasNext = routine.MoveNext();
                yielded = hasNext ? routine.Current : null;
                return true;
            }
            catch (Exception caught)
            {
                exception = caught;
                return false;
            }
        }
    }
}
