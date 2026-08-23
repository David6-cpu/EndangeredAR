using System;
using System.Collections.Generic;
using System.Text;

namespace EndangeredAR.AI
{
    public enum AIAction
    {
        None,
        Taunt,
        Eat
    }

    internal static class AIActionProtocol
    {
        public static bool IsExecutable(AIAction action)
        {
            return action == AIAction.Taunt || action == AIAction.Eat;
        }

        public static AIAction Parse(string rawValue)
        {
            return string.Equals(rawValue, "taunt", StringComparison.Ordinal)
                ? AIAction.Taunt
                : AIAction.None;
        }
    }

    internal static class AIActionIntent
    {
        private static readonly HashSet<string> TauntIntents = new HashSet<string>(StringComparer.Ordinal)
        {
            "森森给我表演一下",
            "给我表演一下",
            "给我表演一个",
            "做个动作",
            "来一个动作",
            "给我看看taunt",
            "森森逗我一下",
            "逗我一下",
            "showmeataunt",
            "performataunt"
        };

        public static AIAction Resolve(string message)
        {
            return TauntIntents.Contains(Normalize(message))
                ? AIAction.Taunt
                : AIAction.None;
        }

        private static string Normalize(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            var normalized = new StringBuilder(message.Length);
            foreach (var character in message)
            {
                if (char.IsLetterOrDigit(character))
                {
                    normalized.Append(char.ToLowerInvariant(character));
                }
            }

            return normalized.ToString();
        }
    }
}
