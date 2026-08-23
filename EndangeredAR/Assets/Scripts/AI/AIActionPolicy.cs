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
        public static AIAction SelectDeterministicIntent(string userMessage, string animalId)
        {
            var action = AIActionIntent.Resolve(userMessage);
            if (!AIActionProtocol.IsExecutable(action))
            {
                return AIAction.None;
            }

            return Select(
                new[]
                {
                    new AIActionCandidate(action, AIActionCandidateSource.DeterministicUserIntent, animalId)
                },
                animalId);
        }

        public static AIAction SelectProviderSuggestion(
            AIAction suggestedAction,
            string originalUserMessage,
            string responseAnimalId,
            string currentAnimalId)
        {
            var deterministicAction = AIActionIntent.Resolve(originalUserMessage);
            if (suggestedAction != deterministicAction || !AIActionProtocol.IsExecutable(suggestedAction))
            {
                return AIAction.None;
            }

            return Select(
                new[]
                {
                    new AIActionCandidate(
                        suggestedAction,
                        AIActionCandidateSource.DeterministicUserIntent,
                        responseAnimalId)
                },
                currentAnimalId);
        }

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
