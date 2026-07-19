using EndangeredAR.Animals;
using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class SensenContentAssetTests
    {
        [Test]
        public void SensenDefinition_ExposesCanonicalConfiguredPresentation()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.AnimalId, Is.EqualTo("sensen"));
            Assert.That(definition.DisplayName, Is.EqualTo("缨冠灰叶猴 森森"));
            Assert.That(definition.ShortName, Is.EqualTo("森森"));
            Assert.That(definition.ScientificName, Is.EqualTo("Trachypithecus poliocephalus"));
            Assert.That(definition.MarkerName, Is.EqualTo("sensen_marker"));
            Assert.That(definition.ModelRelativePath, Is.EqualTo("Models/Sensen/sensen.glb"));
            Assert.That(definition.BaseColorTextureRelativePath, Is.EqualTo("Models/Sensen/sensen_basecolor.png"));
            Assert.That(definition.ExperiencePosition, Is.EqualTo(new Vector3(-1.02f, -0.13f, 0f)));
            Assert.That(definition.ModelLocalOffset, Is.EqualTo(new Vector3(0f, 0.04f, 0f)));
            Assert.That(definition.ModelEulerAngles, Is.EqualTo(new Vector3(0f, 180f, 0f)));
            Assert.That(definition.ModelScale, Is.EqualTo(new Vector3(1.45f, 1.45f, 1.45f)));
            Assert.That(definition.Knowledge, Is.Not.Null);
            Assert.That(definition.Mission, Is.Not.Null);
            Assert.That(definition.IsConfigured, Is.True);
        }

        [Test]
        public void SensenMission_HasExactlyFourAuditedFoodOptions()
        {
            var mission = Resources.Load<MissionDefinition>("Animals/SensenMission");

            Assert.That(mission, Is.Not.Null);
            Assert.That(mission.Options, Has.Length.EqualTo(4));
            AssertOption(mission, "leaf", "嫩叶", true);
            AssertOption(mission, "flower", "花朵", true);
            AssertOption(mission, "snack", "人类零食", false);
            AssertOption(mission, "plastic", "塑料", false);
            Assert.That(mission.Options, Has.Exactly(2).Matches<MissionOptionDefinition>(option => option.IsCorrect));
            Assert.That(mission.Points, Is.EqualTo(20));
            Assert.That(mission.BadgeId, Is.EqualTo("eco-guardian-sensen"));
            Assert.That(mission.Validate(), Is.Empty);
        }

        [Test]
        public void SensenKnowledge_ContainsOnlyReviewedAnimalSpecificTopics()
        {
            var knowledge = Resources.Load<AnimalKnowledgeProfile>("Animals/SensenKnowledge");

            Assert.That(knowledge, Is.Not.Null);
            Assert.That(knowledge.Food, Does.Contain("嫩叶"));
            Assert.That(knowledge.Food, Does.Contain("果实"));
            Assert.That(knowledge.Food, Does.Contain("花朵"));
            Assert.That(knowledge.Habitat, Does.Contain("热带和亚热带森林"));
            Assert.That(knowledge.Threats, Is.EqualTo(new[] { "栖息地破碎", "非法捕猎", "种群隔离" }));
            Assert.That(knowledge.Entries, Has.Length.EqualTo(5));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "food"));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "habitat"));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "threats"));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "protection"));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "mission"));
        }

        private static void AssertOption(MissionDefinition mission, string optionId, string label, bool isCorrect)
        {
            Assert.That(mission.TryGetOption(optionId, out var option), Is.True);
            Assert.That(option.Label, Is.EqualTo(label));
            Assert.That(option.IsCorrect, Is.EqualTo(isCorrect));
        }
    }
}
