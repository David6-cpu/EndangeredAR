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
        private static readonly int EatTriggerHash = Animator.StringToHash("Eat");
        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int TauntStateHash = Animator.StringToHash("Base Layer.Taunt");
        private static readonly int EatStateHash = Animator.StringToHash("Base Layer.Eat");

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

                if (CanInspectAnimator())
                {
                    var stateHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                    if (stateHash == TauntStateHash)
                    {
                        return AIAction.Taunt;
                    }

                    if (stateHash == EatStateHash)
                    {
                        return AIAction.Eat;
                    }
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
            return TryGetActionSpec(action, out var spec) &&
                   string.Equals(supportedAnimalId, "sensen", System.StringComparison.OrdinalIgnoreCase) &&
                   animator != null &&
                   animator.runtimeAnimatorController != null &&
                   animator.layerCount > 0 &&
                   HasTrigger(spec.TriggerHash) &&
                   animator.HasState(0, spec.StateHash) &&
                   HasExpectedClip(spec.ClipName);
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

                if (state.fullPathHash == EatStateHash)
                {
                    return "Eat";
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
            if (!TryGetActionSpec(action, out var spec))
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

            if (!CanInspectAnimator() || !SupportsAction(action))
            {
                return ActionRequestResult.InvalidControllerState;
            }

            if (IsBusy || animator.IsInTransition(0))
            {
                return ActionRequestResult.Busy;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == spec.StateHash)
            {
                return ActionRequestResult.Busy;
            }

            if (state.fullPathHash != IdleStateHash)
            {
                return ActionRequestResult.InvalidControllerState;
            }

            animator.ResetTrigger(spec.TriggerHash);
            animator.SetTrigger(spec.TriggerHash);
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

            if (!TryGetActionSpec(pendingAction, out var spec))
            {
                ClearPendingRequest();
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == spec.StateHash)
            {
                observedAction = true;
            }

            if (observedAction && !animator.IsInTransition(0) && state.fullPathHash == IdleStateHash)
            {
                ClearPendingRequest();
                return;
            }

            var elapsed = Time.unscaledTime - requestStartedAt;
            if ((!observedAction && elapsed >= spec.EnterTimeoutSeconds) || elapsed >= spec.SafetyTimeoutSeconds)
            {
                animator.ResetTrigger(spec.TriggerHash);
                ClearPendingRequest();
            }
        }

        private void OnDisable()
        {
            if (animator != null)
            {
                animator.ResetTrigger(TauntTriggerHash);
                animator.ResetTrigger(EatTriggerHash);
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

        private bool HasExpectedClip(string clipName)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && string.Equals(clip.name, clipName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetActionSpec(AIAction action, out ActionSpec spec)
        {
            switch (action)
            {
                case AIAction.Taunt:
                    spec = new ActionSpec(TauntTriggerHash, TauntStateHash, "Taunt", 1f, 6f);
                    return true;
                case AIAction.Eat:
                    spec = new ActionSpec(EatTriggerHash, EatStateHash, "Sensen_Eat", 1f, 7f);
                    return true;
                default:
                    spec = default;
                    return false;
            }
        }

        private readonly struct ActionSpec
        {
            public ActionSpec(
                int triggerHash,
                int stateHash,
                string clipName,
                float enterTimeoutSeconds,
                float safetyTimeoutSeconds)
            {
                TriggerHash = triggerHash;
                StateHash = stateHash;
                ClipName = clipName;
                EnterTimeoutSeconds = enterTimeoutSeconds;
                SafetyTimeoutSeconds = safetyTimeoutSeconds;
            }

            public int TriggerHash { get; }
            public int StateHash { get; }
            public string ClipName { get; }
            public float EnterTimeoutSeconds { get; }
            public float SafetyTimeoutSeconds { get; }
        }

        private void ClearPendingRequest()
        {
            pendingAction = AIAction.None;
            observedAction = false;
            requestStartedAt = 0f;
        }
    }
}
