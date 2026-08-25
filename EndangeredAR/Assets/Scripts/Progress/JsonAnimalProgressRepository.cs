using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EndangeredAR.Progress
{
    public sealed class JsonAnimalProgressRepository : IAnimalProgressRepository
    {
        public const int CurrentSchemaVersion = 1;

        private readonly string filePath;
        private readonly Func<DateTime> utcNow;

        public JsonAnimalProgressRepository(string filePath, Func<DateTime> utcNow = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A progress file path is required.", nameof(filePath));
            }

            this.filePath = filePath;
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public AnimalProgressDocument Load()
        {
            if (!File.Exists(filePath))
            {
                return CreateEmptyDocument();
            }

            var serializedDocument = File.ReadAllText(filePath);
            try
            {
                var document = JsonUtility.FromJson<AnimalProgressDocument>(serializedDocument);
                if (document == null)
                {
                    throw new InvalidDataException("Progress JSON did not produce a document.");
                }

                return NormalizeDocument(document);
            }
            catch (ArgumentException)
            {
                BackupCorruptFile();
                return CreateEmptyDocument();
            }
            catch (InvalidDataException)
            {
                BackupCorruptFile();
                return CreateEmptyDocument();
            }
        }

        public void Save(AnimalProgressDocument document)
        {
            var normalizedDocument = NormalizeDocument(document);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(normalizedDocument));

            if (File.Exists(filePath))
            {
                try
                {
                    File.Replace(temporaryPath, filePath, filePath + ".bak");
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                }
                catch (IOException)
                {
                }
            }

            File.Copy(temporaryPath, filePath, true);
            File.Delete(temporaryPath);
        }

        private void BackupCorruptFile()
        {
            var backupPath = filePath + ".corrupt-" + utcNow().ToUniversalTime().ToString("yyyyMMdd-HHmmss");
            var collisionIndex = 1;
            while (File.Exists(backupPath))
            {
                backupPath = filePath + ".corrupt-" + utcNow().ToUniversalTime().ToString("yyyyMMdd-HHmmss") + "-" + collisionIndex;
                collisionIndex++;
            }

            File.Copy(filePath, backupPath);
        }

        internal static AnimalProgressDocument CreateEmptyDocument()
        {
            return new AnimalProgressDocument
            {
                schemaVersion = CurrentSchemaVersion,
                animals = new List<AnimalProgressRecord>()
            };
        }

        internal static AnimalProgressDocument NormalizeDocument(AnimalProgressDocument document)
        {
            var normalized = CreateEmptyDocument();
            if (document == null || document.animals == null)
            {
                return normalized;
            }

            var recordsById = new Dictionary<string, AnimalProgressRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in document.animals)
            {
                var normalizedRecord = NormalizeRecord(record);
                if (normalizedRecord == null)
                {
                    continue;
                }

                if (recordsById.TryGetValue(normalizedRecord.animalId, out var existingRecord))
                {
                    MergeRecord(existingRecord, normalizedRecord);
                    continue;
                }

                recordsById.Add(normalizedRecord.animalId, normalizedRecord);
                normalized.animals.Add(normalizedRecord);
            }

            return normalized;
        }

        internal static string NormalizeAnimalId(string animalId)
        {
            return string.IsNullOrWhiteSpace(animalId) ? string.Empty : animalId.Trim().ToLowerInvariant();
        }

        internal static AnimalProgressRecord CloneRecord(AnimalProgressRecord record)
        {
            return NormalizeRecord(record);
        }

        internal static List<ConversationRecord> CloneConversation(IEnumerable<ConversationRecord> messages)
        {
            var copiedMessages = new List<ConversationRecord>();
            if (messages != null)
            {
                foreach (var message in messages)
                {
                    if (message != null)
                    {
                        copiedMessages.Add(new ConversationRecord { role = message.role, content = message.content });
                    }
                }
            }

            TrimConversation(copiedMessages);
            return copiedMessages;
        }

        private static AnimalProgressRecord NormalizeRecord(AnimalProgressRecord record)
        {
            if (record == null)
            {
                return null;
            }

            var animalId = NormalizeAnimalId(record.animalId);
            if (string.IsNullOrEmpty(animalId))
            {
                return null;
            }

            return new AnimalProgressRecord
            {
                animalId = animalId,
                unlocked = record.unlocked,
                unlockedAtUtc = record.unlockedAtUtc,
                learnedKnowledgeIds = CloneStrings(record.learnedKnowledgeIds),
                missionCompleted = record.missionCompleted,
                earnedBadgeIds = CloneStrings(record.earnedBadgeIds),
                recentConversation = CloneConversation(record.recentConversation)
            };
        }

        private static void MergeRecord(AnimalProgressRecord destination, AnimalProgressRecord source)
        {
            destination.unlocked |= source.unlocked;
            if (string.IsNullOrEmpty(destination.unlockedAtUtc))
            {
                destination.unlockedAtUtc = source.unlockedAtUtc;
            }

            AddDistinct(destination.learnedKnowledgeIds, source.learnedKnowledgeIds);
            destination.missionCompleted |= source.missionCompleted;
            AddDistinct(destination.earnedBadgeIds, source.earnedBadgeIds);
            destination.recentConversation.AddRange(CloneConversation(source.recentConversation));
            TrimConversation(destination.recentConversation);
        }

        private static List<string> CloneStrings(IEnumerable<string> values)
        {
            var copiedValues = new List<string>();
            if (values != null)
            {
                foreach (var value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        copiedValues.Add(value.Trim());
                    }
                }
            }

            return copiedValues;
        }

        private static void AddDistinct(List<string> destination, IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                if (!destination.Contains(value))
                {
                    destination.Add(value);
                }
            }
        }

        private static void TrimConversation(List<ConversationRecord> messages)
        {
            const int maximumConversationMessages = 20;
            if (messages.Count > maximumConversationMessages)
            {
                messages.RemoveRange(0, messages.Count - maximumConversationMessages);
            }
        }
    }
}
