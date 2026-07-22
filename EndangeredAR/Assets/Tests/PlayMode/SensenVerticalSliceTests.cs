using System;
using System.Collections;
using System.IO;
using System.Reflection;
using EndangeredAR.API;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Models;
using EndangeredAR.Progress;
using EndangeredAR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EndangeredAR.Tests.PlayMode
{
    public class SensenVerticalSliceTests
    {
        private string temporaryDirectory;
        private string repositoryPath;
        private AnimalDefinition sensenDefinition;
        private string sensenModelPath;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "EndangeredAR-PlayMode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            repositoryPath = Path.Combine(temporaryDirectory, "animal-progress.json");
            AnimalProgressService.RepositoryPathOverrideForTests = repositoryPath;

            // Exercise the loader lifecycle without asking glTFast to decode the real GLB on NullGfxDevice.
            sensenDefinition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            Assert.That(sensenDefinition, Is.Not.Null, "The Sensen definition must be available in Resources.");
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
            if (!string.IsNullOrEmpty(temporaryDirectory) && Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [UnityTest]
        public IEnumerator Startup_HasNoMissingCoreServices()
        {
            yield return LoadDemoScene();

            var catalog = FindSingle<AnimalCatalogService>();
            var progress = FindSingle<AnimalProgressService>();
            var experience = FindSingle<AnimalExperienceController>();
            var controller = FindSingle<DemoAppController>();

            AssertRepositoryIsIsolated(progress);
            Assert.That(catalog.DefaultAnimal, Is.Not.Null);
            Assert.That(experience.CurrentAnimal, Is.Not.Null);
            Assert.That(experience.CurrentAnimal.AnimalId, Is.EqualTo("sensen"));
            Assert.That(controller, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ManualScan_SelectsAndUnlocksSensen()
        {
            yield return LoadDemoScene();

            var progress = FindSingle<AnimalProgressService>();
            var experience = FindSingle<AnimalExperienceController>();
            var scanner = FindSingle<ARImageScanController>();
            AssertRepositoryIsIsolated(progress);

            scanner.SimulateMarkerDetected("sensen");
            yield return null;

            Assert.That(experience.CurrentAnimal, Is.Not.Null);
            Assert.That(experience.CurrentAnimal.AnimalId, Is.EqualTo("sensen"));
            Assert.That(progress.IsUnlocked("sensen"), Is.True);
            Assert.That(progress.UnlockedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RepeatScan_DoesNotIncreaseUnlockedCount()
        {
            yield return LoadDemoScene();

            var progress = FindSingle<AnimalProgressService>();
            var scanner = FindSingle<ARImageScanController>();
            AssertRepositoryIsIsolated(progress);

            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            var unlockedCountAfterFirstScan = progress.UnlockedCount;

            scanner.SimulateMarkerDetected("sensen");
            yield return null;

            Assert.That(unlockedCountAfterFirstScan, Is.EqualTo(1));
            Assert.That(progress.UnlockedCount, Is.EqualTo(unlockedCountAfterFirstScan));
        }

        [UnityTest]
        public IEnumerator SensenExperience_KeepsModelGestureController()
        {
            yield return LoadDemoScene();

            var progress = FindSingle<AnimalProgressService>();
            var scanner = FindSingle<ARImageScanController>();
            var modelLoader = FindSingle<AnimalModelLoader>();
            AssertRepositoryIsIsolated(progress);

            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;

            var existingGestureController = modelLoader.gameObject.GetComponent<AnimalGestureController>();
            Assert.That(existingGestureController, Is.Not.Null,
                "AnimalModelLoader must install the gesture controller when the experience becomes active.");

            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;

            Assert.That(modelLoader.LoadedAnimalId, Is.EqualTo("sensen"));
            Assert.That(modelLoader.GetComponent<AnimalGestureController>(), Is.SameAs(existingGestureController));
        }

        [UnityTest]
        public IEnumerator NetworkUnavailable_LocalFallbackStillReturnsAnswer()
        {
            yield return LoadDemoScene();

            var progress = FindSingle<AnimalProgressService>();
            var scanner = FindSingle<ARImageScanController>();
            var controller = FindSingle<DemoAppController>();
            var chatApiClient = FindSingle<ChatApiClient>();
            AssertRepositoryIsIsolated(progress);

            scanner.SimulateMarkerDetected("sensen");
            yield return null;

            SetPrivateField(chatApiClient, "config", null);
            var chatInput = (InputField)GetPrivateField(controller, "chatInput");
            var sendButton = (Button)GetPrivateField(controller, "sendLocalChatButton");
            Assert.That(chatInput, Is.Not.Null);
            Assert.That(sendButton, Is.Not.Null);

            chatInput.text = "你吃什么？";
            sendButton.onClick.Invoke();
            yield return new WaitForSeconds(0.7f);

            var transcript = (string)GetPrivateField(controller, "chatTranscript");
            StringAssert.Contains("你：你吃什么？", transcript);
            StringAssert.Contains("森森：", transcript);
            StringAssert.DoesNotContain("正在想一想", transcript);
            StringAssert.DoesNotContain("API 地址", transcript);
        }

        private IEnumerator LoadDemoScene()
        {
            var operation = SceneManager.LoadSceneAsync("DemoScene", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "DemoScene must be included in the test player build settings.");
            yield return operation;
            yield return null;
        }

        private void AssertRepositoryIsIsolated(AnimalProgressService progress)
        {
            Assert.That(progress.ActiveRepositoryPath, Is.EqualTo(Path.GetFullPath(repositoryPath)));
        }

        private static T FindSingle<T>() where T : Component
        {
            var matches = UnityEngine.Object.FindObjectsOfType<T>(true);
            Assert.That(matches, Has.Length.EqualTo(1), $"Expected exactly one {typeof(T).Name} in DemoScene.");
            return matches[0];
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field: {fieldName}.");
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field: {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
