using System;
using System.Collections.Generic;
using System.Linq;
using EndangeredAR.Animals;

namespace EndangeredAR.AI.Knowledge
{
    public static class CanonicalKnowledgeRetriever
    {
        public static CanonicalEvidencePackage Retrieve(
            string animalId,
            AnimalKnowledgeProfile profile,
            string message)
        {
            if (!IsSafeAnimalId(animalId) || profile == null)
            {
                return Insufficient(animalId, "canonical_profile_unavailable", string.Empty);
            }

            AnimalKnowledgeRetrieval retrieval;
            try
            {
                retrieval = profile.Retrieve(message);
            }
            catch (Exception)
            {
                return Insufficient(animalId, "canonical_retrieval_failed", profile.UnknownReply);
            }

            var selectedEntries = retrieval.Entries;
            if (selectedEntries.Length == 0)
            {
                return new CanonicalEvidencePackage(
                    animalId,
                    retrieval.AnswerMode,
                    retrieval.EvidenceStatus,
                    retrieval.ClassificationReason,
                    GroundingTopic.None,
                    Array.Empty<CanonicalEvidenceFact>(),
                    Array.Empty<CanonicalEvidenceCitation>(),
                    Array.Empty<string>(),
                    retrieval.EvidenceStatus == "insufficient_evidence"
                        ? profile.UnknownReply
                        : string.Empty);
            }

            var topics = new HashSet<string>(StringComparer.Ordinal);
            var evidenceStatuses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in selectedEntries)
            {
                topics.Add(entry?.Topic ?? string.Empty);
                evidenceStatuses.Add(entry?.EvidenceStatus ?? string.Empty);
            }

            if (topics.Count != 1)
            {
                return Insufficient(animalId, "mixed_canonical_topics", profile.UnknownReply);
            }

            if (evidenceStatuses.Count != 1)
            {
                return Insufficient(animalId, "mixed_canonical_evidence_status", profile.UnknownReply);
            }

            var sourcesById = new Dictionary<string, AnimalKnowledgeSource>(StringComparer.Ordinal);
            foreach (var source in profile.Sources)
            {
                if (source != null && !string.IsNullOrEmpty(source.SourceId))
                {
                    sourcesById[source.SourceId] = source;
                }
            }

            var facts = new List<CanonicalEvidenceFact>();
            var citations = new List<CanonicalEvidenceCitation>();
            var seenCitationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in selectedEntries)
            {
                if (!IsValidEntry(animalId, entry) || entry.SourceIds.Length == 0)
                {
                    return Insufficient(animalId, "invalid_canonical_evidence", profile.UnknownReply);
                }

                foreach (var sourceId in entry.SourceIds)
                {
                    if (!sourcesById.TryGetValue(sourceId, out var source) ||
                        Array.IndexOf(source.AppliesToFactIds, entry.KnowledgeId) < 0)
                    {
                        return Insufficient(animalId, "invalid_canonical_evidence", profile.UnknownReply);
                    }

                    if (seenCitationIds.Add(sourceId))
                    {
                        citations.Add(new CanonicalEvidenceCitation(
                            source.SourceId,
                            source.Title,
                            source.Organization,
                            source.Url));
                    }
                }

                facts.Add(new CanonicalEvidenceFact(entry));
            }

            if (citations.Count == 0)
            {
                return Insufficient(animalId, "invalid_canonical_evidence", profile.UnknownReply);
            }

            var evidenceFound = retrieval.AnswerMode == "grounded_fact" &&
                                retrieval.EvidenceStatus == "evidence_found";
            var topic = evidenceFound && topics.SetEquals(new[] { "diet" })
                ? GroundingTopic.Diet
                : GroundingTopic.None;
            var groundedFactIds = evidenceFound
                ? facts.Select(fact => fact.FactId).ToArray()
                : Array.Empty<string>();

            return new CanonicalEvidencePackage(
                animalId,
                retrieval.AnswerMode,
                retrieval.EvidenceStatus,
                retrieval.ClassificationReason,
                topic,
                facts,
                citations,
                groundedFactIds,
                facts[0].ApprovedAnswer);
        }

        private static bool IsValidEntry(string animalId, AnimalKnowledgeEntry entry)
        {
            return entry != null &&
                   !string.IsNullOrEmpty(entry.KnowledgeId) &&
                   entry.KnowledgeId.StartsWith(animalId + ".", StringComparison.Ordinal) &&
                   !string.IsNullOrEmpty(entry.Topic) &&
                   !string.IsNullOrEmpty(entry.Reply) &&
                   (entry.EvidenceStatus == "evidence_found" || entry.EvidenceStatus == "known_unknown");
        }

        private static bool IsSafeAnimalId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            foreach (var character in value)
            {
                if (!(character == '-' || character == '_' ||
                      character >= 'a' && character <= 'z' ||
                      character >= '0' && character <= '9'))
                {
                    return false;
                }
            }

            return true;
        }

        private static CanonicalEvidencePackage Insufficient(
            string animalId,
            string reason,
            string constraint)
        {
            return new CanonicalEvidencePackage(
                animalId,
                "grounded_fact",
                "insufficient_evidence",
                reason,
                GroundingTopic.None,
                Array.Empty<CanonicalEvidenceFact>(),
                Array.Empty<CanonicalEvidenceCitation>(),
                Array.Empty<string>(),
                constraint);
        }
    }
}
