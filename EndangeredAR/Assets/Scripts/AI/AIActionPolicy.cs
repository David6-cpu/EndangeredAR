using System;
using System.Collections.Generic;

namespace EndangeredAR.AI
{
    internal enum AIActionCandidateSource
    {
        None,
        DeterministicUserIntent
    }

    internal readonly struct AIActionCandidate
    {
        public AIActionCandidate(AIAction action, AIActionCandidateSource source, string animalId)
        {
            Action = action;
            Source = source;
            AnimalId = animalId;
        }

        public AIAction Action { get; }
        public AIActionCandidateSource Source { get; }
        public string AnimalId { get; }
    }

    internal static class AIActionPolicy
    {
        public static AIAction Select(IReadOnlyList<AIActionCandidate> candidates, string expectedAnimalId)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(expectedAnimalId))
            {
                return AIAction.None;
            }

            var selected = AIAction.None;
            foreach (var candidate in candidates)
            {
                if (candidate.Source != AIActionCandidateSource.DeterministicUserIntent ||
                    !string.Equals(candidate.AnimalId, expectedAnimalId, StringComparison.OrdinalIgnoreCase) ||
                    !AIActionProtocol.IsExecutable(candidate.Action))
                {
                    continue;
                }

                if (selected != AIAction.None && selected != candidate.Action)
                {
                    return AIAction.None;
                }

                selected = candidate.Action;
            }

            return selected;
        }
    }
}
