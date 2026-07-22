using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.API;
using EndangeredAR.AR;
using EndangeredAR.Animals;
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
        public void DemoScene_HasCatalogProgressAndExperienceServices()
        {
            var scene = OpenDemoScene();

            Assert.That(FindComponents<AnimalCatalogService>(scene), Has.Count.EqualTo(1));
            Assert.That(FindComponents<AnimalProgressService>(scene), Has.Count.EqualTo(1));
            Assert.That(FindComponents<AnimalExperienceController>(scene), Has.Count.EqualTo(1));
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
