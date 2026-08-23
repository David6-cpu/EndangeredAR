using System;
using EndangeredAR.Animals;
using EndangeredAR.Models;
using EndangeredAR.UI;

namespace EndangeredAR.AI
{
    internal enum AIInteractionValidationResult
    {
        Allowed,
        NoAction,
        UnsupportedAction,
        InvalidIntent,
        MissingCapabilityProfile,
        CapabilityDenied,
        ControllerUnsupported,
        StaleRequest,
        WrongAnimal,
        UnsupportedAnimal,
        InactivePage,
        NoActiveModel,
        Busy
    }

    internal static class AIInteractionValidator
    {
        public static AIInteractionValidationResult Validate(
            AIAction action,
            string originalUserMessage,
            string responseAnimalId,
            string currentAnimalId,
            ChatRequestState requestState,
            ChatRequestTicket requestTicket,
            bool isInteractionPageActive,
            AnimalModelLoader modelLoader,
            out AnimalModelController controller)
        {
            controller = null;
            if (action == AIAction.None)
            {
                return AIInteractionValidationResult.NoAction;
            }

            if (!AIActionProtocol.IsExecutable(action))
            {
                return AIInteractionValidationResult.UnsupportedAction;
            }

            if (AIActionIntent.Resolve(originalUserMessage) != action)
            {
                return AIInteractionValidationResult.InvalidIntent;
            }

            if (requestState == null || !requestState.CanComplete(requestTicket, currentAnimalId))
            {
                return AIInteractionValidationResult.StaleRequest;
            }

            if (string.IsNullOrWhiteSpace(responseAnimalId) ||
                !string.Equals(responseAnimalId, currentAnimalId, StringComparison.OrdinalIgnoreCase))
            {
                return AIInteractionValidationResult.WrongAnimal;
            }

            if (!isInteractionPageActive)
            {
                return AIInteractionValidationResult.InactivePage;
            }

            if (modelLoader == null || !modelLoader.gameObject.activeInHierarchy)
            {
                return AIInteractionValidationResult.NoActiveModel;
            }

            if (!string.Equals(modelLoader.LoadedAnimalId, currentAnimalId, StringComparison.OrdinalIgnoreCase))
            {
                return AIInteractionValidationResult.UnsupportedAnimal;
            }

            if (!modelLoader.TryGetCurrentModelController(out controller))
            {
                controller = null;
                return AIInteractionValidationResult.NoActiveModel;
            }

            if (!string.Equals(controller.SupportedAnimalId, currentAnimalId, StringComparison.OrdinalIgnoreCase))
            {
                controller = null;
                return AIInteractionValidationResult.UnsupportedAnimal;
            }

            if (modelLoader.LoadedCapabilities == null)
            {
                controller = null;
                return AIInteractionValidationResult.MissingCapabilityProfile;
            }

            if (!modelLoader.LoadedCapabilities.Supports(action))
            {
                controller = null;
                return AIInteractionValidationResult.CapabilityDenied;
            }

            if (!controller.SupportsAction(action))
            {
                controller = null;
                return AIInteractionValidationResult.ControllerUnsupported;
            }

            if (controller.IsBusy)
            {
                controller = null;
                return AIInteractionValidationResult.Busy;
            }

            return AIInteractionValidationResult.Allowed;
        }
    }
}
