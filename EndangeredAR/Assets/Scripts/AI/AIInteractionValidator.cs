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

            if (action != AIAction.Taunt)
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

            if (!string.Equals(currentAnimalId, "sensen", StringComparison.OrdinalIgnoreCase))
            {
                return AIInteractionValidationResult.UnsupportedAnimal;
            }

            if (!isInteractionPageActive)
            {
                return AIInteractionValidationResult.InactivePage;
            }

            if (modelLoader == null ||
                !modelLoader.gameObject.activeInHierarchy ||
                !string.Equals(modelLoader.LoadedAnimalId, currentAnimalId, StringComparison.OrdinalIgnoreCase) ||
                !modelLoader.TryGetCurrentModelController(out controller))
            {
                controller = null;
                return AIInteractionValidationResult.NoActiveModel;
            }

            if (!string.Equals(controller.SupportedAnimalId, currentAnimalId, StringComparison.OrdinalIgnoreCase))
            {
                controller = null;
                return AIInteractionValidationResult.UnsupportedAnimal;
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
