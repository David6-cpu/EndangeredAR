using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EndangeredAR.Animals
{
    public sealed class AnimalCatalog
    {
        private readonly Dictionary<string, AnimalDefinition> definitionsById =
            new Dictionary<string, AnimalDefinition>(StringComparer.OrdinalIgnoreCase);

        public AnimalCatalog(IEnumerable<AnimalDefinition> source)
        {
            var animals = new List<AnimalDefinition>();
            var issues = new List<string>();

            if (source != null)
            {
                foreach (var definition in source)
                {
                    if (definition == null)
                    {
                        issues.Add("Animal definition is missing.");
                        continue;
                    }

                    var animalId = definition.AnimalId;
                    if (string.IsNullOrWhiteSpace(animalId))
                    {
                        issues.Add("Animal definition has a blank animal ID.");
                        continue;
                    }

                    if (!definition.IsConfigured)
                    {
                        issues.Add($"Animal definition '{animalId}' is not configured.");
                        continue;
                    }

                    if (definitionsById.ContainsKey(animalId))
                    {
                        issues.Add($"Animal definition '{animalId}' has a duplicate animal ID.");
                        continue;
                    }

                    definitionsById.Add(animalId, definition);
                    animals.Add(definition);
                }
            }

            Animals = new ReadOnlyCollection<AnimalDefinition>(animals);
            Issues = new ReadOnlyCollection<string>(issues);
        }

        public IReadOnlyList<AnimalDefinition> Animals { get; }
        public IReadOnlyList<string> Issues { get; }

        public bool TryGet(string animalId, out AnimalDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(animalId))
            {
                definition = null;
                return false;
            }

            return definitionsById.TryGetValue(animalId.Trim(), out definition);
        }
    }
}
