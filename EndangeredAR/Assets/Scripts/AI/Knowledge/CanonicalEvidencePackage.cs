using System;
using System.Collections.Generic;

namespace EndangeredAR.AI.Knowledge
{
    public sealed class CanonicalEvidenceCitation
    {
        internal CanonicalEvidenceCitation(
            string sourceId,
            string title,
            string organization,
            string url)
        {
            SourceId = sourceId ?? string.Empty;
            Title = title ?? string.Empty;
            Organization = organization ?? string.Empty;
            Url = url ?? string.Empty;
        }

        public string SourceId { get; }
        public string Title { get; }
        public string Organization { get; }
        public string Url { get; }
    }

    public sealed class CanonicalEvidencePackage
    {
        private readonly CanonicalEvidenceFact[] facts;
        private readonly CanonicalEvidenceCitation[] citations;
        private readonly string[] groundedFactIds;

        internal CanonicalEvidencePackage(
            string animalId,
            string answerMode,
            string evidenceStatus,
            string classificationReason,
            GroundingTopic groundingTopic,
            IReadOnlyList<CanonicalEvidenceFact> facts,
            IReadOnlyList<CanonicalEvidenceCitation> citations,
            IReadOnlyList<string> groundedFactIds,
            string approvedAnswerConstraint)
        {
            AnimalId = animalId ?? string.Empty;
            AnswerMode = answerMode ?? string.Empty;
            EvidenceStatus = evidenceStatus ?? string.Empty;
            ClassificationReason = classificationReason ?? string.Empty;
            GroundingTopic = groundingTopic;
            this.facts = Copy(facts);
            this.citations = Copy(citations);
            this.groundedFactIds = Copy(groundedFactIds);
            ApprovedAnswerConstraint = approvedAnswerConstraint ?? string.Empty;
        }

        public string AnimalId { get; }
        public string AnswerMode { get; }
        public string EvidenceStatus { get; }
        public string ClassificationReason { get; }
        public GroundingTopic GroundingTopic { get; }
        public IReadOnlyList<CanonicalEvidenceFact> Facts => Array.AsReadOnly(Copy(facts));
        public IReadOnlyList<CanonicalEvidenceCitation> Citations => Array.AsReadOnly(Copy(citations));
        public IReadOnlyList<string> GroundedFactIds => Array.AsReadOnly(Copy(groundedFactIds));
        public string ApprovedAnswerConstraint { get; }

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<T>();
            }

            var result = new T[values.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = values[index];
            }

            return result;
        }
    }
}
