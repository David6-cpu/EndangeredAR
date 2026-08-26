using System;

namespace EndangeredAR.AI
{
    public enum MemoryUseMode
    {
        None,
        ExplicitRecall,
        HistoryBoundary,
        Reunion
    }

    public static class MemoryUseModeProtocol
    {
        public static bool TryParseExact(string wireValue, out MemoryUseMode mode)
        {
            switch (wireValue)
            {
                case "none":
                    mode = MemoryUseMode.None;
                    return true;
                case "reunion":
                    mode = MemoryUseMode.Reunion;
                    return true;
                case "explicit_recall":
                    mode = MemoryUseMode.ExplicitRecall;
                    return true;
                case "history_boundary":
                    mode = MemoryUseMode.HistoryBoundary;
                    return true;
                default:
                    mode = default;
                    return false;
            }
        }

        public static string ToWireValue(MemoryUseMode mode)
        {
            switch (mode)
            {
                case MemoryUseMode.None:
                    return "none";
                case MemoryUseMode.Reunion:
                    return "reunion";
                case MemoryUseMode.ExplicitRecall:
                    return "explicit_recall";
                case MemoryUseMode.HistoryBoundary:
                    return "history_boundary";
                default:
                    return "none";
            }
        }
    }

    internal static class CharacterMemoryTransport
    {
        public static ReadOnlyCharacterMemoryContext SelectContext(
            string animalId,
            ReadOnlyCharacterMemoryContext context,
            MemoryUseMode useMode)
        {
            if ((useMode != MemoryUseMode.Reunion && useMode != MemoryUseMode.ExplicitRecall) ||
                context == null ||
                !string.Equals(context.AnimalId, animalId, StringComparison.Ordinal))
            {
                return null;
            }

            var milestones = context.Milestones.Count == 0
                ? Array.Empty<ReadOnlyCharacterMemoryMilestone>()
                : new[] { context.Milestones[0] };
            return ReadOnlyCharacterMemoryContext.Create(
                context.AnimalId,
                context.Status,
                context.Discovered,
                context.CompletedMissionCount,
                context.LearnedKnowledgeCount,
                context.EarnedBadgeCount,
                milestones);
        }

        public static string SanitizeExternalAnswerMode(string answerMode, MemoryUseMode useMode = MemoryUseMode.None)
        {
            return string.Equals(
                answerMode,
                CharacterMemoryAnswerBuilder.MemoryRecallAnswerMode,
                StringComparison.Ordinal) &&
                useMode != MemoryUseMode.ExplicitRecall &&
                useMode != MemoryUseMode.HistoryBoundary
                    ? "social_chat"
                    : answerMode;
        }
    }
}
