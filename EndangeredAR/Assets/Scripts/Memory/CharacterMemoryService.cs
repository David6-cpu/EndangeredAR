using System;
using System.Collections.Generic;
using EndangeredAR.Progress;

namespace EndangeredAR.Memory
{
    public sealed class CharacterMemoryService : IAnimalProgressTransitionSink
    {
        public const string LocalDefaultProfileKey = "local-default";
        private const int MaximumLiveEventsPerAnimal = 64;
        private const int MaximumRecentMilestones = 8;

        private readonly ICharacterMemoryRepository repository;
        private readonly string profileKey;
        private readonly Func<DateTime> utcNow;
        private readonly Func<string> eventIdFactory;
        private CharacterMemoryDocument document;
        private bool initialized;
        private bool canWrite;

        public CharacterMemoryService(
            ICharacterMemoryRepository repository,
            Func<DateTime> utcNow = null,
            Func<string> eventIdFactory = null)
            : this(repository, LocalDefaultProfileKey, utcNow, eventIdFactory)
        {
        }

        internal CharacterMemoryService(
            ICharacterMemoryRepository repository,
            string profileKey,
            Func<DateTime> utcNow = null,
            Func<string> eventIdFactory = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            if (!CharacterMemoryIdValidator.IsValid(profileKey))
            {
                throw new ArgumentException("A valid local profile key is required.", nameof(profileKey));
            }

            this.profileKey = profileKey;
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            this.eventIdFactory = eventIdFactory ?? (() => Guid.NewGuid().ToString("N"));
            document = CharacterMemoryDocumentUtility.CreateEmpty();
            Status = CharacterMemoryStoreStatus.Unavailable;
            LastOperationResult = CharacterMemoryOperationResult.Unavailable;
        }

        public CharacterMemoryStoreStatus Status { get; private set; }
        public CharacterMemoryOperationResult LastOperationResult { get; private set; }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            try
            {
                var result = repository.Load();
                Status = result?.Status ?? CharacterMemoryStoreStatus.Unavailable;
                canWrite = result != null && result.CanWrite && result.Document != null;
                document = canWrite
                    ? CharacterMemoryDocumentUtility.Clone(result.Document)
                    : CharacterMemoryDocumentUtility.CreateEmpty();
                LastOperationResult = canWrite
                    ? CharacterMemoryOperationResult.NoChanges
                    : CharacterMemoryOperationResult.Unavailable;
            }
            catch (Exception exception) when (IsRepositoryFailure(exception))
            {
                Status = CharacterMemoryStoreStatus.Unavailable;
                canWrite = false;
                document = CharacterMemoryDocumentUtility.CreateEmpty();
                LastOperationResult = CharacterMemoryOperationResult.Unavailable;
            }
        }

        public void AppendBatch(AnimalProgressTransitionBatch batch)
        {
            EnsureInitialized();
            if (!canWrite)
            {
                LastOperationResult = CharacterMemoryOperationResult.Unavailable;
                return;
            }

            if (!TryCreateBatchEvents(batch, out var requestedEvents))
            {
                LastOperationResult = CharacterMemoryOperationResult.InvalidInput;
                return;
            }

            var candidate = CharacterMemoryDocumentUtility.Clone(document);
            var profile = GetOrCreateProfile(candidate, profileKey);
            var record = GetOrCreateRecord(profile, batch.AnimalId);
            NormalizeRecord(record);

            var existingKeys = CollectExistingKeys(record, profileKey, batch.AnimalId);
            var newEvents = new List<CharacterMemoryEvent>();
            foreach (var requestedEvent in requestedEvents)
            {
                if (existingKeys.Add(requestedEvent.IdempotencyKey))
                {
                    newEvents.Add(requestedEvent);
                }
            }

            if (newEvents.Count == 0)
            {
                LastOperationResult = CharacterMemoryOperationResult.NoChanges;
                return;
            }

            var requiredFolds = record.events.Count + newEvents.Count - MaximumLiveEventsPerAnimal;
            if (requiredFolds > 0 &&
                !TryFoldExistingEvents(record, profileKey, batch.AnimalId, requiredFolds))
            {
                LastOperationResult = CharacterMemoryOperationResult.CapacityExceeded;
                return;
            }

            foreach (var memoryEvent in newEvents)
            {
                record.events.Add(memoryEvent.ToRecord());
            }

            try
            {
                repository.Save(candidate);
                document = candidate;
                LastOperationResult = CharacterMemoryOperationResult.Saved;
            }
            catch (Exception exception) when (IsRepositoryFailure(exception))
            {
                LastOperationResult = CharacterMemoryOperationResult.SaveFailed;
            }
        }

        public CharacterMemoryProjection GetProjection(string animalId)
        {
            EnsureInitialized();
            if (!CharacterMemoryIdValidator.IsValid(animalId))
            {
                return CharacterMemoryProjection.Empty;
            }

            var profile = FindProfile(document, profileKey);
            var record = FindRecord(profile, animalId);
            return record == null
                ? CharacterMemoryProjection.Empty
                : BuildProjection(record, profileKey, animalId);
        }

        internal DateTime GetUtcNow()
        {
            return utcNow().ToUniversalTime();
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private bool TryCreateBatchEvents(
            AnimalProgressTransitionBatch batch,
            out List<CharacterMemoryEvent> events)
        {
            events = new List<CharacterMemoryEvent>();
            if (batch == null ||
                !CharacterMemoryIdValidator.IsValid(batch.AnimalId) ||
                batch.Transitions == null ||
                batch.Transitions.Count == 0)
            {
                return false;
            }

            var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var transition in batch.Transitions)
            {
                string eventId;
                try
                {
                    eventId = eventIdFactory();
                }
                catch (Exception)
                {
                    return false;
                }

                if (!CharacterMemoryEvent.TryCreateBusiness(
                        profileKey,
                        batch.AnimalId,
                        transition,
                        batch.OccurredAtUtc,
                        eventId,
                        out var memoryEvent))
                {
                    return false;
                }

                if (uniqueKeys.Add(memoryEvent.IdempotencyKey))
                {
                    events.Add(memoryEvent);
                }
            }

            return events.Count > 0;
        }

        private static HashSet<string> CollectExistingKeys(
            CharacterMemoryRecord record,
            string expectedProfileKey,
            string expectedAnimalId)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (record.foldedProjection.discovered)
            {
                keys.Add(CharacterMemoryEvent.CreateIdempotencyKey(
                    expectedProfileKey,
                    expectedAnimalId,
                    CharacterMemoryEventType.AnimalDiscovered,
                    expectedAnimalId));
            }

            AddFoldedKeys(
                keys,
                record.foldedProjection.completedMissionIds,
                expectedProfileKey,
                expectedAnimalId,
                CharacterMemoryEventType.MissionCompleted);
            AddFoldedKeys(
                keys,
                record.foldedProjection.learnedKnowledgeIds,
                expectedProfileKey,
                expectedAnimalId,
                CharacterMemoryEventType.KnowledgeLearned);
            AddFoldedKeys(
                keys,
                record.foldedProjection.earnedBadgeIds,
                expectedProfileKey,
                expectedAnimalId,
                CharacterMemoryEventType.BadgeEarned);

            foreach (var rawEvent in record.events)
            {
                if (CharacterMemoryEvent.TryParseRecord(
                        rawEvent,
                        expectedProfileKey,
                        expectedAnimalId,
                        out var memoryEvent))
                {
                    keys.Add(memoryEvent.IdempotencyKey);
                }
            }

            return keys;
        }

        private static void AddFoldedKeys(
            ISet<string> keys,
            IEnumerable<string> subjectIds,
            string expectedProfileKey,
            string expectedAnimalId,
            CharacterMemoryEventType eventType)
        {
            if (subjectIds == null)
            {
                return;
            }

            foreach (var subjectId in subjectIds)
            {
                if (CharacterMemoryIdValidator.IsValid(subjectId))
                {
                    keys.Add(CharacterMemoryEvent.CreateIdempotencyKey(
                        expectedProfileKey,
                        expectedAnimalId,
                        eventType,
                        subjectId));
                }
            }
        }

        private static bool TryFoldExistingEvents(
            CharacterMemoryRecord record,
            string expectedProfileKey,
            string expectedAnimalId,
            int requiredFolds)
        {
            for (var foldIndex = 0; foldIndex < requiredFolds; foldIndex++)
            {
                var selectedIndex = -1;
                CharacterMemoryEvent selected = default;
                for (var index = 0; index < record.events.Count; index++)
                {
                    if (!CharacterMemoryEvent.TryParseRecord(
                            record.events[index],
                            expectedProfileKey,
                            expectedAnimalId,
                            out var candidate))
                    {
                        continue;
                    }

                    if (selectedIndex < 0 || CompareForFolding(candidate, selected) < 0)
                    {
                        selectedIndex = index;
                        selected = candidate;
                    }
                }

                if (selectedIndex < 0)
                {
                    return false;
                }

                Fold(record.foldedProjection, selected);
                record.events.RemoveAt(selectedIndex);
            }

            return true;
        }

        private static int CompareForFolding(CharacterMemoryEvent left, CharacterMemoryEvent right)
        {
            if (!left.OccurredAt.HasValue && right.OccurredAt.HasValue)
            {
                return -1;
            }

            if (left.OccurredAt.HasValue && !right.OccurredAt.HasValue)
            {
                return 1;
            }

            if (left.OccurredAt.HasValue && right.OccurredAt.HasValue)
            {
                var timestampComparison = left.OccurredAt.Value.CompareTo(right.OccurredAt.Value);
                if (timestampComparison != 0)
                {
                    return timestampComparison;
                }
            }

            return string.CompareOrdinal(left.IdempotencyKey, right.IdempotencyKey);
        }

        private static void Fold(CharacterMemoryFoldedProjection folded, CharacterMemoryEvent memoryEvent)
        {
            switch (memoryEvent.EventType)
            {
                case CharacterMemoryEventType.AnimalDiscovered:
                    folded.discovered = true;
                    break;
                case CharacterMemoryEventType.MissionCompleted:
                    AddUnique(folded.completedMissionIds, memoryEvent.SubjectId);
                    break;
                case CharacterMemoryEventType.KnowledgeLearned:
                    AddUnique(folded.learnedKnowledgeIds, memoryEvent.SubjectId);
                    break;
                case CharacterMemoryEventType.BadgeEarned:
                    AddUnique(folded.earnedBadgeIds, memoryEvent.SubjectId);
                    break;
            }

            AddUnique(folded.idempotencyKeys, memoryEvent.IdempotencyKey);
        }

        private static CharacterMemoryProjection BuildProjection(
            CharacterMemoryRecord record,
            string expectedProfileKey,
            string expectedAnimalId)
        {
            NormalizeRecord(record);
            var missions = CreateValidatedSet(record.foldedProjection.completedMissionIds);
            var knowledge = CreateValidatedSet(record.foldedProjection.learnedKnowledgeIds);
            var badges = CreateValidatedSet(record.foldedProjection.earnedBadgeIds);
            var discovered = record.foldedProjection.discovered;
            var recent = new List<CharacterMemoryEvent>();

            foreach (var rawEvent in record.events)
            {
                if (!CharacterMemoryEvent.TryParseRecord(
                        rawEvent,
                        expectedProfileKey,
                        expectedAnimalId,
                        out var memoryEvent))
                {
                    continue;
                }

                switch (memoryEvent.EventType)
                {
                    case CharacterMemoryEventType.AnimalDiscovered:
                        discovered = true;
                        break;
                    case CharacterMemoryEventType.MissionCompleted:
                        missions.Add(memoryEvent.SubjectId);
                        break;
                    case CharacterMemoryEventType.KnowledgeLearned:
                        knowledge.Add(memoryEvent.SubjectId);
                        break;
                    case CharacterMemoryEventType.BadgeEarned:
                        badges.Add(memoryEvent.SubjectId);
                        break;
                }

                if (memoryEvent.Origin == CharacterMemoryEventOrigin.Business && memoryEvent.OccurredAt.HasValue)
                {
                    recent.Add(memoryEvent);
                }
            }

            recent.Sort(CompareRecent);
            var milestones = new List<CharacterMemoryMilestone>();
            var milestoneCount = Math.Min(MaximumRecentMilestones, recent.Count);
            for (var index = 0; index < milestoneCount; index++)
            {
                var memoryEvent = recent[index];
                milestones.Add(new CharacterMemoryMilestone(
                    memoryEvent.EventType,
                    memoryEvent.SubjectId,
                    memoryEvent.OccurredAtUtc,
                    memoryEvent.IdempotencyKey));
            }

            return new CharacterMemoryProjection(
                discovered,
                ToArray(missions),
                ToArray(knowledge),
                ToArray(badges),
                milestones);
        }

        private static int CompareRecent(CharacterMemoryEvent left, CharacterMemoryEvent right)
        {
            var timestampComparison = right.OccurredAt.Value.CompareTo(left.OccurredAt.Value);
            return timestampComparison != 0
                ? timestampComparison
                : string.CompareOrdinal(left.IdempotencyKey, right.IdempotencyKey);
        }

        private static SortedSet<string> CreateValidatedSet(IEnumerable<string> values)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (values == null)
            {
                return set;
            }

            foreach (var value in values)
            {
                if (CharacterMemoryIdValidator.IsValid(value))
                {
                    set.Add(value);
                }
            }

            return set;
        }

        private static string[] ToArray(SortedSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static CharacterMemoryProfile GetOrCreateProfile(CharacterMemoryDocument source, string key)
        {
            source.profiles ??= new List<CharacterMemoryProfile>();
            var profile = FindProfile(source, key);
            if (profile != null)
            {
                profile.animals ??= new List<CharacterMemoryRecord>();
                return profile;
            }

            profile = new CharacterMemoryProfile
            {
                profileKey = key,
                animals = new List<CharacterMemoryRecord>()
            };
            source.profiles.Add(profile);
            return profile;
        }

        private static CharacterMemoryProfile FindProfile(CharacterMemoryDocument source, string key)
        {
            if (source?.profiles == null)
            {
                return null;
            }

            foreach (var profile in source.profiles)
            {
                if (profile?.profileKey == key)
                {
                    return profile;
                }
            }

            return null;
        }

        private static CharacterMemoryRecord GetOrCreateRecord(CharacterMemoryProfile profile, string animalId)
        {
            var record = FindRecord(profile, animalId);
            if (record != null)
            {
                return record;
            }

            record = new CharacterMemoryRecord { animalId = animalId };
            profile.animals.Add(record);
            return record;
        }

        private static CharacterMemoryRecord FindRecord(CharacterMemoryProfile profile, string animalId)
        {
            if (profile?.animals == null)
            {
                return null;
            }

            foreach (var record in profile.animals)
            {
                if (record?.animalId == animalId)
                {
                    return record;
                }
            }

            return null;
        }

        private static void NormalizeRecord(CharacterMemoryRecord record)
        {
            record.events ??= new List<CharacterMemoryEventRecord>();
            record.foldedProjection ??= new CharacterMemoryFoldedProjection();
            record.foldedProjection.completedMissionIds ??= new List<string>();
            record.foldedProjection.learnedKnowledgeIds ??= new List<string>();
            record.foldedProjection.earnedBadgeIds ??= new List<string>();
            record.foldedProjection.idempotencyKeys ??= new List<string>();
            record.reconciliationSuppressionKeys ??= new List<string>();
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
                values.Sort(StringComparer.Ordinal);
            }
        }

        private static bool IsRepositoryFailure(Exception exception)
        {
            return exception is InvalidOperationException ||
                   exception is System.IO.IOException ||
                   exception is UnauthorizedAccessException;
        }
    }
}
