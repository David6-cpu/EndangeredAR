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
            if (request != null)
            {
                request.Context = CreateContextSnapshot(request.animalId);
            }

            var config = aiConfig;
            var localProvider = new LocalLLMProvider(config == null ? null : config.localServerUrl);
            var cloudProvider = chatApiClient == null ? null : new CloudLLMProvider(chatApiClient);
            var knowledgeProvider = new LocalKnowledgeProvider(localKnowledgeService);
            var router = new AIRouter(localProvider, cloudProvider, knowledgeProvider);

            yield return router.Route(
                request,
                config == null ? AIRouteMode.CloudOnly : config.routeMode,
                config == null ? DefaultLocalTimeoutSeconds : config.localTimeoutSeconds,
                config == null ? DefaultTotalTimeoutSeconds : config.totalTimeoutSeconds,
                onSuccess,
                onError);
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
    }
}
