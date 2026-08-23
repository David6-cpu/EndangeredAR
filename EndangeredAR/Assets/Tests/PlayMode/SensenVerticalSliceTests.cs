using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
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

        [UnityTest]
        public IEnumerator RiggedSensen_TauntRejectsDuplicateRequestsAndReturnsToIdle()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;

            Assert.That(loader.TryGetCurrentModelController(out var controller), Is.True);
            var animator = controller.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            yield return WaitForAnimatorState(animator, "Idle", 2f);

            var riggedRootPosition = controller.transform.localPosition;
            var riggedRootRotation = controller.transform.localRotation;
            var riggedRootScale = controller.transform.localScale;
            Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.Played));
            Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.Busy),
                "A same-frame second click must not leave another Trigger queued.");

            yield return WaitForAnimatorTransition(animator, 2f);
            Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.Busy));
            yield return WaitForAnimatorState(animator, "Taunt", 2f);
            Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.Busy));
            yield return WaitUntilNotBusy(controller, 5f);

            Assert.That(controller.CurrentStateLabel, Is.EqualTo("Idle"));
            Assert.That(Vector3.Distance(controller.transform.localPosition, riggedRootPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(controller.transform.localRotation, riggedRootRotation), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(controller.transform.localScale, riggedRootScale), Is.LessThan(0.0001f));
            Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.Played),
                "A fresh Taunt should be accepted after the previous action returns to Idle.");
        }

        [UnityTest]
        public IEnumerator AICompletion_ExplicitTauntWritesHistoryOnceAndExecutesOnce()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var appController = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            InvokePrivate(appController, "EnterModelView");
            Assert.That(loader.TryGetCurrentModelController(out var animationController), Is.True);
            var animator = animationController.GetComponentInChildren<Animator>(true);
            yield return WaitForAnimatorState(animator, "Idle", 2f);

            var requestState = (ChatRequestState)GetPrivateField(appController, "chatRequestState");
            var history = (System.Collections.Generic.List<ChatMessage>)GetPrivateField(appController, "chatHistory");
            var historyCount = history.Count;
            var ticket = requestState.Begin("sensen");
            var response = new AIResponse
            {
                animalId = "sensen",
                reply = "好呀，看我的！",
                answerMode = "social_chat",
                ActionSuggestion = AIAction.Taunt
            };

            InvokePrivate(appController, "FinishCloudAnswer", ticket, "森森，给我表演一下", response);
            Assert.That(animationController.IsBusy, Is.True);
            Assert.That(history, Has.Count.EqualTo(historyCount + 2));
            Assert.That(history[^2].role, Is.EqualTo("user"));
            Assert.That(history[^2].content, Is.EqualTo("森森，给我表演一下"));
            Assert.That(history[^1].role, Is.EqualTo("assistant"));

            InvokePrivate(appController, "FinishCloudAnswer", ticket, "森森，给我表演一下", response);
            Assert.That(history, Has.Count.EqualTo(historyCount + 2), "A duplicate provider callback must not duplicate history.");
            yield return WaitUntilNotBusy(animationController, 5f);
            Assert.That(animationController.CurrentStateLabel, Is.EqualTo("Idle"));
        }

        [UnityTest]
        public IEnumerator AICompletion_GroundedFactDoesNotTriggerTaunt()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var appController = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            InvokePrivate(appController, "EnterModelView");
            Assert.That(loader.TryGetCurrentModelController(out var animationController), Is.True);

            var requestState = (ChatRequestState)GetPrivateField(appController, "chatRequestState");
            var ticket = requestState.Begin("sensen");
            InvokePrivate(appController, "FinishCloudAnswer", ticket, "你的学名是什么？", new AIResponse
            {
                animalId = "sensen",
                reply = "我的学名是 Semnopithecus priam。",
                answerMode = "grounded_fact",
                ActionSuggestion = AIAction.None
            });

            Assert.That(animationController.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator AICompletion_HiddenPageAndStaleResponseNeverTriggerTaunt()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var appController = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            Assert.That(loader.TryGetCurrentModelController(out var animationController), Is.True);

            var requestState = (ChatRequestState)GetPrivateField(appController, "chatRequestState");
            var hiddenTicket = requestState.Begin("sensen");
            SetPrivateField(appController, "isModelView", false);
            InvokePrivate(appController, "FinishCloudAnswer", hiddenTicket, "做个动作", TauntResponse());
            Assert.That(animationController.IsBusy, Is.False);

            var staleTicket = requestState.Begin("sensen");
            requestState.Invalidate();
            SetPrivateField(appController, "isModelView", true);
            InvokePrivate(appController, "FinishCloudAnswer", staleTicket, "做个动作", TauntResponse());
            Assert.That(animationController.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator AICompletion_WrongAnimalResponseNeverTriggersCurrentAnimal()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var appController = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            InvokePrivate(appController, "EnterModelView");
            Assert.That(loader.TryGetCurrentModelController(out var animationController), Is.True);

            var requestState = (ChatRequestState)GetPrivateField(appController, "chatRequestState");
            var history = (System.Collections.Generic.List<ChatMessage>)GetPrivateField(appController, "chatHistory");
            var historyCount = history.Count;
            var ticket = requestState.Begin("sensen");
            var response = TauntResponse();
            response.animalId = "other-animal";
            InvokePrivate(appController, "FinishCloudAnswer", ticket, "做个动作", response);

            Assert.That(animationController.IsBusy, Is.False);
            Assert.That(history, Has.Count.EqualTo(historyCount + 2),
                "An action mismatch must not turn a valid chat completion into a UI failure.");
        }

        [UnityTest]
        public IEnumerator AICompletion_BusyTauntAcceptsReplyWithoutQueuingSecondAction()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var appController = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            InvokePrivate(appController, "EnterModelView");
            Assert.That(loader.TryGetCurrentModelController(out var animationController), Is.True);
            var animator = animationController.GetComponentInChildren<Animator>(true);
            yield return WaitForAnimatorState(animator, "Idle", 2f);
            Assert.That(animationController.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.Played));

            var history = (System.Collections.Generic.List<ChatMessage>)GetPrivateField(appController, "chatHistory");
            var historyCount = history.Count;
            var requestState = (ChatRequestState)GetPrivateField(appController, "chatRequestState");
            var ticket = requestState.Begin("sensen");
            InvokePrivate(appController, "FinishCloudAnswer", ticket, "做个动作", TauntResponse());
            Assert.That(history, Has.Count.EqualTo(historyCount + 2), "The reply should still complete while the Animator is busy.");

            yield return WaitUntilNotBusy(animationController, 5f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(animationController.CurrentStateLabel, Is.EqualTo("Idle"), "No second Taunt may remain queued.");
        }

        [UnityTest]
        public IEnumerator RestoringConversationTextNeverReplaysAIAction()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var progress = FindSingle<AnimalProgressService>();
            var appController = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            Assert.That(loader.TryGetCurrentModelController(out var animationController), Is.True);

            progress.ReplaceConversation("sensen", new[]
            {
                new ConversationRecord { role = "user", content = "森森，给我表演一下" },
                new ConversationRecord { role = "assistant", content = "好呀，看我的！" }
            });
            InvokePrivate(appController, "RestoreConversation", sensenDefinition);
            yield return null;

            Assert.That(animationController.IsBusy, Is.False);
            var history = (System.Collections.Generic.List<ChatMessage>)GetPrivateField(appController, "chatHistory");
            Assert.That(history, Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AnimationController_FailsSafelyForInactiveUnsupportedAndInvalidAnimators()
        {
            var root = new GameObject("Animation Controller Contract Test");
            var controller = root.AddComponent<AnimalModelController>();
            try
            {
                Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.MissingAnimator));

                root.SetActive(false);
                Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.Inactive));
                root.SetActive(true);

                var animator = root.AddComponent<Animator>();
                SetPrivateField(controller, "animator", animator);
                SetPrivateField(controller, "supportedAnimalId", "another-animal");
                Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.UnsupportedAnimal));

                SetPrivateField(controller, "supportedAnimalId", "sensen");
                Assert.That(controller.TryPlayTaunt(), Is.EqualTo(TauntRequestResult.InvalidControllerState));
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Loader_AnimalSwitchNeverReturnsTheDeactivatedPreviousController()
        {
            yield return LoadDemoScene();

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var alternateDefinition = ScriptableObject.CreateInstance<AnimalDefinition>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            Assert.That(loader.TryGetCurrentModelController(out var previous), Is.True);

            try
            {
                SetPrivateField(alternateDefinition, "animalId", "animal-02");
                SetPrivateField(alternateDefinition, "modelRelativePath", "Models/__headless_test_missing__.glb");
                SetPrivateField(alternateDefinition, "modelPrefab", sensenDefinition.ModelPrefab);

                loader.Configure(alternateDefinition);
                Assert.That(previous.gameObject.activeInHierarchy, Is.False,
                    "The previous animal Root must be deactivated before delayed destruction.");
                Assert.That(loader.LoadedAnimalId, Is.EqualTo("animal-02"));
                Assert.That(loader.TryGetCurrentModelController(out var current), Is.False,
                    "A Sensen-only controller must not be returned after switching animals.");
                Assert.That(current, Is.Null);

                yield return null;
                Assert.That(loader.TryGetCurrentModelController(out current), Is.False);
                Assert.That(current, Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(alternateDefinition);
            }
        }

        [UnityTest]
        public IEnumerator Loader_GlbFallbackWithoutAnimationControllerFailsClosed()
        {
            yield return LoadDemoScene();

            var loader = FindSingle<AnimalModelLoader>();
            var modelPrefab = GetPrivateField(sensenDefinition, "modelPrefab");
            GameObject fallbackRoot = null;
            GameObject unrelatedControllerRoot = null;
            try
            {
                SetPrivateField(sensenDefinition, "modelPrefab", null);
                loader.Configure(sensenDefinition);
                yield return null;

                fallbackRoot = new GameObject("Animal GLB Runtime Root");
                fallbackRoot.transform.SetParent(loader.transform, false);
                unrelatedControllerRoot = new GameObject("Unrelated Animation Controller");
                unrelatedControllerRoot.AddComponent<AnimalModelController>();

                Assert.That(loader.TryGetCurrentModelController(out var controller), Is.False);
                Assert.That(controller, Is.Null);
            }
            finally
            {
                SetPrivateField(sensenDefinition, "modelPrefab", modelPrefab);
                if (fallbackRoot != null)
                {
                    UnityEngine.Object.Destroy(fallbackRoot);
                }

                if (unrelatedControllerRoot != null)
                {
                    UnityEngine.Object.Destroy(unrelatedControllerRoot);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DevelopmentPanel_IsSingletonAndTauntHasNoBusinessSideEffects()
        {
            yield return LoadDemoScene();

            var bootstrapType = Type.GetType("EndangeredAR.Development.DevelopmentToolsBootstrap, EndangeredAR.Runtime");
            var panelType = Type.GetType("EndangeredAR.Development.AnimalAnimationDebugPanel, EndangeredAR.Runtime");
            Assert.That(bootstrapType, Is.Not.Null);
            Assert.That(panelType, Is.Not.Null);

            var ensureInitialized = bootstrapType.GetMethod(
                "EnsureInitialized",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(ensureInitialized, Is.Not.Null);
            ensureInitialized.Invoke(null, null);
            ensureInitialized.Invoke(null, null);

            var panels = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(component => component != null && component.GetType() == panelType)
                .ToArray();
            Assert.That(panels, Has.Length.EqualTo(1));

            var scanner = FindSingle<ARImageScanController>();
            var loader = FindSingle<AnimalModelLoader>();
            var appController = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            Assert.That(loader.TryGetCurrentModelController(out var animationController), Is.True);
            var animator = animationController.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            yield return WaitForAnimatorState(animator, "Idle", 2f);
            yield return new WaitForSeconds(0.5f);

            var transcriptBefore = (string)GetPrivateField(appController, "chatTranscript");
            var progressBefore = File.Exists(repositoryPath) ? File.ReadAllText(repositoryPath) : string.Empty;
            var tauntButton = panels[0].GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Play Taunt");

            tauntButton.onClick.Invoke();
            tauntButton.onClick.Invoke();
            Assert.That(animationController.IsBusy, Is.True);
            yield return WaitUntilNotBusy(animationController, 5f);

            Assert.That((string)GetPrivateField(appController, "chatTranscript"), Is.EqualTo(transcriptBefore));
            Assert.That(File.Exists(repositoryPath) ? File.ReadAllText(repositoryPath) : string.Empty,
                Is.EqualTo(progressBefore));
            Assert.That(animationController.CurrentStateLabel, Is.EqualTo("Idle"));
        }

        private IEnumerator LoadDemoScene()
        {
            var operation = SceneManager.LoadSceneAsync("DemoScene", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "DemoScene must be included in the test player build settings.");
            yield return operation;
            yield return null;
        }

        private static IEnumerator WaitForAnimatorState(Animator animator, string stateName, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (animator != null && !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Animator did not reach {stateName} within {timeoutSeconds:0.0} seconds.");
        }

        private static IEnumerator WaitUntilNotBusy(AnimalModelController controller, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (controller != null && !controller.IsBusy)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Taunt request stayed busy beyond {timeoutSeconds:0.0} seconds.");
        }

        private static IEnumerator WaitForAnimatorTransition(Animator animator, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (animator != null && animator.IsInTransition(0))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Animator did not enter a transition within {timeoutSeconds:0.0} seconds.");
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

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method: {methodName}.");
            method.Invoke(target, arguments);
        }

        private static AIResponse TauntResponse()
        {
            return new AIResponse
            {
                animalId = "sensen",
                reply = "好呀，看我的！",
                answerMode = "social_chat",
                ActionSuggestion = AIAction.Taunt
            };
        }
    }
}
