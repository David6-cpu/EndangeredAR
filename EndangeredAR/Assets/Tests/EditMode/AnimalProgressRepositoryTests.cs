using System;
using System.Collections.Generic;
using System.IO;
using EndangeredAR.Progress;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AnimalProgressRepositoryTests
    {
        private readonly List<string> temporaryDirectories = new List<string>();
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            AnimalProgressService.RepositoryPathOverrideForTests = null;

            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();

            foreach (var directory in temporaryDirectories)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }

            temporaryDirectories.Clear();
        }

        [Test]
        public void Load_MissingFileReturnsCurrentEmptyDocument()
        {
            var repository = new JsonAnimalProgressRepository(CreateRepositoryPath());

            var document = repository.Load();

            Assert.That(document.schemaVersion, Is.EqualTo(JsonAnimalProgressRepository.CurrentSchemaVersion));
            Assert.That(document.animals, Is.Empty);
        }

        [Test]
        public void SaveAndLoad_PreservesIndependentAnimalState()
        {
            var path = CreateRepositoryPath();
            var service = CreateService(path);
            service.Unlock(" Pangolin ");
            service.MarkMissionCompleted("leopard", "guardian", "habitat");

            var reloaded = new JsonAnimalProgressRepository(path).Load();

            Assert.That(reloaded.animals, Has.Count.EqualTo(2));
            var pangolin = FindRecord(reloaded, "pangolin");
            var leopard = FindRecord(reloaded, "leopard");
            Assert.That(pangolin.unlocked, Is.True);
            Assert.That(pangolin.missionCompleted, Is.False);
            Assert.That(leopard.unlocked, Is.False);
            Assert.That(leopard.missionCompleted, Is.True);
            Assert.That(leopard.earnedBadgeIds, Is.EqualTo(new[] { "guardian" }));
            Assert.That(leopard.learnedKnowledgeIds, Is.EqualTo(new[] { "habitat" }));
        }

        [Test]
        public void Unlock_ReturnsTrueOnlyForFirstUnlock()
        {
            var service = CreateService(CreateRepositoryPath());

            var firstUnlock = service.Unlock(" Pangolin ");
            var repeatedUnlock = service.Unlock("PANGOLIN");

            Assert.That(firstUnlock, Is.True);
            Assert.That(repeatedUnlock, Is.False);
            Assert.That(service.UnlockedCount, Is.EqualTo(1));
            Assert.That(service.IsUnlocked("pangolin"), Is.True);
        }

        [Test]
        public void Initialize_ExplicitPathAfterAwake_ReplacesTestOverrideRepository()
        {
            var initialPath = CreateRepositoryPath();
            var explicitPath = CreateRepositoryPath();
            AnimalProgressService.RepositoryPathOverrideForTests = initialPath;
            var gameObject = new GameObject("AnimalProgressServiceExplicitPathTest");
            createdObjects.Add(gameObject);
            var service = gameObject.AddComponent<AnimalProgressService>();

            service.Initialize();
            Assert.That(service.ActiveRepositoryPath, Is.EqualTo(Path.GetFullPath(initialPath)));
            service.Initialize(explicitPath);
            service.Unlock("pangolin");

            Assert.That(service.ActiveRepositoryPath, Is.EqualTo(Path.GetFullPath(explicitPath)));
            Assert.That(File.Exists(initialPath), Is.False);
            Assert.That(FindRecord(new JsonAnimalProgressRepository(explicitPath).Load(), "pangolin").unlocked, Is.True);
        }

        [Test]
        public void Conversations_AreTrimmedToTwentyMessagesPerAnimal()
        {
            var service = CreateService(CreateRepositoryPath());
            var messages = new List<ConversationRecord>();
            for (var index = 0; index < 22; index++)
            {
                messages.Add(new ConversationRecord { role = "user", content = index.ToString() });
            }

            service.ReplaceConversation(" PANGOLIN ", messages);
            var returned = service.GetConversation("pangolin");
            returned[0].content = "mutated";

            Assert.That(returned, Has.Count.EqualTo(20));
            Assert.That(returned[0].content, Is.EqualTo("mutated"));
            Assert.That(returned[19].content, Is.EqualTo("21"));
            Assert.That(service.GetConversation("PANGOLIN")[0].content, Is.EqualTo("2"));
        }

        [Test]
        public void Load_CorruptJsonCreatesBackupAndReturnsDefault()
        {
            var path = CreateRepositoryPath();
            File.WriteAllText(path, "{ not-json");
            var repository = new JsonAnimalProgressRepository(path, () => new DateTime(2026, 7, 19, 9, 30, 0, DateTimeKind.Utc));

            var document = repository.Load();

            Assert.That(document.schemaVersion, Is.EqualTo(JsonAnimalProgressRepository.CurrentSchemaVersion));
            Assert.That(document.animals, Is.Empty);
            var backupPath = path + ".corrupt-20260719-093000";
            Assert.That(File.Exists(backupPath), Is.True);
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo("{ not-json"));
        }

        [Test]
        public void Load_PreservesUnknownAnimalRecords()
        {
            var path = CreateRepositoryPath();
            var document = new AnimalProgressDocument();
            document.animals.Add(new AnimalProgressRecord
            {
                animalId = "future-animal",
                unlocked = true,
                earnedBadgeIds = new List<string> { "future-badge" }
            });
            document.animals.Add(new AnimalProgressRecord
            {
                animalId = " Pangolin ",
                unlocked = true
            });
            new JsonAnimalProgressRepository(path).Save(document);

            var service = CreateService(path);
            service.MarkMissionCompleted("pangolin", "guardian", "habitat");
            var reloaded = new JsonAnimalProgressRepository(path).Load();

            Assert.That(reloaded.animals, Has.Count.EqualTo(2));
            var futureAnimal = FindRecord(reloaded, "future-animal");
            Assert.That(futureAnimal.unlocked, Is.True);
            Assert.That(futureAnimal.earnedBadgeIds, Is.EqualTo(new[] { "future-badge" }));
            Assert.That(FindRecord(reloaded, "PANGOLIN").missionCompleted, Is.True);
        }

        private string CreateRepositoryPath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "EndangeredAR-ProgressTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            temporaryDirectories.Add(directory);
            return Path.Combine(directory, "animal-progress.json");
        }

        private AnimalProgressService CreateService(string path)
        {
            AnimalProgressService.RepositoryPathOverrideForTests = path;
            var gameObject = new GameObject("AnimalProgressServiceTest");
            createdObjects.Add(gameObject);
            var service = gameObject.AddComponent<AnimalProgressService>();
            return service;
        }

        private AnimalProgressRecord FindRecord(AnimalProgressDocument document, string animalId)
        {
            foreach (var record in document.animals)
            {
                if (string.Equals(record.animalId, animalId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            Assert.Fail("Animal record was not found: " + animalId);
            return null;
        }
    }
}
