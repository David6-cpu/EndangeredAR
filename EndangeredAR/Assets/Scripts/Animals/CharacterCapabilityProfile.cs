using System;
using System.Collections.Generic;
using EndangeredAR.AI;
using UnityEngine;

namespace EndangeredAR.Animals
{
    [CreateAssetMenu(menuName = "Endangered AR/Character Capability Profile")]
    public sealed class CharacterCapabilityProfile : ScriptableObject
    {
        [SerializeField] private AIAction[] supportedActions = Array.Empty<AIAction>();

        public AIAction[] SupportedActions
        {
            get
            {
                var unique = new List<AIAction>();
                foreach (var action in supportedActions ?? Array.Empty<AIAction>())
                {
                    if (!IsSupportedValue(action) || unique.Contains(action))
                    {
                        continue;
                    }

                    unique.Add(action);
                }

                return unique.ToArray();
            }
        }

        public bool Supports(AIAction action)
        {
            if (!IsSupportedValue(action))
            {
                return false;
            }

            foreach (var candidate in supportedActions ?? Array.Empty<AIAction>())
            {
                if (candidate == action)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSupportedValue(AIAction action)
        {
            return action != AIAction.None && Enum.IsDefined(typeof(AIAction), action);
        }
    }
}
