namespace EndangeredAR.Memory
{
    public readonly struct CharacterMemoryMilestone
    {
        public CharacterMemoryMilestone(
            CharacterMemoryEventType eventType,
            string subjectId,
            string occurredAtUtc,
            string idempotencyKey)
        {
            EventType = eventType;
            SubjectId = subjectId;
            OccurredAtUtc = occurredAtUtc;
            IdempotencyKey = idempotencyKey;
        }

        public CharacterMemoryEventType EventType { get; }
        public string SubjectId { get; }
        public string OccurredAtUtc { get; }
        public string IdempotencyKey { get; }
    }
}
