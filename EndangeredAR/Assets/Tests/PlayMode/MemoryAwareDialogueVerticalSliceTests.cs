using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.API;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Progress;
using EndangeredAR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EndangeredAR.Tests.PlayMode
{
    public sealed class MemoryAwareDialogueVerticalSliceTests
    {
        private string temporaryDirectory;
        private AnimalDefinition sensenDefinition;
        private string sensenModelPath;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "EndangeredAR-MemoryDialoguePlayMode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            AnimalProgressService.RepositoryPathOverrideForTests = Path.Combine(
                temporaryDirectory,
                "animal-progress.json");

            sensenDefinition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            Assert.That(sensenDefinition, Is.Not.Null);
            sensenModelPath = (string)GetPrivateField(sensenDefinition, "modelRelativePath");
            SetPrivateField(sensenDefinition, "modelRelativePath", "Models/__headless_test_missing__.glb");
        }

        [TearDown]
        public void TearDown()
        {
            if (sensenDefinition != null)
            {
                SetPrivateField(sensenDefinition, "modelRelativePath", sensenModelPath);
            }

            AnimalProgressService.RepositoryPathOverrideForTests = null;
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [UnityTest]
        public IEnumerator ExplicitRecallAndHistoryBoundary_UseApplicationOwnedFactsOnly()
        {
            yield return LoadPreparedScene();
            var manager = FindSingle<AIManager>();

            var recall = Send(manager, "你还记得我以前做过什么吗？");
            Assert.That(recall.source, Is.EqualTo("local_llm"));
            Assert.That(recall.answerMode, Is.EqualTo("memory_recall"));
            Assert.That(recall.reply, Does.Contain(sensenDefinition.Mission.Title));
            Assert.That(recall.citations, Is.Empty);
            Assert.That(recall.ActionSuggestion, Is.EqualTo(AIAction.None));

            var boundary = Send(manager, "你记得我以前问过你吃什么吗？");
            Assert.That(boundary.reply, Does.Contain("长期保存完整聊天内容"));
            Assert.That(boundary.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(boundary.ActionSuggestion, Is.EqualTo(AIAction.None));
        }

        [UnityTest]
        public IEnumerator Reunion_ClearInFlight_DoesNotSaveTheStaleMemoryClaim()
        {
            yield return LoadPreparedScene();
            var manager = FindSingle<AIManager>();
            var app = FindSingle<DemoAppController>();
            var experience = FindSingle<AnimalExperienceController>();
            var progress = FindSingle<AnimalProgressService>();
            SetPrivateField(manager, "aiConfig", null);
            SetPrivateField(manager, "chatApiClient", null);

            const string message = "森森，我回来了。";
            var response = Send(manager, message);
            Assert.That(response.reply, Does.Contain(sensenDefinition.Mission.Title));
            Assert.That(experience.CharacterMemory.ClearAnimalMemory("sensen").ToString(), Is.EqualTo("Saved"));

            InvokePrivate(app, "EnterModelView");
            var requestState = (ChatRequestState)GetPrivateField(app, "chatRequestState");
            InvokePrivate(
                app,
                "FinishCloudAnswer",
                requestState.Begin("sensen"),
                message,
                response);

            var conversation = progress.GetConversation("sensen");
            Assert.That(conversation, Is.Empty,
                "A cleared in-flight memory response must become a retry status, not assistant history.");
            Assert.That(progress.IsUnlocked("sensen"), Is.True);
            Assert.That(progress.TryGetSnapshot("sensen", out var snapshot), Is.True);
            Assert.That(snapshot.missionCompleted, Is.True);
        }

        [UnityTest]
        public IEnumerator ScientificQuestion_RemainsGroundedAndMemoryFree()
        {
            yield return LoadPreparedScene();
            var manager = FindSingle<AIManager>();
            SetPrivateField(manager, "aiConfig", null);
            SetPrivateField(manager, "chatApiClient", null);
            var response = Send(manager, "你的学名是什么？");

            Assert.That(response.answerMode, Is.EqualTo("grounded_fact"));
            Assert.That(response.reply, Does.Contain("Semnopithecus priam"));
            Assert.That(response.citations, Is.Not.Empty);
            Assert.That(response.reply, Does.Not.Contain(sensenDefinition.Mission.Title));
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
        }

        [UnityTest]
        public IEnumerator MixedRecallAndTaunt_RemainsOneValidatedOriginalIntent()
        {
            yield return LoadPreparedScene();
            var response = Send(FindSingle<AIManager>(), "你还记得我吗，给我表演一下。");

            Assert.That(response.answerMode, Is.EqualTo("memory_recall"));
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.Taunt));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.citations, Is.Empty);
        }

        private IEnumerator LoadPreparedScene()
        {
            var operation = SceneManager.LoadSceneAsync("DemoScene", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            yield return operation;
            yield return null;

            FindSingle<ARImageScanController>().SimulateMarkerDetected("sensen");
            yield return null;
            FindSingle<AIManager>().ConfigureProviders(
                new MemoryAwareLocalProvider(sensenDefinition.Mission.Title));
            FindSingle<AnimalProgressService>().MarkMissionCompleted(
                "sensen",
                sensenDefinition.Mission.MissionId,
                sensenDefinition.Mission.BadgeId,
                sensenDefinition.Mission.LearnedKnowledgeId);
        }

        private AIResponse Send(AIManager manager, string message)
        {
            AIResponse response = null;
            var request = new AIRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                animalId = "sensen",
                message = message,
                history = Array.Empty<ChatMessage>(),
                knowledgeProfile = sensenDefinition.Knowledge
            };
            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));
            Assert.That(response, Is.Not.Null);
            return response;
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

        private static T FindSingle<T>() where T : UnityEngine.Object
        {
            var values = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(values, Has.Length.EqualTo(1), typeof(T).Name);
            return values[0];
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private sealed class MemoryAwareLocalProvider : IAIProvider
        {
            private readonly string missionTitle;

            public MemoryAwareLocalProvider(string missionTitle)
            {
                this.missionTitle = missionTitle;
            }

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
                        : "social_chat",
                    evidenceStatus = "not_required",
                    GroundingTopic = GroundingTopic.None,
                    GroundedFactIds = Array.Empty<string>(),
                    ActionSuggestion = AIAction.None,
                    citations = Array.Empty<AICitation>()
                };
                response.ContentAuthority = request.ContentAuthority;
                response.LanguageGenerator = LanguageGenerator.LocalLlm;

                switch (request.MemoryUseMode)
                {
                    case MemoryUseMode.ExplicitRecall:
                        response.reply = request.MemoryContext.Status == CharacterMemoryContextStatus.Available
                            ? $"我记得你以前完成过“{missionTitle}”。"
                            : "我目前没有可用于长期回忆的里程碑记录。";
                        break;
                    case MemoryUseMode.HistoryBoundary:
                        response.reply = "我不会长期保存完整聊天内容，所以不能准确复述以前的问题。";
                        break;
                    case MemoryUseMode.Reunion:
                        response.reply = request.MemoryContext.Status == CharacterMemoryContextStatus.Available
                            ? $"欢迎回来，我记得你以前完成过“{missionTitle}”。"
                            : "很高兴见到你！";
                        break;
                    default:
                        if (request.message.Contains("学名"))
                        {
                            response.reply = "我的学名是 Semnopithecus priam。";
                            response.answerMode = "grounded_fact";
                            response.evidenceStatus = "evidence_found";
                            response.citations = new[]
                            {
                                new AICitation { sourceId = "gbif-4267223", title = "GBIF" }
                            };
                        }
                        else
                        {
                            response.reply = "我在这里陪你。";
                        }

                        break;
                }

                onSuccess(response);
                yield break;
            }
        }
    }
}
