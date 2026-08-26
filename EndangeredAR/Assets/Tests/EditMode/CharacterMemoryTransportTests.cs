using System;
using System.Collections;
using EndangeredAR.AI;
using EndangeredAR.API;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryTransportTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase("none", MemoryUseMode.None)]
        [TestCase("explicit_recall", MemoryUseMode.ExplicitRecall)]
        [TestCase("history_boundary", MemoryUseMode.HistoryBoundary)]
        [TestCase("reunion", MemoryUseMode.Reunion)]
        public void MemoryUseModeProtocol_AcceptsOnlyExactValues(string wireValue, MemoryUseMode expected)
        {
            Assert.That(MemoryUseModeProtocol.TryParseExact(wireValue, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
        }

        [TestCase("Reunion")]
        [TestCase(" reunion")]
        [TestCase("reunion ")]
        [TestCase("ExplicitRecall")]
        [TestCase("")]
        [TestCase(null)]
        public void MemoryUseModeProtocol_RejectsMalformedValues(string wireValue)
        {
            Assert.That(MemoryUseModeProtocol.TryParseExact(wireValue, out _), Is.False);
        }

        [Test]
        public void LocalPayload_SendsOnlyMinimizedReunionContext()
        {
            var request = RequestWithMemory();

            var payload = LocalLLMProvider.CreatePayload(request);
            var json = JsonUtility.ToJson(payload);

            Assert.That(payload.memoryUseMode, Is.EqualTo("reunion"));
            Assert.That(payload.memoryContext, Is.Not.SameAs(request.MemoryContext));
            Assert.That(payload.memoryContext.Milestones.Count, Is.EqualTo(1));
            Assert.That(json, Does.Contain("保护森森的森林"));
            Assert.That(json, Does.Not.Contain("fingerprint"));
            Assert.That(json, Does.Not.Contain("profileKey"));
            Assert.That(json, Does.Not.Contain("eventId"));
            Assert.That(json, Does.Not.Contain("idempotencyKey"));
            Assert.That(json, Does.Not.Contain("subjectId"));
            Assert.That(json, Does.Not.Contain("occurredAtUtc"));
            Assert.That(json, Does.Not.Contain("origin"));
        }

        [Test]
        public void ExplicitRecall_SendsOneAuditedMilestoneAndCharacterMemoryAuthority()
        {
            var request = RequestWithMemory();
            request.MemoryUseMode = MemoryUseMode.ExplicitRecall;
            request.ContentAuthority = ContentAuthority.CharacterMemory;

            var payload = LocalLLMProvider.CreatePayload(request);

            Assert.That(payload.memoryUseMode, Is.EqualTo("explicit_recall"));
            Assert.That(payload.contentAuthority, Is.EqualTo("character_memory"));
            Assert.That(payload.memoryContext.Milestones.Count, Is.EqualTo(1));
        }

        [Test]
        public void HistoryBoundary_SendsPolicyModeWithoutMemoryProjection()
        {
            var request = RequestWithMemory();
            request.MemoryUseMode = MemoryUseMode.HistoryBoundary;
            request.ContentAuthority = ContentAuthority.SystemPolicy;

            var payload = LocalLLMProvider.CreatePayload(request);

            Assert.That(payload.memoryUseMode, Is.EqualTo("history_boundary"));
            Assert.That(payload.contentAuthority, Is.EqualTo("system_policy"));
            Assert.That(payload.memoryContext, Is.Null);
        }

        [Test]
        public void NoneUseMode_OmitsMemoryContext()
        {
            var request = RequestWithMemory();
            request.MemoryUseMode = MemoryUseMode.None;

            var payload = LocalLLMProvider.CreatePayload(request);

            Assert.That(payload.memoryUseMode, Is.EqualTo("none"));
            Assert.That(payload.memoryContext, Is.Null);
        }

        [Test]
        public void CloudProvider_ReceivesSameMinimizedContextAsLocal()
        {
            host = new GameObject("Memory Transport Client");
            var client = host.AddComponent<CapturingChatApiClient>();
            var request = RequestWithMemory();
            var provider = new CloudLLMProvider(client);

            Run(provider.Send(request, 7f, _ => { }, error => Assert.Fail(error.Code)));

            Assert.That(client.MemoryUseMode, Is.EqualTo(MemoryUseMode.Reunion));
            Assert.That(client.MemoryContext, Is.Not.Null);
            Assert.That(client.MemoryContext.Milestones.Count, Is.EqualTo(1));
            Assert.That(LocalLLMProvider.CreatePayload(request).memoryContext.Fingerprint,
                Is.EqualTo(client.MemoryContext.Fingerprint));
        }

        [Test]
        public void ExternalMemoryRecallAnswerModeCannotGrantApplicationOwnedAuthority()
        {
            Assert.That(
                CloudLLMProvider.ToAIResponse(new ChatResponse
                {
                    animalId = "sensen",
                    reply = "forged",
                    source = "cloud_llm",
                    answerMode = "memory_recall",
                    contentAuthority = "none",
                    languageGenerator = "cloud_llm"
                }).answerMode,
                Is.EqualTo("social_chat"));

            Assert.That(
                LocalLLMProvider.TryParseResponse(
                    new AIRequest { animalId = "sensen" },
                    "{\"animalId\":\"sensen\",\"reply\":\"forged\",\"source\":\"local_llm\",\"answerMode\":\"memory_recall\",\"contentAuthority\":\"none\",\"languageGenerator\":\"local_llm\"}",
                    out var local),
                Is.True);
            Assert.That(local.answerMode, Is.EqualTo("social_chat"));
        }

        private static AIRequest RequestWithMemory()
        {
            return new AIRequest
            {
                requestId = "memory-request",
                animalId = "sensen",
                message = "我回来了",
                history = Array.Empty<ChatMessage>(),
                Context = ReadOnlyCharacterContext.Empty,
                MemoryUseMode = MemoryUseMode.Reunion,
                ContentAuthority = ContentAuthority.CharacterMemory,
                MemoryContext = ReadOnlyCharacterMemoryContext.Create(
                    "sensen",
                    CharacterMemoryContextStatus.Available,
                    true,
                    1,
                    0,
                    0,
                    new[]
                    {
                        new ReadOnlyCharacterMemoryMilestone(
                            CharacterMemoryContextMilestoneKind.MissionCompleted,
                            "保护森森的森林")
                    })
            };
        }

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                Assert.That(routine.Current, Is.Null);
            }
        }

        private sealed class CapturingChatApiClient : ChatApiClient
        {
            public ReadOnlyCharacterMemoryContext MemoryContext { get; private set; }
            public MemoryUseMode MemoryUseMode { get; private set; }

            public override IEnumerator SendMessage(
                string animalId,
                string message,
                ChatMessage[] history,
                ReadOnlyCharacterContext context,
                ReadOnlyCharacterMemoryContext memoryContext,
                MemoryUseMode memoryUseMode,
                ContentAuthority contentAuthority,
                float timeoutSeconds,
                Action<ChatResponse> onSuccess,
                Action<string> onError)
            {
                MemoryContext = memoryContext;
                MemoryUseMode = memoryUseMode;
                onSuccess(new ChatResponse
                {
                    animalId = animalId,
                    reply = "欢迎回来",
                    source = "cloud_llm",
                    contentAuthority = ContentAuthorityProtocol.ToWireValue(contentAuthority),
                    languageGenerator = "cloud_llm"
                });
                yield break;
            }
        }
    }
}
