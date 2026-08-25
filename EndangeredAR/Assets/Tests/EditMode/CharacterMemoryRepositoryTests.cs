using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EndangeredAR.Memory;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryRepositoryTests
    {
        private static readonly DateTime FixedUtc = new DateTime(2026, 8, 25, 2, 15, 0, DateTimeKind.Utc);

        private string temporaryDirectory;
        private string memoryPath;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "endangeredar-memory-repository-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            memoryPath = Path.Combine(temporaryDirectory, "character-memory.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void CharacterMemoryRepositoryType_IsAvailableInRuntimeAssembly()
        {
            Assert.That(
                Type.GetType("EndangeredAR.Memory.JsonCharacterMemoryRepository, EndangeredAR.Runtime"),
                Is.Not.Null);
        }

        [Test]
        public void RepositoryContract_ExposesVersionedLoadAndSave()
        {
            var assembly = Type.GetType(
                "EndangeredAR.Memory.JsonCharacterMemoryRepository, EndangeredAR.Runtime")?.Assembly;
            var repositoryType = assembly?.GetType("EndangeredAR.Memory.JsonCharacterMemoryRepository");
            var documentType = assembly?.GetType("EndangeredAR.Memory.CharacterMemoryDocument");
            var loadResultType = assembly?.GetType("EndangeredAR.Memory.CharacterMemoryLoadResult");

            Assert.That(documentType, Is.Not.Null);
            Assert.That(loadResultType, Is.Not.Null);
            Assert.That(repositoryType?.GetConstructors().Any(candidate =>
                candidate.GetParameters().Length == 2 &&
                candidate.GetParameters()[0].ParameterType == typeof(string)), Is.True);
            Assert.That(repositoryType?.GetMethod("Load")?.ReturnType, Is.EqualTo(loadResultType));
            Assert.That(
                repositoryType?.GetMethod("Save")?.GetParameters().Single().ParameterType,
                Is.EqualTo(documentType));
        }

        [Test]
        public void ProtocolAndIdContracts_AreStrictAndCaseSensitive()
        {
            var assembly = Type.GetType(
                "EndangeredAR.Memory.JsonCharacterMemoryRepository, EndangeredAR.Runtime").Assembly;
            var eventProtocol = assembly.GetType("EndangeredAR.Memory.CharacterMemoryEventTypeProtocol");
            var originProtocol = assembly.GetType("EndangeredAR.Memory.CharacterMemoryEventOriginProtocol");
            var idValidator = assembly.GetType("EndangeredAR.Memory.CharacterMemoryIdValidator");

            Assert.That(eventProtocol, Is.Not.Null);
            Assert.That(originProtocol, Is.Not.Null);
            Assert.That(idValidator, Is.Not.Null);

            var eventParser = eventProtocol.GetMethod("TryParseExact");
            var validEvent = new object[] { "animal_discovered", null };
            var uppercaseEvent = new object[] { "Animal_Discovered", null };
            var paddedEvent = new object[] { " animal_discovered", null };

            Assert.That(eventParser.Invoke(null, validEvent), Is.True);
            Assert.That(eventParser.Invoke(null, uppercaseEvent), Is.False);
            Assert.That(eventParser.Invoke(null, paddedEvent), Is.False);

            var originParser = originProtocol.GetMethod("TryParseExact");
            Assert.That(originParser.Invoke(null, new object[] { "business", null }), Is.True);
            Assert.That(originParser.Invoke(null, new object[] { "Business", null }), Is.False);

            var isValidId = idValidator.GetMethod("IsValid");
            Assert.That(isValidId.Invoke(null, new object[] { "sensen.diet" }), Is.True);
            Assert.That(isValidId.Invoke(null, new object[] { "Sensen" }), Is.False);
            Assert.That(isValidId.Invoke(null, new object[] { "sensen|diet" }), Is.False);
            Assert.That(isValidId.Invoke(null, new object[] { "sensen\nignore" }), Is.False);
            Assert.That(isValidId.Invoke(null, new object[] { new string('a', 97) }), Is.False);
        }

        [Test]
        public void Load_MissingFileCreatesEmptyPrimaryAndBackup()
        {
            var result = CreateRepository().Load();

            Assert.That(result.Status, Is.EqualTo(CharacterMemoryStoreStatus.Available));
            Assert.That(result.CanWrite, Is.True);
            Assert.That(result.Document.schemaVersion, Is.EqualTo(1));
            Assert.That(result.Document.profiles, Is.Empty);
            Assert.That(File.Exists(memoryPath), Is.True);
            Assert.That(File.Exists(memoryPath + ".bak"), Is.True);
            Assert.That(Path.GetDirectoryName(memoryPath), Is.Not.EqualTo(Application.persistentDataPath));
        }

        [Test]
        public void SaveAndLoad_RoundTripsDefensiveCopyAndUnknownEventType()
        {
            var repository = CreateRepository();
            repository.Load();
            var document = CreateDocumentWithEvent("future_event");

            repository.Save(document);
            document.profiles[0].animals[0].events[0].subjectId = "mutated-after-save";
            var loaded = repository.Load();

            Assert.That(loaded.Document, Is.Not.SameAs(document));
            Assert.That(loaded.Document.profiles[0].animals[0].events[0].eventType, Is.EqualTo("future_event"));
            Assert.That(loaded.Document.profiles[0].animals[0].events[0].subjectId, Is.EqualTo("sensen.diet"));
            Assert.That(File.Exists(memoryPath + ".tmp"), Is.False);
        }

        [Test]
        public void Load_CorruptPrimaryRestoresValidBackupAndQuarantinesPrimary()
        {
            var repository = CreateRepository();
            repository.Load();
            repository.Save(CreateDocumentWithEvent("knowledge_learned"));
            File.WriteAllText(memoryPath, "{ broken primary");

            var recovered = repository.Load();

            Assert.That(recovered.Status, Is.EqualTo(CharacterMemoryStoreStatus.RecoveredFromBackup));
            Assert.That(recovered.Document.profiles[0].animals[0].events[0].subjectId, Is.EqualTo("sensen.diet"));
            Assert.That(Directory.GetFiles(temporaryDirectory, "character-memory.json.corrupt-*").Length, Is.EqualTo(1));
            Assert.DoesNotThrow(() => JsonUtility.FromJson<CharacterMemoryDocument>(File.ReadAllText(memoryPath)));
        }

        [Test]
        public void Load_CorruptPrimaryAndBackupCreatesWritableEmptyDocument()
        {
            File.WriteAllText(memoryPath, "{ broken primary");
            File.WriteAllText(memoryPath + ".bak", "{ broken backup");

            var recovered = CreateRepository().Load();

            Assert.That(recovered.Status, Is.EqualTo(CharacterMemoryStoreStatus.RecoveredEmpty));
            Assert.That(recovered.CanWrite, Is.True);
            Assert.That(recovered.Document.profiles, Is.Empty);
            Assert.That(Directory.GetFiles(temporaryDirectory, "*.corrupt-*").Length, Is.EqualTo(2));
            Assert.That(File.Exists(memoryPath), Is.True);
            Assert.That(File.Exists(memoryPath + ".bak"), Is.True);
        }

        [Test]
        public void FutureVersionDoesNotGetOverwritten()
        {
            const string futureJson = "{\"schemaVersion\":2,\"profiles\":[],\"futureField\":\"keep-me\"}";
            File.WriteAllText(memoryPath, futureJson);
            var repository = CreateRepository();

            var result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(CharacterMemoryStoreStatus.FutureVersion));
            Assert.That(result.CanWrite, Is.False);
            Assert.Throws<InvalidOperationException>(() => repository.Save(new CharacterMemoryDocument()));
            Assert.That(File.ReadAllText(memoryPath), Is.EqualTo(futureJson));
        }

        [Test]
        public void SaveWithoutLoad_FutureVersionDoesNotGetOverwritten()
        {
            const string futureJson = "{\"schemaVersion\":2,\"profiles\":[],\"futureField\":\"keep-me\"}";
            File.WriteAllText(memoryPath, futureJson);

            Assert.Throws<InvalidOperationException>(() =>
                CreateRepository().Save(new CharacterMemoryDocument()));
            Assert.That(File.ReadAllText(memoryPath), Is.EqualTo(futureJson));
        }

        [Test]
        public void Load_SameSchemaUnknownFieldsDoNotBlockKnownRecords()
        {
            const string json = "{\"schemaVersion\":1,\"futureField\":true,\"profiles\":[{\"profileKey\":\"local-default\",\"animals\":[{\"animalId\":\"sensen\",\"events\":[{\"schemaVersion\":1,\"eventId\":\"event-1\",\"idempotencyKey\":\"v1|local-default|sensen|future_event|sensen\",\"profileKey\":\"local-default\",\"animalId\":\"sensen\",\"eventType\":\"future_event\",\"subjectId\":\"sensen\",\"eventOrigin\":\"business\",\"unknown\":42}]}]}]}";
            File.WriteAllText(memoryPath, json);

            var result = CreateRepository().Load();

            Assert.That(result.Status, Is.EqualTo(CharacterMemoryStoreStatus.Available));
            Assert.That(result.Document.profiles[0].animals[0].events[0].eventType, Is.EqualTo("future_event"));
        }

        [Test]
        public void Schema_DoesNotExposeSensitiveOrFreeTextFields()
        {
            var fieldNames = typeof(CharacterMemoryEventRecord)
                .GetFields()
                .Select(field => field.Name)
                .Concat(typeof(CharacterMemoryProfile).GetFields().Select(field => field.Name))
                .ToArray();

            Assert.That(fieldNames, Does.Not.Contain("chat"));
            Assert.That(fieldNames, Does.Not.Contain("message"));
            Assert.That(fieldNames, Does.Not.Contain("reply"));
            Assert.That(fieldNames, Does.Not.Contain("prompt"));
            Assert.That(fieldNames, Does.Not.Contain("userId"));
            Assert.That(fieldNames, Does.Not.Contain("deviceId"));
        }

        private JsonCharacterMemoryRepository CreateRepository()
        {
            return new JsonCharacterMemoryRepository(memoryPath, () => FixedUtc);
        }

        private static CharacterMemoryDocument CreateDocumentWithEvent(string eventType)
        {
            return new CharacterMemoryDocument
            {
                profiles = new List<CharacterMemoryProfile>
                {
                    new CharacterMemoryProfile
                    {
                        profileKey = "local-default",
                        animals = new List<CharacterMemoryRecord>
                        {
                            new CharacterMemoryRecord
                            {
                                animalId = "sensen",
                                events = new List<CharacterMemoryEventRecord>
                                {
                                    new CharacterMemoryEventRecord
                                    {
                                        eventId = "event-1",
                                        idempotencyKey = "v1|local-default|sensen|" + eventType + "|sensen.diet",
                                        profileKey = "local-default",
                                        animalId = "sensen",
                                        eventType = eventType,
                                        subjectId = "sensen.diet",
                                        occurredAtUtc = "2026-08-25T02:15:00.0000000Z",
                                        eventOrigin = "business"
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
