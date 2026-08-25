using System;
using System.Collections.Generic;
using System.IO;
using EndangeredAR.Memory;
using EndangeredAR.Progress;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryServiceTests
    {
        [Test]
        public void TransitionAndMemoryServiceContracts_ExistInRuntimeAssembly()
        {
            var runtimeAssembly = Type.GetType(
                "EndangeredAR.Memory.JsonCharacterMemoryRepository, EndangeredAR.Runtime").Assembly;

            Assert.That(runtimeAssembly.GetType("EndangeredAR.Progress.AnimalProgressTransition"), Is.Not.Null);
            Assert.That(runtimeAssembly.GetType("EndangeredAR.Progress.AnimalProgressTransitionBatch"), Is.Not.Null);
            Assert.That(runtimeAssembly.GetType("EndangeredAR.Progress.IAnimalProgressTransitionSink"), Is.Not.Null);
            Assert.That(runtimeAssembly.GetType("EndangeredAR.Memory.CharacterMemoryService"), Is.Not.Null);
            Assert.That(runtimeAssembly.GetType("EndangeredAR.Memory.CharacterMemoryProjection"), Is.Not.Null);
        }

        [Test]
        public void AppendBatch_PersistsThreeTrustedEventsWithOneSave()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var service = CreateService(repository);
            service.Initialize();

            service.AppendBatch(CreateMissionBatch());

            var events = repository.Stored.profiles[0].animals[0].events;
            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events[0].idempotencyKey, Is.EqualTo(
                "v1|local-default|sensen|mission_completed|sensen-food-mission"));
            Assert.That(events[1].idempotencyKey, Is.EqualTo(
                "v1|local-default|sensen|knowledge_learned|sensen.diet"));
            Assert.That(events[2].idempotencyKey, Is.EqualTo(
                "v1|local-default|sensen|badge_earned|eco-guardian-sensen"));
            Assert.That(events.ConvertAll(memoryEvent => memoryEvent.eventOrigin), Is.EqualTo(new[]
            {
                "business", "business", "business"
            }));
            Assert.That(service.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.Saved));
        }

        [Test]
        public void AppendBatch_DeduplicatesLiveAndFoldedEvents()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var service = CreateService(repository);
            service.Initialize();

            service.AppendBatch(CreateMissionBatch());
            service.AppendBatch(CreateMissionBatch());

            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(repository.Stored.profiles[0].animals[0].events.Count, Is.EqualTo(3));
            Assert.That(service.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.NoChanges));

            var stored = repository.Stored;
            var record = stored.profiles[0].animals[0];
            var foldedEvent = record.events[0];
            record.foldedProjection.completedMissionIds.Add(foldedEvent.subjectId);
            record.foldedProjection.idempotencyKeys.Add(foldedEvent.idempotencyKey);
            record.events.RemoveAt(0);
            repository.ReplaceStored(stored);

            var reloaded = CreateService(repository);
            reloaded.Initialize();
            reloaded.AppendBatch(new AnimalProgressTransitionBatch(
                "sensen",
                FixedOccurredAt,
                new[]
                {
                    new AnimalProgressTransition(
                        AnimalProgressTransitionType.MissionCompleted,
                        "sensen-food-mission")
                }));

            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(reloaded.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.NoChanges));
        }

        [Test]
        public void AppendBatch_DoesNotTrustAnUncorroboratedFoldedIdempotencyKey()
        {
            var document = CreateDocumentWithEvents(0, false);
            document.profiles[0].animals[0].foldedProjection.idempotencyKeys.Add(
                "v1|local-default|sensen|mission_completed|sensen-food-mission");
            var repository = new InMemoryCharacterMemoryRepository(document);
            var service = CreateService(repository);
            service.Initialize();

            service.AppendBatch(new AnimalProgressTransitionBatch(
                "sensen",
                FixedOccurredAt,
                new[]
                {
                    new AnimalProgressTransition(
                        AnimalProgressTransitionType.MissionCompleted,
                        "sensen-food-mission")
                }));

            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(repository.Stored.profiles[0].animals[0].events.Count, Is.EqualTo(1));
            Assert.That(service.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.Saved));
        }

        [Test]
        public void AppendBatch_SaveFailureLeavesNoPartialEvents()
        {
            var repository = new InMemoryCharacterMemoryRepository { ThrowOnSave = true };
            var service = CreateService(repository);
            service.Initialize();

            service.AppendBatch(CreateMissionBatch());

            Assert.That(repository.SaveAttempts, Is.EqualTo(1));
            Assert.That(repository.Stored.profiles, Is.Empty);
            Assert.That(service.GetProjection("sensen").CompletedMissionIds, Is.Empty);
            Assert.That(service.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.SaveFailed));
        }

        [Test]
        public void AppendBatch_InvalidTransitionRejectsWholeBatch()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var service = CreateService(repository);
            service.Initialize();
            var batch = new AnimalProgressTransitionBatch(
                "sensen",
                FixedOccurredAt,
                new[]
                {
                    new AnimalProgressTransition(AnimalProgressTransitionType.KnowledgeLearned, "sensen.diet"),
                    new AnimalProgressTransition(AnimalProgressTransitionType.BadgeEarned, "Contains User Text")
                });

            service.AppendBatch(batch);

            Assert.That(repository.SaveCalls, Is.Zero);
            Assert.That(repository.Stored.profiles, Is.Empty);
            Assert.That(service.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.InvalidInput));
        }

        [Test]
        public void AppendBatch_FoldsOldestSupportedEventAtSixtyFive()
        {
            var repository = new InMemoryCharacterMemoryRepository(CreateDocumentWithEvents(64, false));
            var service = CreateService(repository);
            service.Initialize();

            service.AppendBatch(new AnimalProgressTransitionBatch(
                "sensen",
                "2026-08-25T04:00:00.0000000Z",
                new[]
                {
                    new AnimalProgressTransition(AnimalProgressTransitionType.KnowledgeLearned, "knowledge-64")
                }));

            var record = repository.Stored.profiles[0].animals[0];
            Assert.That(record.events.Count, Is.EqualTo(64));
            Assert.That(record.foldedProjection.learnedKnowledgeIds, Does.Contain("knowledge-00"));
            Assert.That(record.foldedProjection.idempotencyKeys, Does.Contain(
                "v1|local-default|sensen|knowledge_learned|knowledge-00"));
            Assert.That(service.GetProjection("sensen").LearnedKnowledgeIds.Count, Is.EqualTo(65));
        }

        [Test]
        public void AppendBatch_SixtyFourUnknownEventsRejectsWithoutDeletingFutureData()
        {
            var repository = new InMemoryCharacterMemoryRepository(CreateDocumentWithEvents(64, true));
            var service = CreateService(repository);
            service.Initialize();

            service.AppendBatch(new AnimalProgressTransitionBatch(
                "sensen",
                FixedOccurredAt,
                new[]
                {
                    new AnimalProgressTransition(AnimalProgressTransitionType.KnowledgeLearned, "sensen.diet")
                }));

            Assert.That(repository.SaveCalls, Is.Zero);
            Assert.That(repository.Stored.profiles[0].animals[0].events.Count, Is.EqualTo(64));
            Assert.That(
                repository.Stored.profiles[0].animals[0].events.ConvertAll(memoryEvent => memoryEvent.eventType),
                Is.All.EqualTo("future_event"));
            Assert.That(service.LastOperationResult, Is.EqualTo(CharacterMemoryOperationResult.CapacityExceeded));
        }

        [Test]
        public void AppendBatch_IsolatesAnimalAndProfilePartitions()
        {
            var repository = new InMemoryCharacterMemoryRepository();
            var defaultService = CreateService(repository);
            defaultService.Initialize();
            defaultService.AppendBatch(CreateDiscoveryBatch("sensen"));

            var secondProfile = new CharacterMemoryService(
                repository,
                "test-profile",
                () => FixedUtc,
                NextEventId);
            secondProfile.Initialize();
            secondProfile.AppendBatch(CreateDiscoveryBatch("pangolin"));

            Assert.That(repository.Stored.profiles.Count, Is.EqualTo(2));
            Assert.That(defaultService.GetProjection("sensen").Discovered, Is.True);
            Assert.That(defaultService.GetProjection("pangolin").Discovered, Is.False);
            Assert.That(secondProfile.GetProjection("pangolin").Discovered, Is.True);
            Assert.That(secondProfile.GetProjection("sensen").Discovered, Is.False);
        }

        private const string FixedOccurredAt = "2026-08-25T02:30:00.0000000Z";
        private static readonly DateTime FixedUtc = new DateTime(2026, 8, 25, 2, 30, 0, DateTimeKind.Utc);
        private static int eventSequence;

        private static CharacterMemoryService CreateService(InMemoryCharacterMemoryRepository repository)
        {
            eventSequence = 0;
            return new CharacterMemoryService(repository, () => FixedUtc, NextEventId);
        }

        private static string NextEventId()
        {
            eventSequence++;
            return "event-" + eventSequence.ToString("00");
        }

        private static AnimalProgressTransitionBatch CreateMissionBatch()
        {
            return new AnimalProgressTransitionBatch(
                "sensen",
                FixedOccurredAt,
                new[]
                {
                    new AnimalProgressTransition(
                        AnimalProgressTransitionType.MissionCompleted,
                        "sensen-food-mission"),
                    new AnimalProgressTransition(
                        AnimalProgressTransitionType.KnowledgeLearned,
                        "sensen.diet"),
                    new AnimalProgressTransition(
                        AnimalProgressTransitionType.BadgeEarned,
                        "eco-guardian-sensen")
                });
        }

        private static AnimalProgressTransitionBatch CreateDiscoveryBatch(string animalId)
        {
            return new AnimalProgressTransitionBatch(
                animalId,
                FixedOccurredAt,
                new[]
                {
                    new AnimalProgressTransition(AnimalProgressTransitionType.AnimalDiscovered, animalId)
                });
        }

        internal static CharacterMemoryDocument CreateDocumentWithEvents(int count, bool unknown)
        {
            var record = new CharacterMemoryRecord { animalId = "sensen" };
            for (var index = 0; index < count; index++)
            {
                var subjectId = "knowledge-" + index.ToString("00");
                var eventType = unknown ? "future_event" : "knowledge_learned";
                record.events.Add(new CharacterMemoryEventRecord
                {
                    eventId = "seed-" + index.ToString("00"),
                    idempotencyKey = "v1|local-default|sensen|" + eventType + "|" + subjectId,
                    profileKey = "local-default",
                    animalId = "sensen",
                    eventType = eventType,
                    subjectId = subjectId,
                    occurredAtUtc = FixedUtc.AddMinutes(index).ToString("o"),
                    eventOrigin = "business"
                });
            }

            return new CharacterMemoryDocument
            {
                profiles = new List<CharacterMemoryProfile>
                {
                    new CharacterMemoryProfile
                    {
                        profileKey = "local-default",
                        animals = new List<CharacterMemoryRecord> { record }
                    }
                }
            };
        }
    }

    internal sealed class InMemoryCharacterMemoryRepository : ICharacterMemoryRepository
    {
        internal InMemoryCharacterMemoryRepository(CharacterMemoryDocument initial = null)
        {
            Stored = CharacterMemoryDocumentUtility.Clone(initial ?? new CharacterMemoryDocument());
        }

        internal CharacterMemoryDocument Stored { get; private set; }
        internal int SaveAttempts { get; private set; }
        internal int SaveCalls { get; private set; }
        internal bool ThrowOnSave { get; set; }

        public CharacterMemoryLoadResult Load()
        {
            return new CharacterMemoryLoadResult(
                CharacterMemoryDocumentUtility.Clone(Stored),
                CharacterMemoryStoreStatus.Available,
                true);
        }

        public void Save(CharacterMemoryDocument document)
        {
            SaveAttempts++;
            if (ThrowOnSave)
            {
                throw new IOException("simulated memory save failure");
            }

            SaveCalls++;
            Stored = CharacterMemoryDocumentUtility.Clone(document);
        }

        internal void ReplaceStored(CharacterMemoryDocument document)
        {
            Stored = CharacterMemoryDocumentUtility.Clone(document);
        }
    }
}
