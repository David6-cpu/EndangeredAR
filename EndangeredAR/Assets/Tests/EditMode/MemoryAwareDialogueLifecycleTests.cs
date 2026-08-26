using System;
using System.Collections;
using System.Collections.Generic;
using EndangeredAR.AI;
using EndangeredAR.API;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class MemoryAwareDialogueLifecycleTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        [Test]
        public void ExplicitRecall_UsesLocalLanguageGeneratorWithMemoryAuthority()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = AvailableContext();
            var request = Request("你还记得我以前做过什么吗？");
            AIResponse response = null;

            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.answerMode, Is.EqualTo("memory_recall"));
            Assert.That(response.reply, Does.Contain("保护森森的森林"));
            Assert.That(response.citations, Is.Empty);
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.GroundedFactIds, Is.Empty);
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
            Assert.That(request.ContentAuthority, Is.EqualTo(ContentAuthority.CharacterMemory));
            Assert.That(request.MemoryUseMode, Is.EqualTo(MemoryUseMode.ExplicitRecall));
        }

        [Test]
        public void HistoryBoundary_PreemptsDietAndNeverCreatesAnAction()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = AvailableContext();
            var request = Request("你记得我以前问过你吃什么吗？");
            AIResponse response = null;

            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.answerMode, Is.EqualTo("memory_recall"));
            Assert.That(response.reply, Does.Contain("长期保存完整聊天内容"));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
            Assert.That(request.ContentAuthority, Is.EqualTo(ContentAuthority.SystemPolicy));
            Assert.That(request.MemoryUseMode, Is.EqualTo(MemoryUseMode.HistoryBoundary));
        }

        [Test]
        public void Reunion_UsesLocalLanguageGeneratorWithOneTrustedMemoryClaim()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = AvailableContext();
            var request = Request("森森，我回来了。");
            AIResponse response = null;

            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            Assert.That(response, Is.Not.Null);
            Assert.That(response.reply, Does.Contain("保护森森的森林"));
            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.answerMode, Is.EqualTo("social_chat"));
            Assert.That(response.citations, Is.Empty);
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
        }

        [Test]
        public void ClearInFlight_ReplacesOldClaimBeforeDisplay()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = AvailableContext();
            var request = Request("森森，我回来了。");
            AIResponse response = null;
            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));
            Assert.That(response.reply, Does.Contain("保护森森的森林"));

            memoryProvider.Context = ReadOnlyCharacterMemoryContext.EmptyFor("sensen");
            var refreshed = manager.RefreshMemoryDependentResponse(response, "sensen", request.message);

            Assert.That(refreshed, Is.Null);
        }

        [Test]
        public void StoreUnavailable_ReunionDoesNotFabricateMemory()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = ReadOnlyCharacterMemoryContext.UnavailableFor("sensen");
            var request = Request("我又来看你了。");
            AIResponse response = null;

            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.reply, Does.Contain("暂时无法读取长期记忆"));
            Assert.That(response.reply, Does.Not.Contain("任务"));
        }

        [Test]
        public void MixedRecallAndTaunt_UsesOriginalIntentWithoutMemoryActionAuthority()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = AvailableContext();
            var request = Request("你还记得我吗，给我表演一下。");
            AIResponse response = null;

            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            Assert.That(response.answerMode, Is.EqualTo("memory_recall"));
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.Taunt));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
        }

        [Test]
        public void ScientificQuestion_DoesNotAttachMemoryContext()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = AvailableContext();
            var request = Request("你的学名是什么？");
            AIResponse response = null;

            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            Assert.That(request.MemoryUseMode, Is.EqualTo(MemoryUseMode.None));
            Assert.That(request.MemoryContext, Is.Null);
            Assert.That(response.answerMode, Is.EqualTo("grounded_fact"));
            Assert.That(response.citations, Is.Not.Empty);
            Assert.That(response.reply, Does.Not.Contain("保护森森的森林"));
        }

        [Test]
        public void DuplicateRefresh_IsStableAndDoesNotConsumeBusinessState()
        {
            var manager = CreateManager(out var memoryProvider);
            memoryProvider.Context = AvailableContext();
            var request = Request("我回来了。");
            AIResponse response = null;
            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            var first = manager.RefreshMemoryDependentResponse(response, "sensen", request.message).reply;
            var second = manager.RefreshMemoryDependentResponse(response, "sensen", request.message).reply;

            Assert.That(second, Is.EqualTo(first));
            Assert.That(memoryProvider.SnapshotCalls, Is.GreaterThanOrEqualTo(3));
        }

        private AIManager CreateManager(out MutableMemoryContextProvider memoryProvider)
        {
            var host = new GameObject("Memory Aware Dialogue Tests");
            createdObjects.Add(host);
            var manager = host.AddComponent<AIManager>();
            var knowledge = host.AddComponent<LocalKnowledgeChatService>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("localKnowledgeService").objectReferenceValue = knowledge;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            manager.ConfigureContextProvider(new StubCurrentContextProvider(CurrentContext()));
            memoryProvider = new MutableMemoryContextProvider();
            manager.ConfigureMemoryContextProvider(memoryProvider);
            manager.ConfigureProviders(new MemoryAwareLocalProvider());
            return manager;
        }

        private static AIRequest Request(string message)
        {
            return new AIRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                animalId = "sensen",
                message = message,
                history = Array.Empty<ChatMessage>(),
                knowledgeProfile = AssetDatabase.LoadAssetAtPath<AnimalKnowledgeProfile>(
                    "Assets/Resources/Animals/SensenKnowledge.asset")
            };
        }

        private static ReadOnlyCharacterContext CurrentContext()
        {
            return ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState("sensen", true, 1, 1),
                new ReadOnlyTaskState("sensen-food", "帮森森寻找食物", true),
                ReadOnlyInteractionState.Empty);
        }

        private static ReadOnlyCharacterMemoryContext AvailableContext()
        {
            return ReadOnlyCharacterMemoryContext.Create(
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
                        "保护森森的森林"),
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.KnowledgeLearned,
                        "森森的食性知识")
                });
        }

        private static void Run(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
            }
        }

        private sealed class StubCurrentContextProvider : IReadOnlyCharacterContextProvider
        {
            private readonly ReadOnlyCharacterContext context;

            public StubCurrentContextProvider(ReadOnlyCharacterContext context)
            {
                this.context = context;
            }

            public ReadOnlyCharacterContext CreateSnapshot(string animalId)
            {
                return context;
            }
        }

        private sealed class MutableMemoryContextProvider : IReadOnlyCharacterMemoryContextProvider
        {
            public ReadOnlyCharacterMemoryContext Context { get; set; } =
                ReadOnlyCharacterMemoryContext.EmptyFor("sensen");
            public int SnapshotCalls { get; private set; }

            public ReadOnlyCharacterMemoryContext CreateSnapshot(
                string animalId,
                ReadOnlyCharacterContext currentContext)
            {
                SnapshotCalls++;
                return Context;
            }
        }

        private sealed class MemoryAwareLocalProvider : IAIProvider
        {
            public string ProviderId => "local_llm";

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                var response = new AIResponse
                {
                    animalId = request.animalId,
                    source = "local_llm",
                    answerMode = request.MemoryUseMode == MemoryUseMode.ExplicitRecall ||
                                 request.MemoryUseMode == MemoryUseMode.HistoryBoundary
                        ? "memory_recall"
                        : request.knowledgeProfile?.Retrieve(request.message).AnswerMode ?? "social_chat",
                    evidenceStatus = "not_required",
                    GroundingTopic = GroundingTopic.None,
                    ActionSuggestion = AIAction.None,
                    citations = Array.Empty<AICitation>()
                };
                response.ContentAuthority = request.ContentAuthority;
                response.LanguageGenerator = LanguageGenerator.LocalLlm;

                switch (request.MemoryUseMode)
                {
                    case MemoryUseMode.ExplicitRecall:
                        response.reply = request.MemoryContext.Status == CharacterMemoryContextStatus.Available
                            ? "我记得你以前完成过“保护森森的森林”。"
                            : request.MemoryContext.Status == CharacterMemoryContextStatus.Empty
                                ? "我目前没有可用于长期回忆的里程碑记录。"
                                : "我现在暂时无法读取长期记忆记录。";
                        break;
                    case MemoryUseMode.HistoryBoundary:
                        response.reply = "我不会长期保存完整聊天内容，所以不能准确复述以前的问题。";
                        break;
                    case MemoryUseMode.Reunion:
                        response.reply = request.MemoryContext.Status == CharacterMemoryContextStatus.Available
                            ? "欢迎回来，我记得你以前完成过“保护森森的森林”。"
                            : request.MemoryContext.Status == CharacterMemoryContextStatus.Empty
                                ? "很高兴见到你！"
                                : "很高兴见到你，不过我现在暂时无法读取长期记忆记录。";
                        break;
                    default:
                        response.reply = request.message.Contains("学名")
                            ? "我的学名是 Semnopithecus priam。"
                            : "我在这里陪你。";
                        if (request.message.Contains("学名"))
                        {
                            response.answerMode = "grounded_fact";
                            response.evidenceStatus = "evidence_found";
                            response.citations = new[]
                            {
                                new AICitation { sourceId = "gbif-4267223", title = "GBIF" }
                            };
                        }
                        break;
                }

                onSuccess(response);
                yield break;
            }
        }
    }
}
