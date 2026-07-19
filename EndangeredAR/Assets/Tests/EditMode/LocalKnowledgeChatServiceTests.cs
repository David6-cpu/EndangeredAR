using System;
using System.Collections.Generic;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using NUnit.Framework;
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

            Assert.DoesNotThrow(() => Answer(service, null, "anything"));
            var answer = Answer(service, null, "anything");

            Assert.That(answer.IsMatch, Is.False);
            Assert.That(answer.Reply, Is.Not.Empty);
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
        public void Answer_LegacyWrapperReturnsAssetBackedAnswer()
        {
            var definition = Array.Find(Resources.LoadAll<AnimalDefinition>("Animals"), candidate =>
                candidate != null && candidate.Knowledge != null && candidate.Knowledge.Entries.Length > 0);
            Assert.That(definition, Is.Not.Null, "Current resources need a configured animal knowledge profile.");

            var entry = Array.Find(definition.Knowledge.Entries, candidate =>
                candidate != null && candidate.Keywords.Length > 0 && !string.IsNullOrWhiteSpace(candidate.Keywords[0]));
            Assert.That(entry, Is.Not.Null, "Current resources need an answer entry with a keyword.");

            var answer = CreateService().Answer(entry.Keywords[0]);

            Assert.That(answer.IsMatch, Is.True);
            Assert.That(answer.Reply, Is.EqualTo(entry.Reply));
            Assert.That(answer.SuggestedQuestions, Is.EqualTo(entry.SuggestedQuestions));
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

        private static ChatAnswer Answer(LocalKnowledgeChatService service, AnimalKnowledgeProfile profile, string message)
        {
            return service.Answer(profile, message);
        }
    }
}
