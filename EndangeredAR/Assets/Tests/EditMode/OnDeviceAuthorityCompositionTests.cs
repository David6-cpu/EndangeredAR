using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EndangeredAR.AI;
using EndangeredAR.AI.OnDevice;
using EndangeredAR.AI.Prompt;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceAuthorityCompositionTests
    {
        [Test]
        public void CanonicalDiet_UsesOnDeviceLanguageAndUnityOwnedMetadata()
        {
            var provider = new FakeOnDeviceProvider("我会吃嫩叶、果实和花朵。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);
            var request = Request("你平时吃什么？", ContentAuthority.CanonicalKnowledge);

            var response = Send(composer, request);

            Assert.That(response.reply, Is.EqualTo("我会吃嫩叶、果实和花朵。"));
            Assert.That(response.source, Is.EqualTo("on_device_llm"));
            Assert.That(response.LanguageGenerator, Is.EqualTo(LanguageGenerator.OnDeviceLlm));
            Assert.That(response.ContentAuthority, Is.EqualTo(ContentAuthority.CanonicalKnowledge));
            Assert.That(response.answerMode, Is.EqualTo("grounded_fact"));
            Assert.That(response.evidenceStatus, Is.EqualTo("evidence_found"));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.Diet));
            Assert.That(response.GroundedFactIds, Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(response.citations, Is.Not.Empty);
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Contain("CANONICAL EVIDENCE"));
        }

        [Test]
        public void SocialChat_UsesNoExternalAuthorityBlock()
        {
            var provider = new FakeOnDeviceProvider("我愿意陪你聊一会儿。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);
            var response = Send(composer, Request("我今天有一点累。", ContentAuthority.None));

            Assert.That(response.answerMode, Is.EqualTo("social_chat"));
            Assert.That(response.GroundedFactIds, Is.Empty);
            Assert.That(response.citations, Is.Empty);
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Not.Contain("CANONICAL EVIDENCE"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Not.Contain("CURRENT READ-ONLY STATE"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Not.Contain("PAST CHARACTER MEMORY"));
        }

        [Test]
        public void CurrentProgress_UsesOnlyCurrentReadOnlyState()
        {
            var provider = new FakeOnDeviceProvider("你可以继续完成寻找食物任务。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);
            var request = Request("我下一步该做什么？", ContentAuthority.CurrentProgress);
            request.Context = ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState("sensen", true, 1, 1),
                new ReadOnlyTaskState("sensen-food", "帮森森寻找食物", false),
                ReadOnlyInteractionState.Empty);

            var response = Send(composer, request);

            Assert.That(response.answerMode, Is.EqualTo("social_chat"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Contain("CURRENT READ-ONLY STATE"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Contain("帮森森寻找食物"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Not.Contain("PAST CHARACTER MEMORY"));
        }

        [Test]
        public void ExplicitMemory_UsesBoundedPastFactsAndNoGroundingMetadata()
        {
            var provider = new FakeOnDeviceProvider("你以前完成过一项保护任务。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);
            var request = Request("你还记得我以前做过什么吗？", ContentAuthority.CharacterMemory);
            request.MemoryUseMode = MemoryUseMode.ExplicitRecall;
            request.MemoryContext = ReadOnlyCharacterMemoryContext.Create(
                "sensen",
                CharacterMemoryContextStatus.Available,
                true,
                1,
                1,
                1,
                new[]
                {
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.MissionCompleted,
                        "帮森森寻找食物")
                });

            var response = Send(composer, request);

            Assert.That(response.answerMode, Is.EqualTo("memory_recall"));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.GroundedFactIds, Is.Empty);
            Assert.That(response.citations, Is.Empty);
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Contain("PAST CHARACTER MEMORY"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Contain("帮森森寻找食物"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Not.Contain("profileKey"));
        }

        [Test]
        public void HistoryBoundary_UsesSystemPolicyAndNeverDietEvidence()
        {
            var provider = new FakeOnDeviceProvider("我不会长期保存完整聊天内容。");
            var composer = new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);
            var request = Request("你记得我以前问过你吃什么吗？", ContentAuthority.SystemPolicy);
            request.MemoryUseMode = MemoryUseMode.HistoryBoundary;

            var response = Send(composer, request);

            Assert.That(response.answerMode, Is.EqualTo("memory_recall"));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.GroundedFactIds, Is.Empty);
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Contain("SYSTEM POLICY"));
            Assert.That(provider.LastRequest.Messages[0].Content, Does.Not.Contain("CANONICAL EVIDENCE"));
        }

        [Test]
        public void AIManager_UsesInjectedOnDeviceProviderAndPreservesUnityAuthority()
        {
            var host = new GameObject("OnDeviceAIManagerTests");
            var manager = host.AddComponent<AIManager>();
            var provider = new FakeOnDeviceProvider("我的学名是 Semnopithecus priam。");
            manager.ConfigureOnDeviceProvider(provider);
            var request = Request("你的学名是什么？", ContentAuthority.None);
            AIResponse response = null;
            AIProviderError error = null;

            var routine = manager.Send(request, value => response = value, value => error = value);
            RunCoroutine(routine);

            Assert.That(error, Is.Null);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("on_device_llm"));
            Assert.That(response.ContentAuthority, Is.EqualTo(ContentAuthority.CanonicalKnowledge));
            Assert.That(response.GroundedFactIds, Is.EqualTo(new[] { "sensen.scientific_name" }));
            UnityEngine.Object.DestroyImmediate(host);
        }

        private static AIRequest Request(string message, ContentAuthority authority)
        {
            return new AIRequest
            {
                requestId = "request_1",
                animalId = "sensen",
                message = message,
                history = Array.Empty<EndangeredAR.API.ChatMessage>(),
                ContentAuthority = authority,
                knowledgeProfile = Resources.Load<AnimalKnowledgeProfile>("Animals/SensenKnowledge")
            };
        }

        private static AIResponse Send(OnDeviceAIResponseComposer composer, AIRequest request)
        {
            AIResponse response = null;
            AIProviderError error = null;
            var routine = composer.Send(request, 5f, value => response = value, value => error = value);
            while (routine.MoveNext())
            {
                Assert.That(routine.Current, Is.Null);
            }

            Assert.That(error, Is.Null);
            Assert.That(response, Is.Not.Null);
            return response;
        }

        private static void RunCoroutine(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                {
                    RunCoroutine(nested);
                }
                else
                {
                    Assert.That(routine.Current, Is.Null);
                }
            }
        }

        private sealed class FakeOnDeviceProvider : IOnDeviceLLMProvider
        {
            private readonly string reply;

            public FakeOnDeviceProvider(string reply)
            {
                this.reply = reply;
            }

            public string GeneratorId => "on_device_llm";
            public OnDeviceLLMRequest LastRequest { get; private set; }

            public int CountTokens(IReadOnlyList<OnDeviceChatMessage> messages) => 128;

            public IEnumerator Prepare(
                float timeoutSeconds,
                Action onReady,
                Action<OnDeviceLLMError> onError)
            {
                onReady();
                yield break;
            }

            public IEnumerator Send(
                OnDeviceLLMRequest request,
                float timeoutSeconds,
                Action<OnDeviceLLMResult> onSuccess,
                Action<OnDeviceLLMError> onError)
            {
                LastRequest = request;
                onSuccess(new OnDeviceLLMResult(
                    request.GenerationId,
                    reply,
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
