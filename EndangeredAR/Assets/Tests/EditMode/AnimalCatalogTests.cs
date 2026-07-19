using System.Collections.Generic;
using EndangeredAR.Animals;
using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEngine;

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

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }
    }
}
