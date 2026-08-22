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
        public IEnumerator HomeVisuals_DoNotUseTheRejectedGeneratedBackground()
        {
            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            var homePanel = (GameObject)GetPrivateField(controller, "homePanel");
            var homeImage = homePanel.GetComponent<Image>();

            Assert.That(homeImage, Is.Not.Null);
            Assert.That(homeImage.sprite, Is.Null,
                "The rejected bg-home-forest artwork must not cover the product home screen.");
        }

        [UnityTest]
        public IEnumerator BottomScanButton_RoundedBorderFitsItsSourceTexture()
        {
            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            var scanNavigationButton = (Button)GetPrivateField(controller, "discoverButton");
            var image = scanNavigationButton.GetComponent<Image>();

            Assert.That(image, Is.Not.Null);
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.sprite.border.x + image.sprite.border.z,
                Is.LessThanOrEqualTo(image.sprite.texture.width),
                "Horizontal sliced borders must not overlap inside the rounded source texture.");
            Assert.That(image.sprite.border.y + image.sprite.border.w,
                Is.LessThanOrEqualTo(image.sprite.texture.height),
                "Vertical sliced borders must not overlap inside the rounded source texture.");
        }

        [UnityTest]
        public IEnumerator BottomNavigationRoundedSprite_HasSymmetricTransparentCorners()
        {
            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            var scanNavigationButton = (Button)GetPrivateField(controller, "discoverButton");
            var texture = scanNavigationButton.GetComponent<Image>().sprite.texture;
            var lastPixel = texture.width - 1;
            var topLeft = texture.GetPixel(0, lastPixel).a;
            var topRight = texture.GetPixel(lastPixel, lastPixel).a;
            var bottomLeft = texture.GetPixel(0, 0).a;
            var bottomRight = texture.GetPixel(lastPixel, 0).a;

            Assert.That(topLeft, Is.LessThan(0.05f), "A rounded corner must start transparent.");
            Assert.That(topRight, Is.EqualTo(topLeft).Within(0.001f), "Top corners must be symmetric.");
            Assert.That(bottomLeft, Is.EqualTo(topLeft).Within(0.001f), "Left corners must be symmetric.");
            Assert.That(bottomRight, Is.EqualTo(topLeft).Within(0.001f), "All rounded corners must be symmetric.");
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
        public IEnumerator ManualScan_ReconfigurationKeepsRiggedRendererVisibleAndFallbackHidden()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var modelLoader = FindSingle<AnimalModelLoader>();
            var fallback = modelLoader.GetComponent<Renderer>();

            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;

            var runtimeRoot = modelLoader.transform.Find("Animal GLB Runtime Root");
            Assert.That(runtimeRoot, Is.Not.Null);
            var riggedRenderer = runtimeRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(riggedRenderer, Is.Not.Null);
            Assert.That(riggedRenderer.enabled, Is.True,
                "The scan flow configures the selected animal twice; the second load must not disable the new rigged renderer.");
            Assert.That(riggedRenderer.gameObject.activeInHierarchy, Is.True);
            Assert.That(fallback, Is.Not.Null);
            Assert.That(fallback.enabled, Is.False);
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

        [UnityTest]
        public IEnumerator CompletedMission_EntersAsReplayWithoutFreshRewardCopy()
        {
            var document = new AnimalProgressDocument();
            document.animals.Add(new AnimalProgressRecord
            {
                animalId = "sensen",
                unlocked = true,
                missionCompleted = true,
                earnedBadgeIds = { "eco-guardian" }
            });
            new JsonAnimalProgressRepository(repositoryPath).Save(document);

            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            InvokePrivate(controller, "EnterMissionView");
            yield return null;

            var badgeText = (Text)GetPrivateField(controller, "badgeText");
            StringAssert.Contains("已收藏", badgeText.text);
            StringAssert.DoesNotStartWith("已获得：", badgeText.text);
        }

        [UnityTest]
        public IEnumerator OverlayViews_HideModelActionButtons()
        {
            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            var missionButton = (Button)GetPrivateField(controller, "missionButton");
            var cardButton = (Button)GetPrivateField(controller, "cardButton");

            InvokePrivate(controller, "EnterModelView");
            yield return null;
            Assert.That(missionButton.gameObject.activeSelf, Is.True);
            Assert.That(cardButton.gameObject.activeSelf, Is.True);

            InvokePrivate(controller, "EnterMissionView");
            yield return null;

            Assert.That(missionButton.gameObject.activeSelf, Is.False,
                "The model mission shortcut must not bleed through the mission overlay.");
            Assert.That(cardButton.gameObject.activeSelf, Is.False,
                "The model card shortcut must not bleed through the mission overlay.");

            InvokePrivate(controller, "ShowCardPanel");
            yield return null;

            Assert.That(missionButton.gameObject.activeSelf, Is.False,
                "The model mission shortcut must not be rendered into the knowledge card.");
            Assert.That(cardButton.gameObject.activeSelf, Is.False,
                "The model card shortcut must not be rendered into the knowledge card.");
        }

        [UnityTest]
        public IEnumerator StretchedCardControls_KeepTheirLabelsVisible()
        {
            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            var saveButton = (Button)GetPrivateField(controller, "cardSaveButton");
            var backButton = (Button)GetPrivateField(controller, "cardBackButton");
            var saveLabel = saveButton.GetComponentInChildren<Text>(true);
            var backLabel = backButton.GetComponentInChildren<Text>(true);

            Assert.That(saveLabel, Is.Not.Null);
            Assert.That(backLabel, Is.Not.Null);
            Assert.That(saveLabel.gameObject.activeSelf, Is.True);
            Assert.That(backLabel.gameObject.activeSelf, Is.True);
            Assert.That(saveLabel.text, Is.EqualTo("保存 PNG"));
            Assert.That(backLabel.text, Is.EqualTo("返回展示"));
        }

        [UnityTest]
        public IEnumerator ModelChatBubble_IsCompactAndReadable()
        {
            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            var bubble = (GameObject)GetPrivateField(controller, "modelChatBubble");
            var bubbleText = (Text)GetPrivateField(controller, "modelChatBubbleText");
            var bubbleRect = bubble.GetComponent<RectTransform>();

            Assert.That(bubbleRect.anchorMax.y - bubbleRect.anchorMin.y, Is.LessThanOrEqualTo(0.15f));
            Assert.That(bubbleText.fontSize, Is.GreaterThanOrEqualTo(27));
        }

        [UnityTest]
        public IEnumerator KnowledgeCard_CapturesOnlyTheShareSurface()
        {
            yield return LoadDemoScene();

            var controller = FindSingle<DemoAppController>();
            InvokePrivate(controller, "ShowCardPanel");
            yield return null;

            var cardPanel = (GameObject)GetPrivateField(controller, "cardPanel");
            var captureRect = (RectTransform)GetPrivateField(controller, "cardCaptureRect");
            var saveButton = (Button)GetPrivateField(controller, "cardSaveButton");
            var backButton = (Button)GetPrivateField(controller, "cardBackButton");

            Assert.That(captureRect.name, Is.EqualTo("Share Card Surface"));
            Assert.That(saveButton.transform.IsChildOf(captureRect), Is.False,
                "The saved PNG must not include the save control.");
            Assert.That(backButton.transform.IsChildOf(captureRect), Is.False,
                "The saved PNG must not include the back control.");
            Assert.That(captureRect.Find("Card Sensen Avatar"), Is.Not.Null);
            Assert.That(captureRect.Find("Card Header"), Is.Not.Null);
            Assert.That(captureRect.Find("Card Content"), Is.Not.Null);
            Assert.That(captureRect.Find("Card Badge Status"), Is.Not.Null);
            Assert.That(captureRect.Find("Card Action"), Is.Not.Null);
            Assert.That(saveButton.transform.parent, Is.EqualTo(cardPanel.transform));
            Assert.That(backButton.transform.parent, Is.EqualTo(cardPanel.transform));
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

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method: {methodName}.");
            method.Invoke(target, null);
        }
    }
}
