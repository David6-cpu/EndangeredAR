using System;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.Models;
using EndangeredAR.Progress;
using EndangeredAR.UI;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AIActionCompletionTests
    {
        private GameObject loaderObject;
        private GameObject modelRoot;

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(loaderObject);
        }

        [Test]
        public void Completion_ProducesOneShotActionOnlyAfterTicketIsConsumed()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoader(out var controller);
            var response = TauntResponse();

            Assert.That(Resolve(state, ticket, true, loader, response, out var reply, out var action, out var validation), Is.True);
            Assert.That(reply, Is.EqualTo(response.reply));
            Assert.That(validation, Is.EqualTo(AIInteractionValidationResult.Allowed));
            Assert.That(action, Is.Not.Null);
            Assert.That(action.Action, Is.EqualTo(AIAction.Taunt));
            Assert.That(state.CanComplete(ticket, "sensen"), Is.False, "The reply ticket must be consumed before action execution is exposed.");
            Assert.That(controller.IsBusy, Is.False, "Resolving a completion must not execute the animation.");

            Assert.That(action.TryExecute(out _), Is.True);
            Assert.That(action.TryExecute(out _), Is.False, "A validated action must never execute twice.");
        }

        [Test]
        public void Completion_StaleTicketCannotExposeActionOrReply()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoader(out _);
            state.Invalidate();

            Assert.That(Resolve(state, ticket, true, loader, TauntResponse(), out var reply, out var action, out var validation), Is.False);
            Assert.That(reply, Is.Null);
            Assert.That(action, Is.Null);
            Assert.That(validation, Is.EqualTo(AIInteractionValidationResult.StaleRequest));
        }

        [Test]
        public void Completion_InactivePageStillAcceptsReplyButNeverExposesAction()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoader(out _);

            Assert.That(Resolve(state, ticket, false, loader, TauntResponse(), out var reply, out var action, out var validation), Is.True);
            Assert.That(reply, Is.Not.Empty);
            Assert.That(action, Is.Null);
            Assert.That(validation, Is.EqualTo(AIInteractionValidationResult.InactivePage));
        }

        [Test]
        public void Completion_ResponseAnimalMismatchAcceptsReplyButNeverExposesAction()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoader(out _);
            var response = TauntResponse();
            response.animalId = "other-animal";

            Assert.That(Resolve(state, ticket, true, loader, response, out _, out var action, out var validation), Is.True);
            Assert.That(action, Is.Null);
            Assert.That(validation, Is.EqualTo(AIInteractionValidationResult.WrongAnimal));
        }

        [Test]
        public void Completion_GroundedFactNeverExposesAction()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoader(out _);
            var response = new AIResponse
            {
                animalId = "sensen",
                reply = "我的学名是 Semnopithecus priam。",
                answerMode = "grounded_fact",
                ActionSuggestion = AIAction.None
            };

            Assert.That(Resolve(state, ticket, true, loader, response, out _, out var action, out var validation), Is.True);
            Assert.That(action, Is.Null);
            Assert.That(validation, Is.EqualTo(AIInteractionValidationResult.NoAction));
        }

        [Test]
        public void Completion_BusyControllerAcceptsReplyWithoutQueuingAction()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoader(out var controller);
            SetPrivateField(controller, "requestPending", true);

            Assert.That(Resolve(state, ticket, true, loader, TauntResponse(), out _, out var action, out var validation), Is.True);
            Assert.That(action, Is.Null);
            Assert.That(validation, Is.EqualTo(AIInteractionValidationResult.Busy));
        }

        [Test]
        public void ConversationRecordSchema_RemainsTextOnly()
        {
            var fields = typeof(ConversationRecord).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(fields, Is.EqualTo(new[] { "content", "role" }));
        }

        private static bool Resolve(
            ChatRequestState state,
            ChatRequestTicket ticket,
            bool isInteractionPageActive,
            AnimalModelLoader loader,
            AIResponse response,
            out string reply,
            out ValidatedAIAction action,
            out AIInteractionValidationResult validation)
        {
            return DemoAppController.TryResolveAICompletionWithAction(
                state,
                ticket,
                "sensen",
                isInteractionPageActive,
                loader,
                response,
                "森森，给我表演一下",
                new Func<string, string>(_ => "安全的本地知识回答"),
                out reply,
                out action,
                out validation);
        }

        private AnimalModelLoader CreateLoader(out AnimalModelController controller)
        {
            loaderObject = new GameObject("Loader");
            var loader = loaderObject.AddComponent<AnimalModelLoader>();
            SetPrivateField(loader, "loadedAnimalId", "sensen");

            modelRoot = new GameObject("Animal GLB Runtime Root");
            modelRoot.transform.SetParent(loader.transform, false);
            var model = new GameObject("Rigged Sensen");
            model.transform.SetParent(modelRoot.transform, false);
            var animator = model.AddComponent<Animator>();
            controller = model.AddComponent<AnimalModelController>();
            SetPrivateField(controller, "animator", animator);
            return loader;
        }

        private static AIResponse TauntResponse()
        {
            return new AIResponse
            {
                animalId = "sensen",
                reply = "好呀，看我的！",
                answerMode = "social_chat",
                ActionSuggestion = AIAction.Taunt
            };
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
