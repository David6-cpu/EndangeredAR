using System;
using System.Globalization;
using EndangeredAR.Progress;

namespace EndangeredAR.Memory
{
    internal readonly struct CharacterMemoryEvent
    {
        private CharacterMemoryEvent(
            string eventId,
            string idempotencyKey,
            string profileKey,
            string animalId,
            CharacterMemoryEventType eventType,
            string subjectId,
            string occurredAtUtc,
            DateTime? occurredAt,
            CharacterMemoryEventOrigin origin)
        {
            EventId = eventId;
            IdempotencyKey = idempotencyKey;
            ProfileKey = profileKey;
            AnimalId = animalId;
            EventType = eventType;
            SubjectId = subjectId;
            OccurredAtUtc = occurredAtUtc;
            OccurredAt = occurredAt;
            Origin = origin;
        }

        internal string EventId { get; }
        internal string IdempotencyKey { get; }
        internal string ProfileKey { get; }
        internal string AnimalId { get; }
        internal CharacterMemoryEventType EventType { get; }
        internal string SubjectId { get; }
        internal string OccurredAtUtc { get; }
        internal DateTime? OccurredAt { get; }
        internal CharacterMemoryEventOrigin Origin { get; }

        internal static bool TryCreateBusiness(
            string profileKey,
            string animalId,
            AnimalProgressTransition transition,
            string occurredAtUtc,
            string eventId,
            out CharacterMemoryEvent memoryEvent)
        {
            memoryEvent = default;
            return TryMap(transition.Type, out var eventType) &&
                   TryCreate(
                       profileKey,
                       animalId,
                       eventType,
                       transition.SubjectId,
                       occurredAtUtc,
                       CharacterMemoryEventOrigin.Business,
                       eventId,
                       out memoryEvent);
        }

        internal static bool TryCreate(
            string profileKey,
            string animalId,
            CharacterMemoryEventType eventType,
            string subjectId,
            string occurredAtUtc,
            CharacterMemoryEventOrigin origin,
            string eventId,
            out CharacterMemoryEvent memoryEvent)
        {
            memoryEvent = default;
            if (string.IsNullOrEmpty(CharacterMemoryEventTypeProtocol.ToWireValue(eventType)) ||
                string.IsNullOrEmpty(CharacterMemoryEventOriginProtocol.ToWireValue(origin)) ||
                !CharacterMemoryIdValidator.IsValid(profileKey) ||
                !CharacterMemoryIdValidator.IsValid(animalId) ||
                !CharacterMemoryIdValidator.IsValid(subjectId) ||
                !CharacterMemoryIdValidator.IsValid(eventId) ||
                eventType == CharacterMemoryEventType.AnimalDiscovered && subjectId != animalId)
            {
                return false;
            }

            DateTime? occurredAt = null;
            if (!string.IsNullOrEmpty(occurredAtUtc))
            {
                if (!TryParseUtc(occurredAtUtc, out var parsed))
                {
                    return false;
                }

                occurredAt = parsed;
            }
            else if (origin == CharacterMemoryEventOrigin.Business)
            {
                return false;
            }

            var idempotencyKey = CreateIdempotencyKey(profileKey, animalId, eventType, subjectId);
            memoryEvent = new CharacterMemoryEvent(
                eventId,
                idempotencyKey,
                profileKey,
                animalId,
                eventType,
                subjectId,
                occurredAtUtc ?? string.Empty,
                occurredAt,
                origin);
            return true;
        }

        internal static bool TryParseRecord(
            CharacterMemoryEventRecord record,
            string expectedProfileKey,
            string expectedAnimalId,
            out CharacterMemoryEvent memoryEvent)
        {
            memoryEvent = default;
            if (record == null ||
                record.schemaVersion != JsonCharacterMemoryRepository.CurrentSchemaVersion ||
                record.profileKey != expectedProfileKey ||
                record.animalId != expectedAnimalId ||
                !CharacterMemoryIdValidator.IsValid(record.eventId) ||
                !CharacterMemoryIdValidator.IsValid(record.profileKey) ||
                !CharacterMemoryIdValidator.IsValid(record.animalId) ||
                !CharacterMemoryIdValidator.IsValid(record.subjectId) ||
                !CharacterMemoryEventTypeProtocol.TryParseExact(record.eventType, out var eventType) ||
                !CharacterMemoryEventOriginProtocol.TryParseExact(record.eventOrigin, out var origin) ||
                eventType == CharacterMemoryEventType.AnimalDiscovered && record.subjectId != record.animalId)
            {
                return false;
            }

            var expectedKey = CreateIdempotencyKey(
                record.profileKey,
                record.animalId,
                eventType,
                record.subjectId);
            if (record.idempotencyKey != expectedKey)
            {
                return false;
            }

            DateTime? occurredAt = null;
            if (!string.IsNullOrEmpty(record.occurredAtUtc))
            {
                if (!TryParseUtc(record.occurredAtUtc, out var parsed))
                {
                    return false;
                }

                occurredAt = parsed;
            }
            else if (origin == CharacterMemoryEventOrigin.Business)
            {
                return false;
            }

            memoryEvent = new CharacterMemoryEvent(
                record.eventId,
                record.idempotencyKey,
                record.profileKey,
                record.animalId,
                eventType,
                record.subjectId,
                record.occurredAtUtc,
                occurredAt,
                origin);
            return true;
        }

        internal CharacterMemoryEventRecord ToRecord()
        {
            return new CharacterMemoryEventRecord
            {
                schemaVersion = JsonCharacterMemoryRepository.CurrentSchemaVersion,
                eventId = EventId,
                idempotencyKey = IdempotencyKey,
                profileKey = ProfileKey,
                animalId = AnimalId,
                eventType = CharacterMemoryEventTypeProtocol.ToWireValue(EventType),
                subjectId = SubjectId,
                occurredAtUtc = OccurredAtUtc,
                eventOrigin = CharacterMemoryEventOriginProtocol.ToWireValue(Origin)
            };
        }

        internal static string CreateIdempotencyKey(
            string profileKey,
            string animalId,
            CharacterMemoryEventType eventType,
            string subjectId)
        {
            return "v1|" + profileKey + "|" + animalId + "|" +
                CharacterMemoryEventTypeProtocol.ToWireValue(eventType) + "|" + subjectId;
        }

        private static bool TryMap(
            AnimalProgressTransitionType transitionType,
            out CharacterMemoryEventType eventType)
        {
            switch (transitionType)
            {
                case AnimalProgressTransitionType.AnimalDiscovered:
                    eventType = CharacterMemoryEventType.AnimalDiscovered;
                    return true;
                case AnimalProgressTransitionType.MissionCompleted:
                    eventType = CharacterMemoryEventType.MissionCompleted;
                    return true;
                case AnimalProgressTransitionType.KnowledgeLearned:
                    eventType = CharacterMemoryEventType.KnowledgeLearned;
                    return true;
                case AnimalProgressTransitionType.BadgeEarned:
                    eventType = CharacterMemoryEventType.BadgeEarned;
                    return true;
                default:
                    eventType = default;
                    return false;
            }
        }

        private static bool TryParseUtc(string value, out DateTime parsed)
        {
            return DateTime.TryParseExact(
                       value,
                       "o",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out parsed) &&
                   parsed.Kind == DateTimeKind.Utc;
        }
    }
}
