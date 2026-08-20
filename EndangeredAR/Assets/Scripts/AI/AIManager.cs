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

        public IEnumerator Send(
            AIRequest request,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
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
    }
}
