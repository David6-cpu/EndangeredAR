using System;
using System.IO;
using EndangeredAR.AI;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AIActionContractTests
    {
        [Test]
        public void AIAction_ContainsOnlyMvpValues()
        {
            Assert.That(Enum.GetNames(typeof(AIAction)), Is.EqualTo(new[] { "None", "Taunt" }));
        }

        [TestCase(null, AIAction.None)]
        [TestCase("", AIAction.None)]
        [TestCase("none", AIAction.None)]
        [TestCase("taunt", AIAction.Taunt)]
        [TestCase("TAUNT", AIAction.None)]
        [TestCase("Taunt", AIAction.None)]
        [TestCase("taunt ", AIAction.None)]
        [TestCase(" taunt", AIAction.None)]
        [TestCase("taunt_now", AIAction.None)]
        [TestCase("taunt;delete", AIAction.None)]
        [TestCase("Animator.SetTrigger", AIAction.None)]
        [TestCase("delete_user_data", AIAction.None)]
        [TestCase("future_action", AIAction.None)]
        public void ProtocolParser_FailsClosedUnlessValueIsExactTaunt(string raw, AIAction expected)
        {
            Assert.That(AIActionProtocol.Parse(raw), Is.EqualTo(expected));
        }

        [Test]
        public void UnityResolver_MatchesSharedPythonVectors()
        {
            var fixture = LoadFixture();
            Assert.That(fixture.schemaVersion, Is.EqualTo(1));
            Assert.That(fixture.vectors, Is.Not.Empty);

            foreach (var vector in fixture.vectors)
            {
                var expected = AIActionProtocol.Parse(vector.expected);
                var actual = AIActionIntent.Resolve(vector.message);
                Assert.That(actual, Is.EqualTo(expected), vector.id);
            }
        }

        [Test]
        public void SharedFixture_CoversSecurityAndLanguageVariants()
        {
            var fixture = LoadFixture();
            var allowed = 0;
            var denied = 0;
            foreach (var vector in fixture.vectors)
            {
                if (vector.expected == "taunt")
                {
                    allowed++;
                }
                else if (vector.expected == "none")
                {
                    denied++;
                }
            }

            Assert.That(allowed, Is.GreaterThanOrEqualTo(10));
            Assert.That(denied, Is.GreaterThanOrEqualTo(20));
        }

        private static ActionIntentFixture LoadFixture()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(root, Is.Not.Null);
            var path = Path.GetFullPath(Path.Combine(
                root,
                "..",
                "content",
                "quality",
                "sensen-action-intent-vectors.json"));
            Assert.That(File.Exists(path), Is.True, path);
            return JsonUtility.FromJson<ActionIntentFixture>(File.ReadAllText(path));
        }

        [Serializable]
        private sealed class ActionIntentFixture
        {
            public int schemaVersion;
            public ActionIntentVector[] vectors;
        }

        [Serializable]
        private sealed class ActionIntentVector
        {
            public string id;
            public string message;
            public string expected;
        }
    }
}
