using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EndangeredAR.Progress
{
    public sealed class AnimalProgressService : MonoBehaviour
    {
        internal static string RepositoryPathOverrideForTests;

        private JsonAnimalProgressRepository repository;
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
                return;
            }

            ActiveRepositoryPath = !string.IsNullOrWhiteSpace(overridePath)
                ? overridePath
                : !string.IsNullOrWhiteSpace(RepositoryPathOverrideForTests)
                    ? RepositoryPathOverrideForTests
                    : Path.Combine(Application.persistentDataPath, "animal-progress.json");
            repository = new JsonAnimalProgressRepository(ActiveRepositoryPath);
            document = repository.Load();
            initialized = true;
        }

        public bool IsUnlocked(string animalId)
        {
            var record = FindRecord(animalId);
            return record != null && record.unlocked;
        }

        public bool Unlock(string animalId)
        {
            var record = GetOrCreateInternal(animalId);
            if (record == null || record.unlocked)
            {
                return false;
            }

            record.unlocked = true;
            record.unlockedAtUtc = DateTime.UtcNow.ToString("o");
            SaveAndNotify(record.animalId);
            return true;
        }

        public AnimalProgressRecord GetOrCreate(string animalId)
        {
            return JsonAnimalProgressRepository.CloneRecord(GetOrCreateInternal(animalId));
        }

        public void MarkMissionCompleted(string animalId, string badgeId, string knowledgeId)
        {
            var record = GetOrCreateInternal(animalId);
            if (record == null)
            {
                return;
            }

            record.missionCompleted = true;
            AddIfPresent(record.earnedBadgeIds, badgeId);
            AddIfPresent(record.learnedKnowledgeIds, knowledgeId);
            SaveAndNotify(record.animalId);
        }

        public void ReplaceConversation(string animalId, IEnumerable<ConversationRecord> messages)
        {
            var record = GetOrCreateInternal(animalId);
            if (record == null)
            {
                return;
            }

            record.recentConversation = JsonAnimalProgressRepository.CloneConversation(messages);
            SaveAndNotify(record.animalId);
        }

        public IReadOnlyList<ConversationRecord> GetConversation(string animalId)
        {
            var record = FindRecord(animalId);
            return JsonAnimalProgressRepository.CloneConversation(record == null ? null : record.recentConversation);
        }

        private AnimalProgressRecord FindRecord(string animalId)
        {
            Initialize();
            var normalizedAnimalId = JsonAnimalProgressRepository.NormalizeAnimalId(animalId);
            if (string.IsNullOrEmpty(normalizedAnimalId))
            {
                return null;
            }

            foreach (var record in document.animals)
            {
                if (string.Equals(record.animalId, normalizedAnimalId, StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        private AnimalProgressRecord GetOrCreateInternal(string animalId)
        {
            var normalizedAnimalId = JsonAnimalProgressRepository.NormalizeAnimalId(animalId);
            if (string.IsNullOrEmpty(normalizedAnimalId))
            {
                return null;
            }

            var existingRecord = FindRecord(normalizedAnimalId);
            if (existingRecord != null)
            {
                return existingRecord;
            }

            var record = new AnimalProgressRecord { animalId = normalizedAnimalId };
            document.animals.Add(record);
            return record;
        }

        private void SaveAndNotify(string animalId)
        {
            repository.Save(document);
            document = repository.Load();
            ProgressChanged?.Invoke(animalId);
        }

        private static void AddIfPresent(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value.Trim()))
            {
                values.Add(value.Trim());
            }
        }
    }
}
