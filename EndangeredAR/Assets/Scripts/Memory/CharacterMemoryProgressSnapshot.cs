using System;
using System.Collections.Generic;

namespace EndangeredAR.Memory
{
    public sealed class CharacterMemoryProgressSnapshot
    {
        public CharacterMemoryProgressSnapshot(
            string animalId,
            bool unlocked,
            string unlockedAtUtc,
            string missionId,
            bool missionCompleted,
            IReadOnlyList<string> learnedKnowledgeIds,
            IReadOnlyList<string> earnedBadgeIds)
        {
            AnimalId = animalId;
            Unlocked = unlocked;
            UnlockedAtUtc = unlockedAtUtc;
            MissionId = missionId;
            MissionCompleted = missionCompleted;
            LearnedKnowledgeIds = Copy(learnedKnowledgeIds);
            EarnedBadgeIds = Copy(earnedBadgeIds);
        }

        public string AnimalId { get; }
        public bool Unlocked { get; }
        public string UnlockedAtUtc { get; }
        public string MissionId { get; }
        public bool MissionCompleted { get; }
        public IReadOnlyList<string> LearnedKnowledgeIds { get; }
        public IReadOnlyList<string> EarnedBadgeIds { get; }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}
