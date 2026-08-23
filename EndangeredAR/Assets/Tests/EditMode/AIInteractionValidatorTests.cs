using System.Collections.Generic;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.Models;
using EndangeredAR.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AIInteractionValidatorTests
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
        public void Validate_AllowsCurrentExplicitTauntWithoutConsumingTicketOrExecuting()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out var expectedController);

            var result = AIInteractionValidator.Validate(
                AIAction.Taunt,
                "森森，给我表演一下",
                "sensen",
                "sensen",
                state,
                ticket,
                true,
                loader,
                out var controller);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.Allowed));
            Assert.That(controller, Is.SameAs(expectedController));
            Assert.That(state.CanComplete(ticket, "sensen"), Is.True, "Validation must not consume completion.");
            Assert.That(expectedController.IsBusy, Is.False, "Validation must not execute the action.");
        }

        [Test]
        public void Validate_NoneAndUnknownActionsFailClosed()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out _);

            Assert.That(Validate(AIAction.None, "做个动作", state, ticket, loader),
                Is.EqualTo(AIInteractionValidationResult.NoAction));
            Assert.That(Validate((AIAction)999, "做个动作", state, ticket, loader),
                Is.EqualTo(AIInteractionValidationResult.UnsupportedAction));
        }

        [Test]
        public void Validate_RejectsMissingCapabilityProfile()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out _);
            SetPrivateField(loader, "loadedCapabilities", null);

            var result = Validate(AIAction.Taunt, "做个动作", state, ticket, loader);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.MissingCapabilityProfile));
        }

        [Test]
        public void Validate_RejectsActionDeniedByProfileEvenWhenControllerSupportsIt()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out var controller);
            SetPrivateField(loader, "loadedCapabilities", CreateProfile());

            Assert.That(controller.SupportsAction(AIAction.Taunt), Is.True);
            var result = Validate(AIAction.Taunt, "做个动作", state, ticket, loader);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.CapabilityDenied));
        }

        [Test]
        public void Validate_RejectsControllerWithoutActionSupportEvenWhenProfileAllowsIt()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out var controller);
            var animator = controller.GetComponent<Animator>();
            animator.runtimeAnimatorController = new AnimatorController();
            createdObjects.Add(animator.runtimeAnimatorController);

            Assert.That(loader.LoadedCapabilities.Supports(AIAction.Taunt), Is.True);
            Assert.That(controller.SupportsAction(AIAction.Taunt), Is.False);
            var result = Validate(AIAction.Taunt, "做个动作", state, ticket, loader);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.ControllerUnsupported));
        }

        [Test]
        public void Validate_RejectsStaleRequestWithoutConsumingAnythingElse()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            state.Invalidate();
            var loader = CreateLoaderWithController(out _);

            var result = Validate(AIAction.Taunt, "做个动作", state, ticket, loader);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.StaleRequest));
        }

        [Test]
        public void Validate_RejectsResponseAnimalMismatch()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out _);

            var result = AIInteractionValidator.Validate(
                AIAction.Taunt,
                "做个动作",
                "other-animal",
                "sensen",
                state,
                ticket,
                true,
                loader,
                out _);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.WrongAnimal));
        }

        [Test]
        public void Validate_RejectsUnsupportedCurrentAnimal()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("red-panda");
            var loader = CreateLoaderWithController(out _);

            var result = AIInteractionValidator.Validate(
                AIAction.Taunt,
                "做个动作",
                "red-panda",
                "red-panda",
                state,
                ticket,
                true,
                loader,
                out _);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.UnsupportedAnimal));
        }

        [Test]
        public void Validate_RejectsInactiveInteractionPage()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out _);

            var result = AIInteractionValidator.Validate(
                AIAction.Taunt,
                "做个动作",
                "sensen",
                "sensen",
                state,
                ticket,
                false,
                loader,
                out _);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.InactivePage));
        }

        [Test]
        public void Validate_RejectsLegacyFallbackWithoutController()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loaderObject = new GameObject("Legacy Loader");
            createdObjects.Add(loaderObject);
            var loader = loaderObject.AddComponent<AnimalModelLoader>();
            SetPrivateField(loader, "loadedAnimalId", "sensen");
            var root = new GameObject("Animal GLB Runtime Root");
            createdObjects.Add(root);
            root.transform.SetParent(loader.transform, false);

            var result = Validate(AIAction.Taunt, "做个动作", state, ticket, loader);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.NoActiveModel));
        }

        [Test]
        public void Validate_RejectsBusyControllerWithoutQueuingAction()
        {
            var state = new ChatRequestState();
            var ticket = state.Begin("sensen");
            var loader = CreateLoaderWithController(out var controller);
            SetPrivateField(controller, "pendingAction", AIAction.Taunt);

            var result = Validate(AIAction.Taunt, "做个动作", state, ticket, loader);

            Assert.That(result, Is.EqualTo(AIInteractionValidationResult.Busy));
            Assert.That(controller.IsBusy, Is.True);
        }

        private AIInteractionValidationResult Validate(
            AIAction action,
            string message,
            ChatRequestState state,
            ChatRequestTicket ticket,
            AnimalModelLoader loader)
        {
            return AIInteractionValidator.Validate(
                action,
                message,
                "sensen",
                "sensen",
                state,
                ticket,
                true,
                loader,
                out _);
        }

        private AnimalModelLoader CreateLoaderWithController(out AnimalModelController controller)
        {
            var loaderObject = new GameObject("Loader");
            createdObjects.Add(loaderObject);
            var loader = loaderObject.AddComponent<AnimalModelLoader>();
            SetPrivateField(loader, "loadedAnimalId", "sensen");
            var root = new GameObject("Animal GLB Runtime Root");
            createdObjects.Add(root);
            root.transform.SetParent(loader.transform, false);

            var model = new GameObject("Rigged Sensen");
            createdObjects.Add(model);
            model.transform.SetParent(root.transform, false);
            var animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Sensen/SensenRigged.controller");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            controller = model.AddComponent<AnimalModelController>();
            SetPrivateField(controller, "animator", animator);
            SetPrivateField(loader, "loadedCapabilities", CreateProfile(AIAction.Taunt));
            return loader;
        }

        private CharacterCapabilityProfile CreateProfile(params AIAction[] actions)
        {
            var profile = ScriptableObject.CreateInstance<CharacterCapabilityProfile>();
            createdObjects.Add(profile);
            SetPrivateField(profile, "supportedActions", actions);
            return profile;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
