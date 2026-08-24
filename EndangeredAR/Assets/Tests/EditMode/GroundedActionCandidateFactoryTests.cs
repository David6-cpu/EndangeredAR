using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class GroundedActionCandidateFactoryTests
    {
        [Test]
        public void DietIntentClassifier_MatchesSharedPositiveAndRejectVectors()
        {
            var classifier = FindType("EndangeredAR.AI.GroundedDietIntentClassifier");
            var isEligible = classifier.GetMethod("IsEligible", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(isEligible, Is.Not.Null);

            foreach (var item in LoadFixture().cases)
            {
                var actual = (bool)isEligible.Invoke(null, new object[] { item.message });
                Assert.That(actual, Is.EqualTo(item.expectedEatEligibility), $"{item.category}: {item.message}");
            }
        }

        [Test]
        public void Factory_ValidCanonicalDietEvidenceCreatesGroundedKnowledgeCandidate()
        {
            var profile = LoadSensenProfile();
            var response = ValidDietResponse(profile);

            var created = TryCreate(response, "森森，你平时吃什么？", "sensen", profile, out var candidate);

            Assert.That(created, Is.True);
            Assert.That(ReadProperty(candidate, "Action"), Is.EqualTo(AIAction.Eat));
            Assert.That(ReadProperty(candidate, "Source").ToString(), Is.EqualTo("GroundedKnowledge"));
            Assert.That(ReadProperty(candidate, "AnimalId"), Is.EqualTo("sensen"));
        }

        [Test]
        public void Factory_FailsClosedForMissingOrForgedEvidence()
        {
            var profile = LoadSensenProfile();
            var missingCitation = ValidDietResponse(profile);
            missingCitation.citations = Array.Empty<AICitation>();
            Assert.That(TryCreate(missingCitation, "你平时吃什么？", "sensen", profile, out _), Is.False);

            var forgedFact = ValidDietResponse(profile);
            forgedFact.GroundedFactIds = new[] { "sensen.diet.fake" };
            Assert.That(TryCreate(forgedFact, "你平时吃什么？", "sensen", profile, out _), Is.False);

            var forgedCitation = ValidDietResponse(profile);
            forgedCitation.citations = new[] { new AICitation { sourceId = "model-invented-source" } };
            Assert.That(TryCreate(forgedCitation, "你平时吃什么？", "sensen", profile, out _), Is.False);
        }

        [Test]
        public void Factory_FailsClosedForMixedTopicsWrongAnimalAndUnsafeIntent()
        {
            var profile = LoadSensenProfile();
            var mixed = ValidDietResponse(profile);
            var habitat = profile.Entries.Single(entry => entry.KnowledgeId == "sensen.habitat");
            mixed.GroundedFactIds = new[] { "sensen.diet", habitat.KnowledgeId };
            mixed.citations = mixed.citations.Concat(new[] { new AICitation { sourceId = habitat.SourceIds[0] } }).ToArray();
            Assert.That(TryCreate(mixed, "你平时吃什么？", "sensen", profile, out _), Is.False);

            var wrongAnimal = ValidDietResponse(profile);
            wrongAnimal.animalId = "red-panda";
            Assert.That(TryCreate(wrongAnimal, "你平时吃什么？", "sensen", profile, out _), Is.False);

            Assert.That(TryCreate(ValidDietResponse(profile), "薯片适合你吗？", "sensen", profile, out _), Is.False);
        }

        [TestCase("grounded_fact", "insufficient_evidence", GroundingTopic.Diet)]
        [TestCase("social_chat", "evidence_found", GroundingTopic.Diet)]
        [TestCase("grounded_fact", "evidence_found", GroundingTopic.None)]
        public void Factory_RequiresExactGroundedEvidenceContract(
            string answerMode,
            string evidenceStatus,
            GroundingTopic topic)
        {
            var profile = LoadSensenProfile();
            var response = ValidDietResponse(profile);
            response.answerMode = answerMode;
            response.evidenceStatus = evidenceStatus;
            response.GroundingTopic = topic;

            Assert.That(TryCreate(response, "你平时吃什么？", "sensen", profile, out _), Is.False);
        }

        private static bool TryCreate(
            AIResponse response,
            string message,
            string currentAnimalId,
            AnimalKnowledgeProfile profile,
            out object candidate)
        {
            var factory = FindType("EndangeredAR.AI.GroundedActionCandidateFactory");
            var method = factory.GetMethod("TryCreate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { response, message, currentAnimalId, profile, null };
            var result = (bool)method.Invoke(null, arguments);
            candidate = arguments[4];
            return result;
        }

        private static AIResponse ValidDietResponse(AnimalKnowledgeProfile profile)
        {
            var diet = profile.Entries.Single(entry => entry.KnowledgeId == "sensen.diet");
            return new AIResponse
            {
                animalId = "sensen",
                reply = diet.Reply,
                answerMode = "grounded_fact",
                evidenceStatus = "evidence_found",
                GroundingTopic = GroundingTopic.Diet,
                GroundedFactIds = new[] { diet.KnowledgeId },
                citations = diet.SourceIds.Select(sourceId => new AICitation { sourceId = sourceId }).ToArray()
            };
        }

        private static AnimalKnowledgeProfile LoadSensenProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AnimalKnowledgeProfile>(
                "Assets/Resources/Animals/SensenKnowledge.asset");
            Assert.That(profile, Is.Not.Null);
            return profile;
        }

        private static Type FindType(string name)
        {
            var type = typeof(AIResponse).Assembly.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object ReadProperty(object value, string propertyName)
        {
            Assert.That(value, Is.Not.Null);
            var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(value);
        }

        private static DietActionFixture LoadFixture()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "content",
                "quality",
                "sensen-grounded-diet-action-vectors.json"));
            return JsonUtility.FromJson<DietActionFixture>(File.ReadAllText(path));
        }

        [Serializable]
        private sealed class DietActionFixture
        {
            public DietActionCase[] cases;
        }

        [Serializable]
        private sealed class DietActionCase
        {
            public string message;
            public bool expectedEatEligibility;
            public string category;
        }
    }
}
