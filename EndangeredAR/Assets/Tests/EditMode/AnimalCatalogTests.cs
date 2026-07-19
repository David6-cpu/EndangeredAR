using System.Collections.Generic;
using EndangeredAR.Animals;
using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EndangeredAR.Tests.EditMode
{
    public class AnimalCatalogTests
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
        public void Build_KeepsConfiguredDefinitionsInSourceOrder()
        {
            var pangolin = CreateDefinition("pangolin");
            var leopard = CreateDefinition("leopard");

            var catalog = new AnimalCatalog(new[] { pangolin, leopard });

            Assert.That(catalog.Animals, Is.EqualTo(new[] { pangolin, leopard }));
            Assert.That(catalog.Issues, Is.Empty);
        }

        [Test]
        public void Build_SkipsNullBlankAndDuplicateDefinitions()
        {
            var pangolin = CreateDefinition("pangolin");
            var blankId = CreateDefinition("   ");
            var duplicatePangolin = CreateDefinition(" PANGOLIN ");
            var leopard = CreateDefinition("leopard");

            var catalog = new AnimalCatalog(new AnimalDefinition[]
            {
                null, pangolin, blankId, duplicatePangolin, leopard
            });

            Assert.That(catalog.Animals, Is.EqualTo(new[] { pangolin, leopard }));
            Assert.That(catalog.Issues, Has.Count.EqualTo(3));
            Assert.That(catalog.Issues, Has.Some.Contains("duplicate animal ID"));
        }

        [Test]
        public void Build_SkipsIncompleteDefinitionAndRecordsIssue()
        {
            var incomplete = CreateIncompleteDefinition("incomplete");
            var configured = CreateDefinition("pangolin");

            Assert.That(incomplete.IsConfigured, Is.False);

            var catalog = new AnimalCatalog(new[] { incomplete, configured });

            Assert.That(catalog.Animals, Is.EqualTo(new[] { configured }));
            Assert.That(catalog.Issues, Has.Count.EqualTo(1));
            Assert.That(catalog.Issues, Has.Some.EqualTo("Animal definition 'incomplete' is not configured."));
        }

        [Test]
        public void TryGet_UsesCaseInsensitiveAnimalId()
        {
            var pangolin = CreateDefinition("pangolin");
            var catalog = new AnimalCatalog(new[] { pangolin });

            var found = catalog.TryGet(" PANGOLIN ", out var definition);

            Assert.That(found, Is.True);
            Assert.That(definition, Is.SameAs(pangolin));
        }

        [Test]
        public void TryGet_UnknownIdReturnsFalseWithoutThrowing()
        {
            var catalog = new AnimalCatalog(new[] { CreateDefinition("pangolin") });

            Assert.DoesNotThrow(() => catalog.TryGet(null, out _));
            Assert.That(catalog.TryGet("leopard", out var definition), Is.False);
            Assert.That(definition, Is.Null);
        }

        [Test]
        public void TryGet_WhitespaceOnlyIdReturnsFalseWithNullDefinition()
        {
            var catalog = new AnimalCatalog(new[] { CreateDefinition("pangolin") });

            var found = catalog.TryGet("   ", out var definition);

            Assert.That(found, Is.False);
            Assert.That(definition, Is.Null);
        }

        [Test]
        public void Service_InitializeIsIdempotent()
        {
            var pangolin = CreateDefinition("pangolin");
            var service = CreateService(new[] { pangolin }, "pangolin");

            service.Initialize();
            var catalog = service.Catalog;
            var defaultAnimal = service.DefaultAnimal;
            service.Initialize();

            Assert.That(service.Catalog, Is.SameAs(catalog));
            Assert.That(service.DefaultAnimal, Is.SameAs(defaultAnimal));
        }

        [Test]
        public void Service_LogsIssuesOnlyOnceAcrossRepeatedInitializeAndTryGetCalls()
        {
            var incomplete = CreateIncompleteDefinition("incomplete");
            var service = CreateService(new[] { incomplete }, "pangolin");

            LogAssert.Expect(LogType.Warning, "AnimalCatalogService: Animal definition 'incomplete' is not configured.");
            service.Initialize();
            service.Initialize();
            service.TryGet("pangolin", out _);
            service.TryGet("leopard", out _);
        }

        [Test]
        public void Service_SelectsConfiguredDefaultIdCaseInsensitively()
        {
            var pangolin = CreateDefinition("pangolin");
            var leopard = CreateDefinition("leopard");
            var service = CreateService(new[] { pangolin, leopard }, " PANGOLIN ");

            service.Initialize();

            Assert.That(service.DefaultAnimal, Is.SameAs(pangolin));
        }

        [Test]
        public void Service_MissingDefaultFallsBackToFirstValidDefinition()
        {
            var pangolin = CreateDefinition("pangolin");
            var leopard = CreateDefinition("leopard");
            var service = CreateService(new[] { pangolin, leopard }, "missing");

            service.Initialize();

            Assert.That(service.DefaultAnimal, Is.SameAs(pangolin));
        }

        [Test]
        public void Service_TryGetLazilyInitializes()
        {
            var pangolin = CreateDefinition("pangolin");
            var service = CreateService(new[] { pangolin }, "pangolin");

            var found = service.TryGet("pangolin", out var definition);

            Assert.That(service.Catalog, Is.Not.Null);
            Assert.That(found, Is.True);
            Assert.That(definition, Is.SameAs(pangolin));
        }

        private AnimalDefinition CreateDefinition(string animalId)
        {
            var knowledge = Create<AnimalKnowledgeProfile>();
            var mission = Create<MissionDefinition>();
            var definition = Create<AnimalDefinition>();
            definition.Configure(animalId, "Display", "Short", "Scientific", "marker", "model", "texture",
                Vector3.zero, Vector3.zero, Vector3.zero, Vector3.one, "Welcome", Color.green,
                null, null, knowledge, mission);
            return definition;
        }

        private AnimalDefinition CreateIncompleteDefinition(string animalId)
        {
            var definition = Create<AnimalDefinition>();
            definition.Configure(animalId, "Display", "Short", "Scientific", "marker", "model", "texture",
                Vector3.zero, Vector3.zero, Vector3.zero, Vector3.one, "Welcome", Color.green,
                null, null, null, null);
            return definition;
        }

        private AnimalCatalogService CreateService(AnimalDefinition[] definitions, string defaultAnimalId)
        {
            var gameObject = new GameObject("AnimalCatalogServiceTest");
            createdObjects.Add(gameObject);
            var service = gameObject.AddComponent<AnimalCatalogService>();
            var serializedService = new SerializedObject(service);
            var definitionsProperty = serializedService.FindProperty("definitions");

            definitionsProperty.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
            {
                definitionsProperty.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serializedService.FindProperty("defaultAnimalId").stringValue = defaultAnimalId;
            serializedService.ApplyModifiedPropertiesWithoutUndo();
            return service;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }
    }
}
