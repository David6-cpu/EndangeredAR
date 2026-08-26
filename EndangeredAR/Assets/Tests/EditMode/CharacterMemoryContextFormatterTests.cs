using System;
using System.Collections.Generic;
using System.Linq;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.Memory;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryContextFormatterTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        [Test]
        public void Formatter_ProducesBoundedSafeContextWithoutRawIdsOrTime()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            Assert.That(definition, Is.Not.Null);
            var projection = new CharacterMemoryProjection(
                true,
                new[] { definition.Mission.MissionId },
                new[] { "sensen.diet" },
                new[] { definition.Mission.BadgeId },
                new[]
                {
                    new CharacterMemoryMilestone(
                        CharacterMemoryEventType.MissionCompleted,
                        definition.Mission.MissionId,
                        "2026-08-25T10:00:00.0000000Z",
                        "v1|local-default|sensen|mission_completed|sensen-food"),
                    new CharacterMemoryMilestone(
                        CharacterMemoryEventType.KnowledgeLearned,
                        "sensen.diet",
                        "2026-08-25T09:00:00.0000000Z",
                        "v1|local-default|sensen|knowledge_learned|sensen.diet"),
                    new CharacterMemoryMilestone(
                        CharacterMemoryEventType.BadgeEarned,
                        definition.Mission.BadgeId,
                        "2026-08-25T08:00:00.0000000Z",
                        "v1|local-default|sensen|badge_earned|eco-guardian-sensen")
                });
            var current = CurrentContext(definition, true, 1, 1, true);

            var context = CharacterMemoryContextFormatter.Format(
                CharacterMemoryStoreStatus.Available,
                projection,
                definition,
                current);

            Assert.That(context.Status, Is.EqualTo(CharacterMemoryContextStatus.Available));
            Assert.That(context.AnimalId, Is.EqualTo("sensen"));
            Assert.That(context.CompletedMissionCount, Is.EqualTo(1));
            Assert.That(context.LearnedKnowledgeCount, Is.EqualTo(1));
            Assert.That(context.EarnedBadgeCount, Is.EqualTo(1));
            Assert.That(context.Milestones, Has.Count.LessThanOrEqualTo(3));
            Assert.That(context.Milestones.Select(item => item.Kind), Is.Unique);
            var json = JsonUtility.ToJson(context);
            StringAssert.DoesNotContain("sensen.diet", json);
            StringAssert.DoesNotContain(definition.Mission.BadgeId, json);
            StringAssert.DoesNotContain("2026-08-25", json);
            StringAssert.DoesNotContain("local-default", json);
        }

        [Test]
        public void Formatter_ClearDoesNotRebuildMemoryFromCurrentProgress()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            var current = CurrentContext(definition, true, 1, 1, true);

            var context = CharacterMemoryContextFormatter.Format(
                CharacterMemoryStoreStatus.Available,
                CharacterMemoryProjection.Empty,
                definition,
                current);

            Assert.That(context.Status, Is.EqualTo(CharacterMemoryContextStatus.Empty));
            Assert.That(context.Discovered, Is.False);
            Assert.That(context.CompletedMissionCount, Is.Zero);
            Assert.That(context.LearnedKnowledgeCount, Is.Zero);
            Assert.That(context.EarnedBadgeCount, Is.Zero);
            Assert.That(context.Milestones, Is.Empty);
        }

        [Test]
        public void Formatter_UnavailableStoreFailsClosed()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            var context = CharacterMemoryContextFormatter.Format(
                CharacterMemoryStoreStatus.FutureVersion,
                new CharacterMemoryProjection(true, new[] { definition.Mission.MissionId }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<CharacterMemoryMilestone>()),
                definition,
                CurrentContext(definition, true, 0, 0, true));

            Assert.That(context.Status, Is.EqualTo(CharacterMemoryContextStatus.Unavailable));
            Assert.That(context.Milestones, Is.Empty);
        }

        [Test]
        public void Formatter_UnresolvableIdsAreOmittedAndBadgeRemainsAggregateOnly()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            var projection = new CharacterMemoryProjection(
                false,
                new[] { "unknown-mission" },
                new[] { "unknown-knowledge" },
                new[] { definition.Mission.BadgeId },
                new[]
                {
                    new CharacterMemoryMilestone(CharacterMemoryEventType.MissionCompleted, "unknown-mission", "2026-01-01T00:00:00Z", "key-a"),
                    new CharacterMemoryMilestone(CharacterMemoryEventType.KnowledgeLearned, "unknown-knowledge", "2026-01-01T00:00:00Z", "key-b"),
                    new CharacterMemoryMilestone(CharacterMemoryEventType.BadgeEarned, definition.Mission.BadgeId, "2026-01-01T00:00:00Z", "key-c")
                });

            var context = CharacterMemoryContextFormatter.Format(
                CharacterMemoryStoreStatus.Available,
                projection,
                definition,
                CurrentContext(definition, false, 0, 1, false));

            Assert.That(context.CompletedMissionCount, Is.Zero);
            Assert.That(context.LearnedKnowledgeCount, Is.Zero);
            Assert.That(context.EarnedBadgeCount, Is.EqualTo(1));
            Assert.That(context.Milestones, Is.Empty);
            Assert.That(JsonUtility.ToJson(context), Does.Not.Contain(definition.Mission.BadgeId));
        }

        [Test]
        public void Provider_ReadsFreshProjectionAndReturnsUnavailableOnFailure()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            var catalog = CreateCatalog(definition);
            var repository = new InMemoryMemoryRepository();
            var memory = new CharacterMemoryService(
                repository,
                new EmptySnapshotSource(),
                () => new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc),
                () => "event-1");
            memory.Initialize();
            memory.AppendBatch(new EndangeredAR.Progress.AnimalProgressTransitionBatch(
                "sensen",
                "2026-08-25T10:00:00.0000000Z",
                new[]
                {
                    new EndangeredAR.Progress.AnimalProgressTransition(
                        EndangeredAR.Progress.AnimalProgressTransitionType.AnimalDiscovered,
                        "sensen")
                }));
            var provider = new ReadOnlyCharacterMemoryContextProvider(memory, catalog);

            var first = provider.CreateSnapshot(
                "sensen",
                CurrentContext(definition, true, 0, 0, false));
            memory.ClearAnimalMemory("sensen");
            var second = provider.CreateSnapshot(
                "sensen",
                CurrentContext(definition, true, 0, 0, false));

            Assert.That(first.Status, Is.EqualTo(CharacterMemoryContextStatus.Available));
            Assert.That(second.Status, Is.EqualTo(CharacterMemoryContextStatus.Empty));
            Assert.That(second.Fingerprint, Is.Not.EqualTo(first.Fingerprint));
            Assert.That(
                new ReadOnlyCharacterMemoryContextProvider(null, catalog)
                    .CreateSnapshot("sensen", CurrentContext(definition, true, 0, 0, false)).Status,
                Is.EqualTo(CharacterMemoryContextStatus.Unavailable));
        }

        private AnimalCatalogService CreateCatalog(AnimalDefinition definition)
        {
            var host = new GameObject("Memory Context Catalog");
            createdObjects.Add(host);
            var catalog = host.AddComponent<AnimalCatalogService>();
            var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("definitions");
            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = definition;
            serialized.FindProperty("defaultAnimalId").stringValue = "sensen";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.Initialize();
            return catalog;
        }

        private static ReadOnlyCharacterContext CurrentContext(
            AnimalDefinition definition,
            bool unlocked,
            int knowledgeCount,
            int badgeCount,
            bool missionCompleted)
        {
            return ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState(definition.AnimalId, unlocked, knowledgeCount, badgeCount),
                new ReadOnlyTaskState(definition.Mission.MissionId, definition.Mission.Title, missionCompleted),
                ReadOnlyInteractionState.Empty);
        }

        private sealed class InMemoryMemoryRepository : ICharacterMemoryRepository
        {
            private CharacterMemoryDocument document = CharacterMemoryDocumentUtility.CreateEmpty();

            public CharacterMemoryLoadResult Load()
            {
                return new CharacterMemoryLoadResult(
                    CharacterMemoryDocumentUtility.Clone(document),
                    CharacterMemoryStoreStatus.Available,
                    true);
            }

            public void Save(CharacterMemoryDocument value)
            {
                document = CharacterMemoryDocumentUtility.Clone(value);
            }
        }

        private sealed class EmptySnapshotSource : ICharacterMemoryProgressSnapshotSource
        {
            public IReadOnlyList<CharacterMemoryProgressSnapshot> GetSnapshots()
            {
                return Array.Empty<CharacterMemoryProgressSnapshot>();
            }
        }
    }
}
