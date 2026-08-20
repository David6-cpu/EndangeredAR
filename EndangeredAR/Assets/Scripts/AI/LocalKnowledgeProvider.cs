using System;
using System.Collections;
using EndangeredAR.Chat;

namespace EndangeredAR.AI
{
    public sealed class LocalKnowledgeProvider : IAIProvider
    {
        private readonly LocalKnowledgeChatService localKnowledgeService;

        public LocalKnowledgeProvider(LocalKnowledgeChatService localKnowledgeService)
        {
            this.localKnowledgeService = localKnowledgeService;
        }

        public string ProviderId => "unity_knowledge";

        public IEnumerator Send(
            AIRequest request,
            float timeoutSeconds,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            if (localKnowledgeService == null)
            {
                onError?.Invoke(new AIProviderError("knowledge_unavailable", "Local knowledge is unavailable.", false));
                yield break;
            }

            var answer = localKnowledgeService.Answer(request == null ? null : request.knowledgeProfile, request == null ? string.Empty : request.message);
            onSuccess?.Invoke(new AIResponse
            {
                animalId = request == null ? null : request.animalId,
                reply = answer.Reply,
                source = ProviderId,
                suggestedQuestions = answer.SuggestedQuestions
            });
        }
    }
}
