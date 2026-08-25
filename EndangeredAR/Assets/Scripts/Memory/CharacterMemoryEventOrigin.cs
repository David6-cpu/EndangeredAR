namespace EndangeredAR.Memory
{
    public enum CharacterMemoryEventOrigin
    {
        Business,
        Bootstrap,
        Reconcile
    }

    public static class CharacterMemoryEventOriginProtocol
    {
        public static bool TryParseExact(string wireValue, out CharacterMemoryEventOrigin origin)
        {
            switch (wireValue)
            {
                case "business":
                    origin = CharacterMemoryEventOrigin.Business;
                    return true;
                case "bootstrap":
                    origin = CharacterMemoryEventOrigin.Bootstrap;
                    return true;
                case "reconcile":
                    origin = CharacterMemoryEventOrigin.Reconcile;
                    return true;
                default:
                    origin = default;
                    return false;
            }
        }

        public static string ToWireValue(CharacterMemoryEventOrigin origin)
        {
            switch (origin)
            {
                case CharacterMemoryEventOrigin.Business:
                    return "business";
                case CharacterMemoryEventOrigin.Bootstrap:
                    return "bootstrap";
                case CharacterMemoryEventOrigin.Reconcile:
                    return "reconcile";
                default:
                    return string.Empty;
            }
        }
    }
}
