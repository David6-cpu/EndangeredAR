using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EndangeredAR.Animals;
using EndangeredAR.Models;
using EndangeredAR.Missions;
using EndangeredAR.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AnimalExperienceControllerTests
    {
        private const string ControllerTypeName = "EndangeredAR.Animals.AnimalExperienceController, EndangeredAR.Runtime";

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private readonly List<string> temporaryDirectories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            AnimalProgressService.RepositoryPathOverrideForTests = null;

            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();

            foreach (var directory in temporaryDirectories)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }

            temporaryDirectories.Clear();
        }

        [Test]
        public void Prepare_UnknownAnimalDoesNotChangeCurrent()
        {
            var pangolin = CreateDefinition("pangolin", "pangolin-mission", new Vector3(1f, 2f, 3f));
            var setup = CreateSetup(pangolin);
            var prepared = Select(setup.controller, "Prepare", "pangolin");
            var currentAnimal = CurrentAnimal(setup.controller);
            var currentProgress = CurrentProgress(setup.controller);
            var hostPosition = setup.host.position;

            var result = Select(setup.controller, "Prepare", "missing");

            Assert.That(Status(result), Is.EqualTo("UnknownAnimal"));
            Assert.That(CurrentAnimal(setup.controller), Is.SameAs(currentAnimal));
            Assert.That(CurrentProgress(setup.controller), Is.SameAs(currentProgress));
            Assert.That(setup.host.position, Is.EqualTo(hostPosition));
            Assert.That(setup.progress.IsUnlocked("pangolin"), Is.False);

            var unconfiguredHost = new GameObject("Unconfigured Experience Controller");
            createdObjects.Add(unconfiguredHost);
            var unconfiguredController = unconfiguredHost.AddComponent(ControllerType());

            Assert.DoesNotThrow(() => Select(unconfiguredController, "Prepare", "pangolin"));
            Assert.That(Status(Select(unconfiguredController, "Prepare", "pangolin")), Is.EqualTo("UnknownAnimal"));
            Assert.That(CurrentAnimal(unconfiguredController), Is.Null);
        }

        [Test]
        public void SelectFromScan_FirstTimeUnlocksAndConfiguresAnimal()
        {
            var leopard = CreateDefinition("leopard", "leopard-mission", new Vector3(4f, 5f, 6f));
            var setup = CreateSetup(leopard);
            var expectedRotation = setup.host.rotation;
            var expectedScale = setup.host.localScale;
            AnimalDefinition changedAnimal = null;
            var stateWasConfiguredWhenChanged = false;
            SubscribeToCurrentAnimalChanged(setup.controller, definition =>
            {
                changedAnimal = definition;
                stateWasConfiguredWhenChanged =
                    CurrentAnimal(setup.controller) == definition &&
                    CurrentProgress(setup.controller).animalId == definition.AnimalId &&
                    setup.mission.CurrentMissionId == definition.Mission.MissionId &&
                    setup.loader.LoadedAnimalId == definition.AnimalId &&
                    setup.host.position == definition.ExperiencePosition;
            });

            var result = Select(setup.controller, "SelectFromScan", "leopard");

            Assert.That(Status(result), Is.EqualTo("NewlyUnlocked"));
            Assert.That(Animal(result), Is.SameAs(leopard));
            Assert.That(CurrentAnimal(setup.controller), Is.SameAs(leopard));
            Assert.That(CurrentProgress(setup.controller).animalId, Is.EqualTo("leopard"));
            Assert.That(CurrentProgress(setup.controller).unlocked, Is.True);
            Assert.That(setup.mission.CurrentMissionId, Is.EqualTo("leopard-mission"));
            Assert.That(setup.loader.LoadedAnimalId, Is.EqualTo("leopard"));
            Assert.That(setup.host.position, Is.EqualTo(leopard.ExperiencePosition));
            Assert.That(setup.host.rotation, Is.EqualTo(expectedRotation));
            Assert.That(setup.host.localScale, Is.EqualTo(expectedScale));
            Assert.That(changedAnimal, Is.SameAs(leopard));
            Assert.That(stateWasConfiguredWhenChanged, Is.True);
        }

        [Test]
        public void SelectFromScan_RepeatDoesNotDuplicateUnlockReward()
        {
            var pangolin = CreateDefinition("pangolin", "pangolin-mission", Vector3.zero);
            var setup = CreateSetup(pangolin);

            var first = Select(setup.controller, "SelectFromScan", "pangolin");
            var firstUnlockedAtUtc = CurrentProgress(setup.controller).unlockedAtUtc;
            var repeated = Select(setup.controller, "SelectFromScan", "PANGOLIN");

            Assert.That(Status(first), Is.EqualTo("NewlyUnlocked"));
            Assert.That(Status(repeated), Is.EqualTo("Selected"));
            Assert.That(setup.progress.UnlockedCount, Is.EqualTo(1));
            Assert.That(CurrentProgress(setup.controller).unlockedAtUtc, Is.EqualTo(firstUnlockedAtUtc));
        }

        [Test]
        public void SelectFromCatalog_LockedAnimalIsRejected()
        {
            var pangolin = CreateDefinition("pangolin", "pangolin-mission", new Vector3(1f, 0f, 0f));
            var leopard = CreateDefinition("leopard", "leopard-mission", new Vector3(2f, 0f, 0f));
            var setup = CreateSetup(pangolin, leopard);
            Select(setup.controller, "Prepare", "pangolin");
            var currentAnimal = CurrentAnimal(setup.controller);
            var currentProgress = CurrentProgress(setup.controller);
            var hostPosition = setup.host.position;
            var missionId = setup.mission.CurrentMissionId;
            var loadedAnimalId = setup.loader.LoadedAnimalId;

            var result = Select(setup.controller, "SelectFromCatalog", "leopard");

            Assert.That(Status(result), Is.EqualTo("Locked"));
            Assert.That(CurrentAnimal(setup.controller), Is.SameAs(currentAnimal));
            Assert.That(CurrentProgress(setup.controller), Is.SameAs(currentProgress));
            Assert.That(setup.host.position, Is.EqualTo(hostPosition));
            Assert.That(setup.mission.CurrentMissionId, Is.EqualTo(missionId));
            Assert.That(setup.loader.LoadedAnimalId, Is.EqualTo(loadedAnimalId));
        }

        [Test]
        public void SelectFromCatalog_UnlockedAnimalRestoresMissionState()
        {
            var pangolin = CreateDefinition("pangolin", "pangolin-mission", new Vector3(1f, 0f, 0f));
            var leopard = CreateDefinition("leopard", "leopard-mission", new Vector3(2f, 0f, 0f));
            var setup = CreateSetup(pangolin, leopard);
            Select(setup.controller, "SelectFromScan", "pangolin");
            setup.progress.MarkMissionCompleted("pangolin", "pangolin-badge", "pangolin-knowledge");
            Select(setup.controller, "Prepare", "leopard");

            var result = Select(setup.controller, "SelectFromCatalog", "pangolin");

            Assert.That(Status(result), Is.EqualTo("Selected"));
            Assert.That(CurrentAnimal(setup.controller), Is.SameAs(pangolin));
            Assert.That(CurrentProgress(setup.controller).missionCompleted, Is.True);
            Assert.That(setup.mission.CurrentMissionId, Is.EqualTo("pangolin-mission"));
            Assert.That(setup.mission.IsCompleted, Is.True);
        }

        [Test]
        public void SwitchingAnimalsDoesNotReuseMissionOrConversationState()
        {
            var pangolin = CreateDefinition("pangolin", "pangolin-mission", new Vector3(1f, 0f, 0f));
            var leopard = CreateDefinition("leopard", "leopard-mission", new Vector3(2f, 0f, 0f));
            var setup = CreateSetup(pangolin, leopard);
            Select(setup.controller, "SelectFromScan", "pangolin");
            setup.progress.MarkMissionCompleted("pangolin", "pangolin-badge", "pangolin-knowledge");
            setup.progress.ReplaceConversation("pangolin", new[] { new ConversationRecord { role = "user", content = "pangolin conversation" } });
            Select(setup.controller, "SelectFromScan", "leopard");
            setup.progress.ReplaceConversation("leopard", new[] { new ConversationRecord { role = "assistant", content = "leopard conversation" } });

            Select(setup.controller, "SelectFromCatalog", "pangolin");

            Assert.That(CurrentProgress(setup.controller).missionCompleted, Is.True);
            Assert.That(CurrentProgress(setup.controller).recentConversation[0].content, Is.EqualTo("pangolin conversation"));
            Assert.That(setup.mission.IsCompleted, Is.True);

            Select(setup.controller, "SelectFromCatalog", "leopard");

            Assert.That(CurrentProgress(setup.controller).missionCompleted, Is.False);
            Assert.That(CurrentProgress(setup.controller).recentConversation[0].content, Is.EqualTo("leopard conversation"));
            Assert.That(setup.mission.CurrentMissionId, Is.EqualTo("leopard-mission"));
            Assert.That(setup.mission.IsCompleted, Is.False);
        }

        private Setup CreateSetup(params AnimalDefinition[] definitions)
        {
            AnimalProgressService.RepositoryPathOverrideForTests = CreateRepositoryPath();

            var catalogHost = new GameObject("Animal Catalog Service");
            createdObjects.Add(catalogHost);
            var catalog = catalogHost.AddComponent<AnimalCatalogService>();
            var serializedCatalog = new SerializedObject(catalog);
            var definitionProperty = serializedCatalog.FindProperty("definitions");
            definitionProperty.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
            {
                definitionProperty.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serializedCatalog.FindProperty("defaultAnimalId").stringValue = definitions[0].AnimalId;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            var progressHost = new GameObject("Animal Progress Service");
            createdObjects.Add(progressHost);
            var progress = progressHost.AddComponent<AnimalProgressService>();

            var missionHost = new GameObject("Mission Controller");
            createdObjects.Add(missionHost);
            var mission = missionHost.AddComponent<MissionController>();

            var modelHost = new GameObject("Animal Model Loader");
            createdObjects.Add(modelHost);
            var loader = modelHost.AddComponent<AnimalModelLoader>();

            var host = new GameObject("Experience Host").transform;
            createdObjects.Add(host.gameObject);
            host.SetPositionAndRotation(new Vector3(9f, 8f, 7f), Quaternion.Euler(10f, 20f, 30f));
            host.localScale = new Vector3(1.2f, 1.3f, 1.4f);

            var controllerHost = new GameObject("Animal Experience Controller");
            createdObjects.Add(controllerHost);
            var controller = controllerHost.AddComponent(ControllerType());
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("animalCatalogService").objectReferenceValue = catalog;
            serializedController.FindProperty("animalProgressService").objectReferenceValue = progress;
            serializedController.FindProperty("missionController").objectReferenceValue = mission;
            serializedController.FindProperty("modelLoader").objectReferenceValue = loader;
            serializedController.FindProperty("experienceHostTransform").objectReferenceValue = host;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            controller.GetType().GetMethod("Initialize").Invoke(controller, null);

            return new Setup(controller, progress, mission, loader, host);
        }

        private AnimalDefinition CreateDefinition(string animalId, string missionId, Vector3 experiencePosition)
        {
            var knowledge = ScriptableObject.CreateInstance<AnimalKnowledgeProfile>();
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            mission.Configure(
                missionId,
                missionId,
                "Choose an option",
                new[] { new MissionOptionDefinition("option", "Option", true) },
                "Correct",
                "Wrong",
                animalId + "-knowledge",
                "Fact",
                animalId + "-badge",
                10);
            var definition = ScriptableObject.CreateInstance<AnimalDefinition>();
            definition.Configure(
                animalId,
                animalId,
                animalId,
                "Scientific",
                "marker",
                string.Empty,
                string.Empty,
                experiencePosition,
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                "Welcome",
                Color.white,
                null,
                null,
                knowledge,
                mission);
            createdObjects.Add(knowledge);
            createdObjects.Add(mission);
            createdObjects.Add(definition);
            return definition;
        }

        private string CreateRepositoryPath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "EndangeredAR-ExperienceTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            temporaryDirectories.Add(directory);
            return Path.Combine(directory, "animal-progress.json");
        }

        private static Type ControllerType()
        {
            var controllerType = Type.GetType(ControllerTypeName);
            Assert.That(controllerType, Is.Not.Null, "AnimalExperienceController must exist in the runtime assembly.");
            return controllerType;
        }

        private static object Select(Component controller, string methodName, string animalId)
        {
            var method = controller.GetType().GetMethod(methodName, new[] { typeof(string) });
            Assert.That(method, Is.Not.Null, "AnimalExperienceController must expose " + methodName + "(string).");
            return method.Invoke(controller, new object[] { animalId });
        }

        private static AnimalDefinition CurrentAnimal(Component controller)
        {
            return (AnimalDefinition)Property(controller, "CurrentAnimal").GetValue(controller);
        }

        private static AnimalProgressRecord CurrentProgress(Component controller)
        {
            return (AnimalProgressRecord)Property(controller, "CurrentProgress").GetValue(controller);
        }

        private static string Status(object result)
        {
            return Property(result, "Status").GetValue(result).ToString();
        }

        private static AnimalDefinition Animal(object result)
        {
            return (AnimalDefinition)Property(result, "Animal").GetValue(result);
        }

        private static PropertyInfo Property(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, target.GetType().Name + " must expose " + propertyName + ".");
            return property;
        }

        private static void SubscribeToCurrentAnimalChanged(Component controller, Action<AnimalDefinition> callback)
        {
            var eventInfo = controller.GetType().GetEvent("CurrentAnimalChanged");
            Assert.That(eventInfo, Is.Not.Null, "AnimalExperienceController must expose CurrentAnimalChanged.");
            eventInfo.AddEventHandler(controller, callback);
        }

        private readonly struct Setup
        {
            public Setup(Component controller, AnimalProgressService progress, MissionController mission, AnimalModelLoader loader, Transform host)
            {
                this.controller = controller;
                this.progress = progress;
                this.mission = mission;
                this.loader = loader;
                this.host = host;
            }

            public Component controller { get; }
            public AnimalProgressService progress { get; }
            public MissionController mission { get; }
            public AnimalModelLoader loader { get; }
            public Transform host { get; }
        }
    }
}
