using System;

namespace EndangeredAR.AI.Prompt
{
    public sealed class OnDevicePromptBudget
    {
        public OnDevicePromptBudget(
            int contextTokens,
            int reservedGenerationTokens,
            int safetyMarginTokens)
        {
            if (contextTokens < 256 || reservedGenerationTokens < 1 || safetyMarginTokens < 1 ||
                reservedGenerationTokens + safetyMarginTokens >= contextTokens)
            {
                throw new ArgumentOutOfRangeException(nameof(contextTokens));
            }

            ContextTokens = contextTokens;
            ReservedGenerationTokens = reservedGenerationTokens;
            SafetyMarginTokens = safetyMarginTokens;
        }

        public int ContextTokens { get; }
        public int ReservedGenerationTokens { get; }
        public int SafetyMarginTokens { get; }
        public int MaximumPromptTokens => ContextTokens - ReservedGenerationTokens - SafetyMarginTokens;

        public static OnDevicePromptBudget FirstProduction => new OnDevicePromptBudget(2048, 128, 64);
    }

    public sealed class OnDevicePromptBudgetExceededException : Exception
    {
        public OnDevicePromptBudgetExceededException()
            : base("The minimum trusted prompt exceeds the on-device context budget.")
        {
        }
    }
}
