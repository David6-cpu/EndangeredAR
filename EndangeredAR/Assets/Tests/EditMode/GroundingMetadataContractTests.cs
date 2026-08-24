using System;
using System.IO;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.API;
using EndangeredAR.Chat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class GroundingMetadataContractTests
    {
        private GameObject serviceObject;

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(serviceObject);
        }

        [Test]
        public void Contracts_ExposeTrustedGroundingMetadataWithoutBreakingRawTransport()
        {
            Assert.That(typeof(ChatResponse).GetField("groundingTopic"), Is.Not.Null);
            Assert.That(typeof(ChatResponse).GetField("groundedFactIds"), Is.Not.Null);
            Assert.That(typeof(AIResponse).GetField("GroundingTopic"), Is.Not.Null);
            Assert.That(typeof(AIResponse).GetField("GroundedFactIds"), Is.Not.Null);
        }

        [TestCase("diet", "Diet")]
        [TestCase("Diet", "None")]
        [TestCase("DIET", "None")]
        [TestCase(" diet", "None")]
        [TestCase("diet ", "None")]
        [TestCase("diet;eat", "None")]
        [TestCase("eat", "None")]
        [TestCase("Animator.SetTrigger", "None")]
        [TestCase("", "None")]
        [TestCase(null, "None")]
        public void GroundingTopicParser_AcceptsOnlyExactLowercaseDiet(string raw, string expected)
        {
            var protocolType = typeof(AIResponse).Assembly.GetType("EndangeredAR.AI.GroundingTopicProtocol");
            Assert.That(protocolType, Is.Not.Null);
            var parse = protocolType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(parse, Is.Not.Null);

            var parsed = parse.Invoke(null, new object[] { raw });

            Assert.That(parsed.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void UnityKnowledge_DietAnswerRetainsTopicFactIdAndSources()
        {
            var answer = CreateService().Answer(LoadSensenProfile(), "森森，你平时吃什么？");

            Assert.That(ReadProperty(answer, "GroundingTopic").ToString(), Is.EqualTo("Diet"));
            Assert.That((string[])ReadProperty(answer, "GroundedFactIds"), Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(answer.SourceIds, Is.Not.Empty);
        }

        [Test]
        public void UnityKnowledge_PreciseQuantityVectorsReturnInsufficientWithoutAuthority()
        {
            var fixture = LoadFixture();
            var service = CreateService();
            var profile = LoadSensenProfile();

            foreach (var item in fixture.cases)
            {
                if (item.category != "unsupported_precise_quantity")
                {
                    continue;
                }

                var answer = service.Answer(profile, item.message);
                Assert.That(answer.EvidenceStatus, Is.EqualTo("insufficient_evidence"), item.message);
                Assert.That(ReadProperty(answer, "GroundingTopic").ToString(), Is.EqualTo("None"), item.message);
                Assert.That((string[])ReadProperty(answer, "GroundedFactIds"), Is.Empty, item.message);
                Assert.That(answer.SourceIds, Is.Empty, item.message);
            }
        }

        private LocalKnowledgeChatService CreateService()
        {
            if (serviceObject == null)
            {
                serviceObject = new GameObject("Local Knowledge Test Service");
            }

            return serviceObject.GetComponent<LocalKnowledgeChatService>() ??
                   serviceObject.AddComponent<LocalKnowledgeChatService>();
        }

        private static AnimalKnowledgeProfile LoadSensenProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AnimalKnowledgeProfile>(
                "Assets/Resources/Animals/SensenKnowledge.asset");
            Assert.That(profile, Is.Not.Null);
            return profile;
        }

        private static object ReadProperty(object value, string propertyName)
        {
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
            public string category;
        }
    }
}
