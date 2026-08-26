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
        [TestCase("system_status", AIFinalSource.SystemStatus)]
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
        public void Recorder_ExposesActualLocalLanguageAndContentAuthority()
        {
            var response = new AIResponse
            {
                source = "local_llm",
                answerMode = "social_chat",
                GroundingTopic = GroundingTopic.None
            };
            response.RouteMode = AIRouteMode.LocalOnly;
            response.ProviderAttempts = new[] { "local_llm" };
            response.ContentAuthority = ContentAuthority.None;
            response.LanguageGenerator = LanguageGenerator.LocalLlm;
            response.FallbackUsed = false;
            response.ElapsedMilliseconds = 321;
            response.MemoryMentionMode = MemoryMentionMode.None;
            response.ProvenanceMemoryStatus = "not_read";

            Assert.That(AIResponseProvenanceRecorder.TryRecord(response), Is.True);
            var snapshot = AIResponseProvenanceRecorder.Latest;

            Assert.That(snapshot.FinalSource, Is.EqualTo(AIFinalSource.LocalLlm));
            Assert.That(snapshot.AnswerMode, Is.EqualTo("social_chat"));
            Assert.That(snapshot.RouteMode, Is.EqualTo("local_only"));
            Assert.That(snapshot.ContentAuthority, Is.EqualTo("none"));
            Assert.That(snapshot.LanguageGenerator, Is.EqualTo("local_llm"));
            Assert.That(snapshot.ProviderAttempts, Is.EqualTo(new[] { "local_llm" }));
            Assert.That(snapshot.GroundingTopic, Is.EqualTo("none"));
            Assert.That(snapshot.MemoryMentionPolicy, Is.EqualTo("none"));
            Assert.That(snapshot.MemoryStatus, Is.EqualTo("not_read"));
            Assert.That(snapshot.FallbackUsed, Is.False);
            Assert.That(snapshot.FallbackReasonCode, Is.Empty);
            Assert.That(snapshot.ElapsedMilliseconds, Is.EqualTo(321));
        }

        [Test]
        public void Recorder_ExposesSystemStatusWithoutLanguageGeneratorOrFallback()
        {
            var response = new AIResponse
            {
                source = "system_status",
                answerMode = "system_status"
            };
            response.RouteMode = AIRouteMode.LocalOnly;
            response.ProviderAttempts = new[] { "local_llm" };
            response.LanguageGenerator = LanguageGenerator.None;
            response.FallbackUsed = false;
            response.ProvenanceErrorCode = "local_model_unavailable";

            Assert.That(AIResponseProvenanceRecorder.TryRecord(response), Is.True);
            var snapshot = AIResponseProvenanceRecorder.Latest;

            Assert.That(snapshot.FinalSource, Is.EqualTo(AIFinalSource.SystemStatus));
            Assert.That(snapshot.LanguageGenerator, Is.EqualTo("none"));
            Assert.That(snapshot.FallbackUsed, Is.False);
            Assert.That(snapshot.ErrorCode, Is.EqualTo("local_model_unavailable"));
        }

        [Test]
        public void Recorder_RejectsUnknownSourceInsteadOfGuessing()
        {
            var response = new AIResponse { source = "local" };

            Assert.That(AIResponseProvenanceRecorder.TryRecord(response), Is.False);
        }
    }
}
