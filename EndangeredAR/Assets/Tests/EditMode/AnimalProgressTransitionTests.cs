using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EndangeredAR.Progress;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class AnimalProgressTransitionTests
    {
        private readonly List<UnityEngine.GameObject> createdObjects = new List<UnityEngine.GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void ProgressService_ExposesRepositoryAndTransitionSinkTestBoundary()
        {
            var runtimeAssembly = typeof(AnimalProgressService).Assembly;
            var repositoryType = runtimeAssembly.GetType("EndangeredAR.Progress.IAnimalProgressRepository");
            var sinkType = runtimeAssembly.GetType("EndangeredAR.Progress.IAnimalProgressTransitionSink");

            Assert.That(repositoryType, Is.Not.Null);
            Assert.That(sinkType, Is.Not.Null);
            Assert.That(
                typeof(AnimalProgressService).GetMethod(
                    "InitializeForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { repositoryType, sinkType, typeof(Func<DateTime>) },
                    null),
                Is.Not.Null);
        }

        [Test]
        public void Unlock_SaveFailureDoesNotMutateCacheEmitTransitionOrNotify()
        {
            var repository = new RecordingProgressRepository { ThrowOnSave = true };
            var sink = new RecordingTransitionSink();
            var service = CreateService(repository, sink);
            var notifications = 0;
            service.ProgressChanged += _ => notifications++;

            Assert.Throws<IOException>(() => service.Unlock("sensen"));

            Assert.That(service.IsUnlocked("sensen"), Is.False);
            Assert.That(service.TryGetSnapshot("sensen", out _), Is.False);
            Assert.That(sink.Batches, Is.Empty);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Mission_SaveFailureDoesNotMutateAnyProgressFieldOrEmit()
        {
            var repository = new RecordingProgressRepository { ThrowOnSave = true };
            var sink = new RecordingTransitionSink();
            var service = CreateService(repository, sink);

            Assert.Throws<IOException>(() => service.MarkMissionCompleted(
                "sensen",
                "sensen-food-mission",
                "eco-guardian-sensen",
                "sensen.diet"));

            Assert.That(service.TryGetSnapshot("sensen", out _), Is.False);
            Assert.That(sink.Batches, Is.Empty);
        }

        [Test]
        public void Unlock_EmitsOneTransitionAfterSaveBeforeProgressNotification()
        {
            var operations = new List<string>();
            var repository = new RecordingProgressRepository(operationLog: operations);
            var sink = new RecordingTransitionSink(operations);
            var service = CreateService(repository, sink);
            service.ProgressChanged += _ => operations.Add("notify");

            var unlocked = service.Unlock("sensen");

            Assert.That(unlocked, Is.True);
            Assert.That(operations, Is.EqualTo(new[] { "save", "sink", "notify" }));
            Assert.That(sink.Batches, Has.Count.EqualTo(1));
            Assert.That(sink.Batches[0].AnimalId, Is.EqualTo("sensen"));
            Assert.That(sink.Batches[0].OccurredAtUtc, Is.EqualTo(FixedOccurredAt));
            Assert.That(sink.Batches[0].Transitions, Has.Count.EqualTo(1));
            Assert.That(sink.Batches[0].Transitions[0].Type, Is.EqualTo(AnimalProgressTransitionType.AnimalDiscovered));
            Assert.That(sink.Batches[0].Transitions[0].SubjectId, Is.EqualTo("sensen"));
        }

        [Test]
        public void Mission_EmitsOnlyNewDeltasInStableOrderAndRepeatedCallDoesNothing()
        {
            var initial = JsonAnimalProgressRepository.CreateEmptyDocument();
            initial.animals.Add(new AnimalProgressRecord
            {
                animalId = "sensen",
                learnedKnowledgeIds = new List<string> { "sensen.diet" }
            });
            var repository = new RecordingProgressRepository(initial);
            var sink = new RecordingTransitionSink();
            var service = CreateService(repository, sink);
            var notifications = 0;
            service.ProgressChanged += _ => notifications++;

            service.MarkMissionCompleted(
                "sensen",
                "sensen-food-mission",
                "eco-guardian-sensen",
                "sensen.diet");
            service.MarkMissionCompleted(
                "sensen",
                "sensen-food-mission",
                "eco-guardian-sensen",
                "sensen.diet");

            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(sink.Batches, Has.Count.EqualTo(1));
            Assert.That(sink.Batches[0].Transitions, Has.Count.EqualTo(2));
            Assert.That(sink.Batches[0].Transitions[0].Type, Is.EqualTo(AnimalProgressTransitionType.MissionCompleted));
            Assert.That(sink.Batches[0].Transitions[0].SubjectId, Is.EqualTo("sensen-food-mission"));
            Assert.That(sink.Batches[0].Transitions[1].Type, Is.EqualTo(AnimalProgressTransitionType.BadgeEarned));
            Assert.That(sink.Batches[0].Transitions[1].SubjectId, Is.EqualTo("eco-guardian-sensen"));
        }

        [Test]
        public void Mission_FirstCompletionEmitsMissionKnowledgeAndBadgeInStableOrder()
        {
            var repository = new RecordingProgressRepository();
            var sink = new RecordingTransitionSink();
            var service = CreateService(repository, sink);

            service.MarkMissionCompleted(
                "sensen",
                "sensen-food-mission",
                "eco-guardian-sensen",
                "sensen.diet");

            Assert.That(sink.Batches, Has.Count.EqualTo(1));
            Assert.That(sink.Batches[0].Transitions, Has.Count.EqualTo(3));
            Assert.That(sink.Batches[0].Transitions[0].Type, Is.EqualTo(AnimalProgressTransitionType.MissionCompleted));
            Assert.That(sink.Batches[0].Transitions[1].Type, Is.EqualTo(AnimalProgressTransitionType.KnowledgeLearned));
            Assert.That(sink.Batches[0].Transitions[2].Type, Is.EqualTo(AnimalProgressTransitionType.BadgeEarned));
        }

        [Test]
        public void ReplaceConversation_SavesAndNotifiesWithoutMemoryTransition()
        {
            var repository = new RecordingProgressRepository();
            var sink = new RecordingTransitionSink();
            var service = CreateService(repository, sink);
            var notifications = 0;
            service.ProgressChanged += _ => notifications++;

            service.ReplaceConversation("sensen", new[]
            {
                new ConversationRecord { role = "user", content = "hello" }
            });

            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(sink.Batches, Is.Empty);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(service.GetConversation("sensen"), Has.Count.EqualTo(1));
        }

        [Test]
        public void ReplaceConversation_SaveFailureLeavesPreviousConversationInCache()
        {
            var initial = JsonAnimalProgressRepository.CreateEmptyDocument();
            initial.animals.Add(new AnimalProgressRecord
            {
                animalId = "sensen",
                recentConversation = new List<ConversationRecord>
                {
                    new ConversationRecord { role = "assistant", content = "before" }
                }
            });
            var repository = new RecordingProgressRepository(initial) { ThrowOnSave = true };
            var sink = new RecordingTransitionSink();
            var service = CreateService(repository, sink);

            Assert.Throws<IOException>(() => service.ReplaceConversation("sensen", new[]
            {
                new ConversationRecord { role = "user", content = "after" }
            }));

            Assert.That(service.GetConversation("sensen"), Has.Count.EqualTo(1));
            Assert.That(service.GetConversation("sensen")[0].content, Is.EqualTo("before"));
            Assert.That(sink.Batches, Is.Empty);
        }

        [Test]
        public void ThrowingTransitionSinkDoesNotRollbackProgressOrSuppressNotification()
        {
            var repository = new RecordingProgressRepository();
            var sink = new RecordingTransitionSink { ThrowOnAppend = true };
            var service = CreateService(repository, sink);
            var notifications = 0;
            service.ProgressChanged += _ => notifications++;

            Assert.That(service.Unlock("sensen"), Is.True);

            Assert.That(service.IsUnlocked("sensen"), Is.True);
            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(sink.Batches, Has.Count.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void GetOrCreate_MissingAnimalReturnsDetachedDefaultWithoutCreatingProgress()
        {
            var repository = new RecordingProgressRepository();
            var service = CreateService(repository, new RecordingTransitionSink());

            var snapshot = service.GetOrCreate("sensen");
            snapshot.unlocked = true;

            Assert.That(snapshot.animalId, Is.EqualTo("sensen"));
            Assert.That(repository.SaveCalls, Is.Zero);
            Assert.That(service.TryGetSnapshot("sensen", out _), Is.False);
            Assert.That(service.IsUnlocked("sensen"), Is.False);
        }

        [Test]
        public void InvalidMissionIdRejectsTheWholeBusinessCommand()
        {
            var repository = new RecordingProgressRepository();
            var sink = new RecordingTransitionSink();
            var service = CreateService(repository, sink);

            service.MarkMissionCompleted(
                "sensen",
                "Localized Mission Title",
                "eco-guardian-sensen",
                "sensen.diet");

            Assert.That(repository.SaveCalls, Is.Zero);
            Assert.That(service.TryGetSnapshot("sensen", out _), Is.False);
            Assert.That(sink.Batches, Is.Empty);
        }

        private const string FixedOccurredAt = "2026-08-25T03:15:00.0000000Z";
        private static readonly DateTime FixedUtc = new DateTime(2026, 8, 25, 3, 15, 0, DateTimeKind.Utc);

        private AnimalProgressService CreateService(
            RecordingProgressRepository repository,
            RecordingTransitionSink sink)
        {
            var host = new UnityEngine.GameObject("Animal Progress Transition Tests");
            createdObjects.Add(host);
            var service = host.AddComponent<AnimalProgressService>();
            service.InitializeForTests(repository, sink, () => FixedUtc);
            return service;
        }
    }

    internal sealed class RecordingProgressRepository : IAnimalProgressRepository
    {
        private readonly IList<string> operationLog;

        internal RecordingProgressRepository(
            AnimalProgressDocument initial = null,
            IList<string> operationLog = null)
        {
            Stored = JsonAnimalProgressRepository.NormalizeDocument(
                initial ?? JsonAnimalProgressRepository.CreateEmptyDocument());
            this.operationLog = operationLog;
        }

        internal AnimalProgressDocument Stored { get; private set; }
        internal int SaveCalls { get; private set; }
        internal bool ThrowOnSave { get; set; }

        public AnimalProgressDocument Load()
        {
            return JsonAnimalProgressRepository.NormalizeDocument(Stored);
        }

        public void Save(AnimalProgressDocument document)
        {
            operationLog?.Add("save");
            if (ThrowOnSave)
            {
                throw new IOException("simulated progress save failure");
            }

            SaveCalls++;
            Stored = JsonAnimalProgressRepository.NormalizeDocument(document);
        }
    }

    internal sealed class RecordingTransitionSink : IAnimalProgressTransitionSink
    {
        private readonly IList<string> operationLog;

        internal RecordingTransitionSink(IList<string> operationLog = null)
        {
            this.operationLog = operationLog;
        }

        internal List<AnimalProgressTransitionBatch> Batches { get; } =
            new List<AnimalProgressTransitionBatch>();
        internal bool ThrowOnAppend { get; set; }

        public void AppendBatch(AnimalProgressTransitionBatch batch)
        {
            operationLog?.Add("sink");
            Batches.Add(batch);
            if (ThrowOnAppend)
            {
                throw new IOException("simulated memory sink failure");
            }
        }
    }
}
