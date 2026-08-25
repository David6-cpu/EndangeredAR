#if UNITY_EDITOR || DEVELOPMENT_BUILD
using EndangeredAR.Animals;
using EndangeredAR.Memory;
using EndangeredAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.Development
{
    public sealed class CharacterMemoryDebugPanel : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.25f;
        private readonly Color panelColor = new Color(0.08f, 0.11f, 0.16f, 0.95f);
        private readonly Color actionColor = new Color(0.12f, 0.48f, 0.56f, 1f);
        private readonly Color destructiveColor = new Color(0.58f, 0.22f, 0.18f, 1f);
        private readonly Color mutedColor = new Color(0.25f, 0.29f, 0.35f, 1f);

        private GameObject collapsedRoot;
        private GameObject expandedRoot;
        private Text animalText;
        private Text statusText;
        private Text eventText;
        private Text projectionText;
        private Text resultText;
        private Button clearAnimalButton;
        private Button clearAllButton;
        private Button reloadButton;
        private float nextRefreshAt;

        private void Awake()
        {
            BuildInterface();
            SetExpanded(false);
            RefreshStatus();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.unscaledTime + RefreshIntervalSeconds;
            RefreshStatus();
        }

        private void BuildInterface()
        {
            var safeArea = CreateRect("Memory Safe Area", transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            collapsedRoot = CreateRect("Memory Collapsed", safeArea).gameObject;
            var collapsedRect = (RectTransform)collapsedRoot.transform;
            collapsedRect.anchorMin = Vector2.up;
            collapsedRect.anchorMax = Vector2.up;
            collapsedRect.pivot = Vector2.up;
            collapsedRect.anchoredPosition = new Vector2(24f, -24f);
            collapsedRect.sizeDelta = new Vector2(124f, 68f);
            var memoryButton = CreateButton(collapsedRect, "MEM", "MEM", actionColor);
            Stretch(memoryButton.GetComponent<RectTransform>());
            memoryButton.onClick.AddListener(() => SetExpanded(true));

            expandedRoot = CreateRect("Memory Expanded", safeArea).gameObject;
            var expandedRect = (RectTransform)expandedRoot.transform;
            expandedRect.anchorMin = Vector2.up;
            expandedRect.anchorMax = Vector2.up;
            expandedRect.pivot = Vector2.up;
            expandedRect.anchoredPosition = new Vector2(24f, -24f);
            expandedRect.sizeDelta = new Vector2(470f, 552f);
            var background = expandedRoot.AddComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = true;

            var title = CreateText(expandedRect, "Title", "CHARACTER MEMORY DEV", 25, FontStyle.Bold);
            Place(title.rectTransform, 22f, -18f, 426f, 38f);
            animalText = CreateText(expandedRect, "Current Animal", "Animal: none", 21, FontStyle.Normal);
            Place(animalText.rectTransform, 22f, -62f, 426f, 32f);
            statusText = CreateText(expandedRect, "Store Status", "Store: unavailable", 21, FontStyle.Normal);
            Place(statusText.rectTransform, 22f, -98f, 426f, 32f);
            eventText = CreateText(expandedRect, "Live Events", "Live events: 0", 21, FontStyle.Normal);
            Place(eventText.rectTransform, 22f, -134f, 426f, 32f);
            projectionText = CreateText(
                expandedRect,
                "Projection Counts",
                "Projection: D0 M0 K0 B0",
                20,
                FontStyle.Normal);
            Place(projectionText.rectTransform, 22f, -170f, 426f, 40f);
            resultText = CreateText(expandedRect, "Last Result", "Result: ready", 19, FontStyle.Normal);
            Place(resultText.rectTransform, 22f, -214f, 426f, 46f);

            clearAnimalButton = CreateButton(
                expandedRect,
                "Clear Animal Memory",
                "CLEAR CURRENT ANIMAL",
                destructiveColor);
            Place(clearAnimalButton.GetComponent<RectTransform>(), 22f, -274f, 426f, 54f);
            clearAnimalButton.onClick.AddListener(ClearCurrentAnimal);

            clearAllButton = CreateButton(
                expandedRect,
                "Clear All Memory",
                "CLEAR ALL MEMORY",
                destructiveColor);
            Place(clearAllButton.GetComponent<RectTransform>(), 22f, -340f, 426f, 54f);
            clearAllButton.onClick.AddListener(ClearAll);

            reloadButton = CreateButton(expandedRect, "Reload Memory", "RELOAD MEMORY", actionColor);
            Place(reloadButton.GetComponent<RectTransform>(), 22f, -406f, 426f, 54f);
            reloadButton.onClick.AddListener(Reload);

            var collapseButton = CreateButton(expandedRect, "Collapse Memory", "COLLAPSE", mutedColor);
            Place(collapseButton.GetComponent<RectTransform>(), 22f, -472f, 426f, 46f);
            collapseButton.onClick.AddListener(() => SetExpanded(false));
        }

        private void ClearCurrentAnimal()
        {
            if (!TryResolve(out var memory, out var animalId) || string.IsNullOrEmpty(animalId))
            {
                resultText.text = "Result: no current animal memory";
                RefreshStatus();
                return;
            }

            resultText.text = "Result: " + memory.ClearAnimalMemory(animalId);
            RefreshStatus();
        }

        private void ClearAll()
        {
            if (!TryResolve(out var memory, out _))
            {
                resultText.text = "Result: memory unavailable";
                RefreshStatus();
                return;
            }

            resultText.text = "Result: " + memory.ClearAllCharacterMemory();
            RefreshStatus();
        }

        private void Reload()
        {
            if (!TryResolve(out var memory, out _))
            {
                resultText.text = "Result: memory unavailable";
                RefreshStatus();
                return;
            }

            resultText.text = "Result: " + memory.ReloadForDevelopment();
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (!TryResolve(out var memory, out var animalId))
            {
                animalText.text = "Animal: none";
                statusText.text = "Store: unavailable";
                eventText.text = "Live events: 0";
                projectionText.text = "Projection: D0 M0 K0 B0";
                clearAnimalButton.interactable = false;
                clearAllButton.interactable = false;
                reloadButton.interactable = false;
                return;
            }

            animalText.text = "Animal: " + (string.IsNullOrEmpty(animalId) ? "none" : animalId);
            statusText.text = "Store: " + memory.Status;
            var projection = string.IsNullOrEmpty(animalId)
                ? CharacterMemoryProjection.Empty
                : memory.GetProjection(animalId);
            eventText.text = "Live events: " +
                             (string.IsNullOrEmpty(animalId) ? 0 : memory.GetLiveEventCount(animalId));
            projectionText.text = "Projection: D" + (projection.Discovered ? 1 : 0) +
                                  " M" + projection.CompletedMissionIds.Count +
                                  " K" + projection.LearnedKnowledgeIds.Count +
                                  " B" + projection.EarnedBadgeIds.Count;
            var available = memory.Status != CharacterMemoryStoreStatus.Unavailable &&
                            memory.Status != CharacterMemoryStoreStatus.FutureVersion;
            clearAnimalButton.interactable = available && !string.IsNullOrEmpty(animalId);
            clearAllButton.interactable = available;
            reloadButton.interactable = true;
        }

        private static bool TryResolve(out CharacterMemoryService memory, out string animalId)
        {
            memory = null;
            animalId = string.Empty;
            var experience = Object.FindFirstObjectByType<AnimalExperienceController>(FindObjectsInactive.Exclude);
            if (experience == null || experience.CharacterMemory == null)
            {
                return false;
            }

            memory = experience.CharacterMemory;
            animalId = experience.CurrentAnimal?.AnimalId ?? string.Empty;
            return true;
        }

        private void SetExpanded(bool expanded)
        {
            collapsedRoot.SetActive(!expanded);
            expandedRoot.SetActive(expanded);
            if (expanded)
            {
                RefreshStatus();
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(rect, "Label", label, 21, FontStyle.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle style)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
#endif
