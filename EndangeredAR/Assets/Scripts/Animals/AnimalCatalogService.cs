using UnityEngine;

namespace EndangeredAR.Animals
{
    public sealed class AnimalCatalogService : MonoBehaviour
    {
        [SerializeField] private AnimalDefinition[] definitions;
        [SerializeField] private string defaultAnimalId = "sensen";

        private bool initialized;

        public AnimalCatalog Catalog { get; private set; }
        public AnimalDefinition DefaultAnimal { get; private set; }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Catalog = new AnimalCatalog(definitions);

            if (!Catalog.TryGet(defaultAnimalId, out var defaultAnimal) && Catalog.Animals.Count > 0)
            {
                defaultAnimal = Catalog.Animals[0];
            }

            DefaultAnimal = defaultAnimal;

            foreach (var issue in Catalog.Issues)
            {
                Debug.LogWarning($"AnimalCatalogService: {issue}", this);
            }
        }

        public bool TryGet(string animalId, out AnimalDefinition definition)
        {
            Initialize();
            return Catalog.TryGet(animalId, out definition);
        }
    }
}
