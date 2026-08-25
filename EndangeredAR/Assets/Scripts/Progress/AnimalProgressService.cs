using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EndangeredAR.Progress
{
    public sealed class AnimalProgressService : MonoBehaviour
    {
        internal static string RepositoryPathOverrideForTests;

        private IAnimalProgressRepository repository;
        private IAnimalProgressTransitionSink transitionSink;
        private Func<DateTime> utcNow = () => DateTime.UtcNow;
        private AnimalProgressDocument document;
        private bool initialized;

        public event Action<string> ProgressChanged;
        internal string ActiveRepositoryPath { get; private set; }

        public int UnlockedCount
        {
            get
            {
                Initialize();
                var count = 0;
                foreach (var record in document.animals)
                {
                    if (record.unlocked)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize(string overridePath = null)
        {
            if (initialized)
            {
                if (string.IsNullOrWhiteSpace(overridePath))
                {
                    return;
                }

                var explicitRepositoryPath = ResolveRepositoryPath(overridePath);
                if (string.Equals(ActiveRepositoryPath, explicitRepositoryPath, StringComparison.Ordinal))
                {
                    return;
                }

                InitializeRepository(explicitRepositoryPath);
                return;
            }

            InitializeRepository(ResolveRepositoryPath(overridePath));
        }

        public bool IsUnlocked(string animalId)
        {
            var record = FindRecord(animalId);
            return record != null && record.unlocked;
        }

        public bool Unlock(string animalId)
        {
            if (!TryCreateCandidateRecord(animalId, out var candidate, out var record) || record.unlocked)
            {
                return false;
            }

            var occurredAtUtc = GetOccurredAtUtc();
            record.unlocked = true;
            record.unlockedAtUtc = occurredAtUtc;
            SaveCommitNotify(
                candidate,
                record.animalId,
                new AnimalProgressTransitionBatch(
                    record.animalId,
                    occurredAtUtc,
                    new[]
                    {
                        new AnimalProgressTransition(
                            AnimalProgressTransitionType.AnimalDiscovered,
                            record.animalId)
                    }));
            return true;
        }

        public AnimalProgressRecord GetOrCreate(string animalId)
        {
            Initialize();
            var normalizedAnimalId = JsonAnimalProgressRepository.NormalizeAnimalId(animalId);
            if (string.IsNullOrEmpty(normalizedAnimalId))
            {
                return null;
            }

            var existing = FindRecord(document, normalizedAnimalId);
            return existing == null
                ? new AnimalProgressRecord { animalId = normalizedAnimalId }
                : JsonAnimalProgressRepository.CloneRecord(existing);
        }

        public bool TryGetSnapshot(string animalId, out AnimalProgressRecord snapshot)
        {
            snapshot = JsonAnimalProgressRepository.CloneRecord(FindRecord(animalId));
            return snapshot != null;
        }

        public IReadOnlyList<AnimalProgressRecord> GetAllSnapshots()
        {
            Initialize();
            var snapshots = new List<AnimalProgressRecord>();
            foreach (var record in document.animals)
            {
                var snapshot = JsonAnimalProgressRepository.CloneRecord(record);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }

            return snapshots;
        }

        public void MarkMissionCompleted(
            string animalId,
            string missionId,
            string badgeId,
            string knowledgeId)
        {
            if (!AnimalProgressIdentifier.IsValid(missionId) ||
                !IsOptionalIdentifierValid(badgeId) ||
                !IsOptionalIdentifierValid(knowledgeId) ||
                !TryCreateCandidateRecord(animalId, out var candidate, out var record))
            {
                return;
            }

            var transitions = new List<AnimalProgressTransition>();
            if (!record.missionCompleted)
            {
                record.missionCompleted = true;
                transitions.Add(new AnimalProgressTransition(
                    AnimalProgressTransitionType.MissionCompleted,
                    missionId));
            }

            if (AddIfMissing(record.learnedKnowledgeIds, knowledgeId))
            {
                transitions.Add(new AnimalProgressTransition(
                    AnimalProgressTransitionType.KnowledgeLearned,
                    knowledgeId));
            }

            if (AddIfMissing(record.earnedBadgeIds, badgeId))
            {
                transitions.Add(new AnimalProgressTransition(
                    AnimalProgressTransitionType.BadgeEarned,
                    badgeId));
            }

            if (transitions.Count == 0)
            {
                return;
            }

            var occurredAtUtc = GetOccurredAtUtc();
            SaveCommitNotify(
                candidate,
                record.animalId,
                new AnimalProgressTransitionBatch(record.animalId, occurredAtUtc, transitions));
        }

        public void ReplaceConversation(string animalId, IEnumerable<ConversationRecord> messages)
        {
            if (!TryCreateCandidateRecord(animalId, out var candidate, out var record))
            {
                return;
            }

            record.recentConversation = JsonAnimalProgressRepository.CloneConversation(messages);
            SaveCommitNotify(candidate, record.animalId, null);
        }

        public IReadOnlyList<ConversationRecord> GetConversation(string animalId)
        {
            var record = FindRecord(animalId);
            return JsonAnimalProgressRepository.CloneConversation(record == null ? null : record.recentConversation);
        }

        internal void InitializeForTests(
            IAnimalProgressRepository testRepository,
            IAnimalProgressTransitionSink testTransitionSink,
            Func<DateTime> testUtcNow)
        {
            repository = testRepository ?? throw new ArgumentNullException(nameof(testRepository));
            transitionSink = testTransitionSink;
            utcNow = testUtcNow ?? (() => DateTime.UtcNow);
            document = JsonAnimalProgressRepository.NormalizeDocument(repository.Load());
            ActiveRepositoryPath = null;
            initialized = true;
        }

        internal void ConfigureTransitionSink(IAnimalProgressTransitionSink sink)
        {
            transitionSink = sink;
        }

        private static string ResolveRepositoryPath(string overridePath)
        {
            var repositoryPath = !string.IsNullOrWhiteSpace(overridePath)
                ? overridePath
                : !string.IsNullOrWhiteSpace(RepositoryPathOverrideForTests)
                    ? RepositoryPathOverrideForTests
                    : Path.Combine(Application.persistentDataPath, "animal-progress.json");
            return Path.GetFullPath(repositoryPath);
        }

        private void InitializeRepository(string repositoryPath)
        {
            ActiveRepositoryPath = repositoryPath;
            repository = new JsonAnimalProgressRepository(repositoryPath);
            document = repository.Load();
            utcNow = () => DateTime.UtcNow;
            initialized = true;
        }

        private AnimalProgressRecord FindRecord(string animalId)
        {
            Initialize();
            var normalizedAnimalId = JsonAnimalProgressRepository.NormalizeAnimalId(animalId);
            return string.IsNullOrEmpty(normalizedAnimalId)
                ? null
                : FindRecord(document, normalizedAnimalId);
        }

        private static AnimalProgressRecord FindRecord(
            AnimalProgressDocument source,
            string normalizedAnimalId)
        {
            if (source?.animals == null)
            {
                return null;
            }

            foreach (var record in source.animals)
            {
                if (record != null &&
                    string.Equals(record.animalId, normalizedAnimalId, StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        private bool TryCreateCandidateRecord(
            string animalId,
            out AnimalProgressDocument candidate,
            out AnimalProgressRecord record)
        {
            Initialize();
            candidate = null;
            record = null;
            var normalizedAnimalId = JsonAnimalProgressRepository.NormalizeAnimalId(animalId);
            if (string.IsNullOrEmpty(normalizedAnimalId) || !AnimalProgressIdentifier.IsValid(normalizedAnimalId))
            {
                return false;
            }

            candidate = JsonAnimalProgressRepository.NormalizeDocument(document);
            record = FindRecord(candidate, normalizedAnimalId);
            if (record == null)
            {
                record = new AnimalProgressRecord { animalId = normalizedAnimalId };
                candidate.animals.Add(record);
            }

            return true;
        }

        private void SaveCommitNotify(
            AnimalProgressDocument candidate,
            string animalId,
            AnimalProgressTransitionBatch transitionBatch)
        {
            repository.Save(candidate);
            document = JsonAnimalProgressRepository.NormalizeDocument(candidate);

            if (transitionBatch != null && transitionBatch.Transitions.Count > 0 && transitionSink != null)
            {
                try
                {
                    transitionSink.AppendBatch(transitionBatch);
                }
                catch (Exception)
                {
                    Debug.LogWarning("Character Memory could not record a committed progress transition.");
                }
            }

            ProgressChanged?.Invoke(animalId);
        }

        private string GetOccurredAtUtc()
        {
            return utcNow().ToUniversalTime().ToString("o");
        }

        private static bool IsOptionalIdentifierValid(string value)
        {
            return string.IsNullOrEmpty(value) || AnimalProgressIdentifier.IsValid(value);
        }

        private static bool AddIfMissing(List<string> values, string value)
        {
            if (string.IsNullOrEmpty(value) || values.Contains(value))
            {
                return false;
            }

            values.Add(value);
            return true;
        }
    }
}
