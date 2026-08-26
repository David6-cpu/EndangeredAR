#if UNITY_EDITOR || DEVELOPMENT_BUILD
using EndangeredAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.Development
{
    public sealed class AIProvenanceDebugPanel : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.25f;
        private readonly Color panelColor = new Color(0.08f, 0.11f, 0.16f, 0.95f);
        private readonly Color actionColor = new Color(0.20f, 0.42f, 0.62f, 1f);
        private readonly Color mutedColor = new Color(0.25f, 0.29f, 0.35f, 1f);

        private GameObject collapsedRoot;
        private GameObject expandedRoot;
        private Text detailsText;
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
            var safeArea = CreateRect("AI Route Safe Area", transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            collapsedRoot = CreateRect("AI Route Collapsed", safeArea).gameObject;
            var collapsedRect = (RectTransform)collapsedRoot.transform;
            collapsedRect.anchorMin = Vector2.up;
            collapsedRect.anchorMax = Vector2.up;
            collapsedRect.pivot = Vector2.up;
            collapsedRect.anchoredPosition = new Vector2(164f, -24f);
            collapsedRect.sizeDelta = new Vector2(124f, 68f);
            var routeButton = CreateButton(collapsedRect, "AI", "AI", actionColor);
            Stretch(routeButton.GetComponent<RectTransform>());
            routeButton.onClick.AddListener(() => SetExpanded(true));

            expandedRoot = CreateRect("AI Route Expanded", safeArea).gameObject;
            var expandedRect = (RectTransform)expandedRoot.transform;
            expandedRect.anchorMin = Vector2.up;
            expandedRect.anchorMax = Vector2.up;
            expandedRect.pivot = Vector2.up;
            expandedRect.anchoredPosition = new Vector2(24f, -104f);
            expandedRect.sizeDelta = new Vector2(640f, 640f);
            var background = expandedRoot.AddComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = true;

            var title = CreateText(expandedRect, "Title", "AI ROUTE PROVENANCE", 25, FontStyle.Bold);
            Place(title.rectTransform, 22f, -18f, 596f, 38f);
            detailsText = CreateText(expandedRect, "Route Details", "No completed reply", 20, FontStyle.Normal);
            Place(detailsText.rectTransform, 22f, -64f, 596f, 490f);

            var collapseButton = CreateButton(expandedRect, "Collapse", "COLLAPSE", mutedColor);
            Place(collapseButton.GetComponent<RectTransform>(), 22f, -568f, 596f, 48f);
            collapseButton.onClick.AddListener(() => SetExpanded(false));
        }

        private void RefreshStatus()
        {
            var value = AIResponseProvenanceRecorder.Latest;
            if (value == null)
            {
                detailsText.text = "No completed reply";
                return;
            }

            var attempts = value.ProviderAttempts.Count == 0
                ? "none"
                : string.Join(" > ", value.ProviderAttempts);
            detailsText.text =
                "finalSource: " + value.FinalSourceWireValue + "\n" +
                "answerMode: " + value.AnswerMode + "\n" +
                "routeMode: " + value.RouteMode + "\n" +
                "contentAuthority: " + value.ContentAuthority + "\n" +
                "languageGenerator: " + value.LanguageGenerator + "\n" +
                "providerAttempt: " + attempts + "\n" +
                "groundingTopic: " + value.GroundingTopic + "\n" +
                "memoryMentionPolicy: " + value.MemoryMentionPolicy + "\n" +
                "memoryStatus: " + value.MemoryStatus + "\n" +
                "fallbackUsed: " + (value.FallbackUsed ? "yes" : "no") + "\n" +
                "fallbackReason: " + (string.IsNullOrEmpty(value.FallbackReasonCode) ? "none" : value.FallbackReasonCode) + "\n" +
                "errorCode: " + (string.IsNullOrEmpty(value.ErrorCode) ? "none" : value.ErrorCode) + "\n" +
                "elapsedMs: " + value.ElapsedMilliseconds;
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
            text.alignment = TextAnchor.UpperLeft;
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
