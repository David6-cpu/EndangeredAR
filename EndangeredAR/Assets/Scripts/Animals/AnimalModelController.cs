using UnityEngine;

namespace EndangeredAR.Animals
{
    public enum TauntRequestResult
    {
        Played,
        Busy,
        Inactive,
        UnsupportedAnimal,
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

        private bool requestPending;
        private bool observedTaunt;
        private float requestStartedAt;

        public string SupportedAnimalId => supportedAnimalId;
        public bool IsBusy => requestPending;

        public bool IsAnimatorOwnedBy(Transform modelRoot)
        {
            return modelRoot != null &&
                   animator != null &&
                   animator.gameObject.activeInHierarchy &&
                   animator.transform.IsChildOf(modelRoot);
        }

        public bool CanRequestTaunt
        {
            get
            {
                if (!gameObject.activeInHierarchy ||
                    !string.Equals(supportedAnimalId, "sensen", System.StringComparison.OrdinalIgnoreCase) ||
                    requestPending ||
                    !CanInspectAnimator() ||
                    !HasTauntTrigger() ||
                    animator.IsInTransition(0))
                {
                    return false;
                }

                return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == IdleStateHash;
            }
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
                    return requestPending ? "Transition (Busy)" : "Transition";
                }

                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == TauntStateHash)
                {
                    return "Taunt";
                }

                if (state.fullPathHash == IdleStateHash)
                {
                    return requestPending ? "Idle (Pending)" : "Idle";
                }

                return "Unknown";
            }
        }

        public TauntRequestResult TryPlayTaunt()
        {
            if (!gameObject.activeInHierarchy)
            {
                return TauntRequestResult.Inactive;
            }

            if (!string.Equals(supportedAnimalId, "sensen", System.StringComparison.OrdinalIgnoreCase))
            {
                return TauntRequestResult.UnsupportedAnimal;
            }

            if (animator == null)
            {
                return TauntRequestResult.MissingAnimator;
            }

            if (!CanInspectAnimator() || !HasTauntTrigger())
            {
                return TauntRequestResult.InvalidControllerState;
            }

            if (requestPending || animator.IsInTransition(0))
            {
                return TauntRequestResult.Busy;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == TauntStateHash)
            {
                return TauntRequestResult.Busy;
            }

            if (state.fullPathHash != IdleStateHash)
            {
                return TauntRequestResult.InvalidControllerState;
            }

            animator.ResetTrigger(TauntTriggerHash);
            animator.SetTrigger(TauntTriggerHash);
            requestPending = true;
            observedTaunt = false;
            requestStartedAt = Time.unscaledTime;
            return TauntRequestResult.Played;
        }

        private void Update()
        {
            if (!requestPending)
            {
                return;
            }

            if (!CanInspectAnimator())
            {
                ClearPendingRequest();
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == TauntStateHash)
            {
                observedTaunt = true;
            }

            if (observedTaunt && !animator.IsInTransition(0) && state.fullPathHash == IdleStateHash)
            {
                ClearPendingRequest();
                return;
            }

            var elapsed = Time.unscaledTime - requestStartedAt;
            if ((!observedTaunt && elapsed >= EnterTauntTimeoutSeconds) || elapsed >= TauntSafetyTimeoutSeconds)
            {
                animator.ResetTrigger(TauntTriggerHash);
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

        private bool HasTauntTrigger()
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == TauntTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearPendingRequest()
        {
            requestPending = false;
            observedTaunt = false;
            requestStartedAt = 0f;
        }
    }
}
