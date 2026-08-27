using System;
using System.Collections.Generic;
using EndangeredAR.Animals;

namespace EndangeredAR.AI.Knowledge
{
    public sealed class CanonicalEvidenceFact
    {
        private readonly string[] items;
        private readonly string[] sourceIds;

        internal CanonicalEvidenceFact(AnimalKnowledgeEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            FactId = entry.KnowledgeId ?? string.Empty;
            Topic = entry.Topic ?? string.Empty;
            Claim = entry.Claim ?? string.Empty;
            ApprovedAnswer = entry.Reply ?? string.Empty;
            DisplayValue = entry.DisplayValue ?? string.Empty;
            EvidenceStatus = entry.EvidenceStatus ?? string.Empty;
            items = Copy(entry.Items);
            sourceIds = Copy(entry.SourceIds);
        }

        public string FactId { get; }
        public string Topic { get; }
        public string Claim { get; }
        public string ApprovedAnswer { get; }
        public string DisplayValue { get; }
        public string EvidenceStatus { get; }
        public IReadOnlyList<string> Items => Array.AsReadOnly(Copy(items));
        public IReadOnlyList<string> SourceIds => Array.AsReadOnly(Copy(sourceIds));

        private static string[] Copy(string[] values)
        {
            return values == null ? Array.Empty<string>() : (string[])values.Clone();
        }
    }
}
