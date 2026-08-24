using System;
using System.Collections.Generic;
using EndangeredAR.Animals;
using EndangeredAR.Progress;

namespace EndangeredAR.AI
{
    public interface IReadOnlyCharacterContextProvider
    {
        ReadOnlyCharacterContext CreateSnapshot(string animalId);
    }

    public sealed class ReadOnlyCharacterContextProvider : IReadOnlyCharacterContextProvider
    {
        private readonly AnimalProgressService progress;
        private readonly AnimalCatalogService catalog;

        public ReadOnlyCharacterContextProvider(
            AnimalProgressService progress,
            AnimalCatalogService catalog)
        {
            this.progress = progress;
            this.catalog = catalog;
        }

        public ReadOnlyCharacterContext CreateSnapshot(string animalId)
        {
            if (progress == null || catalog == null || !catalog.TryGet(animalId, out var definition) || definition == null)
            {
                return ReadOnlyCharacterContext.Empty;
            }

            progress.TryGetSnapshot(definition.AnimalId, out var record);
            var mission = definition.Mission;
            return ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState(
                    definition.AnimalId,
                    record != null && record.unlocked,
                    CountDistinct(record?.learnedKnowledgeIds),
                    CountDistinct(record?.earnedBadgeIds)),
                mission == null
                    ? ReadOnlyTaskState.Empty
                    : new ReadOnlyTaskState(
                        mission.MissionId,
                        mission.Title,
                        record != null && record.missionCompleted),
                ReadOnlyInteractionState.Empty);
        }

        private static int CountDistinct(IEnumerable<string> values)
        {
            if (values == null)
            {
                return 0;
            }

            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    distinct.Add(value.Trim());
                }
            }

            return distinct.Count;
        }
    }
}
