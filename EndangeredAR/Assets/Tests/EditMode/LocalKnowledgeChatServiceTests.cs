using System;
using System.Collections.Generic;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class LocalKnowledgeChatServiceTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void Answer_UsesOnlyProvidedAnimalProfile()
        {
            var service = CreateService();
            var selectedProfile = CreateProfile("selected-keyword", "Selected reply.", "Selected suggestion.", "Selected unknown.");
            var otherProfile = CreateProfile("selected-keyword", "Other reply.", "Other suggestion.", "Other unknown.");
            SetDefaultProfile(service, otherProfile);

            var answer = Answer(service, selectedProfile, "selected-keyword");

            Assert.That(answer.IsMatch, Is.True);
            Assert.That(answer.Reply, Is.EqualTo("Selected reply."));
            Assert.That(answer.SuggestedQuestions, Is.EqualTo(new[] { "Selected suggestion." }));
            Assert.That(answer.Reply, Is.Not.EqualTo(otherProfile.Entries[0].Reply));
        }

        [Test]
        public void Answer_UnknownQuestionUsesProvidedProfileFallback()
        {
            var service = CreateService();
            var profile = CreateProfile("known", "Known reply.", "Known suggestion.", "Profile fallback.");

            var answer = Answer(service, profile, "unmatched question");

            Assert.That(answer.IsMatch, Is.False);
            Assert.That(answer.Reply, Is.EqualTo("Profile fallback."));
            Assert.That(answer.SuggestedQuestions, Is.EqualTo(new[] { "Known suggestion." }));
        }

        [Test]
        public void Answer_NullProfileReturnsSafeGenericFallback()
        {
            var service = CreateService();
            var conflictingDefault = CreateProfile("anything", "Conflicting reply.", "Conflicting suggestion.", "Conflicting fallback.");
            SetDefaultProfile(service, conflictingDefault);

            Assert.DoesNotThrow(() => Answer(service, null, "anything"));
            var answer = Answer(service, null, "anything");

            Assert.That(answer.IsMatch, Is.False);
            Assert.That(answer.Reply, Is.EqualTo(ChatAnswer.GenericFallback.Reply));
            Assert.That(answer.SuggestedQuestions, Is.Empty);
        }

        [Test]
        public void Answer_ReturnsSuggestionsSafeFromCallerMutation()
        {
            var service = CreateService();
            var profile = CreateProfile("known", "Known reply.", "Original suggestion.", "Profile fallback.");

            var answer = Answer(service, profile, "known");
            answer.SuggestedQuestions[0] = "Changed by caller.";

            Assert.That(answer.SuggestedQuestions, Is.EqualTo(new[] { "Original suggestion." }));
            Assert.That(profile.Entries[0].SuggestedQuestions, Is.EqualTo(new[] { "Original suggestion." }));
        }

        [Test]
        public void Answer_LegacyWrapperSkipsIncompleteSerializedDefaultProfile()
        {
            var definition = Array.Find(Resources.LoadAll<AnimalDefinition>("Animals"), candidate =>
                candidate != null && candidate.IsConfigured && HasUsableKnowledge(candidate.Knowledge));
            Assert.That(definition, Is.Not.Null, "Current resources need a configured animal knowledge profile.");

            var entry = Array.Find(definition.Knowledge.Entries, candidate =>
                candidate != null && candidate.Keywords.Length > 0 && !string.IsNullOrWhiteSpace(candidate.Keywords[0]));
            Assert.That(entry, Is.Not.Null, "Current resources need an answer entry with a keyword.");

            var service = CreateService();
            SetDefaultProfile(service, CreateProfile("incomplete", string.Empty, "", string.Empty));

            var answer = service.Answer(entry.Keywords[0]);

            Assert.That(answer.IsMatch, Is.True);
            Assert.That(answer.Reply, Is.EqualTo(entry.Reply));
            Assert.That(answer.SuggestedQuestions, Is.EqualTo(entry.SuggestedQuestions));
        }

        [Test]
        public void SelectLegacyProfile_SkipsIncompleteCandidatesAndSortsDefinitionsByAnimalId()
        {
            var incompleteDefault = CreateProfile("incomplete", string.Empty, string.Empty, string.Empty);
            var unconfiguredDefinition = ScriptableObject.CreateInstance<AnimalDefinition>();
            createdObjects.Add(unconfiguredDefinition);
            var incompleteDefinition = CreateDefinition("aardvark", incompleteDefault);
            var laterProfile = CreateProfile("later", "Later reply.", "Later suggestion.", "Later fallback.");
            var laterDefinition = CreateDefinition("zebra", laterProfile);
            var expectedProfile = CreateProfile("expected", "Expected reply.", "Expected suggestion.", "Expected fallback.");
            var expectedDefinition = CreateDefinition("antelope", expectedProfile);

            var selected = LocalKnowledgeChatService.SelectLegacyProfile(
                incompleteDefault,
                new[] { unconfiguredDefinition, incompleteDefinition, laterDefinition, expectedDefinition },
                Array.Empty<AnimalKnowledgeProfile>());

            Assert.That(selected, Is.SameAs(expectedProfile));
        }

        [Test]
        public void SelectLegacyProfile_SkipsIncompleteStandaloneProfilesAndSortsByAssetName()
        {
            var incompleteProfile = CreateProfile("incomplete", string.Empty, string.Empty, string.Empty);
            incompleteProfile.name = "Aardvark";
            var laterProfile = CreateProfile("later", "Later reply.", "Later suggestion.", "Later fallback.");
            laterProfile.name = "Zebra";
            var expectedProfile = CreateProfile("expected", "Expected reply.", "Expected suggestion.", "Expected fallback.");
            expectedProfile.name = "Antelope";

            var selected = LocalKnowledgeChatService.SelectLegacyProfile(
                null,
                Array.Empty<AnimalDefinition>(),
                new[] { incompleteProfile, laterProfile, expectedProfile });

            Assert.That(selected, Is.SameAs(expectedProfile));
        }

        [Test]
        public void SelectLegacyProfile_SkipsReplyOnlyProfileBeforeReachableProfile()
        {
            var unreachableProfile = CreateProfile(" ", "Unreachable reply.", "Unreachable suggestion.", string.Empty);
            var expectedProfile = CreateProfile("reachable", "Reachable reply.", "Reachable suggestion.", string.Empty);

            var selected = LocalKnowledgeChatService.SelectLegacyProfile(
                null,
                Array.Empty<AnimalDefinition>(),
                new[] { unreachableProfile, expectedProfile });

            Assert.That(selected, Is.SameAs(expectedProfile));
        }

        [Test]
        public void SelectLegacyProfile_SkipsDuplicateDefinitionIdsBeforeSelectingNextUniqueId()
        {
            var duplicateFirstProfile = CreateProfile("first", "First reply.", "First suggestion.", "First fallback.");
            var duplicateSecondProfile = CreateProfile("second", "Second reply.", "Second suggestion.", "Second fallback.");
            var expectedProfile = CreateProfile("expected", "Expected reply.", "Expected suggestion.", "Expected fallback.");

            var selected = LocalKnowledgeChatService.SelectLegacyProfile(
                null,
                new[]
                {
                    CreateDefinition("Aardvark", duplicateFirstProfile),
                    CreateDefinition(" aardvark ", duplicateSecondProfile),
                    CreateDefinition("Antelope", expectedProfile)
                },
                Array.Empty<AnimalKnowledgeProfile>());

            Assert.That(selected, Is.SameAs(expectedProfile));
        }

        [Test]
        public void SelectLegacyProfile_SkipsDuplicateStandaloneProfileNamesBeforeSelectingNextUniqueName()
        {
            var duplicateFirstProfile = CreateProfile("first", "First reply.", "First suggestion.", "First fallback.");
            duplicateFirstProfile.name = "Aardvark";
            var duplicateSecondProfile = CreateProfile("second", "Second reply.", "Second suggestion.", "Second fallback.");
            duplicateSecondProfile.name = " aardvark ";
            var expectedProfile = CreateProfile("expected", "Expected reply.", "Expected suggestion.", "Expected fallback.");
            expectedProfile.name = "Antelope";

            var selected = LocalKnowledgeChatService.SelectLegacyProfile(
                null,
                Array.Empty<AnimalDefinition>(),
                new[] { duplicateFirstProfile, duplicateSecondProfile, expectedProfile });

            Assert.That(selected, Is.SameAs(expectedProfile));
        }

        private AnimalKnowledgeProfile CreateProfile(string keyword, string reply, string suggestion, string unknownReply)
        {
            var profile = ScriptableObject.CreateInstance<AnimalKnowledgeProfile>();
            profile.Configure("", "", "", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                new[] { new AnimalKnowledgeEntry("entry", new[] { keyword }, reply, new[] { suggestion }) },
                unknownReply, new[] { suggestion });
            createdObjects.Add(profile);
            return profile;
        }

        private LocalKnowledgeChatService CreateService()
        {
            var gameObject = new GameObject("LocalKnowledgeChatServiceTests");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<LocalKnowledgeChatService>();
        }

        private AnimalDefinition CreateDefinition(string animalId, AnimalKnowledgeProfile profile)
        {
            var definition = ScriptableObject.CreateInstance<AnimalDefinition>();
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            definition.Configure(
                animalId,
                animalId,
                animalId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                string.Empty,
                Color.white,
                null,
                null,
                profile,
                mission);
            createdObjects.Add(definition);
            createdObjects.Add(mission);
            return definition;
        }

        private static ChatAnswer Answer(LocalKnowledgeChatService service, AnimalKnowledgeProfile profile, string message)
        {
            return service.Answer(profile, message);
        }

        private static bool HasUsableKnowledge(AnimalKnowledgeProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(profile.UnknownReply))
            {
                return true;
            }

            return Array.Exists(profile.Entries, entry => entry != null && !string.IsNullOrWhiteSpace(entry.Reply));
        }

        private static void SetDefaultProfile(LocalKnowledgeChatService service, AnimalKnowledgeProfile profile)
        {
            var serializedService = new SerializedObject(service);
            serializedService.FindProperty("defaultProfile").objectReferenceValue = profile;
            serializedService.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
