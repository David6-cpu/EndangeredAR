using System;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class ReadOnlyCharacterMemoryContextTests
    {
        [Test]
        public void ContextDtos_ExposeImmutableValueDataOnly()
        {
            var dtoTypes = new[]
            {
                typeof(ReadOnlyCharacterMemoryContext),
                typeof(ReadOnlyCharacterMemoryMilestone)
            };

            foreach (var dtoType in dtoTypes)
            {
                Assert.That(dtoType.GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty, dtoType.Name);
                Assert.That(
                    dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .All(property => property.SetMethod == null),
                    Is.True,
                    dtoType.Name);
            }
        }

        [Test]
        public void Context_CopiesMilestonesAndDoesNotSerializeInternalFingerprint()
        {
            var source = new[]
            {
                new ReadOnlyCharacterMemoryMilestone(
                    CharacterMemoryContextMilestoneKind.MissionCompleted,
                    "帮森森寻找食物")
            };
            var context = ReadOnlyCharacterMemoryContext.Create(
                "sensen",
                CharacterMemoryContextStatus.Available,
                true,
                1,
                1,
                1,
                source);

            source[0] = new ReadOnlyCharacterMemoryMilestone(
                CharacterMemoryContextMilestoneKind.BadgeEarned,
                "不应替换原值");

            Assert.That(context.Milestones.Single().Kind, Is.EqualTo(CharacterMemoryContextMilestoneKind.MissionCompleted));
            Assert.That(context.Milestones.Single().DisplayLabel, Is.EqualTo("帮森森寻找食物"));
            Assert.That(context.Fingerprint, Is.Not.Empty);
            var json = JsonUtility.ToJson(context);
            StringAssert.DoesNotContain("fingerprint", json.ToLowerInvariant());
            StringAssert.DoesNotContain("subjectId", json);
            StringAssert.DoesNotContain("eventId", json);
            StringAssert.DoesNotContain("occurredAtUtc", json);
        }

        [Test]
        public void Fingerprint_IsStableAndChangesWithSafeContext()
        {
            var first = ReadOnlyCharacterMemoryContext.Create(
                "sensen",
                CharacterMemoryContextStatus.Available,
                true,
                1,
                1,
                0,
                new[]
                {
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.KnowledgeLearned,
                        "森森的食性知识")
                });
            var same = ReadOnlyCharacterMemoryContext.Create(
                "sensen",
                CharacterMemoryContextStatus.Available,
                true,
                1,
                1,
                0,
                new[]
                {
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.KnowledgeLearned,
                        "森森的食性知识")
                });
            var cleared = ReadOnlyCharacterMemoryContext.EmptyFor("sensen");

            Assert.That(first.Fingerprint, Is.EqualTo(same.Fingerprint));
            Assert.That(cleared.Fingerprint, Is.Not.EqualTo(first.Fingerprint));
        }

        [TestCase("available", CharacterMemoryContextStatus.Available)]
        [TestCase("empty", CharacterMemoryContextStatus.Empty)]
        [TestCase("unavailable", CharacterMemoryContextStatus.Unavailable)]
        public void StatusProtocol_AcceptsOnlyExactLowercase(string wireValue, CharacterMemoryContextStatus expected)
        {
            Assert.That(CharacterMemoryContextStatusProtocol.TryParseExact(wireValue, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
            Assert.That(CharacterMemoryContextStatusProtocol.ToWireValue(parsed), Is.EqualTo(wireValue));
        }

        [TestCase("Available")]
        [TestCase(" available")]
        [TestCase("available ")]
        [TestCase("")]
        [TestCase(null)]
        public void StatusProtocol_RejectsNonExactValues(string wireValue)
        {
            Assert.That(CharacterMemoryContextStatusProtocol.TryParseExact(wireValue, out _), Is.False);
        }
    }
}
