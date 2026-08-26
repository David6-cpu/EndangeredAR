using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.Memory;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryDevelopmentToolsTests
    {
        private const string BootstrapPath = "Assets/Scripts/Development/DevelopmentToolsBootstrap.cs";
        private const string AnimationPanelPath = "Assets/Scripts/Development/AnimalAnimationDebugPanel.cs";
        private const string MemoryPanelPath = "Assets/Scripts/Development/CharacterMemoryDebugPanel.cs";
        private const string ProvenancePanelPath = "Assets/Scripts/Development/AIProvenanceDebugPanel.cs";

        [TearDown]
        public void TearDown()
        {
            var root = GameObject.Find("EndangeredAR Development Tools");
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Sources_AreDevelopmentConditionedAndDoNotExposeStorageOrAiWritePaths()
        {
            Assert.That(File.Exists(BootstrapPath), Is.True);
            Assert.That(File.Exists(AnimationPanelPath), Is.True);
            Assert.That(File.Exists(MemoryPanelPath), Is.True);
            Assert.That(File.Exists(ProvenancePanelPath), Is.True);

            var bootstrap = File.ReadAllText(BootstrapPath);
            var memoryPanel = File.ReadAllText(MemoryPanelPath);
            var provenancePanel = File.ReadAllText(ProvenancePanelPath);
            Assert.That(bootstrap.TrimStart(), Does.StartWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(memoryPanel.TrimStart(), Does.StartWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(provenancePanel.TrimStart(), Does.StartWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));

            StringAssert.DoesNotContain("message", provenancePanel.ToLowerInvariant());
            StringAssert.DoesNotContain("prompt", provenancePanel.ToLowerInvariant());
            StringAssert.DoesNotContain("apikey", provenancePanel.ToLowerInvariant());
            StringAssert.DoesNotContain("authorization", provenancePanel.ToLowerInvariant());

            var combined = bootstrap + memoryPanel;
            StringAssert.DoesNotContain("character-memory.json", combined);
            StringAssert.DoesNotContain("Application.persistentDataPath", combined);
            StringAssert.DoesNotContain("ICharacterMemoryRepository", combined);
            StringAssert.DoesNotContain("AppendBatch", combined);
            StringAssert.DoesNotContain("AIManager", combined);
            StringAssert.DoesNotContain("AIResponse", combined);
            StringAssert.DoesNotContain("MissionController", combined);
            StringAssert.DoesNotContain("AnimalProgressService", combined);
        }

        [Test]
        public void Bootstrap_CreatesExactlyOnePanelOfEachDevelopmentToolOnOneRoot()
        {
            var bootstrapType = Type.GetType(
                "EndangeredAR.Development.DevelopmentToolsBootstrap, EndangeredAR.Runtime");
            var animationPanelType = Type.GetType(
                "EndangeredAR.Development.AnimalAnimationDebugPanel, EndangeredAR.Runtime");
            var memoryPanelType = Type.GetType(
                "EndangeredAR.Development.CharacterMemoryDebugPanel, EndangeredAR.Runtime");
            var provenancePanelType = Type.GetType(
                "EndangeredAR.Development.AIProvenanceDebugPanel, EndangeredAR.Runtime");
            Assert.That(bootstrapType, Is.Not.Null);
            Assert.That(animationPanelType, Is.Not.Null);
            Assert.That(memoryPanelType, Is.Not.Null);
            Assert.That(provenancePanelType, Is.Not.Null);

            var ensure = bootstrapType.GetMethod(
                "EnsureInitialized",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(ensure, Is.Not.Null);
            ensure.Invoke(null, null);
            ensure.Invoke(null, null);

            var components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var animationPanels = components.Where(value => value.GetType() == animationPanelType).ToArray();
            var memoryPanels = components.Where(value => value.GetType() == memoryPanelType).ToArray();
            var provenancePanels = components.Where(value => value.GetType() == provenancePanelType).ToArray();
            Assert.That(animationPanels, Has.Length.EqualTo(1));
            Assert.That(memoryPanels, Has.Length.EqualTo(1));
            Assert.That(provenancePanels, Has.Length.EqualTo(1));
            Assert.That(memoryPanels[0].gameObject, Is.SameAs(animationPanels[0].gameObject));
            Assert.That(provenancePanels[0].gameObject, Is.SameAs(animationPanels[0].gameObject));
            Assert.That(animationPanels[0].GetComponent<Canvas>(), Is.Not.Null);
        }

        [Test]
        public void MemoryPanel_CachesNoExperienceMemoryAnimalOrUnityRuntimeReference()
        {
            var panelType = Type.GetType(
                "EndangeredAR.Development.CharacterMemoryDebugPanel, EndangeredAR.Runtime");
            Assert.That(panelType, Is.Not.Null);

            var forbiddenTypes = new[]
            {
                typeof(AnimalExperienceController),
                typeof(CharacterMemoryService),
                typeof(AnimalDefinition),
                typeof(Animator)
            };
            var fields = panelType.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(
                fields.Where(field => forbiddenTypes.Contains(field.FieldType)),
                Is.Empty);
            Assert.That(panelType.GetMethod("AppendBatch"), Is.Null);
            Assert.That(panelType.GetMethod("ClearCurrentAnimal", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(panelType.GetMethod("ClearAll", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(panelType.GetMethod("Reload", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        }

        [Test]
        public void MemoryService_ExposesOnlyAnInternalReadOnlyLiveEventCountQuery()
        {
            var method = typeof(CharacterMemoryService).GetMethod(
                "GetLiveEventCount",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.IsPublic, Is.False);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(int)));
            Assert.That(method.GetParameters().Select(value => value.ParameterType),
                Is.EqualTo(new[] { typeof(string) }));
        }

        [Test]
        public void AiContracts_ContainNoCharacterMemoryTypesOrWriteCommands()
        {
            var contractTypes = new[] { typeof(AIRequest), typeof(AIResponse) };
            foreach (var contractType in contractTypes)
            {
                var memberTypes = contractType
                    .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(member => member is FieldInfo field
                        ? field.FieldType
                        : member is PropertyInfo property
                            ? property.PropertyType
                            : null)
                    .Where(type => type != null)
                    .ToArray();
                Assert.That(memberTypes.Any(type => type.Namespace == "EndangeredAR.Memory"), Is.False);
            }

            Assert.That(typeof(AIResponse).GetMember("memoryUpdate"), Is.Empty);
            Assert.That(typeof(AIResponse).GetMember("completeTask"), Is.Empty);
            Assert.That(typeof(AIResponse).GetMember("awardBadge"), Is.Empty);
        }
    }
}
