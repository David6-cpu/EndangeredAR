using System.Collections.Generic;

namespace EndangeredAR.Memory
{
    internal static class CharacterMemoryDocumentUtility
    {
        internal static CharacterMemoryDocument CreateEmpty()
        {
            return new CharacterMemoryDocument
            {
                schemaVersion = JsonCharacterMemoryRepository.CurrentSchemaVersion,
                profiles = new List<CharacterMemoryProfile>()
            };
        }

        internal static CharacterMemoryDocument Clone(CharacterMemoryDocument source)
        {
            var clone = CreateEmpty();
            if (source == null)
            {
                return clone;
            }

            clone.schemaVersion = source.schemaVersion;
            if (source.profiles == null)
            {
                return clone;
            }

            foreach (var profile in source.profiles)
            {
                if (profile != null)
                {
                    clone.profiles.Add(CloneProfile(profile));
                }
            }

            return clone;
        }

        private static CharacterMemoryProfile CloneProfile(CharacterMemoryProfile source)
        {
            var clone = new CharacterMemoryProfile
            {
                profileKey = source.profileKey,
                bootstrapCompleted = source.bootstrapCompleted,
                animals = new List<CharacterMemoryRecord>()
            };

            if (source.animals != null)
            {
                foreach (var animal in source.animals)
                {
                    if (animal != null)
                    {
                        clone.animals.Add(CloneRecord(animal));
                    }
                }
            }

            return clone;
        }

        private static CharacterMemoryRecord CloneRecord(CharacterMemoryRecord source)
        {
            var clone = new CharacterMemoryRecord
            {
                animalId = source.animalId,
                events = new List<CharacterMemoryEventRecord>(),
                foldedProjection = CloneFoldedProjection(source.foldedProjection),
                reconciliationSuppressionKeys = CloneStrings(source.reconciliationSuppressionKeys)
            };

            if (source.events != null)
            {
                foreach (var memoryEvent in source.events)
                {
                    if (memoryEvent != null)
                    {
                        clone.events.Add(CloneEvent(memoryEvent));
                    }
                }
            }

            return clone;
        }

        private static CharacterMemoryEventRecord CloneEvent(CharacterMemoryEventRecord source)
        {
            return new CharacterMemoryEventRecord
            {
                schemaVersion = source.schemaVersion,
                eventId = source.eventId,
                idempotencyKey = source.idempotencyKey,
                profileKey = source.profileKey,
                animalId = source.animalId,
                eventType = source.eventType,
                subjectId = source.subjectId,
                occurredAtUtc = source.occurredAtUtc,
                eventOrigin = source.eventOrigin
            };
        }

        private static CharacterMemoryFoldedProjection CloneFoldedProjection(CharacterMemoryFoldedProjection source)
        {
            if (source == null)
            {
                return new CharacterMemoryFoldedProjection();
            }

            return new CharacterMemoryFoldedProjection
            {
                discovered = source.discovered,
                completedMissionIds = CloneStrings(source.completedMissionIds),
                learnedKnowledgeIds = CloneStrings(source.learnedKnowledgeIds),
                earnedBadgeIds = CloneStrings(source.earnedBadgeIds),
                idempotencyKeys = CloneStrings(source.idempotencyKeys)
            };
        }

        private static List<string> CloneStrings(IEnumerable<string> source)
        {
            return source == null ? new List<string>() : new List<string>(source);
        }
    }
}
