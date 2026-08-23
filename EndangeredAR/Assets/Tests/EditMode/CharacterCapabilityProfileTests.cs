using EndangeredAR.AI;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class CharacterCapabilityProfileTests
    {
        [Test]
        public void SensenCapability_AllowsOnlyTaunt()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Capabilities, Is.Not.Null);
            Assert.That(definition.Capabilities.Supports(AIAction.Taunt), Is.True);
            Assert.That(definition.Capabilities.Supports(AIAction.None), Is.False);
            Assert.That(definition.Capabilities.SupportedActions, Is.EqualTo(new[] { AIAction.Taunt }));
        }

        [Test]
        public void Supports_RejectsNoneDuplicatesAndUnlistedFutureValues()
        {
            var profile = ScriptableObject.CreateInstance<CharacterCapabilityProfile>();
            try
            {
                var serialized = new SerializedObject(profile);
                var actions = serialized.FindProperty("supportedActions");
                actions.arraySize = 4;
                actions.GetArrayElementAtIndex(0).enumValueIndex = (int)AIAction.None;
                actions.GetArrayElementAtIndex(1).enumValueIndex = (int)AIAction.Taunt;
                actions.GetArrayElementAtIndex(2).enumValueIndex = (int)AIAction.Taunt;
                actions.GetArrayElementAtIndex(3).intValue = 999;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.Supports(AIAction.None), Is.False);
                Assert.That(profile.Supports(AIAction.Taunt), Is.True);
                Assert.That(profile.Supports((AIAction)999), Is.False);
                Assert.That(profile.SupportedActions, Is.EqualTo(new[] { AIAction.Taunt }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void DefinitionWithoutCapabilityProfile_FailsClosed()
        {
            var definition = ScriptableObject.CreateInstance<AnimalDefinition>();
            try
            {
                Assert.That(definition.Capabilities, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }
    }
}
