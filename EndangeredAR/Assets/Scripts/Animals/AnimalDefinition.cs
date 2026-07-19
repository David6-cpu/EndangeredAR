using EndangeredAR.Missions;
using UnityEngine;

namespace EndangeredAR.Animals
{
    [CreateAssetMenu(menuName = "Endangered AR/Animal Definition")]
    public sealed class AnimalDefinition : ScriptableObject
    {
        [SerializeField] private string animalId;
        [SerializeField] private string displayName;
        [SerializeField] private string shortName;
        [SerializeField] private string scientificName;
        [SerializeField] private string markerName;
        [SerializeField] private string modelRelativePath;
        [SerializeField] private string baseColorTextureRelativePath;
        [SerializeField] private Vector3 experiencePosition;
        [SerializeField] private Vector3 modelLocalOffset;
        [SerializeField] private Vector3 modelEulerAngles;
        [SerializeField] private Vector3 modelScale = Vector3.one;
        [SerializeField] private string welcomeText;
        [SerializeField] private Color themeColor = Color.white;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Sprite lockedSilhouette;
        [SerializeField] private AnimalKnowledgeProfile knowledge;
        [SerializeField] private MissionDefinition mission;

        public string AnimalId => animalId?.Trim();
        public string DisplayName => displayName;
        public string ShortName => shortName;
        public string ScientificName => scientificName;
        public string MarkerName => markerName;
        public string ModelRelativePath => modelRelativePath;
        public string BaseColorTextureRelativePath => baseColorTextureRelativePath;
        public Vector3 ExperiencePosition => experiencePosition;
        public Vector3 ModelLocalOffset => modelLocalOffset;
        public Vector3 ModelEulerAngles => modelEulerAngles;
        public Vector3 ModelScale => modelScale;
        public string WelcomeText => welcomeText;
        public Color ThemeColor => themeColor;
        public Sprite Portrait => portrait;
        public Sprite LockedSilhouette => lockedSilhouette;
        public AnimalKnowledgeProfile Knowledge => knowledge;
        public MissionDefinition Mission => mission;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(AnimalId) && knowledge != null && mission != null;

        internal void Configure(
            string configuredAnimalId,
            string configuredDisplayName,
            string configuredShortName,
            string configuredScientificName,
            string configuredMarkerName,
            string configuredModelRelativePath,
            string configuredBaseColorTextureRelativePath,
            Vector3 configuredExperiencePosition,
            Vector3 configuredModelLocalOffset,
            Vector3 configuredModelEulerAngles,
            Vector3 configuredModelScale,
            string configuredWelcomeText,
            Color configuredThemeColor,
            Sprite configuredPortrait,
            Sprite configuredLockedSilhouette,
            AnimalKnowledgeProfile configuredKnowledge,
            MissionDefinition configuredMission)
        {
            animalId = configuredAnimalId;
            displayName = configuredDisplayName;
            shortName = configuredShortName;
            scientificName = configuredScientificName;
            markerName = configuredMarkerName;
            modelRelativePath = configuredModelRelativePath;
            baseColorTextureRelativePath = configuredBaseColorTextureRelativePath;
            experiencePosition = configuredExperiencePosition;
            modelLocalOffset = configuredModelLocalOffset;
            modelEulerAngles = configuredModelEulerAngles;
            modelScale = configuredModelScale;
            welcomeText = configuredWelcomeText;
            themeColor = configuredThemeColor;
            portrait = configuredPortrait;
            lockedSilhouette = configuredLockedSilhouette;
            knowledge = configuredKnowledge;
            mission = configuredMission;
        }
    }
}
