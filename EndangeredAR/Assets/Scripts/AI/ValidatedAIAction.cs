using EndangeredAR.Animals;

namespace EndangeredAR.AI
{
    internal sealed class ValidatedAIAction
    {
        private AnimalModelController controller;
        private bool consumed;

        internal ValidatedAIAction(AIAction action, AnimalModelController controller)
        {
            Action = action;
            this.controller = controller;
        }

        public AIAction Action { get; }

        public bool TryExecute(out TauntRequestResult result)
        {
            result = TauntRequestResult.InvalidControllerState;
            if (consumed || Action != AIAction.Taunt || controller == null)
            {
                return false;
            }

            consumed = true;
            var currentController = controller;
            controller = null;
            result = currentController.TryPlayTaunt();
            return true;
        }
    }
}
