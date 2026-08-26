using System;

namespace EndangeredAR.AI
{
    internal static class CharacterMemoryDialogueResolver
    {
        public static AIResponse CreateDeterministicResponse(
            AIRequest request,
            ReadOnlyCharacterMemoryContext context,
            MemoryMentionMode mentionMode)
        {
            var response = new AIResponse
            {
                animalId = request?.animalId,
                source = "memory_deterministic",
                routeReason = mentionMode == MemoryMentionMode.ConversationHistoryBoundary
                    ? "deterministic_conversation_history_boundary"
                    : "deterministic_memory_recall",
                answerMode = CharacterMemoryAnswerBuilder.MemoryRecallAnswerMode,
                evidenceStatus = "not_required",
                GroundingTopic = GroundingTopic.None,
                GroundedFactIds = Array.Empty<string>(),
                ActionSuggestion = mentionMode == MemoryMentionMode.ExplicitRecall
                    ? AIActionPolicy.SelectDeterministicIntent(request?.message, request?.animalId)
                    : AIAction.None,
                citations = Array.Empty<AICitation>()
            };
            ApplyDeterministicReply(response, mentionMode, context);
            AttachSnapshot(response, mentionMode, context, string.Empty);
            return response;
        }

        public static AIResponse PrepareReunionResponse(
            AIRequest request,
            AIResponse response,
            ReadOnlyCharacterMemoryContext context)
        {
            response ??= new AIResponse();
            var providerTail = response.reply ?? string.Empty;
            response.animalId = request?.animalId;
            response.reply = CharacterMemoryAnswerBuilder.BuildReunion(context, providerTail);
            response.source = string.IsNullOrWhiteSpace(response.source) ? "memory_deterministic" : response.source;
            response.routeReason = string.IsNullOrWhiteSpace(response.routeReason)
                ? "memory_reunion_safe_reply"
                : response.routeReason;
            response.answerMode = "social_chat";
            response.evidenceStatus = "not_required";
            response.missionHint = null;
            response.GroundingTopic = GroundingTopic.None;
            response.GroundedFactIds = Array.Empty<string>();
            response.citations = Array.Empty<AICitation>();
            AttachSnapshot(response, MemoryMentionMode.Reunion, context, providerTail);
            return response;
        }

        public static AIResponse Refresh(
            AIResponse response,
            string animalId,
            string originalMessage,
            ReadOnlyCharacterMemoryContext currentContext)
        {
            if (response == null || response.MemoryMentionMode == MemoryMentionMode.None)
            {
                return response;
            }

            var currentMode = MemoryMentionPolicy.Classify(originalMessage);
            if (currentMode != response.MemoryMentionMode ||
                currentContext == null ||
                !string.Equals(currentContext.AnimalId, animalId, StringComparison.Ordinal))
            {
                currentContext = ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }

            if (string.Equals(
                    response.MemoryContextFingerprint,
                    currentContext.Fingerprint,
                    StringComparison.Ordinal))
            {
                return response;
            }

            response.animalId = animalId;
            if (response.MemoryMentionMode == MemoryMentionMode.Reunion)
            {
                response.reply = CharacterMemoryAnswerBuilder.BuildReunion(
                    currentContext,
                    response.ReunionProviderTail);
            }
            else
            {
                ApplyDeterministicReply(response, response.MemoryMentionMode, currentContext);
            }

            AttachSnapshot(
                response,
                response.MemoryMentionMode,
                currentContext,
                response.ReunionProviderTail);
            return response;
        }

        private static void ApplyDeterministicReply(
            AIResponse response,
            MemoryMentionMode mentionMode,
            ReadOnlyCharacterMemoryContext context)
        {
            response.reply = mentionMode == MemoryMentionMode.ConversationHistoryBoundary
                ? CharacterMemoryAnswerBuilder.BuildConversationHistoryBoundary()
                : CharacterMemoryAnswerBuilder.BuildExplicitRecall(context);
        }

        private static void AttachSnapshot(
            AIResponse response,
            MemoryMentionMode mentionMode,
            ReadOnlyCharacterMemoryContext context,
            string providerTail)
        {
            response.MemoryMentionMode = mentionMode;
            response.MemoryContextFingerprint = context?.Fingerprint ?? string.Empty;
            response.ReunionProviderTail = providerTail ?? string.Empty;
        }
    }
}
