using System;
using EndangeredAR.Models;
using EndangeredAR.Missions;
using EndangeredAR.Progress;
using UnityEngine;

namespace EndangeredAR.Animals
{
    public enum AnimalSelectionStatus
    {
        Selected,
        NewlyUnlocked,
        Locked,
        UnknownAnimal
    }

    public readonly struct AnimalSelectionResult
    {
        public AnimalSelectionResult(AnimalSelectionStatus status, AnimalDefinition animal)
        {
            Status = status;
            Animal = animal;
        }

        public AnimalSelectionStatus Status { get; }
        public AnimalDefinition Animal { get; }
        public bool IsSuccess => Status == AnimalSelectionStatus.Selected ||
                                 Status == AnimalSelectionStatus.NewlyUnlocked;
    }

    public sealed class AnimalExperienceController : MonoBehaviour
    {
        [SerializeField] private AnimalCatalogService animalCatalogService;
        [SerializeField] private AnimalProgressService animalProgressService;
        [SerializeField] private MissionController missionController;
        [SerializeField] private AnimalModelLoader modelLoader;
        [SerializeField] private Transform experienceHostTransform;

        private bool initialized;

        public event Action<AnimalDefinition> CurrentAnimalChanged;

        public AnimalDefinition CurrentAnimal { get; private set; }
        public AnimalProgressRecord CurrentProgress => CurrentAnimal == null || animalProgressService == null
            ? null
            : animalProgressService.GetOrCreate(CurrentAnimal.AnimalId);

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            animalCatalogService?.Initialize();
            animalProgressService?.Initialize();
        }

        public AnimalSelectionResult Prepare(string animalId)
        {
            if (!TryGetDefinition(animalId, out var definition))
            {
                return Unknown();
            }

            return Select(definition, AnimalSelectionStatus.Selected);
        }

        public AnimalSelectionResult SelectFromScan(string animalId)
        {
            if (!TryGetDefinition(animalId, out var definition))
            {
                return Unknown();
            }

            var isNewlyUnlocked = animalProgressService.Unlock(definition.AnimalId);
            return Select(
                definition,
                isNewlyUnlocked ? AnimalSelectionStatus.NewlyUnlocked : AnimalSelectionStatus.Selected);
        }

        public AnimalSelectionResult SelectFromCatalog(string animalId)
        {
            if (!TryGetDefinition(animalId, out var definition))
            {
                return Unknown();
            }

            if (!animalProgressService.IsUnlocked(definition.AnimalId))
            {
                return new AnimalSelectionResult(AnimalSelectionStatus.Locked, definition);
            }

            return Select(definition, AnimalSelectionStatus.Selected);
        }

        private bool TryGetDefinition(string animalId, out AnimalDefinition definition)
        {
            definition = null;
            Initialize();

            if (animalCatalogService == null ||
                animalProgressService == null ||
                missionController == null ||
                modelLoader == null ||
                experienceHostTransform == null)
            {
                return false;
            }

            return animalCatalogService.TryGet(animalId, out definition);
        }

        private AnimalSelectionResult Select(AnimalDefinition definition, AnimalSelectionStatus status)
        {
            var selectedProgress = animalProgressService.GetOrCreate(definition.AnimalId);
            if (selectedProgress == null)
            {
                return Unknown();
            }

            var animalChanged = CurrentAnimal == null ||
                                !string.Equals(CurrentAnimal.AnimalId, definition.AnimalId, StringComparison.OrdinalIgnoreCase);
            if (animalChanged)
            {
                missionController.Configure(null);
            }

            CurrentAnimal = definition;
            missionController.Configure(definition.Mission, selectedProgress.missionCompleted);
            modelLoader.Configure(definition);
            experienceHostTransform.position = definition.ExperiencePosition;
            CurrentAnimalChanged?.Invoke(definition);

            return new AnimalSelectionResult(status, definition);
        }

        private static AnimalSelectionResult Unknown()
        {
            return new AnimalSelectionResult(AnimalSelectionStatus.UnknownAnimal, null);
        }
    }
}
