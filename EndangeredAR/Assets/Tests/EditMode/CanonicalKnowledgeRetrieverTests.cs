using System;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.AI.Knowledge;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CanonicalKnowledgeRetrieverTests
    {
        [Test]
        public void DietEvidence_ContainsOnlyCanonicalFactsAndLinkedCitations()
        {
            var profile = Resources.Load<AnimalKnowledgeProfile>("Animals/SensenKnowledge");

            var package = CanonicalKnowledgeRetriever.Retrieve("sensen", profile, "你平时吃什么？");

            Assert.That(package.AnswerMode, Is.EqualTo("grounded_fact"));
            Assert.That(package.EvidenceStatus, Is.EqualTo("evidence_found"));
            Assert.That(package.GroundingTopic, Is.EqualTo(GroundingTopic.Diet));
            Assert.That(package.GroundedFactIds, Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(package.Facts.Select(fact => fact.FactId), Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(package.Citations, Is.Not.Empty);
            Assert.That(package.Citations.All(citation =>
                package.Facts[0].SourceIds.Contains(citation.SourceId)), Is.True);
            Assert.That(package.ApprovedAnswerConstraint, Does.Contain("嫩叶"));
        }

        [Test]
        public void KnownUnknown_KeepsRealCitationButGrantsNoGroundingAuthority()
        {
            var profile = Resources.Load<AnimalKnowledgeProfile>("Animals/SensenKnowledge");

            var package = CanonicalKnowledgeRetriever.Retrieve("sensen", profile, "野外还剩多少只？");

            Assert.That(package.EvidenceStatus, Is.EqualTo("insufficient_evidence"));
            Assert.That(package.Facts.Select(fact => fact.FactId),
                Is.EqualTo(new[] { "sensen.population.global" }));
            Assert.That(package.Citations, Is.Not.Empty);
            Assert.That(package.GroundedFactIds, Is.Empty);
            Assert.That(package.GroundingTopic, Is.EqualTo(GroundingTopic.None));
        }

        [Test]
        public void InvalidSourceToFactLink_FailsClosed()
        {
            var profile = CreateProfile(
                new[] { Entry("sensen.diet", "diet", "same", "source-a") },
                new[] { Source("source-a", "sensen.other") });

            var package = CanonicalKnowledgeRetriever.Retrieve("sensen", profile, "same");

            Assert.That(package.EvidenceStatus, Is.EqualTo("insufficient_evidence"));
            Assert.That(package.ClassificationReason, Is.EqualTo("invalid_canonical_evidence"));
            Assert.That(package.Facts, Is.Empty);
            Assert.That(package.Citations, Is.Empty);
            Assert.That(package.GroundedFactIds, Is.Empty);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void EqualScoreMixedTopics_FailClosed()
        {
            var profile = CreateProfile(
                new[]
                {
                    Entry("sensen.diet", "diet", "same", "source-a"),
                    Entry("sensen.habitat", "habitat", "same", "source-b")
                },
                new[]
                {
                    Source("source-a", "sensen.diet"),
                    Source("source-b", "sensen.habitat")
                });

            var package = CanonicalKnowledgeRetriever.Retrieve("sensen", profile, "same");

            Assert.That(package.EvidenceStatus, Is.EqualTo("insufficient_evidence"));
            Assert.That(package.ClassificationReason, Is.EqualTo("mixed_canonical_topics"));
            Assert.That(package.Facts, Is.Empty);
            Assert.That(package.Citations, Is.Empty);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void EvidenceContracts_AreImmutableAndHaveNoModelResponseInput()
        {
            Assert.That(typeof(CanonicalEvidencePackage).GetProperties()
                .All(property => property.SetMethod == null), Is.True);
            Assert.That(typeof(CanonicalEvidenceFact).GetProperties()
                .All(property => property.SetMethod == null), Is.True);
            Assert.That(typeof(CanonicalKnowledgeRetriever).GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .SelectMany(method => method.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(AIResponse)), Is.False);
        }

        private static AnimalKnowledgeProfile CreateProfile(
            AnimalKnowledgeEntry[] entries,
            AnimalKnowledgeSource[] sources)
        {
            var profile = ScriptableObject.CreateInstance<AnimalKnowledgeProfile>();
            profile.Configure(
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                entries,
                sources,
                "没有可靠证据。",
                Array.Empty<string>());
            return profile;
        }

        private static AnimalKnowledgeEntry Entry(
            string id,
            string topic,
            string alias,
            string sourceId)
        {
            return new AnimalKnowledgeEntry(
                id,
                topic,
                "可信事实",
                Array.Empty<string>(),
                new[] { alias },
                "可信回答约束",
                "可信显示值",
                Array.Empty<string>(),
                new[] { sourceId },
                "high",
                "evidence_found",
                "2026-08-27",
                string.Empty,
                Array.Empty<string>());
        }

        private static AnimalKnowledgeSource Source(string id, string factId)
        {
            return new AnimalKnowledgeSource(
                id,
                "来源",
                "机构",
                "reference",
                "https://example.test/source",
                "2026-08-27",
                "2026-08-27",
                new[] { factId },
                string.Empty);
        }
    }
}
