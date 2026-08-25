using System;

namespace EndangeredAR.Memory
{
    [Serializable]
    public sealed class CharacterMemoryEventRecord
    {
        public int schemaVersion = JsonCharacterMemoryRepository.CurrentSchemaVersion;
        public string eventId;
        public string idempotencyKey;
        public string profileKey;
        public string animalId;
        public string eventType;
        public string subjectId;
        public string occurredAtUtc;
        public string eventOrigin;
    }
}
