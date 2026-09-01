#if UNITY_EDITOR || DEVELOPMENT_BUILD
using EndangeredAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.Development
{
    internal sealed class SafeResearchPairCapturePanel : MonoBehaviour
    {
        private readonly Color panelColor = new Color(0.08f, 0.11f, 0.16f, 0.96f);
        private readonly Color actionColor = new Color(0.20f, 0.42f, 0.62f, 1f);
        private readonly Color mutedColor = new Color(0.25f, 0.29f, 0.35f, 1f);

        private GameObject collapsedRoot;
        private GameObject expandedRoot;
        private Text detailsText;

        private void Awake()
        {
            BuildInterface();
            SetExpanded(false);
        }

        private void BuildInterface()
        {
            var safeArea = CreateRect("Safe Pair Capture Safe Area", transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            collapsedRoot = CreateRect("Safe Pair Capture Collapsed", safeArea).gameObject;
            var collapsedRect = (RectTransform)collapsedRoot.transform;
            collapsedRect.anchorMin = Vector2.up;
            collapsedRect.anchorMax = Vector2.up;
            collapsedRect.pivot = Vector2.up;
            collapsedRect.anchoredPosition = new Vector2(304f, -24f);
            collapsedRect.sizeDelta = new Vector2(124f, 68f);
            var openButton = CreateButton(collapsedRect, "Pair", "PAIR", actionColor);
            Stretch(openButton.GetComponent<RectTransform>());
            openButton.onClick.AddListener(() => SetExpanded(true));

            expandedRoot = CreateRect("Safe Pair Capture Expanded", safeArea).gameObject;
            var expandedRect = (RectTransform)expandedRoot.transform;
            expandedRect.anchorMin = Vector2.up;
            expandedRect.anchorMax = Vector2.up;
            expandedRect.pivot = Vector2.up;
            expandedRect.anchoredPosition = new Vector2(24f, -104f);
            expandedRect.sizeDelta = new Vector2(720f, 500f);
            var background = expandedRoot.AddComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = true;

            var title = CreateText(expandedRect, "Title", "R3.4A.4 SAFE PAIR CAPTURE", 25, FontStyle.Bold);
            Place(title.rectTransform, 22f, -18f, 480f, 38f);
            detailsText = CreateText(
                expandedRect,
                "Details",
                "No approved validated completion is available.",
                20,
                FontStyle.Normal);
            Place(detailsText.rectTransform, 22f, -72f, 676f, 180f);

            var captureButton = CreateButton(
                expandedRect,
                "Capture Current Safe Pair",
                "CAPTURE CURRENT SAFE PAIR",
                actionColor);
            Place(captureButton.GetComponent<RectTransform>(), 22f, -270f, 676f, 72f);
            captureButton.onClick.AddListener(CaptureCurrent);

            var closeButton = CreateButton(expandedRect, "Close", "CLOSE", mutedColor);
            Place(closeButton.GetComponent<RectTransform>(), 22f, -360f, 676f, 64f);
            closeButton.onClick.AddListener(() => SetExpanded(false));
        }

        private void CaptureCurrent()
        {
            if (!SafeResearchPairCapture.TryCaptureCurrent(out var captured))
            {
                detailsText.text = "Capture rejected: " + SafeResearchPairCapture.LastFailure;
                return;
            }

            detailsText.text =
                "promptId: " + captured.PromptId + "\n" +
                "completionId: " + captured.CompletionId + "\n" +
                "finalSource: " + captured.FinalSource + "\n" +
                "answerMode: " + captured.AnswerMode + "\n" +
                "contentAuthority: " + captured.ContentAuthority + "\n" +
                "validation: " + captured.ValidationResult + "\n" +
                "persistence: none";
        }

        private void SetExpanded(bool expanded)
        {
            collapsedRoot.SetActive(!expanded);
            expandedRoot.SetActive(expanded);
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

        private static Text CreateText(Transform parent, string name, string value, int size, FontStyle style)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
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
