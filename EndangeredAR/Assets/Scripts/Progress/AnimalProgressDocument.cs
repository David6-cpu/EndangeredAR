using System;
using System.Collections.Generic;

namespace EndangeredAR.Progress
{
    [Serializable]
    public sealed class AnimalProgressDocument
    {
        public int schemaVersion = JsonAnimalProgressRepository.CurrentSchemaVersion;
        public List<AnimalProgressRecord> animals = new List<AnimalProgressRecord>();
    }

    [Serializable]
    public sealed class AnimalProgressRecord
    {
        public string animalId;
        public bool unlocked;
        public string unlockedAtUtc;
        public List<string> learnedKnowledgeIds = new List<string>();
        public bool missionCompleted;
        public List<string> earnedBadgeIds = new List<string>();
        public List<ConversationRecord> recentConversation = new List<ConversationRecord>();
    }

    [Serializable]
    public sealed class ConversationRecord
    {
        public string role;
        public string content;
    }
}
