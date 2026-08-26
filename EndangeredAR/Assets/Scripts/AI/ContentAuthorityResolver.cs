using System;
using System.Text;
using EndangeredAR.Animals;

namespace EndangeredAR.AI
{
    internal static class ContentAuthorityResolver
    {
        public static ContentAuthority Resolve(AIRequest request, MemoryMentionMode mentionMode)
        {
            if (mentionMode == MemoryMentionMode.ConversationHistoryBoundary)
            {
                return ContentAuthority.SystemPolicy;
            }

            if (mentionMode == MemoryMentionMode.ExplicitRecall || mentionMode == MemoryMentionMode.Reunion)
            {
                return ContentAuthority.CharacterMemory;
            }

            if (IsCurrentTaskQuestion(request?.message))
            {
                return ContentAuthority.CurrentProgress;
            }

            AnimalKnowledgeRetrieval retrieval = null;
            try
            {
                retrieval = request?.knowledgeProfile?.Retrieve(request.message);
            }
            catch (Exception)
            {
                retrieval = null;
            }

            if (retrieval == null)
            {
                return ContentAuthority.None;
            }

            if (retrieval.AnswerMode == "grounded_fact")
            {
                return ContentAuthority.CanonicalKnowledge;
            }

            return retrieval.AnswerMode == "off_domain"
                ? ContentAuthority.SystemPolicy
                : ContentAuthority.None;
        }

        private static bool IsCurrentTaskQuestion(string message)
        {
            var normalized = Normalize(message);
            return normalized.Contains("下一步") ||
                   normalized.Contains("当前任务") ||
                   normalized.Contains("现在的任务") ||
                   (normalized.Contains("任务") &&
                    (normalized.Contains("做什么") || normalized.Contains("怎么做") || normalized.Contains("进度")));
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in (value ?? string.Empty).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
