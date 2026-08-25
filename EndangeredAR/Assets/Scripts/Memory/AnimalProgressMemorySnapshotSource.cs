using System;
using System.Collections.Generic;
using System.Globalization;
using EndangeredAR.Animals;
using EndangeredAR.Progress;

namespace EndangeredAR.Memory
{
    public sealed class AnimalProgressMemorySnapshotSource : ICharacterMemoryProgressSnapshotSource
    {
        private readonly AnimalProgressService progress;
        private readonly AnimalCatalogService catalog;

        public AnimalProgressMemorySnapshotSource(
            AnimalProgressService progress,
            AnimalCatalogService catalog)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public IReadOnlyList<CharacterMemoryProgressSnapshot> GetSnapshots()
        {
            catalog.Initialize();
            var records = progress.GetAllSnapshots();
            var snapshots = new List<CharacterMemoryProgressSnapshot>();
            foreach (var record in records)
            {
                if (record == null || !CharacterMemoryIdValidator.IsValid(record.animalId))
                {
                    continue;
                }

                var missionId = string.Empty;
                if (catalog.TryGet(record.animalId, out var definition) &&
                    definition?.Mission != null &&
                    CharacterMemoryIdValidator.IsValid(definition.Mission.MissionId))
                {
                    missionId = definition.Mission.MissionId;
                }

                snapshots.Add(new CharacterMemoryProgressSnapshot(
                    record.animalId,
                    record.unlocked,
                    SanitizeUtc(record.unlockedAtUtc),
                    missionId,
                    record.missionCompleted,
                    CopyControlledIds(record.learnedKnowledgeIds),
                    CopyControlledIds(record.earnedBadgeIds)));
            }

            snapshots.Sort((left, right) => string.CompareOrdinal(left.AnimalId, right.AnimalId));
            return snapshots;
        }

        private static string SanitizeUtc(string value)
        {
            return DateTime.TryParseExact(
                       value,
                       "o",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out var parsed) &&
                   parsed.Kind == DateTimeKind.Utc
                ? value
                : string.Empty;
        }

        private static string[] CopyControlledIds(IEnumerable<string> values)
        {
            var controlled = new SortedSet<string>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (var value in values)
                {
                    if (CharacterMemoryIdValidator.IsValid(value))
                    {
                        controlled.Add(value);
                    }
                }
            }

            var result = new string[controlled.Count];
            controlled.CopyTo(result);
            return result;
        }
    }
}
