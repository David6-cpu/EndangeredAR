using System;
using System.Collections;
using System.Collections.Generic;
using EndangeredAR.AI;
using EndangeredAR.AI.OnDevice;
using EndangeredAR.AI.Prompt;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceRepairLifecycleTests
    {
        [Test]
        public void InvalidFirstCompletion_IsRepairedExactlyOnceWithSameAuthority()
        {
            var provider = new SequencedProvider(
                "我的学名是 Ailuropoda melanoleuca。",
                "我的学名是 Semnopithecus priam。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);

            var outcome = Send(composer, ScientificNameRequest());

            Assert.That(outcome.Error, Is.Null);
            Assert.That(outcome.Response.reply, Is.EqualTo("我的学名是 Semnopithecus priam。"));
            Assert.That(provider.SendCount, Is.EqualTo(2));
            Assert.That(provider.Requests[1].Messages[0].Content, Does.Contain("STRICT RESPONSE REPAIR"));
            Assert.That(provider.Requests[1].Messages[0].Content, Does.Contain("Semnopithecus priam"));
            Assert.That(provider.Requests[1].Messages[0].Content, Does.Not.Contain("Ailuropoda melanoleuca"));
            Assert.That(outcome.Response.GroundedFactIds, Is.EqualTo(new[] { "sensen.scientific_name" }));
        }

        [Test]
        public void TwoInvalidCompletions_FailClosedWithoutCharacterReply()
        {
            var provider = new SequencedProvider(
                "我的学名是 Ailuropoda melanoleuca。",
                "我的学名还是 Ailuropoda melanoleuca。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);

            var outcome = Send(composer, ScientificNameRequest());

            Assert.That(outcome.Response, Is.Null);
            Assert.That(outcome.Error, Is.Not.Null);
            Assert.That(outcome.Error.Code, Is.EqualTo("on_device_response_validation_failed"));
            Assert.That(provider.SendCount, Is.EqualTo(2));
        }

        [Test]
        public void ValidFirstCompletion_DoesNotSpendRepairAttempt()
        {
            var provider = new SequencedProvider("我的学名是 Semnopithecus priam。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);

            var outcome = Send(composer, ScientificNameRequest());

            Assert.That(outcome.Error, Is.Null);
            Assert.That(outcome.Response, Is.Not.Null);
            Assert.That(provider.SendCount, Is.EqualTo(1));
        }

        private static AIRequest ScientificNameRequest()
        {
            return new AIRequest
            {
                requestId = "repair_1",
                animalId = "sensen",
                message = "你的学名是什么？",
                ContentAuthority = ContentAuthority.CanonicalKnowledge,
                knowledgeProfile = Resources.Load<AnimalKnowledgeProfile>("Animals/SensenKnowledge")
            };
        }

        private static Outcome Send(OnDeviceAIResponseComposer composer, AIRequest request)
        {
            var outcome = new Outcome();
            var routine = composer.Send(
                request,
                5f,
                value => outcome.Response = value,
                value => outcome.Error = value);
            while (routine.MoveNext())
            {
                Assert.That(routine.Current, Is.Null);
            }

            return outcome;
        }

        private sealed class Outcome
        {
            public AIResponse Response;
            public AIProviderError Error;
        }

        private sealed class SequencedProvider : IOnDeviceLLMProvider
        {
            private readonly Queue<string> replies;

            public SequencedProvider(params string[] replies)
            {
                this.replies = new Queue<string>(replies);
            }

            public string GeneratorId => "on_device_llm";
            public int SendCount { get; private set; }
            public List<OnDeviceLLMRequest> Requests { get; } = new List<OnDeviceLLMRequest>();

            public IEnumerator Prepare(float timeoutSeconds, Action onReady, Action<OnDeviceLLMError> onError)
            {
                onReady();
                yield break;
            }

            public int CountTokens(IReadOnlyList<OnDeviceChatMessage> messages) => 128;

            public IEnumerator Send(
                OnDeviceLLMRequest request,
                float timeoutSeconds,
                Action<OnDeviceLLMResult> onSuccess,
                Action<OnDeviceLLMError> onError)
            {
                SendCount++;
                Requests.Add(request);
                onSuccess(new OnDeviceLLMResult(
                    request.GenerationId,
                    replies.Dequeue(),
                    OnDeviceLLMMetrics.Empty));
                yield break;
            }

            public void Cancel(string generationId)
            {
            }

            public void OnApplicationPause(bool paused)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
