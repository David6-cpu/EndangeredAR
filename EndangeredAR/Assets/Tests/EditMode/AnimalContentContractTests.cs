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
        public void KnowledgeProfile_MatchesKeywordsCaseInsensitively()
        {
            var profile = Create<AnimalKnowledgeProfile>();
            profile.Configure("Endangered", "Forest", "Leaves", new string[0], new string[0], new string[0],
                new[] { new AnimalKnowledgeEntry("food", new[] { "LEAF" }, "Leaves are food.", new string[0]) },
                "I do not know that yet.", new string[0]);

            var found = profile.TryFindAnswer("Do you eat leaf?", out var entry);

            Assert.That(found, Is.True);
            Assert.That(entry.KnowledgeId, Is.EqualTo("food"));
        }

        [Test]
        public void KnowledgeProfile_SkipsNullEntriesAndBlankKeywords()
        {
            var profile = Create<AnimalKnowledgeProfile>();
            profile.Configure("Endangered", "Forest", "Leaves", new string[0], new string[0], new string[0],
                new AnimalKnowledgeEntry[]
                {
                    null,
                    new AnimalKnowledgeEntry("food", new[] { null, "   ", "leaf" }, "Leaves are food.", new string[0])
                }, "I do not know that yet.", new string[0]);

            Assert.DoesNotThrow(() => profile.TryFindAnswer("leaf", out _));
            var found = profile.TryFindAnswer("leaf", out var entry);

            Assert.That(found, Is.True);
            Assert.That(entry.Reply, Is.EqualTo("Leaves are food."));
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

        [Test]
        public void MissionDefinition_RejectsBlankOptionIds()
        {
            var mission = Create<MissionDefinition>();
            mission.Configure("food", "Food", "Choose food", new[]
            {
                new MissionOptionDefinition("   ", "Leaf", false),
                new MissionOptionDefinition("flower", "Flower", true)
            }, "Correct", "Wrong", "food", "Fact", "badge", 20);

            Assert.That(mission.Validate(), Has.Some.EqualTo("Mission option ID is required."));
        }

        [Test]
        public void MissionDefinition_RejectsContentWithoutCorrectOptions()
        {
            var mission = Create<MissionDefinition>();
            mission.Configure("food", "Food", "Choose food", new[]
            {
                new MissionOptionDefinition("leaf", "Leaf", false),
                new MissionOptionDefinition("flower", "Flower", false)
            }, "Correct", "Wrong", "food", "Fact", "badge", 20);

            Assert.That(mission.Validate(), Has.Some.EqualTo("Mission requires at least one correct option."));
        }

        [Test]
        public void MissionDefinition_HandlesNullOptionArraysAndEntries()
        {
            var mission = Create<MissionDefinition>();
            mission.Configure("food", "Food", "Choose food", null, "Correct", "Wrong", "food", "Fact", "badge", 20);

            Assert.DoesNotThrow(() => mission.Validate());
            Assert.That(mission.Validate(), Has.Some.EqualTo("Mission requires at least one correct option."));

            mission.Configure("food", "Food", "Choose food", new MissionOptionDefinition[]
            {
                null,
                new MissionOptionDefinition("leaf", "Leaf", true)
            }, "Correct", "Wrong", "food", "Fact", "badge", 20);

            Assert.DoesNotThrow(() => mission.Validate());
            Assert.That(mission.Validate(), Has.Some.EqualTo("Mission option ID is required."));
            Assert.That(mission.TryGetOption("leaf", out var option), Is.True);
            Assert.That(option.Label, Is.EqualTo("Leaf"));
        }

        [Test]
        public void ContentCollections_ReturnDefensiveCopies()
        {
            var sourceThreats = new[] { "Habitat loss" };
            var sourceProtectionActions = new[] { "Protect forests" };
            var sourceFacts = new[] { "Active at night" };
            var sourceEntries = new[]
            {
                new AnimalKnowledgeEntry("food", new[] { "leaf" }, "Leaves are food.", new[] { "What do you eat?" })
            };
            var sourceSuggestions = new[] { "Where do you live?" };
            var profile = Create<AnimalKnowledgeProfile>();
            profile.Configure("Endangered", "Forest", "Leaves", sourceThreats, sourceProtectionActions, sourceFacts,
                sourceEntries, "I do not know that yet.", sourceSuggestions);

            sourceThreats[0] = "Changed";
            sourceProtectionActions[0] = "Changed";
            sourceFacts[0] = "Changed";
            sourceEntries[0] = null;
            sourceSuggestions[0] = "Changed";

            var threats = profile.Threats;
            var protectionActions = profile.ProtectionActions;
            var dailyFacts = profile.DailyFacts;
            var entries = profile.Entries;
            var suggestions = profile.DefaultSuggestions;
            threats[0] = "Changed returned threat";
            protectionActions[0] = "Changed returned action";
            dailyFacts[0] = "Changed returned fact";
            entries[0] = null;
            suggestions[0] = "Changed returned suggestion";

            Assert.That(profile.Threats, Is.EqualTo(new[] { "Habitat loss" }));
            Assert.That(profile.ProtectionActions, Is.EqualTo(new[] { "Protect forests" }));
            Assert.That(profile.DailyFacts, Is.EqualTo(new[] { "Active at night" }));
            Assert.That(profile.Entries[0].KnowledgeId, Is.EqualTo("food"));
            Assert.That(profile.DefaultSuggestions, Is.EqualTo(new[] { "Where do you live?" }));

            var entry = profile.Entries[0];
            var keywords = entry.Keywords;
            var entrySuggestions = entry.SuggestedQuestions;
            keywords[0] = "Changed keyword";
            entrySuggestions[0] = "Changed question";

            Assert.That(entry.Keywords, Is.EqualTo(new[] { "leaf" }));
            Assert.That(entry.SuggestedQuestions, Is.EqualTo(new[] { "What do you eat?" }));

            var sourceOptions = new[] { new MissionOptionDefinition("leaf", "Leaf", true) };
            var mission = Create<MissionDefinition>();
            mission.Configure("food", "Food", "Choose food", sourceOptions,
                "Correct", "Wrong", "food", "Fact", "badge", 20);
            sourceOptions[0] = null;

            var options = mission.Options;
            options[0] = null;

            Assert.That(mission.Options[0].OptionId, Is.EqualTo("leaf"));
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }
    }
}
