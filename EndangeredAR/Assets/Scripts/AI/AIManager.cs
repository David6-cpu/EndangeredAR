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
                onSuccess?.Invoke(CharacterMemoryDialogueResolver.CreateDeterministicResponse(
                    request,
                    memoryContext,
                    mentionMode));
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
                onSuccess?.Invoke(mentionMode == MemoryMentionMode.Reunion
                    ? CharacterMemoryDialogueResolver.PrepareReunionResponse(request, response, memoryContext)
                    : response);
            };
            Action<AIProviderError> routeError = error =>
            {
                if (mentionMode == MemoryMentionMode.Reunion)
                {
                    onSuccess?.Invoke(CharacterMemoryDialogueResolver.PrepareReunionResponse(
                        request,
                        new AIResponse
                        {
                            animalId = request?.animalId,
                            reply = SafeReunionTailGuard.FallbackTail,
                            source = "unity_memory",
                            routeReason = "memory_reunion_provider_unavailable",
                            ActionSuggestion = AIAction.None
                        },
                        memoryContext));
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
