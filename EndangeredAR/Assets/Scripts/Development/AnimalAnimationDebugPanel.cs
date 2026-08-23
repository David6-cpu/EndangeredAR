#if UNITY_EDITOR || DEVELOPMENT_BUILD
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.Models;
using EndangeredAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.Development
{
    public sealed class AnimalAnimationDebugPanel : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.2f;
        private readonly Color panelColor = new Color(0.03f, 0.12f, 0.08f, 0.94f);
        private readonly Color actionColor = new Color(0.12f, 0.56f, 0.25f, 1f);
        private readonly Color mutedColor = new Color(0.23f, 0.31f, 0.26f, 1f);

        private GameObject collapsedRoot;
        private GameObject expandedRoot;
        private Text animalText;
        private Text animatorText;
        private Text resultText;
        private Button tauntButton;
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
            var safeArea = CreateRect("Safe Area", transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            collapsedRoot = CreateRect("Collapsed", safeArea).gameObject;
            var collapsedRect = (RectTransform)collapsedRoot.transform;
            collapsedRect.anchorMin = Vector2.one;
            collapsedRect.anchorMax = Vector2.one;
            collapsedRect.pivot = Vector2.one;
            collapsedRect.anchoredPosition = new Vector2(-24f, -24f);
            collapsedRect.sizeDelta = new Vector2(112f, 68f);
            var devButton = CreateButton(collapsedRect, "DEV", "DEV", actionColor);
            Stretch(devButton.GetComponent<RectTransform>());
            devButton.onClick.AddListener(() => SetExpanded(true));

            expandedRoot = CreateRect("Expanded", safeArea).gameObject;
            var expandedRect = (RectTransform)expandedRoot.transform;
            expandedRect.anchorMin = Vector2.one;
            expandedRect.anchorMax = Vector2.one;
            expandedRect.pivot = Vector2.one;
            expandedRect.anchoredPosition = new Vector2(-24f, -24f);
            expandedRect.sizeDelta = new Vector2(360f, 330f);
            var background = expandedRoot.AddComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = true;

            var title = CreateText(expandedRect, "Title", "ANIMATION DEV", 26, FontStyle.Bold);
            Place(title.rectTransform, 22f, -18f, 316f, 38f);
            animalText = CreateText(expandedRect, "Current Animal", "Animal: none", 22, FontStyle.Normal);
            Place(animalText.rectTransform, 22f, -67f, 316f, 34f);
            animatorText = CreateText(expandedRect, "Animator State", "Animator: unavailable", 22, FontStyle.Normal);
            Place(animatorText.rectTransform, 22f, -105f, 316f, 34f);
            resultText = CreateText(expandedRect, "Last Result", "Result: ready", 20, FontStyle.Normal);
            Place(resultText.rectTransform, 22f, -143f, 316f, 48f);

            tauntButton = CreateButton(expandedRect, "Play Taunt", "PLAY TAUNT", actionColor);
            Place(tauntButton.GetComponent<RectTransform>(), 22f, -205f, 316f, 56f);
            tauntButton.onClick.AddListener(RequestTaunt);

            var collapseButton = CreateButton(expandedRect, "Collapse", "COLLAPSE", mutedColor);
            Place(collapseButton.GetComponent<RectTransform>(), 22f, -273f, 316f, 42f);
            collapseButton.onClick.AddListener(() => SetExpanded(false));
        }

        private void RequestTaunt()
        {
            var loader = ResolveLoader();
            if (loader == null || !loader.TryGetCurrentModelController(out var controller))
            {
                resultText.text = "Result: no active rigged Sensen";
                RefreshStatus();
                return;
            }

            var result = controller.TryPlayAction(AIAction.Taunt);
            resultText.text = "Result: " + result;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            var loader = ResolveLoader();
            animalText.text = "Animal: " + (string.IsNullOrWhiteSpace(loader?.LoadedAnimalId) ? "none" : loader.LoadedAnimalId);

            if (loader != null && loader.TryGetCurrentModelController(out var controller))
            {
                animatorText.text = "Animator: " + controller.CurrentStateLabel;
                tauntButton.interactable = controller.CanRequestAction(AIAction.Taunt);
                return;
            }

            animatorText.text = "Animator: unavailable";
            tauntButton.interactable = false;
        }

        private static AnimalModelLoader ResolveLoader()
        {
            return Object.FindFirstObjectByType<AnimalModelLoader>(FindObjectsInactive.Exclude);
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

            var text = CreateText(rect, "Label", label, 22, FontStyle.Bold);
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
