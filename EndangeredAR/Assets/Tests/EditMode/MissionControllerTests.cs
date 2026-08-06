using System.Collections.Generic;
using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class MissionControllerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void Configure_ResetsStateForDifferentMission()
        {
            var controller = CreateController();
            var firstMission = CreateMission("first-mission", 20);
            var secondMission = CreateMission("second-mission", 10);

            controller.Configure(firstMission);
            controller.StartMission();
            controller.SelectOption("plastic");
            controller.Configure(secondMission);

            Assert.That(controller.CurrentMissionId, Is.EqualTo("second-mission"));
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.NotStarted));
            Assert.That(controller.IsCompleted, Is.False);
            Assert.That(controller.Points, Is.Zero);

            controller.Configure(secondMission, alreadyCompleted: true);

            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.NotStarted));
            Assert.That(controller.Points, Is.Zero);
            controller.StartMission();
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Choosing));
            Assert.That(controller.SelectOption("leaf").PointsAwarded, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Completed));
        }

        [Test]
        public void Configure_SameMissionId_PreservesRestoredCompletionWithoutRewardingAgain()
        {
            var controller = CreateController();
            var restoredMission = CreateMission("food", 20);
            var reloadedMission = CreateMission(" FOOD ", 20);

            controller.Configure(restoredMission, alreadyCompleted: true);
            controller.Configure(reloadedMission);

            Assert.That(controller.CurrentMissionId, Is.EqualTo("FOOD"));
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.NotStarted));
            Assert.That(controller.IsCompleted, Is.True);
            Assert.That(controller.Points, Is.Zero);
            controller.StartMission();
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Choosing));
            Assert.That(controller.SelectOption("leaf").PointsAwarded, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Completed));
            Assert.That(controller.Points, Is.Zero);
        }

        [Test]
        public void RestoredReward_AllowsReplayButDoesNotAwardAgain()
        {
            var controller = CreateController();
            controller.Configure(CreateMission("food", 20), alreadyCompleted: true);

            controller.StartMission();
            var wrongResult = controller.SelectOption("plastic");
            var correctResult = controller.SelectOption("leaf");

            Assert.That(wrongResult.Success, Is.False);
            Assert.That(correctResult.Success, Is.True);
            Assert.That(correctResult.PointsAwarded, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Completed));
            Assert.That(controller.Points, Is.Zero);
        }

        [Test]
        public void WrongOption_ReturnsDefinitionFeedbackWithoutReward()
        {
            var controller = CreateController();
            controller.Configure(CreateMission("food", 20));
            controller.StartMission();

            var result = controller.SelectOption("plastic");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Feedback, Is.EqualTo("Wrong feedback"));
            Assert.That(result.PointsAwarded, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Wrong));
            Assert.That(controller.Points, Is.Zero);
        }

        [Test]
        public void CorrectOption_CompletesAndAwardsDefinitionReward()
        {
            var controller = CreateController();
            controller.Configure(CreateMission("food", 20));
            controller.StartMission();

            var result = controller.SelectOption("leaf");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Feedback, Is.EqualTo("Correct feedback"));
            Assert.That(result.LearnedFact, Is.EqualTo("Learned fact"));
            Assert.That(result.LearnedKnowledgeId, Is.EqualTo("food-knowledge"));
            Assert.That(result.BadgeId, Is.EqualTo("forest-badge"));
            Assert.That(result.PointsAwarded, Is.EqualTo(20));
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Completed));
            Assert.That(controller.Points, Is.EqualTo(20));
        }

        [Test]
        public void RepeatedCorrectOption_DoesNotAwardTwice()
        {
            var controller = CreateController();
            controller.Configure(CreateMission("food", 20));
            controller.StartMission();
            controller.SelectOption("leaf");

            var result = controller.SelectOption("flower");

            Assert.That(result.Success, Is.True);
            Assert.That(result.PointsAwarded, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.Completed));
            Assert.That(controller.Points, Is.EqualTo(20));
        }

        [Test]
        public void InvalidOption_ReturnsFailureWithoutThrowing()
        {
            var controller = CreateController();

            Assert.DoesNotThrow(() => controller.SelectOption(null));
            Assert.That(controller.SelectOption(null).Success, Is.False);
            Assert.That(controller.State, Is.EqualTo(MissionController.MissionState.NotStarted));

            controller.Configure(CreateMission("food", 20));

            Assert.DoesNotThrow(() => controller.SelectOption("missing"));
            Assert.That(controller.SelectOption("missing").Success, Is.False);
            Assert.That(controller.Points, Is.Zero);
        }

        [Test]
        public void LegacyWrappers_UnconfiguredController_LoadsValidResourceMissionAndAwardsOnce()
        {
            var controller = CreateController();
            var committedMission = Resources.Load<MissionDefinition>("Animals/SensenMission");
            Assert.That(committedMission, Is.Not.Null);

            controller.StartFoodMission();

            var firstResult = controller.SelectFood("嫩叶");
            var repeatedResult = controller.SelectFood("嫩叶");

            Assert.That(firstResult.Success, Is.True);
            Assert.That(firstResult.PointsAwarded, Is.EqualTo(20));
            Assert.That(repeatedResult.Success, Is.True);
            Assert.That(repeatedResult.PointsAwarded, Is.Zero);
            Assert.That(controller.CurrentMissionId, Is.EqualTo(committedMission.MissionId));
            Assert.That(controller.Points, Is.EqualTo(20));
        }

        private MissionController CreateController()
        {
            var gameObject = new GameObject("Mission Controller Test");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<MissionController>();
        }

        private MissionDefinition CreateMission(string missionId, int points)
        {
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            mission.Configure(
                missionId,
                "Mission",
                "Choose an option",
                new[]
                {
                    new MissionOptionDefinition("leaf", "Leaf", true),
                    new MissionOptionDefinition("flower", "Flower", true),
                    new MissionOptionDefinition("plastic", "Plastic", false)
                },
                "Correct feedback",
                "Wrong feedback",
                "food-knowledge",
                "Learned fact",
                "forest-badge",
                points);
            createdObjects.Add(mission);
            return mission;
        }
    }
}
