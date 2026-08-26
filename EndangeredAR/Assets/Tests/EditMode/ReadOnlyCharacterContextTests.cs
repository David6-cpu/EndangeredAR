using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using EndangeredAR.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class ReadOnlyCharacterContextTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private readonly List<string> temporaryDirectories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            AnimalProgressService.RepositoryPathOverrideForTests = null;
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            foreach (var directory in temporaryDirectories)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void ContextDtos_ExposeValuesWithoutPublicMutationSurface()
        {
            var dtoTypes = new[]
            {
                typeof(ReadOnlyCharacterContext),
                typeof(ReadOnlyCharacterState),
                typeof(ReadOnlyTaskState),
                typeof(ReadOnlyInteractionState)
            };

            foreach (var dtoType in dtoTypes)
            {
                Assert.That(dtoType.GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty, dtoType.Name);
                Assert.That(
                    dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public).All(property => property.SetMethod == null),
                    Is.True,
                    dtoType.Name);
            }
        }

        [Test]
        public void Provider_CreatesSnapshotOnlyFromPersistedProgressAndCanonicalMission()
        {
            var progress = CreateProgressService();
            var catalog = CreateSensenCatalog();
            progress.Unlock("sensen");
            progress.MarkMissionCompleted("sensen", "sensen-food", "eco-guardian", "sensen.diet");
            var provider = new ReadOnlyCharacterContextProvider(progress, catalog);

            var context = provider.CreateSnapshot("sensen");

            Assert.That(context.Character.AnimalId, Is.EqualTo("sensen"));
            Assert.That(context.Character.Unlocked, Is.True);
            Assert.That(context.Character.LearnedKnowledgeCount, Is.EqualTo(1));
            Assert.That(context.Character.EarnedBadgeCount, Is.EqualTo(1));
            Assert.That(context.Task.TaskId, Is.Not.Null.And.Not.Empty);
            Assert.That(context.Task.TaskTitle, Is.Not.Null.And.Not.Empty);
            Assert.That(context.Task.Completed, Is.True);
            Assert.That(context.Interaction.RecentTopics, Is.Empty);
            Assert.That(context.Interaction.RecentMilestones, Is.Empty);

            var json = JsonUtility.ToJson(context);
            StringAssert.DoesNotContain("nickname", json);
            StringAssert.DoesNotContain("userId", json);
            StringAssert.DoesNotContain("level", json);
            StringAssert.DoesNotContain("score", json);
            StringAssert.DoesNotContain("percent", json);
        }

        [Test]
        public void Provider_ReturnedSnapshotCannotMutateProgressState()
        {
            var progress = CreateProgressService();
            var catalog = CreateSensenCatalog();
            progress.Unlock("sensen");
            progress.MarkMissionCompleted("sensen", "sensen-food", "eco-guardian", "sensen.diet");
            var provider = new ReadOnlyCharacterContextProvider(progress, catalog);

            Assert.That(progress.TryGetSnapshot("sensen", out var mutableCopy), Is.True);
            mutableCopy.unlocked = false;
            mutableCopy.learnedKnowledgeIds.Clear();
            mutableCopy.earnedBadgeIds.Clear();

            var freshContext = provider.CreateSnapshot("sensen");
            Assert.That(freshContext.Character.Unlocked, Is.True);
            Assert.That(freshContext.Character.LearnedKnowledgeCount, Is.EqualTo(1));
            Assert.That(freshContext.Character.EarnedBadgeCount, Is.EqualTo(1));
        }

        [Test]
        public void Provider_CreatingSnapshotDoesNotWriteProgressOrRaiseProgressEvents()
        {
            var progress = CreateProgressService();
            var catalog = CreateSensenCatalog();
            progress.Unlock("sensen");
            var repositoryPath = progress.ActiveRepositoryPath;
            var before = File.ReadAllText(repositoryPath);
            var progressEvents = 0;
            progress.ProgressChanged += _ => progressEvents++;
            var provider = new ReadOnlyCharacterContextProvider(progress, catalog);

            var context = provider.CreateSnapshot("sensen");

            Assert.That(context.Character.Unlocked, Is.True);
            Assert.That(File.ReadAllText(repositoryPath), Is.EqualTo(before));
            Assert.That(progressEvents, Is.EqualTo(0));
        }

        [Test]
        public void Provider_UnknownAnimalReturnsEmptyContextWithoutCreatingProgress()
        {
            var progress = CreateProgressService();
            var provider = new ReadOnlyCharacterContextProvider(progress, CreateSensenCatalog());

            var context = provider.CreateSnapshot("unknown-animal");

            Assert.That(context.IsEmpty, Is.True);
            Assert.That(progress.TryGetSnapshot("unknown-animal", out _), Is.False);
        }

        [Test]
        public void AIManager_MissingContextProviderUsesEmptyContextAndStillAnswers()
        {
            var host = new GameObject("AIManager ReadOnly Context Tests");
            createdObjects.Add(host);
            var manager = host.AddComponent<AIManager>();
            var knowledge = host.AddComponent<LocalKnowledgeChatService>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("localKnowledgeService").objectReferenceValue = knowledge;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var request = new AIRequest
            {
                animalId = "sensen",
                message = "你好",
                history = Array.Empty<EndangeredAR.API.ChatMessage>()
            };
            AIResponse response = null;

            Run(manager.Send(request, value => response = value, error => Assert.Fail(error.Code)));

            Assert.That(request.Context, Is.Not.Null);
            Assert.That(request.Context.IsEmpty, Is.True);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
        }

        [Test]
        public void AIManager_UsesInterfaceProviderAndCapturesFreshAnimalSnapshot()
        {
            var host = new GameObject("AIManager Context Provider Tests");
            createdObjects.Add(host);
            var manager = host.AddComponent<AIManager>();
            var knowledge = host.AddComponent<LocalKnowledgeChatService>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("localKnowledgeService").objectReferenceValue = knowledge;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var expected = ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState("sensen", true, 2, 1),
                new ReadOnlyTaskState("food-mission", "帮森森寻找食物", false),
                ReadOnlyInteractionState.Empty);
            IReadOnlyCharacterContextProvider provider = new StubContextProvider(expected);
            manager.ConfigureContextProvider(provider);
            var request = new AIRequest { animalId = "sensen", message = "你好" };

            Run(manager.Send(request, _ => { }, error => Assert.Fail(error.Code)));

            Assert.That(request.Context, Is.SameAs(expected));
        }

        [Test]
        public void ContextIsRequestOnlyAndDoesNotChangeHistorySchema()
        {
            Assert.That(typeof(AIResponse).GetField("Context"), Is.Null);
            Assert.That(typeof(AIResponse).GetField("context"), Is.Null);
            Assert.That(
                typeof(ConversationRecord).GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => field.Name),
                Is.EquivalentTo(new[] { "role", "content" }));
        }

        private AnimalProgressService CreateProgressService()
        {
            var directory = Path.Combine(Path.GetTempPath(), "EndangeredAR-ReadOnlyContext-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            temporaryDirectories.Add(directory);
            AnimalProgressService.RepositoryPathOverrideForTests = Path.Combine(directory, "animal-progress.json");
            var host = new GameObject("Animal Progress Context Tests");
            createdObjects.Add(host);
            return host.AddComponent<AnimalProgressService>();
        }

        private AnimalCatalogService CreateSensenCatalog()
        {
            var sensen = Resources.Load<AnimalDefinition>("Animals/Sensen");
            Assert.That(sensen, Is.Not.Null);
            var host = new GameObject("Animal Catalog Context Tests");
            createdObjects.Add(host);
            var catalog = host.AddComponent<AnimalCatalogService>();
            var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("definitions");
            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = sensen;
            serialized.FindProperty("defaultAnimalId").stringValue = "sensen";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.Initialize();
            return catalog;
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

        private sealed class StubContextProvider : IReadOnlyCharacterContextProvider
        {
            private readonly ReadOnlyCharacterContext context;

            public StubContextProvider(ReadOnlyCharacterContext context)
            {
                this.context = context;
            }

            public ReadOnlyCharacterContext CreateSnapshot(string animalId)
            {
                return context;
            }
        }
    }
}
