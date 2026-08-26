using EndangeredAR.AI;
using EndangeredAR.Development;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class AIResponseProvenanceTests
    {
        [TestCase("memory_deterministic", AIFinalSource.MemoryDeterministic)]
        [TestCase("local_llm", AIFinalSource.LocalLlm)]
        [TestCase("cloud_llm", AIFinalSource.CloudLlm)]
        [TestCase("server_rule", AIFinalSource.ServerRule)]
        [TestCase("server_knowledge", AIFinalSource.ServerKnowledge)]
        [TestCase("unity_fallback", AIFinalSource.UnityFallback)]
        public void FinalSourceProtocol_AcceptsOnlyExactValues(string wireValue, AIFinalSource expected)
        {
            Assert.That(AIFinalSourceProtocol.TryParseExact(wireValue, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
        }

        [TestCase("Local_llm")]
        [TestCase(" local_llm")]
        [TestCase("local_llm ")]
        [TestCase("local")]
        [TestCase("unity_knowledge")]
        [TestCase("")]
        [TestCase(null)]
        public void FinalSourceProtocol_RejectsMalformedOrLegacyValues(string wireValue)
        {
            Assert.That(AIFinalSourceProtocol.TryParseExact(wireValue, out _), Is.False);
        }

        [Test]
        public void Recorder_ExposesOnlyFinalExecutionMetadata()
        {
            var response = new AIResponse
            {
                source = "unity_fallback",
                answerMode = "social_chat",
                routeReason = "local_only_knowledge_fallback",
                GroundingTopic = GroundingTopic.None
            };
            response.RouteMode = AIRouteMode.LocalOnly;
            response.ProviderAttempts = new[] { "local_llm", "unity_fallback" };
            response.FallbackUsed = true;
            response.FallbackReasonCode = "local_request_failed";
            response.ElapsedMilliseconds = 321;
            response.MemoryMentionMode = MemoryMentionMode.None;
            response.ProvenanceMemoryStatus = "not_read";

            Assert.That(AIResponseProvenanceRecorder.TryRecord(response), Is.True);
            var snapshot = AIResponseProvenanceRecorder.Latest;

            Assert.That(snapshot.FinalSource, Is.EqualTo(AIFinalSource.UnityFallback));
            Assert.That(snapshot.AnswerMode, Is.EqualTo("social_chat"));
            Assert.That(snapshot.RouteMode, Is.EqualTo("local_only"));
            Assert.That(snapshot.ProviderAttempts, Is.EqualTo(new[] { "local_llm", "unity_fallback" }));
            Assert.That(snapshot.GroundingTopic, Is.EqualTo("none"));
            Assert.That(snapshot.MemoryMentionPolicy, Is.EqualTo("none"));
            Assert.That(snapshot.MemoryStatus, Is.EqualTo("not_read"));
            Assert.That(snapshot.FallbackUsed, Is.True);
            Assert.That(snapshot.FallbackReasonCode, Is.EqualTo("local_request_failed"));
            Assert.That(snapshot.ElapsedMilliseconds, Is.EqualTo(321));
        }

        [Test]
        public void Recorder_RejectsUnknownSourceInsteadOfGuessing()
        {
            var response = new AIResponse { source = "local" };

            Assert.That(AIResponseProvenanceRecorder.TryRecord(response), Is.False);
        }
    }
}
