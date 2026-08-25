namespace EndangeredAR.Memory
{
    public enum CharacterMemoryEventType
    {
        AnimalDiscovered,
        MissionCompleted,
        KnowledgeLearned,
        BadgeEarned
    }

    public static class CharacterMemoryEventTypeProtocol
    {
        public static bool TryParseExact(string wireValue, out CharacterMemoryEventType eventType)
        {
            switch (wireValue)
            {
                case "animal_discovered":
                    eventType = CharacterMemoryEventType.AnimalDiscovered;
                    return true;
                case "mission_completed":
                    eventType = CharacterMemoryEventType.MissionCompleted;
                    return true;
                case "knowledge_learned":
                    eventType = CharacterMemoryEventType.KnowledgeLearned;
                    return true;
                case "badge_earned":
                    eventType = CharacterMemoryEventType.BadgeEarned;
                    return true;
                default:
                    eventType = default;
                    return false;
            }
        }

        public static string ToWireValue(CharacterMemoryEventType eventType)
        {
            switch (eventType)
            {
                case CharacterMemoryEventType.AnimalDiscovered:
                    return "animal_discovered";
                case CharacterMemoryEventType.MissionCompleted:
                    return "mission_completed";
                case CharacterMemoryEventType.KnowledgeLearned:
                    return "knowledge_learned";
                case CharacterMemoryEventType.BadgeEarned:
                    return "badge_earned";
                default:
                    return string.Empty;
            }
        }
    }
}
