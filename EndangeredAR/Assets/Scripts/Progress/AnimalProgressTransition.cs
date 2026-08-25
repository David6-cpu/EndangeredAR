using System;
using System.Collections.Generic;

namespace EndangeredAR.Progress
{
    internal static class AnimalProgressIdentifier
    {
        internal const int MaximumLength = 96;

        internal static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumLength || !IsAsciiLetterOrDigit(value[0]))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsAsciiLetterOrDigit(character) &&
                    character != '.' &&
                    character != '_' &&
                    character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetterOrDigit(char value)
        {
            return value >= 'a' && value <= 'z' || value >= '0' && value <= '9';
        }
    }

    public enum AnimalProgressTransitionType
    {
        AnimalDiscovered,
        MissionCompleted,
        KnowledgeLearned,
        BadgeEarned
    }

    public readonly struct AnimalProgressTransition
    {
        public AnimalProgressTransition(AnimalProgressTransitionType type, string subjectId)
        {
            Type = type;
            SubjectId = subjectId;
        }

        public AnimalProgressTransitionType Type { get; }
        public string SubjectId { get; }
    }

    public sealed class AnimalProgressTransitionBatch
    {
        private readonly List<AnimalProgressTransition> transitions;

        public AnimalProgressTransitionBatch(
            string animalId,
            string occurredAtUtc,
            IEnumerable<AnimalProgressTransition> transitions)
        {
            AnimalId = animalId;
            OccurredAtUtc = occurredAtUtc;
            this.transitions = transitions == null
                ? new List<AnimalProgressTransition>()
                : new List<AnimalProgressTransition>(transitions);
        }

        public string AnimalId { get; }
        public string OccurredAtUtc { get; }
        public IReadOnlyList<AnimalProgressTransition> Transitions => transitions;
    }
}
