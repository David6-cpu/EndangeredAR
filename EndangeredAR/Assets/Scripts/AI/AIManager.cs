using System;
using System.Collections;
using EndangeredAR.API;
using EndangeredAR.Chat;
using UnityEngine;

namespace EndangeredAR.AI
{
    public sealed class AIManager : MonoBehaviour
    {
        private const float DefaultLocalTimeoutSeconds = 8f;
        private const float DefaultTotalTimeoutSeconds = 38f;

        [SerializeField] private AIConfig aiConfig;
        [SerializeField] private ChatApiClient chatApiClient;
        [SerializeField] private LocalKnowledgeChatService localKnowledgeService;
        private IReadOnlyCharacterContextProvider contextProvider;
        private IReadOnlyCharacterMemoryContextProvider memoryContextProvider;

        internal void ConfigureContextProvider(IReadOnlyCharacterContextProvider provider)
        {
            contextProvider = provider;
        }

        internal void ConfigureMemoryContextProvider(IReadOnlyCharacterMemoryContextProvider provider)
        {
            memoryContextProvider = provider;
        }

        public IEnumerator Send(
            AIRequest request,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            var sendStartedAt = Time.realtimeSinceStartup;
            var currentContext = ReadOnlyCharacterContext.Empty;
            if (request != null)
            {
                currentContext = CreateContextSnapshot(request.animalId);
                request.Context = currentContext;
                request.MemoryContext = null;
                request.MemoryUseMode = MemoryUseMode.None;
            }

            var mentionMode = MemoryMentionPolicy.Classify(request?.message);
            ReadOnlyCharacterMemoryContext memoryContext = null;
            if (mentionMode != MemoryMentionMode.None)
            {
                memoryContext = CreateMemoryContextSnapshot(request?.animalId, currentContext);
            }

            if (mentionMode == MemoryMentionMode.ExplicitRecall ||
                mentionMode == MemoryMentionMode.ConversationHistoryBoundary)
            {
                var deterministicResponse = CharacterMemoryDialogueResolver.CreateDeterministicResponse(
                    request,
                    memoryContext,
                    mentionMode);
                deterministicResponse.ProvenanceRouteMode = "deterministic";
                deterministicResponse.ElapsedMilliseconds = ElapsedMilliseconds(sendStartedAt);
                AttachMemoryProvenance(deterministicResponse, mentionMode, memoryContext);
                onSuccess?.Invoke(deterministicResponse);
                yield break;
            }

            if (request != null && mentionMode == MemoryMentionMode.Reunion)
            {
                request.MemoryContext = memoryContext;
                request.MemoryUseMode = MemoryUseMode.Reunion;
            }

            var config = aiConfig;
            var localProvider = new LocalLLMProvider(config == null ? null : config.localServerUrl);
            var cloudProvider = chatApiClient == null ? null : new CloudLLMProvider(chatApiClient);
            var knowledgeProvider = new LocalKnowledgeProvider(localKnowledgeService);
            var router = new AIRouter(localProvider, cloudProvider, knowledgeProvider);

            Action<AIResponse> routeSuccess = response =>
            {
                var finalResponse = mentionMode == MemoryMentionMode.Reunion
                    ? CharacterMemoryDialogueResolver.PrepareReunionResponse(request, response, memoryContext)
                    : response;
                AttachMemoryProvenance(finalResponse, mentionMode, memoryContext);
                onSuccess?.Invoke(finalResponse);
            };
            Action<AIProviderError> routeError = error =>
            {
                if (mentionMode == MemoryMentionMode.Reunion)
                {
                    var fallbackResponse = CharacterMemoryDialogueResolver.PrepareReunionResponse(
                        request,
                        new AIResponse
                        {
                            animalId = request?.animalId,
                            reply = SafeReunionTailGuard.FallbackTail,
                            source = "memory_deterministic",
                            routeReason = "memory_reunion_provider_unavailable",
                            ActionSuggestion = AIAction.None
                        },
                        memoryContext);
                    fallbackResponse.RouteMode = error.RouteMode;
                    fallbackResponse.ProviderAttempts = error.ProviderAttempts ?? Array.Empty<string>();
                    fallbackResponse.FallbackUsed = true;
                    fallbackResponse.FallbackReasonCode = AIProvenanceProtocol.SanitizeReasonCode(error.Code);
                    fallbackResponse.ElapsedMilliseconds = Math.Max(
                        error.ElapsedMilliseconds,
                        ElapsedMilliseconds(sendStartedAt));
                    AttachMemoryProvenance(fallbackResponse, mentionMode, memoryContext);
                    onSuccess?.Invoke(fallbackResponse);
                    return;
                }

                onError?.Invoke(error);
            };

            yield return router.Route(
                request,
                config == null ? AIRouteMode.CloudOnly : config.routeMode,
                config == null ? DefaultLocalTimeoutSeconds : config.localTimeoutSeconds,
                config == null ? DefaultTotalTimeoutSeconds : config.totalTimeoutSeconds,
                routeSuccess,
                routeError);
        }

        private static void AttachMemoryProvenance(
            AIResponse response,
            MemoryMentionMode mentionMode,
            ReadOnlyCharacterMemoryContext memoryContext)
        {
            if (response == null)
            {
                return;
            }

            response.MemoryMentionMode = mentionMode;
            response.ProvenanceMemoryStatus = memoryContext == null
                ? "not_read"
                : CharacterMemoryContextStatusProtocol.ToWireValue(memoryContext.Status);
        }

        private static long ElapsedMilliseconds(float startedAt)
        {
            return Math.Max(0L, (long)Math.Round((Time.realtimeSinceStartup - startedAt) * 1000f));
        }

        internal AIResponse RefreshMemoryDependentResponse(
            AIResponse response,
            string animalId,
            string originalMessage)
        {
            if (response == null || response.MemoryMentionMode == MemoryMentionMode.None)
            {
                return response;
            }

            var currentContext = CreateContextSnapshot(animalId);
            var memoryContext = CreateMemoryContextSnapshot(animalId, currentContext);
            return CharacterMemoryDialogueResolver.Refresh(
                response,
                animalId,
                originalMessage,
                memoryContext);
        }

        private ReadOnlyCharacterContext CreateContextSnapshot(string animalId)
        {
            if (contextProvider == null)
            {
                return ReadOnlyCharacterContext.Empty;
            }

            try
            {
                return contextProvider.CreateSnapshot(animalId) ?? ReadOnlyCharacterContext.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Read-only character context unavailable ({exception.GetType().Name}); continuing without context.", this);
                return ReadOnlyCharacterContext.Empty;
            }
        }

        private ReadOnlyCharacterMemoryContext CreateMemoryContextSnapshot(
            string animalId,
            ReadOnlyCharacterContext currentContext)
        {
            if (memoryContextProvider == null)
            {
                return ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }

            try
            {
                return memoryContextProvider.CreateSnapshot(animalId, currentContext) ??
                       ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Read-only character Memory context unavailable ({exception.GetType().Name}); continuing safely.",
                    this);
                return ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }
        }
    }
}
