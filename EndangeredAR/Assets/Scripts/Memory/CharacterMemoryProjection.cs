using System;
using System.Collections.Generic;

namespace EndangeredAR.Memory
{
    public sealed class CharacterMemoryProjection
    {
        public static readonly CharacterMemoryProjection Empty = new CharacterMemoryProjection(
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<CharacterMemoryMilestone>());

        public CharacterMemoryProjection(
            bool discovered,
            IReadOnlyList<string> completedMissionIds,
            IReadOnlyList<string> learnedKnowledgeIds,
            IReadOnlyList<string> earnedBadgeIds,
            IReadOnlyList<CharacterMemoryMilestone> recentMilestones)
        {
            Discovered = discovered;
            CompletedMissionIds = Copy(completedMissionIds);
            LearnedKnowledgeIds = Copy(learnedKnowledgeIds);
            EarnedBadgeIds = Copy(earnedBadgeIds);
            RecentMilestones = Copy(recentMilestones);
        }

        public bool Discovered { get; }
        public IReadOnlyList<string> CompletedMissionIds { get; }
        public IReadOnlyList<string> LearnedKnowledgeIds { get; }
        public IReadOnlyList<string> EarnedBadgeIds { get; }
        public IReadOnlyList<CharacterMemoryMilestone> RecentMilestones { get; }

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<T>();
            }

            var copy = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}
