using EndangeredAR.AI;
using UnityEngine;

namespace EndangeredAR.Animals
{
    public enum ActionRequestResult
    {
        Played,
        Busy,
        Inactive,
        UnsupportedAnimal,
        UnsupportedAction,
        MissingAnimator,
        InvalidControllerState
    }

    public class AnimalModelController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string supportedAnimalId = "sensen";

        private static readonly int TauntTriggerHash = Animator.StringToHash("Taunt");
        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int TauntStateHash = Animator.StringToHash("Base Layer.Taunt");
        private const float EnterTauntTimeoutSeconds = 1f;
        private const float TauntSafetyTimeoutSeconds = 6f;

        private AIAction pendingAction;
        private bool observedAction;
        private float requestStartedAt;

        public string SupportedAnimalId => supportedAnimalId;
        public bool IsBusy => pendingAction != AIAction.None;
        public AIAction CurrentAction
        {
            get
            {
                if (IsBusy)
                {
                    return pendingAction;
                }

                if (CanInspectAnimator() &&
                    animator.GetCurrentAnimatorStateInfo(0).fullPathHash == TauntStateHash)
                {
                    return AIAction.Taunt;
                }

                return AIAction.None;
            }
        }

        public bool IsAnimatorOwnedBy(Transform modelRoot)
        {
            return modelRoot != null &&
                   animator != null &&
                   animator.gameObject.activeInHierarchy &&
                   animator.transform.IsChildOf(modelRoot);
        }

        public bool CanRequestTaunt => CanRequestAction(AIAction.Taunt);

        public bool SupportsAction(AIAction action)
        {
            return TryGetActionSpec(action, out var triggerHash, out _) &&
                   string.Equals(supportedAnimalId, "sensen", System.StringComparison.OrdinalIgnoreCase) &&
                   animator != null &&
                   animator.runtimeAnimatorController != null &&
                   HasTrigger(triggerHash);
        }

        public bool CanRequestAction(AIAction action)
        {
            if (!gameObject.activeInHierarchy ||
                !SupportsAction(action) ||
                IsBusy ||
                !CanInspectAnimator() ||
                animator.IsInTransition(0))
            {
                return false;
            }

            return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == IdleStateHash;
        }

        public string CurrentStateLabel
        {
            get
            {
                if (!CanInspectAnimator())
                {
                    return "Unavailable";
                }

                if (animator.IsInTransition(0))
                {
                    return IsBusy ? "Transition (Busy)" : "Transition";
                }

                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == TauntStateHash)
                {
                    return "Taunt";
                }

                if (state.fullPathHash == IdleStateHash)
                {
                    return IsBusy ? "Idle (Pending)" : "Idle";
                }

                return "Unknown";
            }
        }

        public ActionRequestResult TryPlayTaunt()
        {
            return TryPlayAction(AIAction.Taunt);
        }

        public ActionRequestResult TryPlayAction(AIAction action)
        {
            if (!TryGetActionSpec(action, out var triggerHash, out var stateHash))
            {
                return ActionRequestResult.UnsupportedAction;
            }

            if (!gameObject.activeInHierarchy)
            {
                return ActionRequestResult.Inactive;
            }

            if (!string.Equals(supportedAnimalId, "sensen", System.StringComparison.OrdinalIgnoreCase))
            {
                return ActionRequestResult.UnsupportedAnimal;
            }

            if (animator == null)
            {
                return ActionRequestResult.MissingAnimator;
            }

            if (!CanInspectAnimator() || !HasTrigger(triggerHash))
            {
                return ActionRequestResult.InvalidControllerState;
            }

            if (IsBusy || animator.IsInTransition(0))
            {
                return ActionRequestResult.Busy;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == stateHash)
            {
                return ActionRequestResult.Busy;
            }

            if (state.fullPathHash != IdleStateHash)
            {
                return ActionRequestResult.InvalidControllerState;
            }

            animator.ResetTrigger(triggerHash);
            animator.SetTrigger(triggerHash);
            pendingAction = action;
            observedAction = false;
            requestStartedAt = Time.unscaledTime;
            return ActionRequestResult.Played;
        }

        private void Update()
        {
            if (!IsBusy)
            {
                return;
            }

            if (!CanInspectAnimator())
            {
                ClearPendingRequest();
                return;
            }

            if (!TryGetActionSpec(pendingAction, out var triggerHash, out var stateHash))
            {
                ClearPendingRequest();
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == stateHash)
            {
                observedAction = true;
            }

            if (observedAction && !animator.IsInTransition(0) && state.fullPathHash == IdleStateHash)
            {
                ClearPendingRequest();
                return;
            }

            var elapsed = Time.unscaledTime - requestStartedAt;
            if ((!observedAction && elapsed >= EnterTauntTimeoutSeconds) || elapsed >= TauntSafetyTimeoutSeconds)
            {
                animator.ResetTrigger(triggerHash);
                ClearPendingRequest();
            }
        }

        private void OnDisable()
        {
            if (animator != null)
            {
                animator.ResetTrigger(TauntTriggerHash);
            }

            ClearPendingRequest();
        }

        private bool CanInspectAnimator()
        {
            return animator != null &&
                   animator.enabled &&
                   animator.runtimeAnimatorController != null &&
                   animator.layerCount > 0 &&
                   !animator.applyRootMotion;
        }

        private bool HasTrigger(int triggerHash)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == triggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetActionSpec(AIAction action, out int triggerHash, out int stateHash)
        {
            switch (action)
            {
                case AIAction.Taunt:
                    triggerHash = TauntTriggerHash;
                    stateHash = TauntStateHash;
                    return true;
                default:
                    triggerHash = 0;
                    stateHash = 0;
                    return false;
            }
        }

        private void ClearPendingRequest()
        {
            pendingAction = AIAction.None;
            observedAction = false;
            requestStartedAt = 0f;
        }
    }
}
