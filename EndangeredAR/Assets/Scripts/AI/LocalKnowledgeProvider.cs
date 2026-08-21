using System;
using System.Collections;
using System.Collections.Generic;
using EndangeredAR.Animals;
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
                suggestedQuestions = answer.SuggestedQuestions,
                answerMode = answer.AnswerMode,
                evidenceStatus = answer.EvidenceStatus,
                citations = ResolveCitations(request == null ? null : request.knowledgeProfile, answer.SourceIds)
            });
        }

        internal static AICitation[] ResolveCitations(AnimalKnowledgeProfile profile, string[] sourceIds)
        {
            if (profile == null || sourceIds == null || sourceIds.Length == 0)
            {
                return Array.Empty<AICitation>();
            }

            var sourcesById = new Dictionary<string, AnimalKnowledgeSource>(StringComparer.Ordinal);
            foreach (var source in profile.Sources)
            {
                if (source != null && !string.IsNullOrWhiteSpace(source.SourceId))
                {
                    sourcesById[source.SourceId] = source;
                }
            }

            var citations = new List<AICitation>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sourceId in sourceIds)
            {
                if (string.IsNullOrWhiteSpace(sourceId) || !seen.Add(sourceId) || !sourcesById.TryGetValue(sourceId, out var source))
                {
                    continue;
                }

                citations.Add(new AICitation
                {
                    sourceId = source.SourceId,
                    title = source.Title,
                    organization = source.Organization,
                    url = source.Url
                });
            }

            return citations.ToArray();
        }
    }
}
