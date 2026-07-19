using System.Collections.Generic;
using EndangeredAR.Animals;
using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AnimalContentContractTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void AnimalDefinition_RequiresStableAnimalId()
        {
            var definition = Create<AnimalDefinition>();
            definition.Configure("   ", "Name", "Short", "Scientific", "marker", "model", "texture",
                Vector3.zero, Vector3.zero, Vector3.zero, Vector3.one, "Welcome", Color.green,
                null, null, null, null);

            Assert.That(definition.AnimalId, Is.Empty);
            Assert.That(definition.IsConfigured, Is.False);
        }

        [Test]
        public void AnimalDefinition_ExposesModelPresentationWithoutMutation()
        {
            var knowledge = Create<AnimalKnowledgeProfile>();
            var mission = Create<MissionDefinition>();
            var definition = Create<AnimalDefinition>();
            var experiencePosition = new Vector3(-1.02f, -0.13f, 0f);
            var localOffset = new Vector3(0f, 0.04f, 0f);
            var eulerAngles = new Vector3(0f, 180f, 0f);
            var scale = new Vector3(1.45f, 1.45f, 1.45f);

            definition.Configure(" sensen ", "Display", "Short", "Scientific", "sensen_marker",
                "Models/Sensen/sensen.glb", "Models/Sensen/sensen_basecolor.png", experiencePosition,
                localOffset, eulerAngles, scale, "Welcome", Color.green, null, null, knowledge, mission);

            Assert.That(definition.AnimalId, Is.EqualTo("sensen"));
            Assert.That(definition.ModelRelativePath, Is.EqualTo("Models/Sensen/sensen.glb"));
            Assert.That(definition.BaseColorTextureRelativePath, Is.EqualTo("Models/Sensen/sensen_basecolor.png"));
            Assert.That(definition.ExperiencePosition, Is.EqualTo(experiencePosition));
            Assert.That(definition.ModelLocalOffset, Is.EqualTo(localOffset));
            Assert.That(definition.ModelEulerAngles, Is.EqualTo(eulerAngles));
            Assert.That(definition.ModelScale, Is.EqualTo(scale));
            Assert.That(definition.IsConfigured, Is.True);
        }

        [Test]
        public void KnowledgeProfile_ReturnsUnknownFallbackWhenNoKeywordMatches()
        {
            var profile = Create<AnimalKnowledgeProfile>();
            profile.Configure("Endangered", "Forest", "Leaves", new string[0], new string[0], new string[0],
                new[] { new AnimalKnowledgeEntry("food", new[] { "leaf", null }, "Leaves are food.", new string[0]) },
                "I do not know that yet.", new[] { "What do you eat?" });

            var found = profile.TryFindAnswer("Where do you sleep?", out var entry);

            Assert.That(found, Is.False);
            Assert.That(entry.Reply, Is.EqualTo("I do not know that yet."));
            Assert.That(entry.SuggestedQuestions, Is.EqualTo(new[] { "What do you eat?" }));
        }

        [Test]
        public void MissionDefinition_RejectsDuplicateOptionIds()
        {
            var mission = Create<MissionDefinition>();
            mission.Configure("food", "Food", "Choose food", new[]
            {
                new MissionOptionDefinition("leaf", "Leaf", true),
                new MissionOptionDefinition(" leaf ", "Flower", false)
            }, "Correct", "Wrong", "food", "Fact", "badge", 20);

            Assert.That(mission.Validate(), Has.Some.Contains("duplicate option ID"));
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }
    }
}
