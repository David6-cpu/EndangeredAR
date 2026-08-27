using System;
using System.IO;
using System.Linq;
using EndangeredAR.Animals;
using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class SensenContentAssetTests
    {
        [Test]
        public void SensenKnowledge_RuntimeAssetMatchesCanonicalFactsAndSources()
        {
            var knowledge = Resources.Load<AnimalKnowledgeProfile>("Animals/SensenKnowledge");
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "content",
                "animals",
                "sensen.json"));
            var canonical = JsonUtility.FromJson<CanonicalFixture>(File.ReadAllText(path));

            Assert.That(knowledge.Entries.Select(entry => entry.KnowledgeId),
                Is.EqualTo(canonical.facts.Select(fact => fact.factId)));
            Assert.That(knowledge.Sources.Select(source => source.SourceId),
                Is.EqualTo(canonical.sources.Select(source => source.sourceId)));

            foreach (var fact in canonical.facts)
            {
                var entry = Array.Find(knowledge.Entries, value => value.KnowledgeId == fact.factId);
                Assert.That(entry, Is.Not.Null, fact.factId);
                Assert.That(entry.Topic, Is.EqualTo(fact.topic), fact.factId);
                Assert.That(entry.Claim, Is.EqualTo(fact.claim), fact.factId);
                Assert.That(entry.Reply, Is.EqualTo(fact.approvedAnswer), fact.factId);
                Assert.That(entry.DisplayValue, Is.EqualTo(fact.displayValue), fact.factId);
                Assert.That(entry.Items, Is.EqualTo(fact.items ?? Array.Empty<string>()), fact.factId);
                Assert.That(entry.SourceIds, Is.EqualTo(fact.sourceIds), fact.factId);
                Assert.That(entry.Confidence, Is.EqualTo(fact.confidence), fact.factId);
                Assert.That(entry.EvidenceStatus, Is.EqualTo(fact.evidenceStatus), fact.factId);
                Assert.That(entry.LastVerified, Is.EqualTo(fact.lastVerified), fact.factId);
                Assert.That(entry.Notes, Is.EqualTo(fact.notes), fact.factId);
            }

            foreach (var source in canonical.sources)
            {
                var runtimeSource = Array.Find(knowledge.Sources, value => value.SourceId == source.sourceId);
                Assert.That(runtimeSource, Is.Not.Null, source.sourceId);
                Assert.That(runtimeSource.Title, Is.EqualTo(source.title), source.sourceId);
                Assert.That(runtimeSource.Organization, Is.EqualTo(source.organization), source.sourceId);
                Assert.That(runtimeSource.Url, Is.EqualTo(source.url), source.sourceId);
                Assert.That(runtimeSource.AppliesToFactIds, Is.EqualTo(source.appliesToFactIds), source.sourceId);
            }
        }

        [Test]
        public void SensenDefinition_ExposesCanonicalConfiguredPresentation()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.AnimalId, Is.EqualTo("sensen"));
            Assert.That(definition.DisplayName, Is.EqualTo("缨冠灰叶猴 森森"));
            Assert.That(definition.ShortName, Is.EqualTo("森森"));
            Assert.That(definition.ScientificName, Is.EqualTo("Semnopithecus priam"));
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
            Assert.That(knowledge.EndangeredLevel, Is.EqualTo("近危（NT）"));
            Assert.That(knowledge.Habitat, Does.Contain("干旱常绿林"));
            Assert.That(knowledge.Threats, Does.Contain("栖息地丧失和退化"));
            Assert.That(knowledge.Entries, Has.Length.GreaterThanOrEqualTo(12));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "sensen.scientific_name"));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "sensen.population.global"));
            Assert.That(knowledge.Entries, Has.Exactly(1).Matches<AnimalKnowledgeEntry>(entry => entry.KnowledgeId == "sensen.diet"));
            Assert.That(knowledge.Sources, Has.Exactly(1).Matches<AnimalKnowledgeSource>(source => source.SourceId == "iucn-2020-s-priam"));

            var population = System.Array.Find(knowledge.Entries, entry => entry.KnowledgeId == "sensen.population.global");
            Assert.That(population.EvidenceStatus, Is.EqualTo("known_unknown"));
            Assert.That(population.SourceIds, Does.Contain("iucn-2020-s-priam"));

            var status = System.Array.Find(knowledge.Entries, entry => entry.KnowledgeId == "sensen.conservation_status");
            Assert.That(status.Reply, Does.Contain("不是濒危（EN）"));
            Assert.That(status.Reply, Does.Contain("不等于"));
            Assert.That(status.Items, Does.Contain("IUCN：近危（NT）"));
            Assert.That(status.Items, Does.Contain("CITES：附录 I"));
        }

        private static void AssertOption(MissionDefinition mission, string optionId, string label, bool isCorrect)
        {
            Assert.That(mission.TryGetOption(optionId, out var option), Is.True);
            Assert.That(option.Label, Is.EqualTo(label));
            Assert.That(option.IsCorrect, Is.EqualTo(isCorrect));
        }

        [Serializable]
        private sealed class CanonicalFixture
        {
            public CanonicalFactFixture[] facts = Array.Empty<CanonicalFactFixture>();
            public CanonicalSourceFixture[] sources = Array.Empty<CanonicalSourceFixture>();
        }

        [Serializable]
        private sealed class CanonicalFactFixture
        {
            public string factId;
            public string topic;
            public string claim;
            public string approvedAnswer;
            public string displayValue;
            public string[] items = Array.Empty<string>();
            public string[] sourceIds = Array.Empty<string>();
            public string confidence;
            public string evidenceStatus;
            public string lastVerified;
            public string notes;
        }

        [Serializable]
        private sealed class CanonicalSourceFixture
        {
            public string sourceId;
            public string title;
            public string organization;
            public string url;
            public string[] appliesToFactIds = Array.Empty<string>();
        }
    }
}
