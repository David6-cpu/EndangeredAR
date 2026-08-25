using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.Animals;
using EndangeredAR.Memory;
using EndangeredAR.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryLifecycleTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private readonly List<string> temporaryDirectories = new List<string>();
        private static int eventSequence;

        [SetUp]
        public void SetUp()
        {
            eventSequence = 0;
        }

        [TearDown]
        public void TearDown()
        {
            AnimalProgressService.RepositoryPathOverrideForTests = null;
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            foreach (var directory in temporaryDirectories)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void LifecycleContracts_ExistWithoutAiDependencies()
        {
            var runtimeAssembly = typeof(CharacterMemoryService).Assembly;
            Assert.That(runtimeAssembly.GetType(
                "EndangeredAR.Memory.CharacterMemoryProgressSnapshot"), Is.Not.Null);
            Assert.That(runtimeAssembly.GetType(
                "EndangeredAR.Memory.ICharacterMemoryProgressSnapshotSource"), Is.Not.Null);
            Assert.That(runtimeAssembly.GetType(
                "EndangeredAR.Memory.AnimalProgressMemorySnapshotSource"), Is.Not.Null);
            Assert.That(runtimeAssembly.GetType(
                "EndangeredAR.Memory.CharacterMemoryRuntimeComposition"), Is.Not.Null);
            Assert.That(typeof(CharacterMemoryService).GetMethod("Reconcile"), Is.Not.Null);
            Assert.That(typeof(CharacterMemoryService).GetMethod(
                "ClearAnimalMemory", new[] { typeof(string) }), Is.Not.Null);
            Assert.That(typeof(CharacterMemoryService).GetMethod("ClearAllCharacterMemory"), Is.Not.Null);
            Assert.That(typeof(CharacterMemoryService).GetMethod(
                "ReloadForDevelopment", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        }

        [Test]
        public void Initialize_FirstRunBootstrapsFourProgressMilestonesWithoutInventingTimes()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var source = new StaticSnapshotSource(CreateCompletedSnapshot());
            var service = CreateService(repository, source);

            service.Initialize();

            var profile = repository.Stored.profiles.Single();
            var record = profile.animals.Single();
            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(profile.bootstrapCompleted, Is.True);
            Assert.That(record.events, Has.Count.EqualTo(4));
            Assert.That(record.events.Select(value => value.eventOrigin), Is.All.EqualTo("bootstrap"));
            Assert.That(FindEvent(record, "animal_discovered").occurredAtUtc, Is.EqualTo(FixedOccurredAt));
            Assert.That(FindEvent(record, "mission_completed").occurredAtUtc, Is.Empty);
            Assert.That(FindEvent(record, "knowledge_learned").occurredAtUtc, Is.Empty);
            Assert.That(FindEvent(record, "badge_earned").occurredAtUtc, Is.Empty);
            AssertProjectionIsComplete(service.GetProjection("sensen"));
        }

        [Test]
        public void Initialize_BootstrapRunsOnceAndLaterInitializationDoesNotRewrite()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var source = new StaticSnapshotSource(CreateCompletedSnapshot());
            CreateService(repository, source).Initialize();

            CreateService(repository, source).Initialize();

            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(repository.Stored.profiles[0].animals[0].events, Has.Count.EqualTo(4));
        }

        [Test]
        public void Initialize_ReconcilesMissingEventWithoutModifyingSnapshotSource()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var source = new StaticSnapshotSource(CreateCompletedSnapshot());
            CreateService(repository, source).Initialize();
            var stored = repository.Stored;
            stored.profiles[0].animals[0].events.RemoveAll(value => value.eventType == "knowledge_learned");
            repository.ReplaceStored(stored);

            var reloaded = CreateService(repository, source);
            reloaded.Initialize();

            var reconciled = FindEvent(repository.Stored.profiles[0].animals[0], "knowledge_learned");
            Assert.That(repository.SaveCalls, Is.EqualTo(2));
            Assert.That(reconciled.eventOrigin, Is.EqualTo("reconcile"));
            Assert.That(reconciled.occurredAtUtc, Is.Empty);
            Assert.That(source.GetSnapshotsCalls, Is.EqualTo(2));
            AssertProjectionIsComplete(reloaded.GetProjection("sensen"));
        }

        [Test]
        public void Reconcile_SaveFailureIsRepairedByNextInitializationWithoutProgressRollback()
        {
            var repository = new InMemoryCharacterMemoryRepository(CreateBootstrappedEmptyDocument())
            {
                ThrowOnSave = true
            };
            var source = new StaticSnapshotSource(CreateCompletedSnapshot());
            var failed = CreateService(repository, source);

            failed.Initialize();

            Assert.That(failed.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.SaveFailed));
            Assert.That(failed.GetProjection("sensen").Discovered, Is.False);
            Assert.That(source.Snapshots[0].Unlocked, Is.True);

            repository.ThrowOnSave = false;
            var recovered = CreateService(repository, source);
            recovered.Initialize();

            AssertProjectionIsComplete(recovered.GetProjection("sensen"));
            Assert.That(
                repository.Stored.profiles[0].animals[0].events.Select(value => value.eventOrigin),
                Is.All.EqualTo("reconcile"));
        }

        [Test]
        public void ClearAnimalMemory_PersistsSuppressionAndDoesNotImmediatelyReconcile()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var source = new StaticSnapshotSource(CreateCompletedSnapshot());
            var service = CreateService(repository, source);
            service.Initialize();

            var result = service.ClearAnimalMemory("sensen");

            var record = repository.Stored.profiles[0].animals[0];
            Assert.That(result, Is.EqualTo(CharacterMemoryOperationResult.Saved));
            Assert.That(record.events, Is.Empty);
            Assert.That(record.foldedProjection.discovered, Is.False);
            Assert.That(record.reconciliationSuppressionKeys, Has.Count.EqualTo(4));
            Assert.That(service.GetProjection("sensen").Discovered, Is.False);
            Assert.That(source.Snapshots[0].Unlocked, Is.True);

            var saveCallsAfterClear = repository.SaveCalls;
            var restarted = CreateService(repository, source);
            restarted.Initialize();

            Assert.That(repository.SaveCalls, Is.EqualTo(saveCallsAfterClear));
            Assert.That(restarted.GetProjection("sensen").Discovered, Is.False);
        }

        [Test]
        public void BusinessTransition_RemovesItsSuppressionAndRecordsARealEvent()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var source = new StaticSnapshotSource(CreateCompletedSnapshot());
            var service = CreateService(repository, source);
            service.Initialize();
            service.ClearAnimalMemory("sensen");

            service.AppendBatch(new AnimalProgressTransitionBatch(
                "sensen",
                FixedOccurredAt,
                new[]
                {
                    new AnimalProgressTransition(
                        AnimalProgressTransitionType.KnowledgeLearned,
                        "sensen.diet")
                }));

            var record = repository.Stored.profiles[0].animals[0];
            var expectedKey = "v1|local-default|sensen|knowledge_learned|sensen.diet";
            Assert.That(record.reconciliationSuppressionKeys, Does.Not.Contain(expectedKey));
            Assert.That(record.events, Has.Count.EqualTo(1));
            Assert.That(record.events[0].eventOrigin, Is.EqualTo("business"));
            Assert.That(service.GetProjection("sensen").LearnedKnowledgeIds, Is.EqualTo(new[] { "sensen.diet" }));
        }

        [Test]
        public void ClearAllCharacterMemory_IsolatesBoundProfileAndSuppressesEachAnimal()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var source = new StaticSnapshotSource(
                CreateCompletedSnapshot(),
                new CharacterMemoryProgressSnapshot(
                    "pangolin", true, string.Empty, string.Empty, false,
                    Array.Empty<string>(), Array.Empty<string>()));
            var service = CreateService(repository, source);
            service.Initialize();

            var result = service.ClearAllCharacterMemory();

            Assert.That(result, Is.EqualTo(CharacterMemoryOperationResult.Saved));
            Assert.That(repository.Stored.profiles[0].animals, Has.Count.EqualTo(2));
            Assert.That(
                repository.Stored.profiles[0].animals.Select(value => value.events.Count),
                Is.All.Zero);
            Assert.That(repository.Stored.profiles[0].animals[0].reconciliationSuppressionKeys, Is.Not.Empty);
            Assert.That(repository.Stored.profiles[0].animals[1].reconciliationSuppressionKeys, Is.Not.Empty);
        }

        [Test]
        public void FutureVersion_DisablesBootstrapReconcileAndClearWithoutSaving()
        {
            var repository = new FutureVersionMemoryRepository();
            var source = new StaticSnapshotSource(CreateCompletedSnapshot());
            var service = CreateService(repository, source);

            service.Initialize();

            Assert.That(service.Status, Is.EqualTo(CharacterMemoryStoreStatus.FutureVersion));
            Assert.That(service.Reconcile(), Is.EqualTo(CharacterMemoryOperationResult.Unavailable));
            Assert.That(service.ClearAnimalMemory("sensen"), Is.EqualTo(CharacterMemoryOperationResult.Unavailable));
            Assert.That(service.ClearAllCharacterMemory(), Is.EqualTo(CharacterMemoryOperationResult.Unavailable));
            Assert.That(repository.SaveCalls, Is.Zero);
        }

        [Test]
        public void RuntimeComposition_UsesProgressDirectoryBootstrapsAndAttachesSink()
        {
            var directory = CreateTemporaryDirectory();
            var progressPath = Path.Combine(directory, "animal-progress.json");
            AnimalProgressService.RepositoryPathOverrideForTests = progressPath;
            var progress = CreateComponent<AnimalProgressService>("Lifecycle Progress");
            var catalog = CreateSensenCatalog();
            Assert.That(progress.Unlock("sensen"), Is.True);

            var service = CharacterMemoryRuntimeComposition.Create(progress, catalog);

            Assert.That(service, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(directory, "character-memory.json")), Is.True);
            Assert.That(service.GetProjection("sensen").Discovered, Is.True);

            progress.MarkMissionCompleted(
                "sensen", "sensen-food", "eco-guardian", "sensen.diet");

            Assert.That(service.GetProjection("sensen").CompletedMissionIds, Is.EqualTo(new[] { "sensen-food" }));
            Assert.That(service.GetProjection("sensen").LearnedKnowledgeIds, Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(service.GetProjection("sensen").EarnedBadgeIds, Is.EqualTo(new[] { "eco-guardian" }));
        }

        [Test]
        public void SnapshotSource_UsesCanonicalMissionAndSanitizesInvalidUnlockTime()
        {
            var directory = CreateTemporaryDirectory();
            AnimalProgressService.RepositoryPathOverrideForTests = Path.Combine(directory, "animal-progress.json");
            var progress = CreateComponent<AnimalProgressService>("Snapshot Progress");
            progress.MarkMissionCompleted(
                "sensen", "sensen-food", "eco-guardian", "sensen.diet");
            var stored = new JsonAnimalProgressRepository(progress.ActiveRepositoryPath).Load();
            stored.animals[0].unlocked = true;
            stored.animals[0].unlockedAtUtc = "not-a-time";
            new JsonAnimalProgressRepository(progress.ActiveRepositoryPath).Save(stored);
            var reloadedProgress = CreateComponent<AnimalProgressService>("Reloaded Snapshot Progress");
            var source = new AnimalProgressMemorySnapshotSource(reloadedProgress, CreateSensenCatalog());

            var snapshot = source.GetSnapshots().Single(value => value.AnimalId == "sensen");

            Assert.That(snapshot.MissionId, Is.EqualTo("sensen-food"));
            Assert.That(snapshot.MissionCompleted, Is.True);
            Assert.That(snapshot.UnlockedAtUtc, Is.Empty);
            Assert.That(snapshot.LearnedKnowledgeIds, Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(snapshot.EarnedBadgeIds, Is.EqualTo(new[] { "eco-guardian" }));
        }

        private const string FixedOccurredAt = "2026-08-25T03:30:00.0000000Z";
        private static readonly DateTime FixedUtc = new DateTime(2026, 8, 25, 3, 30, 0, DateTimeKind.Utc);

        private static CharacterMemoryProgressSnapshot CreateCompletedSnapshot()
        {
            return new CharacterMemoryProgressSnapshot(
                "sensen",
                true,
                FixedOccurredAt,
                "sensen-food",
                true,
                new[] { "sensen.diet" },
                new[] { "eco-guardian" });
        }

        private static CharacterMemoryDocument CreateBootstrappedEmptyDocument()
        {
            return new CharacterMemoryDocument
            {
                profiles = new List<CharacterMemoryProfile>
                {
                    new CharacterMemoryProfile
                    {
                        profileKey = CharacterMemoryService.LocalDefaultProfileKey,
                        bootstrapCompleted = true
                    }
                }
            };
        }

        private static CharacterMemoryService CreateService(
            ICharacterMemoryRepository repository,
            ICharacterMemoryProgressSnapshotSource source)
        {
            return new CharacterMemoryService(repository, source, () => FixedUtc, NextEventId);
        }

        private static string NextEventId()
        {
            eventSequence++;
            return "lifecycle-event-" + eventSequence.ToString("00");
        }

        private static CharacterMemoryEventRecord FindEvent(CharacterMemoryRecord record, string eventType)
        {
            return record.events.Single(value => value.eventType == eventType);
        }

        private static void AssertProjectionIsComplete(CharacterMemoryProjection projection)
        {
            Assert.That(projection.Discovered, Is.True);
            Assert.That(projection.CompletedMissionIds, Is.EqualTo(new[] { "sensen-food" }));
            Assert.That(projection.LearnedKnowledgeIds, Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(projection.EarnedBadgeIds, Is.EqualTo(new[] { "eco-guardian" }));
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            var host = new GameObject(name);
            createdObjects.Add(host);
            return host.AddComponent<T>();
        }

        private AnimalCatalogService CreateSensenCatalog()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            Assert.That(definition, Is.Not.Null);
            var catalog = CreateComponent<AnimalCatalogService>("Lifecycle Catalog");
            var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("definitions");
            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = definition;
            serialized.FindProperty("defaultAnimalId").stringValue = "sensen";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.Initialize();
            return catalog;
        }

        private string CreateTemporaryDirectory()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "EndangeredAR-MemoryLifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            temporaryDirectories.Add(directory);
            return directory;
        }
    }

    internal sealed class StaticSnapshotSource : ICharacterMemoryProgressSnapshotSource
    {
        internal StaticSnapshotSource(params CharacterMemoryProgressSnapshot[] snapshots)
        {
            Snapshots = snapshots ?? Array.Empty<CharacterMemoryProgressSnapshot>();
        }

        internal IReadOnlyList<CharacterMemoryProgressSnapshot> Snapshots { get; }
        internal int GetSnapshotsCalls { get; private set; }

        public IReadOnlyList<CharacterMemoryProgressSnapshot> GetSnapshots()
        {
            GetSnapshotsCalls++;
            return Snapshots;
        }
    }

    internal sealed class FutureVersionMemoryRepository : ICharacterMemoryRepository
    {
        internal int SaveCalls { get; private set; }

        public CharacterMemoryLoadResult Load()
        {
            return new CharacterMemoryLoadResult(null, CharacterMemoryStoreStatus.FutureVersion, false);
        }

        public void Save(CharacterMemoryDocument document)
        {
            SaveCalls++;
        }
    }
}
