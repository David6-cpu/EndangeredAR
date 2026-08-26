using System;

namespace EndangeredAR.AI
{
    public enum MemoryUseMode
    {
        None,
        Reunion
    }

    public static class MemoryUseModeProtocol
    {
        public static bool TryParseExact(string wireValue, out MemoryUseMode mode)
        {
            switch (wireValue)
            {
                case "none":
                    mode = MemoryUseMode.None;
                    return true;
                case "reunion":
                    mode = MemoryUseMode.Reunion;
                    return true;
                default:
                    mode = default;
                    return false;
            }
        }

        public static string ToWireValue(MemoryUseMode mode)
        {
            switch (mode)
            {
                case MemoryUseMode.None:
                    return "none";
                case MemoryUseMode.Reunion:
                    return "reunion";
                default:
                    return "none";
            }
        }
    }

    internal static class CharacterMemoryTransport
    {
        public static ReadOnlyCharacterMemoryContext SelectContext(
            string animalId,
            ReadOnlyCharacterMemoryContext context,
            MemoryUseMode useMode)
        {
            return useMode == MemoryUseMode.Reunion &&
                context != null &&
                string.Equals(context.AnimalId, animalId, StringComparison.Ordinal)
                    ? context
                    : null;
        }

        public static string SanitizeExternalAnswerMode(string answerMode)
        {
            return string.Equals(
                answerMode,
                CharacterMemoryAnswerBuilder.MemoryRecallAnswerMode,
                StringComparison.Ordinal)
                    ? "social_chat"
                    : answerMode;
        }
    }
}
