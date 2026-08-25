using System.Collections.Generic;
using EndangeredAR.Memory;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryProjectionTests
    {
        [Test]
        public void Projection_MergesFoldedAndLiveSetsInStableOrder()
        {
            var document = CharacterMemoryServiceTests.CreateDocumentWithEvents(3, false);
            var record = document.profiles[0].animals[0];
            record.foldedProjection.discovered = true;
            record.foldedProjection.completedMissionIds.Add("mission-z");
            record.foldedProjection.completedMissionIds.Add("mission-a");
            record.foldedProjection.learnedKnowledgeIds.Add("knowledge-folded");
            record.foldedProjection.earnedBadgeIds.Add("badge-z");
            record.events.Reverse();
            var repository = new InMemoryCharacterMemoryRepository(document);
            var service = new CharacterMemoryService(repository, () => default, () => "event-projection");
            service.Initialize();

            var projection = service.GetProjection("sensen");

            Assert.That(projection.Discovered, Is.True);
            Assert.That(projection.CompletedMissionIds, Is.EqualTo(new[] { "mission-a", "mission-z" }));
            Assert.That(projection.LearnedKnowledgeIds, Is.EqualTo(new[]
            {
                "knowledge-00", "knowledge-01", "knowledge-02", "knowledge-folded"
            }));
            Assert.That(projection.EarnedBadgeIds, Is.EqualTo(new[] { "badge-z" }));
        }

        [Test]
        public void Projection_RecentMilestonesKeepsLatestEightBusinessEvents()
        {
            var document = CharacterMemoryServiceTests.CreateDocumentWithEvents(10, false);
            var record = document.profiles[0].animals[0];
            record.events[9].eventOrigin = "bootstrap";
            record.events[8].eventOrigin = "reconcile";
            var repository = new InMemoryCharacterMemoryRepository(document);
            var service = new CharacterMemoryService(repository, () => default, () => "event-projection");
            service.Initialize();

            var milestones = service.GetProjection("sensen").RecentMilestones;

            Assert.That(milestones.Count, Is.EqualTo(8));
            Assert.That(milestones[0].SubjectId, Is.EqualTo("knowledge-07"));
            Assert.That(milestones[7].SubjectId, Is.EqualTo("knowledge-00"));
        }

        [Test]
        public void Projection_IgnoresUnknownMalformedAndWrongPartitionEvents()
        {
            var document = CharacterMemoryServiceTests.CreateDocumentWithEvents(1, false);
            var record = document.profiles[0].animals[0];
            record.events.Add(CreateEvent("future_event", "future-subject", "local-default", "sensen"));
            record.events.Add(CreateEvent("badge_earned", "Badge With Spaces", "local-default", "sensen"));
            record.events.Add(CreateEvent("badge_earned", "badge-other", "other-profile", "sensen"));
            record.events.Add(CreateEvent("badge_earned", "badge-other-animal", "local-default", "pangolin"));
            var repository = new InMemoryCharacterMemoryRepository(document);
            var service = new CharacterMemoryService(repository, () => default, () => "event-projection");
            service.Initialize();

            var projection = service.GetProjection("sensen");

            Assert.That(projection.LearnedKnowledgeIds, Is.EqualTo(new[] { "knowledge-00" }));
            Assert.That(projection.EarnedBadgeIds, Is.Empty);
            Assert.That(projection.RecentMilestones.Count, Is.EqualTo(1));
        }

        private static CharacterMemoryEventRecord CreateEvent(
            string eventType,
            string subjectId,
            string profileKey,
            string animalId)
        {
            return new CharacterMemoryEventRecord
            {
                eventId = "event-extra",
                idempotencyKey = "v1|" + profileKey + "|" + animalId + "|" + eventType + "|" + subjectId,
                profileKey = profileKey,
                animalId = animalId,
                eventType = eventType,
                subjectId = subjectId,
                occurredAtUtc = "2026-08-25T03:00:00.0000000Z",
                eventOrigin = "business"
            };
        }
    }
}
