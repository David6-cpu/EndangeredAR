using System;
using EndangeredAR.Animals;
using EndangeredAR.Memory;
using UnityEngine;

namespace EndangeredAR.AI
{
    public interface IReadOnlyCharacterMemoryContextProvider
    {
        ReadOnlyCharacterMemoryContext CreateSnapshot(
            string animalId,
            ReadOnlyCharacterContext currentContext);
    }

    public sealed class ReadOnlyCharacterMemoryContextProvider : IReadOnlyCharacterMemoryContextProvider
    {
        private readonly CharacterMemoryService memory;
        private readonly AnimalCatalogService catalog;

        public ReadOnlyCharacterMemoryContextProvider(
            CharacterMemoryService memory,
            AnimalCatalogService catalog)
        {
            this.memory = memory;
            this.catalog = catalog;
        }

        public ReadOnlyCharacterMemoryContext CreateSnapshot(
            string animalId,
            ReadOnlyCharacterContext currentContext)
        {
            if (memory == null || catalog == null ||
                !catalog.TryGet(animalId, out var definition) || definition == null)
            {
                return ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }

            try
            {
                return CharacterMemoryContextFormatter.Format(
                    memory.Status,
                    memory.GetProjection(definition.AnimalId),
                    definition,
                    currentContext);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Read-only character Memory context unavailable ({exception.GetType().Name}).");
                return ReadOnlyCharacterMemoryContext.UnavailableFor(definition.AnimalId);
            }
        }
    }
}
