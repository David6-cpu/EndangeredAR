using System;
using System.IO;
using EndangeredAR.AI;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class MemoryMentionPolicyTests
    {
        [Test]
        public void Policy_MatchesSharedVectors()
        {
            foreach (var item in LoadFixture().cases)
            {
                Assert.That(
                    MemoryMentionModeProtocol.ToWireValue(MemoryMentionPolicy.Classify(item.message)),
                    Is.EqualTo(item.expectedMode),
                    $"{item.category}: {item.message}");
            }
        }

        [TestCase("explicit_recall", MemoryMentionMode.ExplicitRecall)]
        [TestCase("conversation_history_boundary", MemoryMentionMode.ConversationHistoryBoundary)]
        [TestCase("reunion", MemoryMentionMode.Reunion)]
        [TestCase("none", MemoryMentionMode.None)]
        public void MentionModeProtocol_AcceptsOnlyExactValues(string wireValue, MemoryMentionMode expected)
        {
            Assert.That(MemoryMentionModeProtocol.TryParseExact(wireValue, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
        }

        [TestCase("Reunion")]
        [TestCase(" reunion")]
        [TestCase("reunion ")]
        [TestCase("memory_recall")]
        [TestCase("")]
        [TestCase(null)]
        public void MentionModeProtocol_RejectsMalformedValues(string wireValue)
        {
            Assert.That(MemoryMentionModeProtocol.TryParseExact(wireValue, out _), Is.False);
        }

        private static MemoryDialogueFixture LoadFixture()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "content",
                "quality",
                "sensen-memory-dialogue-vectors.json"));
            return JsonUtility.FromJson<MemoryDialogueFixture>(File.ReadAllText(path));
        }

        [Serializable]
        private sealed class MemoryDialogueFixture
        {
            public MemoryDialogueCase[] cases;
        }

        [Serializable]
        private sealed class MemoryDialogueCase
        {
            public string message;
            public string expectedMode;
            public string category;
        }
    }
}
