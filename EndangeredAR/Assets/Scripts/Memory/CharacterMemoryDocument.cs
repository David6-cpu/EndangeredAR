using System;
using System.Collections.Generic;

namespace EndangeredAR.Memory
{
    [Serializable]
    public sealed class CharacterMemoryDocument
    {
        public int schemaVersion = JsonCharacterMemoryRepository.CurrentSchemaVersion;
        public List<CharacterMemoryProfile> profiles = new List<CharacterMemoryProfile>();
    }

    [Serializable]
    public sealed class CharacterMemoryProfile
    {
        public string profileKey;
        public bool bootstrapCompleted;
        public List<CharacterMemoryRecord> animals = new List<CharacterMemoryRecord>();
    }

    [Serializable]
    public sealed class CharacterMemoryRecord
    {
        public string animalId;
        public List<CharacterMemoryEventRecord> events = new List<CharacterMemoryEventRecord>();
        public CharacterMemoryFoldedProjection foldedProjection = new CharacterMemoryFoldedProjection();
        public List<string> reconciliationSuppressionKeys = new List<string>();
    }

    [Serializable]
    public sealed class CharacterMemoryFoldedProjection
    {
        public bool discovered;
        public List<string> completedMissionIds = new List<string>();
        public List<string> learnedKnowledgeIds = new List<string>();
        public List<string> earnedBadgeIds = new List<string>();
        public List<string> idempotencyKeys = new List<string>();
    }
}
