using System;
using System.IO;
using EndangeredAR.Animals;
using EndangeredAR.Progress;

namespace EndangeredAR.Memory
{
    internal static class CharacterMemoryRuntimeComposition
    {
        internal static CharacterMemoryService Create(
            AnimalProgressService progress,
            AnimalCatalogService catalog)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            progress.Initialize();
            catalog.Initialize();
            if (string.IsNullOrEmpty(progress.ActiveRepositoryPath))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(progress.ActiveRepositoryPath);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            var repository = new JsonCharacterMemoryRepository(
                Path.Combine(directory, "character-memory.json"),
                () => DateTime.UtcNow);
            var source = new AnimalProgressMemorySnapshotSource(progress, catalog);
            var service = new CharacterMemoryService(repository, source);
            service.Initialize();
            progress.ConfigureTransitionSink(service);
            return service;
        }
    }
}
