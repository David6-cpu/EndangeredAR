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

        public bool TryExecute(out ActionRequestResult result)
        {
            result = ActionRequestResult.InvalidControllerState;
            if (consumed || !AIActionProtocol.IsExecutable(Action) || controller == null)
            {
                return false;
            }

            consumed = true;
            var currentController = controller;
            controller = null;
            result = currentController.TryPlayAction(Action);
            return true;
        }
    }
}
