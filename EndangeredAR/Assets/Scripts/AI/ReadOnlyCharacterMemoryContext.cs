using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EndangeredAR.AI
{
    public enum CharacterMemoryContextStatus
    {
        Unavailable,
        Empty,
        Available
    }

    public static class CharacterMemoryContextStatusProtocol
    {
        public static bool TryParseExact(string wireValue, out CharacterMemoryContextStatus status)
        {
            switch (wireValue)
            {
                case "unavailable":
                    status = CharacterMemoryContextStatus.Unavailable;
                    return true;
                case "empty":
                    status = CharacterMemoryContextStatus.Empty;
                    return true;
                case "available":
                    status = CharacterMemoryContextStatus.Available;
                    return true;
                default:
                    status = default;
                    return false;
            }
        }

        public static string ToWireValue(CharacterMemoryContextStatus status)
        {
            switch (status)
            {
                case CharacterMemoryContextStatus.Unavailable:
                    return "unavailable";
                case CharacterMemoryContextStatus.Empty:
                    return "empty";
                case CharacterMemoryContextStatus.Available:
                    return "available";
                default:
                    return string.Empty;
            }
        }
    }

    public enum CharacterMemoryContextMilestoneKind
    {
        AnimalDiscovered,
        MissionCompleted,
        KnowledgeLearned,
        BadgeEarned
    }

    public static class CharacterMemoryContextMilestoneKindProtocol
    {
        public static bool TryParseExact(string wireValue, out CharacterMemoryContextMilestoneKind kind)
        {
            switch (wireValue)
            {
                case "animal_discovered":
                    kind = CharacterMemoryContextMilestoneKind.AnimalDiscovered;
                    return true;
                case "mission_completed":
                    kind = CharacterMemoryContextMilestoneKind.MissionCompleted;
                    return true;
                case "knowledge_learned":
                    kind = CharacterMemoryContextMilestoneKind.KnowledgeLearned;
                    return true;
                case "badge_earned":
                    kind = CharacterMemoryContextMilestoneKind.BadgeEarned;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        public static string ToWireValue(CharacterMemoryContextMilestoneKind kind)
        {
            switch (kind)
            {
                case CharacterMemoryContextMilestoneKind.AnimalDiscovered:
                    return "animal_discovered";
                case CharacterMemoryContextMilestoneKind.MissionCompleted:
                    return "mission_completed";
                case CharacterMemoryContextMilestoneKind.KnowledgeLearned:
                    return "knowledge_learned";
                case CharacterMemoryContextMilestoneKind.BadgeEarned:
                    return "badge_earned";
                default:
                    return string.Empty;
            }
        }
    }

    [Serializable]
    public sealed class ReadOnlyCharacterMemoryMilestone
    {
        [SerializeField] private string kind;
        [SerializeField] private string displayLabel;

        public ReadOnlyCharacterMemoryMilestone(
            CharacterMemoryContextMilestoneKind kind,
            string displayLabel)
        {
            this.kind = CharacterMemoryContextMilestoneKindProtocol.ToWireValue(kind);
            this.displayLabel = displayLabel ?? string.Empty;
        }

        public CharacterMemoryContextMilestoneKind Kind =>
            CharacterMemoryContextMilestoneKindProtocol.TryParseExact(kind, out var parsed)
                ? parsed
                : default;

        public string DisplayLabel => displayLabel ?? string.Empty;

        internal string KindWireValue => kind ?? string.Empty;

        internal ReadOnlyCharacterMemoryMilestone Copy()
        {
            return new ReadOnlyCharacterMemoryMilestone(Kind, DisplayLabel);
        }
    }

    [Serializable]
    public sealed class ReadOnlyCharacterMemoryContext
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion;
        [SerializeField] private string animalId;
        [SerializeField] private string memoryStatus;
        [SerializeField] private bool discovered;
        [SerializeField] private int completedMissionCount;
        [SerializeField] private int learnedKnowledgeCount;
        [SerializeField] private int earnedBadgeCount;
        [SerializeField] private ReadOnlyCharacterMemoryMilestone[] memoryMilestones;
        [NonSerialized] private string fingerprint;

        private ReadOnlyCharacterMemoryContext(
            string animalId,
            CharacterMemoryContextStatus status,
            bool discovered,
            int completedMissionCount,
            int learnedKnowledgeCount,
            int earnedBadgeCount,
            IReadOnlyList<ReadOnlyCharacterMemoryMilestone> milestones)
        {
            schemaVersion = CurrentSchemaVersion;
            this.animalId = animalId ?? string.Empty;
            memoryStatus = CharacterMemoryContextStatusProtocol.ToWireValue(status);
            this.discovered = discovered;
            this.completedMissionCount = Math.Max(0, completedMissionCount);
            this.learnedKnowledgeCount = Math.Max(0, learnedKnowledgeCount);
            this.earnedBadgeCount = Math.Max(0, earnedBadgeCount);
            memoryMilestones = Copy(milestones);
            fingerprint = ComputeFingerprint();
        }

        public int SchemaVersion => schemaVersion;
        public string AnimalId => animalId ?? string.Empty;
        public CharacterMemoryContextStatus Status =>
            CharacterMemoryContextStatusProtocol.TryParseExact(memoryStatus, out var parsed)
                ? parsed
                : CharacterMemoryContextStatus.Unavailable;
        public bool Discovered => discovered;
        public int CompletedMissionCount => completedMissionCount;
        public int LearnedKnowledgeCount => learnedKnowledgeCount;
        public int EarnedBadgeCount => earnedBadgeCount;
        public IReadOnlyList<ReadOnlyCharacterMemoryMilestone> Milestones => Array.AsReadOnly(Copy(memoryMilestones));
        public bool HasMemory => Status == CharacterMemoryContextStatus.Available;
        internal string Fingerprint => fingerprint ?? (fingerprint = ComputeFingerprint());

        public static ReadOnlyCharacterMemoryContext EmptyFor(string animalId)
        {
            return Create(
                animalId,
                CharacterMemoryContextStatus.Empty,
                false,
                0,
                0,
                0,
                Array.Empty<ReadOnlyCharacterMemoryMilestone>());
        }

        public static ReadOnlyCharacterMemoryContext UnavailableFor(string animalId)
        {
            return Create(
                animalId,
                CharacterMemoryContextStatus.Unavailable,
                false,
                0,
                0,
                0,
                Array.Empty<ReadOnlyCharacterMemoryMilestone>());
        }

        internal static ReadOnlyCharacterMemoryContext Create(
            string animalId,
            CharacterMemoryContextStatus status,
            bool discovered,
            int completedMissionCount,
            int learnedKnowledgeCount,
            int earnedBadgeCount,
            IReadOnlyList<ReadOnlyCharacterMemoryMilestone> milestones)
        {
            return new ReadOnlyCharacterMemoryContext(
                animalId,
                status,
                discovered,
                completedMissionCount,
                learnedKnowledgeCount,
                earnedBadgeCount,
                milestones);
        }

        private string ComputeFingerprint()
        {
            var canonical = new StringBuilder()
                .Append(schemaVersion).Append('|')
                .Append(AnimalId).Append('|')
                .Append(memoryStatus).Append('|')
                .Append(discovered ? '1' : '0').Append('|')
                .Append(completedMissionCount).Append('|')
                .Append(learnedKnowledgeCount).Append('|')
                .Append(earnedBadgeCount);
            foreach (var milestone in memoryMilestones ?? Array.Empty<ReadOnlyCharacterMemoryMilestone>())
            {
                canonical.Append('|')
                    .Append(milestone?.KindWireValue ?? string.Empty)
                    .Append(':')
                    .Append(milestone?.DisplayLabel ?? string.Empty);
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                var result = new StringBuilder(bytes.Length * 2);
                foreach (var value in bytes)
                {
                    result.Append(value.ToString("x2"));
                }

                return result.ToString();
            }
        }

        private static ReadOnlyCharacterMemoryMilestone[] Copy(
            IReadOnlyList<ReadOnlyCharacterMemoryMilestone> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<ReadOnlyCharacterMemoryMilestone>();
            }

            var copy = new ReadOnlyCharacterMemoryMilestone[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index]?.Copy();
            }

            return copy;
        }
    }
}
