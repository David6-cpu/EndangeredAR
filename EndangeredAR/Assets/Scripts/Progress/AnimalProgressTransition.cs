using System;
using System.Collections.Generic;

namespace EndangeredAR.Progress
{
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
