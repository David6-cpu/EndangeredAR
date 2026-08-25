using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Memory;
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
    public sealed class CharacterMemoryVerticalSliceTests
    {
        private string temporaryDirectory;
        private string progressPath;
        private AnimalDefinition sensenDefinition;
        private string sensenModelPath;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "EndangeredAR-MemoryPlayMode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            progressPath = Path.Combine(temporaryDirectory, "animal-progress.json");
            AnimalProgressService.RepositoryPathOverrideForTests = progressPath;

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
        public IEnumerator BusinessMilestones_PersistAcrossReloadAndDuplicateOperationsStayIdempotent()
        {
            yield return LoadDemoScene();
            var progress = FindSingle<AnimalProgressService>();
            var memory = FindSingle<AnimalExperienceController>().CharacterMemory;
            var scanner = FindSingle<ARImageScanController>();

            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            CompleteSensenMission(progress);

            AssertCompleteProjection(memory.GetProjection("sensen"));
            Assert.That(LiveEventCount(memory, "sensen"), Is.EqualTo(4));
            Assert.That(File.Exists(MemoryPath), Is.True);

            scanner.SimulateMarkerDetected("sensen");
            CompleteSensenMission(progress);
            Assert.That(LiveEventCount(memory, "sensen"), Is.EqualTo(4));

            Assert.That(memory.ReloadForDevelopment(), Is.EqualTo(CharacterMemoryOperationResult.NoChanges));
            AssertCompleteProjection(memory.GetProjection("sensen"));
            Assert.That(LiveEventCount(memory, "sensen"), Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DevelopmentPanel_ClearAnimalPreservesProgressConversationAndSuppression()
        {
            yield return LoadDemoScene();
            var progress = FindSingle<AnimalProgressService>();
            var memory = FindSingle<AnimalExperienceController>().CharacterMemory;
            FindSingle<ARImageScanController>().SimulateMarkerDetected("sensen");
            yield return null;
            CompleteSensenMission(progress);
            progress.ReplaceConversation("sensen", new[]
            {
                new ConversationRecord { role = "user", content = "记得这条聊天，但不要写进长期记忆。" }
            });

            var panelType = Type.GetType(
                "EndangeredAR.Development.CharacterMemoryDebugPanel, EndangeredAR.Runtime");
            Assert.That(panelType, Is.Not.Null);
            var panel = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(value => value.GetType() == panelType);
            var clearButton = panel.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Clear Animal Memory");
            clearButton.onClick.Invoke();

            Assert.That(LiveEventCount(memory, "sensen"), Is.Zero);
            Assert.That(memory.GetProjection("sensen").Discovered, Is.False);
            AssertProgressRemainsComplete(progress);
            Assert.That(progress.GetConversation("sensen").Single().content, Does.StartWith("记得这条聊天"));

            memory.ReloadForDevelopment();
            Assert.That(LiveEventCount(memory, "sensen"), Is.Zero);
            Assert.That(memory.GetProjection("sensen").Discovered, Is.False);
            AssertProgressRemainsComplete(progress);
        }

        [UnityTest]
        public IEnumerator ChatAndCharacterActions_NeverAppendMemoryEvents()
        {
            yield return LoadDemoScene();
            var progress = FindSingle<AnimalProgressService>();
            var scanner = FindSingle<ARImageScanController>();
            var experience = FindSingle<AnimalExperienceController>();
            var loader = FindSingle<AnimalModelLoader>();
            var app = FindSingle<DemoAppController>();
            scanner.SimulateMarkerDetected("sensen");
            yield return null;
            yield return null;
            var memory = experience.CharacterMemory;
            var initialCount = LiveEventCount(memory, "sensen");

            progress.ReplaceConversation("sensen", new[]
            {
                new ConversationRecord { role = "user", content = "聊天不属于事件记忆。" }
            });
            Assert.That(LiveEventCount(memory, "sensen"), Is.EqualTo(initialCount));

            Assert.That(loader.TryGetCurrentModelController(out var animation), Is.True);
            var animator = animation.GetComponentInChildren<Animator>(true);
            yield return WaitForAnimatorState(animator, "Idle", 2f);
            Assert.That(animation.TryPlayAction(AIAction.Taunt), Is.EqualTo(ActionRequestResult.Played));
            yield return WaitUntilNotBusy(animation, 5f);
            Assert.That(animation.TryPlayAction(AIAction.Eat), Is.EqualTo(ActionRequestResult.Played));
            yield return WaitUntilNotBusy(animation, 6f);
            Assert.That(LiveEventCount(memory, "sensen"), Is.EqualTo(initialCount));

            InvokePrivate(app, "EnterModelView");
            var requestState = (ChatRequestState)GetPrivateField(app, "chatRequestState");
            InvokePrivate(
                app,
                "FinishCloudAnswer",
                requestState.Begin("sensen"),
                "森森，给我表演一下。",
                new AIResponse
                {
                    animalId = "sensen",
                    reply = "好呀，看我的！",
                    answerMode = "social_chat",
                    ActionSuggestion = AIAction.Taunt
                });
            Assert.That(LiveEventCount(memory, "sensen"), Is.EqualTo(initialCount));
            yield return WaitUntilNotBusy(animation, 5f);
        }

        [UnityTest]
        public IEnumerator CorruptPrimaryMemory_RecoversBackupWithoutChangingBusinessState()
        {
            yield return LoadDemoScene();
            var progress = FindSingle<AnimalProgressService>();
            var memory = FindSingle<AnimalExperienceController>().CharacterMemory;
            FindSingle<ARImageScanController>().SimulateMarkerDetected("sensen");
            yield return null;
            CompleteSensenMission(progress);
            progress.ReplaceConversation("sensen", new[]
            {
                new ConversationRecord { role = "assistant", content = "已有聊天仍应保留。" }
            });
            AssertCompleteProjection(memory.GetProjection("sensen"));

            File.WriteAllText(MemoryPath, "{broken-json");
            Assert.DoesNotThrow(() => memory.ReloadForDevelopment());

            Assert.That(memory.Status, Is.EqualTo(CharacterMemoryStoreStatus.RecoveredFromBackup));
            AssertCompleteProjection(memory.GetProjection("sensen"));
            AssertProgressRemainsComplete(progress);
            Assert.That(progress.GetConversation("sensen").Single().content, Does.StartWith("已有聊天"));
        }

        [UnityTest]
        public IEnumerator AnimalAndClearAllOperations_RemainPartitionedAndNeverClearProgress()
        {
            yield return LoadDemoScene();
            var progress = FindSingle<AnimalProgressService>();
            var memory = FindSingle<AnimalExperienceController>().CharacterMemory;
            FindSingle<ARImageScanController>().SimulateMarkerDetected("sensen");
            yield return null;
            Assert.That(progress.Unlock("pangolin"), Is.True);

            Assert.That(memory.GetProjection("sensen").Discovered, Is.True);
            Assert.That(memory.GetProjection("pangolin").Discovered, Is.True);
            Assert.That(memory.ClearAnimalMemory("sensen"), Is.EqualTo(CharacterMemoryOperationResult.Saved));
            Assert.That(memory.GetProjection("sensen").Discovered, Is.False);
            Assert.That(memory.GetProjection("pangolin").Discovered, Is.True);
            Assert.That(progress.IsUnlocked("sensen"), Is.True);
            Assert.That(progress.IsUnlocked("pangolin"), Is.True);

            Assert.That(memory.ClearAllCharacterMemory(), Is.EqualTo(CharacterMemoryOperationResult.Saved));
            memory.ReloadForDevelopment();
            Assert.That(memory.GetProjection("sensen").Discovered, Is.False);
            Assert.That(memory.GetProjection("pangolin").Discovered, Is.False);
            Assert.That(progress.IsUnlocked("sensen"), Is.True);
            Assert.That(progress.IsUnlocked("pangolin"), Is.True);
        }

        private string MemoryPath => Path.Combine(temporaryDirectory, "character-memory.json");

        private IEnumerator LoadDemoScene()
        {
            var operation = SceneManager.LoadSceneAsync("DemoScene", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            yield return operation;
            yield return null;
        }

        private void CompleteSensenMission(AnimalProgressService progress)
        {
            progress.MarkMissionCompleted(
                "sensen",
                sensenDefinition.Mission.MissionId,
                sensenDefinition.Mission.BadgeId,
                sensenDefinition.Mission.LearnedKnowledgeId);
        }

        private void AssertCompleteProjection(CharacterMemoryProjection projection)
        {
            Assert.That(projection.Discovered, Is.True);
            Assert.That(projection.CompletedMissionIds, Is.EqualTo(new[] { sensenDefinition.Mission.MissionId }));
            Assert.That(projection.LearnedKnowledgeIds, Is.EqualTo(new[] { sensenDefinition.Mission.LearnedKnowledgeId }));
            Assert.That(projection.EarnedBadgeIds, Is.EqualTo(new[] { sensenDefinition.Mission.BadgeId }));
        }

        private void AssertProgressRemainsComplete(AnimalProgressService progress)
        {
            Assert.That(progress.TryGetSnapshot("sensen", out var snapshot), Is.True);
            Assert.That(snapshot.unlocked, Is.True);
            Assert.That(snapshot.missionCompleted, Is.True);
            Assert.That(snapshot.learnedKnowledgeIds, Does.Contain(sensenDefinition.Mission.LearnedKnowledgeId));
            Assert.That(snapshot.earnedBadgeIds, Does.Contain(sensenDefinition.Mission.BadgeId));
        }

        private static int LiveEventCount(CharacterMemoryService memory, string animalId)
        {
            var method = typeof(CharacterMemoryService).GetMethod(
                "GetLiveEventCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(memory, new object[] { animalId });
        }

        private static IEnumerator WaitForAnimatorState(Animator animator, string stateName, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (animator != null &&
                    !animator.IsInTransition(0) &&
                    animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Animator did not reach " + stateName + ".");
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

            Assert.Fail("Animation request stayed busy.");
        }

        private static T FindSingle<T>() where T : Component
        {
            var matches = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(matches, Has.Length.EqualTo(1));
            return matches[0];
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, arguments);
        }
    }
}
