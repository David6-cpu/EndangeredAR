using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.API;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using EndangeredAR.Progress;
using EndangeredAR.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndangeredAR.Tests.EditMode
{
    public class DemoAnimalMigrationTests
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string ControllerPath = "Assets/Scripts/UI/DemoAppController.cs";
        private const string SceneBuilderPath = "Assets/Editor/EndangeredARDemoSceneBuilder.cs";
        private const string AIConfigPath = "Assets/Config/LocalAIConfig.asset";
        private const string ApiConfigPath = "Assets/Config/LocalApiConfig.asset";

        [Test]
        public void DemoController_NoLongerDeclaresEmbeddedAnimalProfileArray()
        {
            StringAssert.DoesNotContain("AnimalProfile[]", ReadProjectFile(ControllerPath));
        }

        [Test]
        public void DemoController_NoLongerDeclaresNestedAnimalProfileType()
        {
            StringAssert.DoesNotContain("class AnimalProfile", ReadProjectFile(ControllerPath));
        }

        [Test]
        public void SensenLearningUi_UsesProtectionStatusInsteadOfEndangeredCategory()
        {
            var controllerSource = ReadProjectFile(ControllerPath);
            var sceneBuilderSource = ReadProjectFile(SceneBuilderPath);

            StringAssert.Contains("保护状态：", controllerSource);
            StringAssert.DoesNotContain("濒危等级：", controllerSource);
            StringAssert.Contains("IUCN：近危（NT）", sceneBuilderSource);
            StringAssert.Contains("CITES：附录 I", sceneBuilderSource);
            StringAssert.DoesNotContain("濒危等级：濒危", sceneBuilderSource);
            StringAssert.Contains("野生动物保护 AR 科普", sceneBuilderSource);
            StringAssert.DoesNotContain("\"濒危动物 AR 科普\"", sceneBuilderSource);
        }

        [Test]
        public void DemoScene_SensenTextDoesNotClaimIucnEndangered()
        {
            var scene = OpenDemoScene();
            var visibleText = FindComponents<Text>(scene).Select(component => component.text).ToArray();

            Assert.That(visibleText, Has.Some.Contains("IUCN：近危（NT）"));
            Assert.That(visibleText, Has.Some.Contains("CITES：附录 I"));
            Assert.That(visibleText, Has.None.Contains("濒危等级：濒危"));
            Assert.That(visibleText, Has.None.Contains("为什么濒危"));
        }

        [Test]
        public void DemoController_BuildAIRequestCapturesAnimalMessageHistoryAndKnowledge()
        {
            var knowledge = ScriptableObject.CreateInstance<AnimalKnowledgeProfile>();
            try
            {
                var history = new List<ChatMessage>
                {
                    new ChatMessage { role = "user", content = "上一问" },
                    new ChatMessage { role = "assistant", content = "上一答" }
                };

                var request = DemoAppController.BuildAIRequest(
                    "sensen",
                    "森森，你平时吃什么？",
                    history,
                    knowledge);

                Assert.That(request.requestId, Is.Not.Null.And.Not.Empty);
                Assert.That(request.animalId, Is.EqualTo("sensen"));
                Assert.That(request.message, Is.EqualTo("森森，你平时吃什么？"));
                Assert.That(request.history, Has.Length.EqualTo(2));
                Assert.That(request.history[0].role, Is.EqualTo("user"));
                Assert.That(request.history[0].content, Is.EqualTo("上一问"));
                Assert.That(request.history[1].role, Is.EqualTo("assistant"));
                Assert.That(request.history[1].content, Is.EqualTo("上一答"));
                Assert.That(request.knowledgeProfile, Is.SameAs(knowledge));

                history[0].content = "被后续修改";
                history.Add(new ChatMessage { role = "user", content = "新消息" });
                Assert.That(request.history, Has.Length.EqualTo(2));
                Assert.That(request.history[0].content, Is.EqualTo("上一问"),
                    "The provider request must own an immutable snapshot of the visible chat history.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(knowledge);
            }
        }

        [Test]
        public void DemoController_UnifiedSuccessForwardsReplyAndMissionHintOnce()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var response = new AIResponse
            {
                animalId = "sensen",
                reply = "我最喜欢嫩叶和花朵。",
                source = "local_llm",
                missionHint = "要不要帮我寻找食物？"
            };
            response.LanguageGenerator = LanguageGenerator.LocalLlm;

            Assert.That(TryResolveAICompletion(
                state,
                ticket,
                "sensen",
                response,
                "你吃什么？",
                out var displayReply), Is.True);
            Assert.That(displayReply, Is.EqualTo("我最喜欢嫩叶和花朵。\n要不要帮我寻找食物？"));

            Assert.That(TryResolveAICompletion(
                state,
                ticket,
                "sensen",
                response,
                "你吃什么？",
                out var duplicateReply), Is.False);
            Assert.That(duplicateReply, Is.Null);
        }

        [Test]
        public void DemoController_GroundedCompletionAppendsConciseSourceLineOnce()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var response = new AIResponse
            {
                animalId = "sensen",
                reply = "我的学名是 Semnopithecus priam。",
                source = "local_llm",
                answerMode = "grounded_fact",
                evidenceStatus = "evidence_found",
                citations = new[]
                {
                    new AICitation { sourceId = "gbif-4267223", title = "GBIF taxon", organization = "GBIF Secretariat" },
                    new AICitation { sourceId = "mdd-1000692", title = "MDD taxon", organization = "Mammal Diversity Database" }
                }
            };
            response.LanguageGenerator = LanguageGenerator.LocalLlm;

            Assert.That(TryResolveAICompletion(
                state,
                ticket,
                "sensen",
                response,
                "你的学名是什么？",
                out var displayReply), Is.True);
            Assert.That(displayReply, Does.Contain("Semnopithecus priam"));
            Assert.That(displayReply, Does.Contain("资料来源：GBIF Secretariat；Mammal Diversity Database"));

            Assert.That(TryResolveAICompletion(
                state,
                ticket,
                "sensen",
                response,
                "你的学名是什么？",
                out var duplicateReply), Is.False);
            Assert.That(duplicateReply, Is.Null);
        }

        [Test]
        public void DemoController_TechnicalProviderContentNeverReachesDisplayCompletion()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var response = new AIResponse
            {
                animalId = "sensen",
                reply = "HTTP 500 UnityWebRequest Exception",
                missionHint = "stack trace at api.moonshot.cn"
            };

            Assert.That(TryResolveAICompletion(
                state,
                ticket,
                "sensen",
                response,
                "你吃什么？",
                out var displayReply), Is.False);
            Assert.That(displayReply, Is.Null,
                "Technical provider content must become an explicit system status, never a character fallback reply.");
        }

        [Test]
        public void DemoController_AnimalSwitchRejectsUnifiedLateCompletion()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            Assert.That(state.InvalidateForAnimalChange("red-panda"), Is.True);

            Assert.That(TryResolveAICompletion(
                state,
                ticket,
                "red-panda",
                new AIResponse { animalId = "sensen", reply = "迟到的森森回答" },
                "问题",
                out var displayReply), Is.False);
            Assert.That(displayReply, Is.Null);
        }

        private static bool TryResolveAICompletion(
            ChatRequestState state,
            ChatRequestTicket ticket,
            string currentAnimalId,
            AIResponse response,
            string userMessage,
            out string displayReply)
        {
            return DemoAppController.TryResolveAICompletion(
                state,
                ticket,
                currentAnimalId,
                response,
                out displayReply);
        }

        [Test]
        public void DemoScene_HasCatalogProgressAndExperienceServices()
        {
            var scene = OpenDemoScene();

            Assert.That(FindComponents<AnimalCatalogService>(scene), Has.Count.EqualTo(1));
            Assert.That(FindComponents<AnimalProgressService>(scene), Has.Count.EqualTo(1));
            Assert.That(FindComponents<AnimalExperienceController>(scene), Has.Count.EqualTo(1));
        }

        [Test]
        public void DemoScene_HasExactlyOneRootAIManager()
        {
            var scene = OpenDemoScene();
            var managers = FindComponents<AIManager>(scene);

            Assert.That(managers, Has.Count.EqualTo(1));
            Assert.That(managers[0].transform.parent, Is.Null,
                "The AI Manager must remain a root GameObject so migration cannot disturb Canvas layout.");
        }

        [Test]
        public void DemoScene_AIManagerAndDemoControllerReferencesAreFullyWired()
        {
            var scene = OpenDemoScene();
            var manager = FindSingle<AIManager>(scene);
            var demo = FindSingle<DemoAppController>(scene);
            var managerProperties = new SerializedObject(manager);
            var demoProperties = new SerializedObject(demo);

            Assert.That(managerProperties.FindProperty("aiConfig").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AIConfig>(AIConfigPath)));
            Assert.That(managerProperties.FindProperty("chatApiClient").objectReferenceValue,
                Is.SameAs(FindSingle<ChatApiClient>(scene)));
            Assert.That(managerProperties.FindProperty("localKnowledgeService").objectReferenceValue,
                Is.SameAs(FindSingle<EndangeredAR.Chat.LocalKnowledgeChatService>(scene)));
            Assert.That(demoProperties.FindProperty("aiManager").objectReferenceValue, Is.SameAs(manager));
        }

        [Test]
        public void LocalAIConfig_DefaultsToLocalOnlyAndPreservesR1Budgets()
        {
            var config = AssetDatabase.LoadAssetAtPath<AIConfig>(AIConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.routeMode, Is.EqualTo(AIRouteMode.LocalOnly));
            Assert.That(config.providerMode, Is.EqualTo(AIProviderMode.OnDevice));
            Assert.That(config.developmentRemoteServerUrl, Is.Empty);
            Assert.That(config.localTimeoutSeconds, Is.EqualTo(8f));
            Assert.That(config.totalTimeoutSeconds, Is.EqualTo(38f));
        }

        [Test]
        public void DemoSceneBuilder_CreatesFullyWiredRootAIManager()
        {
            var builderType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("EndangeredARDemoSceneBuilder", false))
                .FirstOrDefault(type => type != null);
            Assert.That(builderType, Is.Not.Null);

            var createManager = builderType.GetMethod(
                "CreateAIManager",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(createManager, Is.Not.Null,
                "Build Demo Scene must use one helper that creates and wires the R1 AI service.");

            var chatObject = new GameObject("Builder Chat Client Test");
            var knowledgeObject = new GameObject("Builder Knowledge Test");
            AIManager manager = null;
            try
            {
                var chatClient = chatObject.AddComponent<ChatApiClient>();
                var knowledge = knowledgeObject.AddComponent<LocalKnowledgeChatService>();
                manager = (AIManager)createManager.Invoke(null, new object[] { chatClient, knowledge });
                var serialized = new SerializedObject(manager);

                Assert.That(manager, Is.Not.Null);
                Assert.That(manager.transform.parent, Is.Null);
                Assert.That(serialized.FindProperty("aiConfig").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<AIConfig>(AIConfigPath)));
                Assert.That(serialized.FindProperty("chatApiClient").objectReferenceValue, Is.SameAs(chatClient));
                Assert.That(serialized.FindProperty("localKnowledgeService").objectReferenceValue, Is.SameAs(knowledge));
            }
            finally
            {
                if (manager != null)
                {
                    UnityEngine.Object.DestroyImmediate(manager.gameObject);
                }

                UnityEngine.Object.DestroyImmediate(chatObject);
                UnityEngine.Object.DestroyImmediate(knowledgeObject);
            }
        }

        [Test]
        public void LocalApiConfig_DefaultsToPortableEditorProxyAddress()
        {
            var config = AssetDatabase.LoadAssetAtPath<ApiConfig>(ApiConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.baseUrl, Is.EqualTo("http://127.0.0.1:8000"),
                "Fresh editor checkouts must not depend on one developer's LAN address.");
        }

        [Test]
        public void DemoScene_CatalogContainsSensenDefinition()
        {
            var scene = OpenDemoScene();
            var catalog = FindSingle<AnimalCatalogService>(scene);
            var serializedCatalog = new SerializedObject(catalog);
            var definitions = serializedCatalog.FindProperty("definitions");
            var sensen = AssetDatabase.LoadAssetAtPath<AnimalDefinition>("Assets/Resources/Animals/Sensen.asset");

            Assert.That(definitions.arraySize, Is.EqualTo(1));
            Assert.That(definitions.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(sensen));
        }

        [Test]
        public void DemoScene_ScannerHasExactlyOneSensenMapping()
        {
            var scanner = FindSingle<ARImageScanController>(OpenDemoScene());
            var mappings = new SerializedObject(scanner).FindProperty("markerAnimals");

            Assert.That(mappings.arraySize, Is.EqualTo(1));
            Assert.That(mappings.GetArrayElementAtIndex(0).FindPropertyRelative("markerName").stringValue,
                Is.EqualTo("sensen_marker"));
            Assert.That(mappings.GetArrayElementAtIndex(0).FindPropertyRelative("animalId").stringValue,
                Is.EqualTo("sensen"));
        }

        [Test]
        public void Scanner_UnknownOrBlankMarkerDoesNotResolveToSensen()
        {
            var host = new GameObject("Scanner Test");
            try
            {
                var scanner = host.AddComponent<ARImageScanController>();
                var resolve = typeof(ARImageScanController).GetMethod(
                    "ResolveAnimalId",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(resolve, Is.Not.Null);
                Assert.That(resolve.Invoke(scanner, new object[] { "unknown_marker" }), Is.EqualTo(string.Empty));
                Assert.That(resolve.Invoke(scanner, new object[] { "not_sensen_marker" }), Is.EqualTo(string.Empty));
                Assert.That(resolve.Invoke(scanner, new object[] { "sensen_marker_copy" }), Is.EqualTo(string.Empty));
                Assert.That(resolve.Invoke(scanner, new object[] { "  " }), Is.EqualTo(string.Empty));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Scanner_ParameterlessSimulationUsesConfiguredSensenId()
        {
            var host = new GameObject("Scanner Test");
            try
            {
                var scanner = host.AddComponent<ARImageScanController>();
                string detectedAnimalId = null;
                scanner.AnimalMarkerDetected += value => detectedAnimalId = value;

                scanner.SimulateMarkerDetected();

                Assert.That(detectedAnimalId, Is.EqualTo("sensen"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase(1920, 1080, 0, 16f / 9f)]
        [TestCase(1920, 1080, 90, 16f / 9f)]
        [TestCase(1920, 1080, 270, 16f / 9f)]
        [TestCase(1080, 1920, 0, 9f / 16f)]
        public void CameraPreviewAspect_UsesRawTextureRatio(
            int width,
            int height,
            int rotationAngle,
            float expected)
        {
            var method = typeof(ARImageScanController).GetMethod(
                "CalculatePreviewAspectRatio",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            var actual = (float)method.Invoke(null, new object[] { width, height, rotationAngle });
            Assert.That(actual, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void DemoController_AnimalChangeInvalidatesPendingChatAndRejectsDelayedCompletion()
        {
            var requestStateType = typeof(DemoAppController).Assembly.GetType("EndangeredAR.UI.ChatRequestState");
            Assert.That(requestStateType, Is.Not.Null,
                "DemoAppController needs a request state that binds a pending chat completion to its originating animal.");

            var requestState = Activator.CreateInstance(requestStateType, true);
            var begin = requestStateType.GetMethod("Begin", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var invalidateForAnimalChange = requestStateType.GetMethod(
                "InvalidateForAnimalChange",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var canComplete = requestStateType.GetMethod("CanComplete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var isThinking = requestStateType.GetProperty("IsThinking", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(begin, Is.Not.Null);
            Assert.That(invalidateForAnimalChange, Is.Not.Null);
            Assert.That(canComplete, Is.Not.Null);
            Assert.That(isThinking, Is.Not.Null);

            var ticket = begin.Invoke(requestState, new object[] { "sensen" });
            Assert.That(isThinking.GetValue(requestState), Is.EqualTo(true));

            Assert.That(invalidateForAnimalChange.Invoke(requestState, new object[] { "pangolin" }), Is.EqualTo(true));

            Assert.That(isThinking.GetValue(requestState), Is.EqualTo(false),
                "Changing animals must clear the old pending thinking state.");
            Assert.That(canComplete.Invoke(requestState, new[] { ticket, "pangolin" }), Is.EqualTo(false),
                "A delayed Sensen completion must not be applied to Pangolin's conversation.");
            Assert.That(canComplete.Invoke(requestState, new[] { ticket, "sensen" }), Is.EqualTo(false),
                "Invalidated requests must remain rejected even when the user switches back.");
        }

        [Test]
        public void DemoScene_ServiceReferencesAreFullyWired()
        {
            var scene = OpenDemoScene();
            var demo = FindSingle<DemoAppController>(scene);
            var catalog = FindSingle<AnimalCatalogService>(scene);
            var progress = FindSingle<AnimalProgressService>(scene);
            var experience = FindSingle<AnimalExperienceController>(scene);
            var demoProperties = new SerializedObject(demo);
            var experienceProperties = new SerializedObject(experience);

            Assert.That(demoProperties.FindProperty("animalCatalog").objectReferenceValue, Is.SameAs(catalog));
            Assert.That(demoProperties.FindProperty("animalProgress").objectReferenceValue, Is.SameAs(progress));
            Assert.That(demoProperties.FindProperty("animalExperience").objectReferenceValue, Is.SameAs(experience));
            Assert.That(experienceProperties.FindProperty("animalCatalogService").objectReferenceValue, Is.SameAs(catalog));
            Assert.That(experienceProperties.FindProperty("animalProgressService").objectReferenceValue, Is.SameAs(progress));
            Assert.That(experienceProperties.FindProperty("missionController").objectReferenceValue,
                Is.SameAs(FindSingle<EndangeredAR.Missions.MissionController>(scene)));
            Assert.That(experienceProperties.FindProperty("modelLoader").objectReferenceValue,
                Is.SameAs(FindSingle<EndangeredAR.Models.AnimalModelLoader>(scene)));
            Assert.That(experienceProperties.FindProperty("experienceHostTransform").objectReferenceValue,
                Is.SameAs(FindSingle<EndangeredAR.Models.AnimalModelLoader>(scene).transform));
        }

        [Test]
        public void DemoScene_PreservesUiStructuralBaseline()
        {
            var scene = OpenDemoScene();

            Assert.That(FindComponents<RectTransform>(scene), Has.Count.EqualTo(41));
            Assert.That(FindComponents<Canvas>(scene), Has.Count.EqualTo(1));
        }

        [Test]
        public void DemoController_ConversationSnapshotTrimsAndRejectsTechnicalMessages()
        {
            var snapshotMethod = typeof(DemoAppController).GetMethod(
                "BuildConversationSnapshot",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(snapshotMethod, Is.Not.Null);

            var history = new List<ChatMessage>();
            for (var index = 0; index < 24; index++)
            {
                history.Add(new ChatMessage
                {
                    role = index % 2 == 0 ? "user" : "assistant",
                    content = $"message-{index}"
                });
            }

            history.Add(new ChatMessage { role = "assistant", content = "HTTP 500 at https://example.test" });
            history.Add(new ChatMessage { role = "assistant", content = "正在想一想..." });

            var snapshot = (IReadOnlyList<ConversationRecord>)snapshotMethod.Invoke(null, new object[] { history });

            Assert.That(snapshot, Has.Count.EqualTo(20));
            Assert.That(snapshot[0].content, Is.EqualTo("message-4"));
            Assert.That(snapshot.All(record => !record.content.Contains("HTTP") &&
                                               !record.content.Contains("https://") &&
                                               !record.content.Contains("正在想一想")), Is.True);
        }

        private static Scene OpenDemoScene()
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            var components = FindComponents<T>(scene);
            Assert.That(components, Has.Count.EqualTo(1), $"Expected exactly one scene {typeof(T).Name}.");
            return components[0];
        }

        private static List<T> FindComponents<T>(Scene scene) where T : Component
        {
            var components = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components;
        }

        private static string ReadProjectFile(string assetPath)
        {
            return File.ReadAllText(Path.GetFullPath(assetPath));
        }
    }
}
