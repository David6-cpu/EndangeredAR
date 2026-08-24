using System;
using System.Collections.Generic;
using EndangeredAR.Animals;

namespace EndangeredAR.AI
{
    internal static class GroundedActionCandidateFactory
    {
        public static bool TryCreate(
            AIResponse response,
            string originalUserMessage,
            string currentAnimalId,
            AnimalKnowledgeProfile profile,
            out AIActionCandidate candidate)
        {
            candidate = default;
            if (response == null ||
                profile == null ||
                !string.Equals(response.animalId, currentAnimalId, StringComparison.Ordinal) ||
                response.answerMode != "grounded_fact" ||
                response.evidenceStatus != "evidence_found" ||
                response.GroundingTopic != GroundingTopic.Diet ||
                response.GroundedFactIds == null ||
                response.GroundedFactIds.Length == 0 ||
                response.citations == null ||
                response.citations.Length == 0 ||
                !GroundedDietIntentClassifier.IsEligible(originalUserMessage))
            {
                return false;
            }

            var entriesById = new Dictionary<string, AnimalKnowledgeEntry>(StringComparer.Ordinal);
            foreach (var entry in profile.Entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.KnowledgeId))
                {
                    entriesById[entry.KnowledgeId] = entry;
                }
            }

            var sourcesById = new Dictionary<string, AnimalKnowledgeSource>(StringComparer.Ordinal);
            foreach (var source in profile.Sources)
            {
                if (source != null && !string.IsNullOrEmpty(source.SourceId))
                {
                    sourcesById[source.SourceId] = source;
                }
            }

            var factIds = new HashSet<string>(StringComparer.Ordinal);
            var groundedEntries = new List<AnimalKnowledgeEntry>();
            foreach (var factId in response.GroundedFactIds)
            {
                if (string.IsNullOrEmpty(factId) ||
                    !factId.StartsWith(currentAnimalId + ".", StringComparison.Ordinal) ||
                    !factIds.Add(factId) ||
                    !entriesById.TryGetValue(factId, out var entry) ||
                    entry.Topic != "diet" ||
                    entry.EvidenceStatus != "evidence_found")
                {
                    return false;
                }

                groundedEntries.Add(entry);
            }

            var citationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var citation in response.citations)
            {
                if (citation == null ||
                    string.IsNullOrEmpty(citation.sourceId) ||
                    !citationIds.Add(citation.sourceId) ||
                    !sourcesById.TryGetValue(citation.sourceId, out var source) ||
                    !AppliesToGroundedFact(source, factIds) ||
                    !IsDeclaredByGroundedFact(citation.sourceId, groundedEntries))
                {
                    return false;
                }
            }

            foreach (var entry in groundedEntries)
            {
                if (!HasSupportingCitation(entry, citationIds))
                {
                    return false;
                }
            }

            candidate = new AIActionCandidate(
                AIAction.Eat,
                AIActionCandidateSource.GroundedKnowledge,
                response.animalId);
            return true;
        }

        private static bool AppliesToGroundedFact(AnimalKnowledgeSource source, HashSet<string> factIds)
        {
            foreach (var factId in source.AppliesToFactIds)
            {
                if (factIds.Contains(factId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDeclaredByGroundedFact(string sourceId, List<AnimalKnowledgeEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (Array.IndexOf(entry.SourceIds, sourceId) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSupportingCitation(AnimalKnowledgeEntry entry, HashSet<string> citationIds)
        {
            foreach (var sourceId in entry.SourceIds)
            {
                if (citationIds.Contains(sourceId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
