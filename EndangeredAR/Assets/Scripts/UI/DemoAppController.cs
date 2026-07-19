using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EndangeredAR.API;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using EndangeredAR.Missions;
using EndangeredAR.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EndangeredAR.UI
{
    public class DemoAppController : MonoBehaviour
    {
        [SerializeField] private ARImageScanController scanController;
        [SerializeField] private ChatApiClient chatApiClient;
        [SerializeField] private LocalKnowledgeChatService localChatService;
        [SerializeField] private MissionController missionController;
        [SerializeField] private GameObject animalPlaceholder;
        [SerializeField] private Behaviour arSession;
        [SerializeField] private Behaviour arCameraManager;
        [SerializeField] private Behaviour arCameraBackground;
        [SerializeField] private Camera displayCamera;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject scanPanel;
        [SerializeField] private GameObject learnPanel;
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private GameObject bottomNav;
        [SerializeField] private Text statusText;
        [SerializeField] private Text chatText;
        [SerializeField] private Text chatPageText;
        [SerializeField] private Text homeStatusText;
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private InputField chatInput;
        [SerializeField] private Button discoverButton;
        [SerializeField] private Button learnButton;
        [SerializeField] private Button chatButton;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button scanButton;
        [SerializeField] private Button backHomeButton;
        [SerializeField] private Button learnBackButton;
        [SerializeField] private Button chatBackButton;
        [SerializeField] private Button sendLocalChatButton;
        [SerializeField] private Button quickFoodButton;
        [SerializeField] private Button quickDangerButton;
        [SerializeField] private Button quickProtectButton;
        [SerializeField] private Button askFoodButton;
        [SerializeField] private Button askProtectButton;
        [SerializeField] private Font uiFont;
        [SerializeField] private float scanFallbackHintSeconds = 8f;
        [SerializeField] private AnimalProfile[] animalProfiles =
        {
            new AnimalProfile(
                "sensen",
                "缨冠灰叶猴 森森",
                "Models/Sensen/sensen.glb",
                "sensen_marker",
                "你好呀！我是缨冠灰叶猴森森。谢谢你愿意来到我的森林，今天我们一起认识我的食物、家和保护方法吧。",
                "帮森森寻找食物",
                new[]
                {
                    "缨冠灰叶猴主要吃嫩叶、果实和花朵。",
                    "完整森林能给缨冠灰叶猴提供食物、庇护和迁徙通道。",
                    "栖息地破碎、非法捕猎和种群隔离会让它们更加濒危。"
                }),
            new AnimalProfile(
                "animal_02",
                "濒危动物伙伴二号",
                "Models/Animal02/animal_02.glb",
                "animal_02_marker",
                "你好，我是新加入的濒危动物伙伴。先观察我的体态，再一起了解我的栖息地和保护行动吧。",
                "帮伙伴二号寻找安全栖息地",
                new[]
                {
                    "不同濒危动物需要不同的食物、栖息地和迁徙空间。",
                    "保护栖息地比单纯认识动物更重要。",
                    "每一次正确传播科普知识，都会让更多人关注生物多样性。"
                }),
            new AnimalProfile(
                "animal_03",
                "待解锁动物伙伴",
                "Models/Animal03/animal_03.glb",
                "animal_03_marker",
                "这位动物伙伴的模型还在准备中。你可以先体验占位展示，之后替换 GLB 就能正式登场。",
                "为待解锁伙伴准备保护计划",
                new[]
                {
                    "占位动物用于演示多动物扫描流程。",
                    "提供新 GLB 后，只需要替换模型路径即可接入。",
                    "多动物科普可以覆盖更多栖息地、食物链和保护主题。"
                })
        };

        private const string DefaultAnimalId = "sensen";
        private const string ThinkingLine = "正在想一想...";
        private const string UserAvatarFileName = "user_avatar.png";
        private const float CloudAnswerTimeoutSeconds = 40f;
        private const int MaxHistoryMessages = 10;

        private static readonly Color ForestDark = SensenDesignTokens.WithAlpha(SensenDesignTokens.Forest950, 0.96f);
        private static readonly Color Leaf = SensenDesignTokens.Leaf500;
        private static readonly Color Moss = SensenDesignTokens.Moss650;
        private static readonly Color Cream = SensenDesignTokens.Cream100;

        private readonly List<ChatMessage> chatHistory = new List<ChatMessage>();
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private bool isModelView;
        private bool isAnimalUnlocked;
        private bool isChatThinking;
        private bool missionCompleted;
        private bool earnedBadge;
        private int simulatedAnimalIndex;
        private string currentAnimalId = DefaultAnimalId;
        private string chatTranscript;
        private string lastLearnedFact = "完整森林能给缨冠灰叶猴提供食物、庇护和迁徙通道。";
        private Vector3 modelRestPosition;
        private Coroutine modelMotionRoutine;
        private Coroutine feedbackRoutine;
        private Coroutine cameraPermissionRoutine;
        private Coroutine scanFallbackHintRoutine;
        private Coroutine cloudAnswerTimeoutRoutine;

        private Canvas rootCanvas;
        private RectTransform safeAreaRoot;
        private RectTransform contentRoot;
        private RectTransform scanRoot;
        private Text appGuideText;
        private GameObject cameraPreviewPanel;
        private RawImage cameraPreviewImage;
        private AspectRatioFitter cameraPreviewAspect;
        private Text cameraScanHintText;
        private Text missionTitleText;
        private Text missionStatusText;
        private Text badgeText;
        private Text cardHeaderText;
        private Text cardModelHintText;
        private Text cardContentText;
        private Text cardSaveStatusText;
        private RectTransform cardCaptureRect;
        private GameObject missionPanel;
        private GameObject cardPanel;
        private Text profileNameText;
        private Text profileStatsText;
        private Text profileBadgeText;
        private Text profileCollectionText;
        private Text profileActionText;
        private Text profileAvatarStatusText;
        private GameObject modelChatBubble;
        private Text modelChatBubbleText;
        private GameObject modelChatInputBar;
        private Image profileAvatarImage;
        private Button missionButton;
        private Button cardButton;
        private Button missionBackButton;
        private Button cardSaveButton;
        private Button cardBackButton;
        private Button profileBackButton;
        private Button modelBackButton;
        private Button profileUseAvatarButton;
        private Button profileResetAvatarButton;
        private InputField profileAvatarPathInput;
        private Button leafyFoodButton;
        private Button snackFoodButton;
        private Button flowerFoodButton;
        private Button plasticFoodButton;
        private int lastUiButtonClickFrame = -1;
        private const float BottomNavHeight = 170f;
        private const float PageTitleHeight = 178f;

        private static string InitialChatTranscript =>
            "森森：你好呀！我是缨冠灰叶猴森森。谢谢你愿意来到我的森林，今天我们一起认识我的食物、家和保护方法吧。";

        private AnimalProfile CurrentAnimal => FindAnimalProfile(currentAnimalId);

        private void Awake()
        {
            if (displayCamera == null)
            {
                displayCamera = Camera.main;
            }

            if (localChatService == null)
            {
                localChatService = FindObjectOfType<LocalKnowledgeChatService>();
            }

            if (missionController == null)
            {
                missionController = FindObjectOfType<MissionController>();
            }

            if (missionController == null)
            {
                missionController = new GameObject("Mission Controller").AddComponent<MissionController>();
            }

            rootCanvas = FindObjectOfType<Canvas>();
            ConfigureCanvasScaler();
            EnsureEventSystem();
            EnsureCanvasRaycaster();
            EnsureSafeAreaLayout();
            ApplyChineseFont();
            SetCurrentAnimal(DefaultAnimalId, false);
            ResetSceneToStartupState();
            BuildRuntimeEnhancements();
            StyleExistingUi();
            ApplyGeneratedArtAssets();
            NormalizeMobileLayout();
            NormalizeRaycastTargets();
            WireButtons();

            if (scanController != null)
            {
                scanController.AnimalMarkerDetected += ShowAnimal;
                scanController.AnimalMarkerTracked += PlaceAnimalOnMarker;
            }

            EnterHomeView();
        }

        private void Start()
        {
            if (!isModelView)
            {
                EnterHomeView();
            }
        }

        private void LateUpdate()
        {
            if (Input.GetMouseButtonUp(0))
            {
                TryInvokeVisibleButtonAt(Input.mousePosition);
                return;
            }

            if (Input.touchCount <= 0)
            {
                return;
            }

            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Ended)
            {
                TryInvokeVisibleButtonAt(touch.position);
            }
        }

        private void OnDestroy()
        {
            if (scanController == null)
            {
                return;
            }

            scanController.AnimalMarkerDetected -= ShowAnimal;
            scanController.AnimalMarkerTracked -= PlaceAnimalOnMarker;
        }

        private void ApplyChineseFont()
        {
            if (uiFont == null)
            {
                return;
            }

            foreach (var text in FindObjectsOfType<Text>(true))
            {
                text.font = uiFont;
                text.supportRichText = false;
            }
        }

        private void ConfigureCanvasScaler()
        {
            if (rootCanvas == null)
            {
                return;
            }

            var scaler = rootCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1290f, 2796f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void EnsureSafeAreaLayout()
        {
            if (rootCanvas == null)
            {
                return;
            }

            safeAreaRoot = FindOrCreateStretchRect(rootCanvas.transform, "SafeAreaRoot");
            if (safeAreaRoot.GetComponent<SafeAreaFitter>() == null)
            {
                safeAreaRoot.gameObject.AddComponent<SafeAreaFitter>();
            }

            contentRoot = FindOrCreateStretchRect(safeAreaRoot, "ContentRoot");
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = new Vector2(0f, BottomNavHeight);
            contentRoot.offsetMax = Vector2.zero;

            scanRoot = FindOrCreateStretchRect(safeAreaRoot, "ScanRoot");
            scanRoot.anchorMin = Vector2.zero;
            scanRoot.anchorMax = Vector2.one;
            scanRoot.offsetMin = Vector2.zero;
            scanRoot.offsetMax = Vector2.zero;
            scanRoot.gameObject.SetActive(false);

            ReparentTo(homePanel, contentRoot);
            ReparentTo(learnPanel, contentRoot);
            ReparentTo(chatPanel, contentRoot);
            ReparentTo(profilePanel, contentRoot);
            ReparentTo(scanPanel, scanRoot);
            ReparentTo(bottomNav, safeAreaRoot);
            LayoutBottomNav();
        }

        private static RectTransform FindOrCreateStretchRect(Transform parent, string name)
        {
            var existing = parent.Find(name);
            var rect = existing == null ? null : existing.GetComponent<RectTransform>();
            if (rect == null)
            {
                var obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void ReparentTo(GameObject target, Transform parent)
        {
            if (target == null || parent == null)
            {
                return;
            }

            target.transform.SetParent(parent, false);
            var rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private void LayoutBottomNav()
        {
            if (bottomNav == null || safeAreaRoot == null)
            {
                return;
            }

            var rect = bottomNav.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, BottomNavHeight);
            rect.sizeDelta = new Vector2(0f, BottomNavHeight);
            bottomNav.transform.SetAsLastSibling();

            var image = bottomNav.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }

            var layout = bottomNav.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = bottomNav.AddComponent<HorizontalLayoutGroup>();
            }

            layout.padding = new RectOffset(64, 64, 22, 22);
            layout.spacing = 42f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ConfigureBottomNavButton(learnButton, 1f, 104f);
            ConfigureBottomNavButton(discoverButton, 1.15f, 132f);
            ConfigureBottomNavButton(profileButton, 1f, 104f);
            SetButtonVisible(chatButton, false);
        }

        private static void ConfigureBottomNavButton(Button button, float flexibleWidth, float height)
        {
            if (button == null)
            {
                return;
            }

            button.transform.SetParent(button.transform.parent, false);
            var element = button.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = button.gameObject.AddComponent<LayoutElement>();
            }

            element.flexibleWidth = flexibleWidth;
            element.preferredHeight = height;
            element.minHeight = height;
            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
            }

            eventSystem.enabled = true;
            EventSystem.current = eventSystem;

            var inputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            inputModule.enabled = true;
        }

        private void EnsureCanvasRaycaster()
        {
            if (rootCanvas == null)
            {
                return;
            }

            rootCanvas.enabled = true;
            var raycaster = rootCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            raycaster.enabled = true;
        }

        private void ResetSceneToStartupState()
        {
            SetPanelActive(homePanel, true);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(bottomNav, true);
            SetPanelActive(animalPlaceholder, false);
            SetButtonVisible(scanButton, true);
            SetButtonVisible(backHomeButton, false);
            SetButtonVisible(learnBackButton, false);
            SetButtonVisible(chatBackButton, false);
            SetButtonVisible(quickFoodButton, false);
            SetButtonVisible(quickDangerButton, false);
            SetButtonVisible(quickProtectButton, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
        }

        private void NormalizeRaycastTargets()
        {
            if (rootCanvas == null)
            {
                return;
            }

            foreach (var graphic in rootCanvas.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            foreach (var button in rootCanvas.GetComponentsInChildren<Button>(true))
            {
                var target = button.targetGraphic;
                if (target != null)
                {
                    target.raycastTarget = true;
                }
            }

            foreach (var input in rootCanvas.GetComponentsInChildren<InputField>(true))
            {
                var target = input.targetGraphic;
                if (target != null)
                {
                    target.raycastTarget = true;
                }
            }

            if (cameraPreviewImage != null)
            {
                cameraPreviewImage.raycastTarget = false;
            }
        }

        private void RefreshUiInteractionState()
        {
            EnsureEventSystem();
            EnsureCanvasRaycaster();
            NormalizeRaycastTargets();
            RaiseInteractiveControls();
            Canvas.ForceUpdateCanvases();
        }

        private void RaiseInteractiveControls()
        {
            RaiseIfActive(homePanel);
            RaiseIfActive(scanPanel);
            RaiseIfActive(learnPanel);
            RaiseIfActive(chatPanel);
            RaiseIfActive(profilePanel);
            RaiseIfActive(missionPanel);
            RaiseIfActive(cardPanel);
            RaiseIfActive(modelChatBubble);
            RaiseIfActive(modelChatInputBar);
            RaiseIfActive(bottomNav);
            RaiseIfActive(modelBackButton == null ? null : modelBackButton.gameObject);
            RaiseIfActive(missionButton == null ? null : missionButton.gameObject);
            RaiseIfActive(cardButton == null ? null : cardButton.gameObject);
        }

        private static void RaiseIfActive(GameObject target)
        {
            if (target != null && target.activeInHierarchy)
            {
                target.transform.SetAsLastSibling();
            }
        }

        private void WireButtons()
        {
            AddClick(discoverButton, "Scan clicked", StartScanMode);
            AddClick(learnButton, "Learn clicked", EnterLearnView);
            AddClick(profileButton, "Profile clicked", EnterProfileView);
            AddClick(scanButton, "Scan clicked", SimulateScan);
            AddClick(backHomeButton, "Back clicked", EnterHomeView);
            AddClick(learnBackButton, "Back clicked", EnterHomeView);
            AddClick(chatBackButton, "Back clicked", EnterHomeView);
            AddClick(profileBackButton, "Back clicked", EnterHomeView);
            AddClick(profileUseAvatarButton, "Profile clicked", UseAvatarFromInputPath);
            AddClick(profileResetAvatarButton, "Profile clicked", ResetCustomAvatar);
            AddClick(sendLocalChatButton, "Send clicked", () => AskLocal(chatInput == null ? string.Empty : chatInput.text));
            AddClick(modelBackButton, "Back clicked", EnterHomeView);
            AddClick(missionButton, "Food Mission clicked", EnterMissionView);
            AddClick(cardButton, "Card clicked", ShowCardPanel);
            AddClick(leafyFoodButton, "Food Mission clicked", () => SelectFood("嫩叶"));
            AddClick(snackFoodButton, "Food Mission clicked", () => SelectFood("薯片"));
            AddClick(flowerFoodButton, "Food Mission clicked", () => SelectFood("花朵"));
            AddClick(plasticFoodButton, "Food Mission clicked", () => SelectFood("塑料袋"));
        }

        private void AddClick(Button button, string debugLabel, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                lastUiButtonClickFrame = Time.frameCount;
                Debug.Log(debugLabel);
                action?.Invoke();
            });
        }

        private void TryInvokeVisibleButtonAt(Vector2 screenPosition)
        {
            if (lastUiButtonClickFrame == Time.frameCount)
            {
                return;
            }

            var buttons = new[]
            {
                sendLocalChatButton,
                modelBackButton,
                profileUseAvatarButton,
                profileResetAvatarButton,
                profileBackButton,
                leafyFoodButton,
                snackFoodButton,
                flowerFoodButton,
                plasticFoodButton,
                cardButton,
                missionButton,
                scanButton,
                backHomeButton,
                learnBackButton,
                chatBackButton,
                discoverButton,
                learnButton,
                profileButton
            };

            for (var i = 0; i < buttons.Length; i++)
            {
                if (TryInvokeButtonAt(buttons[i], screenPosition))
                {
                    return;
                }
            }
        }

        private bool TryInvokeButtonAt(Button button, Vector2 screenPosition)
        {
            if (button == null || !button.IsActive() || !button.interactable)
            {
                return false;
            }

            var rect = button.GetComponent<RectTransform>();
            if (rect == null)
            {
                return false;
            }

            var eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
            {
                return false;
            }

            button.onClick.Invoke();
            return true;
        }

        private void BuildRuntimeEnhancements()
        {
            if (rootCanvas == null)
            {
                return;
            }

            var safeParent = safeAreaRoot == null ? rootCanvas.transform : safeAreaRoot;
            var contentParent = contentRoot == null ? rootCanvas.transform : contentRoot;
            var scanParent = scanPanel == null ? safeParent : scanPanel.transform;

            missionButton = CreateButton(safeParent, "Food Mission Button", CurrentAnimal.FoodMissionText, new Vector2(-200, -585), new Vector2(320, 62), Leaf);
            cardButton = CreateButton(safeParent, "Knowledge Card Button", "生成科普卡片", new Vector2(200, -585), new Vector2(320, 62), Moss);
            appGuideText = CreateText(safeParent, "App Guide Text", "", new Vector2(0, 825), new Vector2(920, 70), 26, Cream, TextAnchor.MiddleCenter);

            BuildCameraScanPreview(scanParent);
            BuildModelChatUi(safeParent);
            BuildMissionPanel(contentParent);
            BuildCardPanel(safeParent);
            BuildLearnPanelAdaptive();
            BuildProfilePanel(contentParent);
            ApplyAdaptiveScanLayout();
            LayoutBottomNav();
        }

        private void BuildModelChatUi(Transform parent)
        {
            modelBackButton = CreateButton(parent, "Model Back Button", "‹", new Vector2(-430, 790), new Vector2(76, 76), new Color(0.97f, 0.99f, 0.94f, 0.94f));
            modelChatBubble = CreatePanel(parent, "Model Chat Bubble", new Vector2(0.56f, 0.52f), new Vector2(0.91f, 0.70f), new Color(0.97f, 0.99f, 0.94f, 0.94f));
            ApplyRoundedPanel(modelChatBubble, new Color(0.97f, 0.99f, 0.94f, 0.94f), 34f);
            var bubbleImage = modelChatBubble.GetComponent<Image>();
            if (bubbleImage != null)
            {
                bubbleImage.raycastTarget = false;
            }

            modelChatBubbleText = CreateText(modelChatBubble.transform, "Model Chat Bubble Text", "你好！我是动物伙伴。\n你可以直接问我问题。", Vector2.zero, new Vector2(315, 260), 24, new Color(0.09f, 0.14f, 0.12f), TextAnchor.MiddleLeft);
            var pointer = CreateSolidImage(modelChatBubble.transform, "Model Chat Pointer", new Vector2(-205, 10), new Vector2(34, 34), new Color(0.97f, 0.99f, 0.94f, 0.94f));
            pointer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            modelChatInputBar = CreatePanel(parent, "Model Chat Input Bar", new Vector2(0.065f, 0.055f), new Vector2(0.935f, 0.125f), new Color(0.98f, 1f, 0.96f, 0.96f));
            ApplyRoundedPanel(modelChatInputBar, new Color(0.98f, 1f, 0.96f, 0.96f), 42f);
            var inputBarImage = modelChatInputBar.GetComponent<Image>();
            if (inputBarImage != null)
            {
                inputBarImage.raycastTarget = false;
            }

            chatInput = CreateInputField(modelChatInputBar.transform, "Model Chat Input", "输入你想说的...", new Vector2(-110, 0), new Vector2(610, 66));
            sendLocalChatButton = CreateButton(modelChatInputBar.transform, "Model Chat Send Button", "发送", new Vector2(340, 0), new Vector2(150, 66), Leaf);

            modelChatBubble.SetActive(false);
            modelChatInputBar.SetActive(false);
            modelBackButton.gameObject.SetActive(false);
        }

        private void BuildCameraScanPreview(Transform parent)
        {
            HideExistingScanChildren(parent);
            cameraPreviewPanel = new GameObject("Camera Scan Preview", typeof(RectTransform));
            cameraPreviewPanel.transform.SetParent(parent, false);
            cameraPreviewPanel.transform.SetAsFirstSibling();
            var rect = cameraPreviewPanel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var feedObject = new GameObject("Camera Feed", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            feedObject.transform.SetParent(cameraPreviewPanel.transform, false);
            feedObject.transform.SetAsFirstSibling();
            cameraPreviewImage = feedObject.GetComponent<RawImage>();
            cameraPreviewImage.color = new Color(0.08f, 0.14f, 0.11f, 1f);
            cameraPreviewAspect = feedObject.GetComponent<AspectRatioFitter>();
            cameraPreviewAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cameraPreviewAspect.aspectRatio = 9f / 16f;
            var feedRect = feedObject.GetComponent<RectTransform>();
            feedRect.anchorMin = Vector2.zero;
            feedRect.anchorMax = Vector2.one;
            feedRect.offsetMin = Vector2.zero;
            feedRect.offsetMax = Vector2.zero;

            CreateSolidImage(cameraPreviewPanel.transform, "Scan Frame Top", new Vector2(0, 260), new Vector2(690, 8), new Color(0.36f, 0.84f, 0.52f, 0.92f));
            CreateSolidImage(cameraPreviewPanel.transform, "Scan Frame Bottom", new Vector2(0, -260), new Vector2(690, 8), new Color(0.36f, 0.84f, 0.52f, 0.92f));
            CreateSolidImage(cameraPreviewPanel.transform, "Scan Frame Left", new Vector2(-345, 0), new Vector2(8, 520), new Color(0.36f, 0.84f, 0.52f, 0.92f));
            CreateSolidImage(cameraPreviewPanel.transform, "Scan Frame Right", new Vector2(345, 0), new Vector2(8, 520), new Color(0.36f, 0.84f, 0.52f, 0.92f));
            cameraScanHintText = CreateText(cameraPreviewPanel.transform, "Camera Scan Hint", "将动物识别卡放入绿色框内", new Vector2(0, -355), new Vector2(900, 64), 30, Color.white, TextAnchor.MiddleCenter);
            CreateImage(cameraPreviewPanel.transform, "Sensen Marker Reference", "Markers/sensen_marker", new Vector2(0, 455), new Vector2(250, 156), new Color(1f, 1f, 1f, 0.88f), true);
            var instructionCard = CreateFixedPanel(cameraPreviewPanel.transform, "Scan Instruction Card", new Vector2(0, -600), new Vector2(940, 170), new Color(0.02f, 0.13f, 0.09f, 0.78f), 34);
            CreateText(instructionCard.transform, "Scan Instruction Text", "正在扫描动物识别卡\n识别成功后，动物伙伴会自动出现。", Vector2.zero, new Vector2(860, 118), 28, Color.white, TextAnchor.MiddleCenter);
            cameraPreviewPanel.SetActive(false);
        }

        private void HideExistingScanChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (scanButton != null && child == scanButton.transform)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private void ApplyAdaptiveScanLayout()
        {
            if (scanPanel == null)
            {
                return;
            }

            ReparentTo(scanPanel, scanRoot == null ? safeAreaRoot : scanRoot);
            StylePanel(scanPanel, new Color(0.02f, 0.08f, 0.06f, 1f));
            if (scanButton == null)
            {
                return;
            }

            scanButton.transform.SetParent(scanPanel.transform, false);
            var rect = scanButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(760f, 96f);
                rect.anchoredPosition = new Vector2(0f, 82f);
                rect.localScale = Vector3.one;
            }

            StyleButton(scanButton, Leaf);
            scanButton.transform.SetAsLastSibling();
        }

        private void BuildMissionPanel(Transform parent)
        {
            missionPanel = CreatePanel(parent, "Mission Panel", new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f), ForestDark);
            ApplyRoundedPanel(missionPanel, ForestDark, 34f);
            missionTitleText = CreateText(missionPanel.transform, "Mission Title", CurrentAnimal.FoodMissionText, new Vector2(0, 575), new Vector2(820, 72), 46, Color.white, TextAnchor.MiddleCenter);
            missionTitleText.fontStyle = FontStyle.Bold;
            missionStatusText = CreateText(missionPanel.transform, "Mission Status", $"森林餐桌打开啦。请选择适合{CurrentAnimal.ShortName}的食物。", new Vector2(0, 450), new Vector2(820, 120), 30, Cream, TextAnchor.MiddleCenter);
            leafyFoodButton = CreateButton(missionPanel.transform, "Leaf Food Button", "嫩叶", new Vector2(-220, 260), new Vector2(330, 92), Leaf);
            snackFoodButton = CreateButton(missionPanel.transform, "Snack Food Button", "薯片", new Vector2(220, 260), new Vector2(330, 92), Moss);
            flowerFoodButton = CreateButton(missionPanel.transform, "Flower Food Button", "花朵", new Vector2(-220, 120), new Vector2(330, 92), Leaf);
            plasticFoodButton = CreateButton(missionPanel.transform, "Plastic Food Button", "塑料袋", new Vector2(220, 120), new Vector2(330, 92), Moss);
            badgeText = CreateText(missionPanel.transform, "Badge Text", "奖励：生态守护者徽章待解锁", new Vector2(0, -40), new Vector2(820, 100), 30, new Color(1f, 0.88f, 0.42f), TextAnchor.MiddleCenter);
            missionBackButton = CreateButton(missionPanel.transform, "Mission Back Button", "返回展示", new Vector2(0, -360), new Vector2(760, 82), new Color(0.23f, 0.42f, 0.36f));
            AddClick(missionBackButton, "Back clicked", EnterModelView);
            missionPanel.SetActive(false);
        }

        private void BuildCardPanel(Transform parent)
        {
            cardPanel = CreatePanel(parent, "Knowledge Card Panel", new Vector2(0.075f, 0.055f), new Vector2(0.925f, 0.93f), new Color(0.92f, 0.95f, 0.86f, 0.96f));
            ApplyRoundedPanel(cardPanel, new Color(0.92f, 0.95f, 0.86f, 0.96f), 34f);
            cardCaptureRect = cardPanel.GetComponent<RectTransform>();
            CreateImage(cardPanel.transform, "Card Soft Glow", "Cards/card-soft-glow", new Vector2(0, 510), new Vector2(330, 330), new Color(1f, 1f, 1f, 0.62f), true);
            CreateImage(cardPanel.transform, "Card Leaf Corner", "Cards/card-leaf-corner", new Vector2(-315, -500), new Vector2(165, 165), new Color(1f, 1f, 1f, 0.68f), true);
            var cardHeroSurface = CreatePanel(cardPanel.transform, "Card Hero Surface", new Vector2(0.08f, 0.71f), new Vector2(0.92f, 0.96f), new Color(0.06f, 0.17f, 0.12f, 0.94f));
            ApplyRoundedPanel(cardHeroSurface, new Color(0.06f, 0.17f, 0.12f, 0.94f), 24f);
            CreateImage(cardPanel.transform, "Card Sensen Avatar", "Characters/character-sensen-avatar", new Vector2(-245, 470), new Vector2(176, 176), Color.white, true);
            cardHeaderText = CreateText(cardPanel.transform, "Card Header", $"今日认识了{CurrentAnimal.ShortName}", new Vector2(105, 520), new Vector2(500, 64), 40, Color.white, TextAnchor.MiddleLeft);
            cardHeaderText.fontStyle = FontStyle.Bold;
            CreateText(cardPanel.transform, "Card Subtitle", "一张属于你的生态守护记录", new Vector2(105, 464), new Vector2(500, 40), 22, new Color(0.82f, 0.93f, 0.82f), TextAnchor.MiddleLeft);
            CreateImage(cardPanel.transform, "Card Badge Icon", "Badges/badge-eco-guardian", new Vector2(290, 380), new Vector2(110, 110), Color.white, true);
            cardModelHintText = CreateText(cardPanel.transform, "Card Model Hint", $"{CurrentAnimal.ShortName}：谢谢你愿意了解我的森林", new Vector2(-30, 360), new Vector2(610, 42), 21, new Color(0.9f, 0.96f, 0.86f), TextAnchor.MiddleLeft);

            var cardContentSurface = CreatePanel(cardPanel.transform, "Card Content Surface", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.66f), new Color(0.97f, 0.99f, 0.92f, 0.98f));
            ApplyRoundedPanel(cardContentSurface, new Color(0.97f, 0.99f, 0.92f, 0.98f), 24f);
            cardContentText = CreateText(cardPanel.transform, "Card Content", "", new Vector2(0, 30), new Vector2(710, 360), 22, new Color(0.03f, 0.1f, 0.07f), TextAnchor.MiddleLeft);
            cardSaveStatusText = CreateText(cardPanel.transform, "Card Save Status", "", new Vector2(0, -330), new Vector2(780, 72), 19, new Color(0.12f, 0.25f, 0.17f), TextAnchor.MiddleCenter);
            cardSaveButton = CreateButton(cardPanel.transform, "Save Card Button", "保存 PNG", new Vector2(-205, -455), new Vector2(350, 76), Leaf);
            cardBackButton = CreateButton(cardPanel.transform, "Card Back Button", "返回展示", new Vector2(205, -455), new Vector2(350, 76), Moss);
            AddClick(cardSaveButton, "Card clicked", SaveKnowledgeCard);
            AddClick(cardBackButton, "Back clicked", EnterModelView);
            cardPanel.SetActive(false);
        }

        private void BuildLearnPanelAdaptive()
        {
            if (learnPanel == null)
            {
                return;
            }

            ReparentTo(learnPanel, contentRoot);
            ClearChildren(learnPanel.transform);
            StylePanel(learnPanel, new Color(0.83f, 0.92f, 0.76f, 1f));
            var content = CreateScrollPage(learnPanel, "学习中心", "认识濒危动物的栖息地、食物和保护行动");
            CreateInfoCard(content, "Learn Progress Card", "Icons/info-daily-fact", "Learn Progress", 150f, 24, out _);
            CreateInfoCard(content, "Species Learn Card", "Icons/info-endangered-level", "Species Card", 176f, 24, out _);
            CreateInfoCard(content, "Habitat Learn Card", "Icons/info-habitat", "Mission Card", 176f, 24, out _);
            CreateInfoCard(content, "Threat Learn Card", "Icons/info-threat", "Crisis Card", 196f, 23, out _);
            CreateInfoCard(content, "Fun Learn Card", "Icons/info-fun", "Fun Card", 176f, 24, out _);
            learnPanel.SetActive(false);
        }

        private void BuildProfilePanel(Transform parent)
        {
            if (profilePanel != null)
            {
                ClearChildren(profilePanel.transform);
                ReparentTo(profilePanel, contentRoot == null ? parent : contentRoot);
            }
            else
            {
                profilePanel = CreatePanel(parent, "Profile Panel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0.83f, 0.92f, 0.76f, 1f));
            }

            StylePanel(profilePanel, new Color(0.83f, 0.92f, 0.76f, 1f));
            var content = CreateScrollPage(profilePanel, "用户中心", "记录你的生态守护进度");
            profileNameText = CreateInfoCard(content, "Profile Level Card", "Characters/character-sensen-avatar", "Profile Name Text", 184f, 31, out profileStatsText);
            profileBadgeText = CreateInfoCard(content, "Profile Badge Card", "Badges/badge-eco-guardian", "Profile Badge Text", 166f, 24, out _);
            profileCollectionText = CreateInfoCard(content, "Profile Collection Card", "Icons/state-unlocked", "Profile Collection Text", 166f, 24, out _);
            profileActionText = CreateInfoCard(content, "Profile Action Card", "Icons/info-daily-fact", "Profile Action Text", 190f, 22, out _);

            profileAvatarPathInput = null;
            profileUseAvatarButton = null;
            profileResetAvatarButton = null;
            profileBackButton = null;
            profileAvatarStatusText = null;
            profilePanel.SetActive(false);
            LoadSavedCustomAvatar();
            UpdateProfileContent();
        }

        private Transform CreateScrollPage(GameObject panel, string title, string subtitle)
        {
            var titleText = CreateAnchoredText(panel.transform, $"{title} Title", title, TextAnchor.MiddleCenter, 52, new Color(0.06f, 0.18f, 0.1f));
            titleText.fontStyle = FontStyle.Bold;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(48f, -90f);
            titleRect.offsetMax = new Vector2(-48f, -24f);

            var subtitleText = CreateAnchoredText(panel.transform, $"{title} Subtitle", subtitle, TextAnchor.MiddleCenter, 24, new Color(0.24f, 0.39f, 0.28f));
            var subtitleRect = subtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0f, 1f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.offsetMin = new Vector2(48f, -138f);
            subtitleRect.offsetMax = new Vector2(-48f, -92f);

            var scrollObject = new GameObject($"{title} Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = new Vector2(0f, -PageTitleHeight);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(78, 78, 24, 48);
            layout.spacing = 30f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            return content.transform;
        }

        private Text CreateInfoCard(Transform parent, string cardName, string iconAsset, string textName, float height, int fontSize, out Text detailText)
        {
            var card = CreateLayoutCard(parent, cardName, height);
            CreateLayoutIcon(card.transform, iconAsset);
            var textColumn = new GameObject("Text Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
            textColumn.transform.SetParent(card.transform, false);
            var columnLayout = textColumn.GetComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 8f;
            columnLayout.childAlignment = TextAnchor.MiddleLeft;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            var columnElement = textColumn.AddComponent<LayoutElement>();
            columnElement.flexibleWidth = 1f;

            var titleText = CreateLayoutText(textColumn.transform, textName, "", fontSize, FontStyle.Bold);
            detailText = CreateLayoutText(textColumn.transform, $"{textName} Detail", "", Mathf.Max(18, fontSize - 4), FontStyle.Normal);
            return titleText;
        }

        private GameObject CreateLayoutCard(Transform parent, string name, float height)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            card.transform.SetParent(parent, false);
            var image = card.GetComponent<Image>();
            image.color = new Color(0.96f, 0.99f, 0.91f, 0.96f);
            image.sprite = GetRoundedSprite(name, 128, 28);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var element = card.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;

            var layout = card.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(34, 36, 24, 24);
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return card;
        }

        private void CreateLayoutIcon(Transform parent, string assetKey)
        {
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(parent, false);
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = LoadUiSprite(assetKey);
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var element = iconObject.GetComponent<LayoutElement>();
            element.preferredWidth = 104f;
            element.preferredHeight = 104f;
            element.minWidth = 86f;
            element.minHeight = 86f;
        }

        private Text CreateLayoutText(Transform parent, string name, string value, int fontSize, FontStyle style)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(15, fontSize - 8);
            text.resizeTextMaxSize = fontSize;
            text.fontStyle = style;
            text.color = new Color(0.06f, 0.18f, 0.1f);
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            textObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            return text;
        }

        private GameObject CreateProfileCard(string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            CreateFixedPanel(profilePanel.transform, $"{name} Shadow", anchoredPosition + new Vector2(0f, -8f), size, new Color(0.15f, 0.27f, 0.18f, 0.16f), 34);
            return CreateFixedPanel(profilePanel.transform, name, anchoredPosition, size, color, 34);
        }

        private Text CreateAnchoredText(Transform parent, string name, string value, TextAnchor alignment, int fontSize, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.text = value;
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(16, fontSize - 12);
            text.resizeTextMaxSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void StyleExistingUi()
        {
            if (displayCamera != null)
            {
                displayCamera.clearFlags = CameraClearFlags.SolidColor;
                displayCamera.backgroundColor = new Color(0.35f, 0.39f, 0.34f);
            }

            StylePanel(homePanel, new Color(0.03f, 0.11f, 0.085f, 0.95f));
            StylePanel(learnPanel, ForestDark);
            StylePanel(chatPanel, ForestDark);
            StylePanel(profilePanel, new Color(0.83f, 0.92f, 0.76f, 1f));
            StylePanel(scanPanel, new Color(0.025f, 0.075f, 0.06f, 0.88f));
            StylePanel(bottomNav, new Color(0.015f, 0.045f, 0.04f, 0.98f));
            StyleButton(discoverButton, Leaf);
            StyleButton(learnButton, Moss);
            StyleButton(profileButton, Moss);
            SetButtonLabel(discoverButton, "扫描");
            SetButtonLabel(learnButton, "学习");
            SetButtonLabel(profileButton, "我的");
            StyleButton(scanButton, Leaf);
            StyleButton(backHomeButton, Moss);
            StyleButton(learnBackButton, Moss);
            StyleButton(chatBackButton, Moss);
            StyleButton(profileBackButton, Moss);
            StyleButton(modelBackButton, new Color(0.97f, 0.99f, 0.94f, 0.94f));
            StyleButton(profileUseAvatarButton, Leaf);
            StyleButton(profileResetAvatarButton, Moss);
            StyleButton(sendLocalChatButton, Leaf);
            SetButtonVisible(chatButton, false);
            SetButtonVisible(quickFoodButton, false);
            SetButtonVisible(quickDangerButton, false);
            SetButtonVisible(quickProtectButton, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            UpdateLearnContent();
            UpdateCardContent();
            UpdateProfileContent();
        }

        private void ApplyGeneratedArtAssets()
        {
            SetPanelBackground(homePanel, "Backgrounds/bg-home-forest", new Color(1f, 1f, 1f, 0.72f));
            SetPanelBackground(scanPanel, "Backgrounds/bg-discover-camera", new Color(1f, 1f, 1f, 0.78f));
            SetPanelBackground(chatPanel, "Backgrounds/bg-chat-forest", new Color(1f, 1f, 1f, 0.9f));
            SetPanelBackground(learnPanel, "Backgrounds/bg-learn-panel", new Color(1f, 1f, 1f, 0.86f));
            SetPanelBackground(profilePanel, "Backgrounds/bg-learn-panel", new Color(1f, 1f, 1f, 0.86f));
            SetPanelBackground(cardPanel, "Backgrounds/bg-card-share", new Color(1f, 1f, 1f, 0.92f));

            AddButtonIcon(discoverButton, "Icons/tab-discover", 34f);
            AddButtonIcon(learnButton, "Icons/tab-learn", 34f);
            AddButtonIcon(profileButton, "Icons/tab-profile", 34f);
            AddButtonIcon(scanButton, "Icons/action-scan", 48f);
            AddButtonIcon(backHomeButton, "Icons/action-back", 40f);
            AddButtonIcon(learnBackButton, "Icons/action-back", 40f);
            AddButtonIcon(chatBackButton, "Icons/action-back", 40f);
            AddButtonIcon(profileBackButton, "Icons/action-back", 40f);
            AddButtonIcon(modelBackButton, "Icons/action-back", 40f);
            AddButtonIcon(sendLocalChatButton, "Icons/action-send", 36f);
            AddButtonIcon(missionButton, "Icons/action-food-mission", 38f);
            AddButtonIcon(cardButton, "Icons/action-card-generate", 38f);
            AddButtonIcon(missionBackButton, "Icons/action-back", 38f);
            AddButtonIcon(cardSaveButton, "Icons/action-save", 36f);
            AddButtonIcon(cardBackButton, "Icons/action-back", 36f);
            AddButtonIcon(leafyFoodButton, "Icons/food-leaf", 40f);
            AddButtonIcon(snackFoodButton, "Icons/food-snack-wrong", 40f);
            AddButtonIcon(flowerFoodButton, "Icons/food-flower", 40f);
            AddButtonIcon(plasticFoodButton, "Icons/food-plastic-wrong", 40f);

        }

        private void NormalizeMobileLayout()
        {
            EnsureSafeAreaLayout();
            LayoutBottomNav();
            SetButtonVisible(chatButton, false);
        }

        private void UpdateBottomNavSelection(Button activeButton)
        {
            StyleButton(learnButton, activeButton == learnButton ? Leaf : Moss);
            StyleButton(discoverButton, activeButton == discoverButton ? Leaf : Moss);
            StyleButton(profileButton, activeButton == profileButton ? Leaf : Moss);
            if (bottomNav != null)
            {
                bottomNav.transform.SetAsLastSibling();
            }

            learnButton?.transform.SetAsLastSibling();
            discoverButton?.transform.SetAsLastSibling();
            profileButton?.transform.SetAsLastSibling();
        }

        private void NormalizeBottomSafeArea()
        {
            if (bottomNav == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var rect = bottomNav.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            var safeBottom = Mathf.Clamp01(Screen.safeArea.yMin / Screen.height);
            rect.anchorMin = new Vector2(0f, safeBottom);
            rect.anchorMax = new Vector2(1f, Mathf.Min(1f, safeBottom + 0.12f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void EnterHomeView()
        {
            isModelView = false;
            SetPanelActive(homePanel, true);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(cameraPreviewPanel, false);
            SetModelChatVisible(false);
            SetPanelActive(bottomNav, true);
            SetPanelActive(missionPanel, false);
            SetPanelActive(cardPanel, false);
            SetPanelActive(animalPlaceholder, false);
            SetButtonVisible(scanButton, true);
            SetButtonVisible(backHomeButton, false);
            SetButtonVisible(missionButton, false);
            SetButtonVisible(cardButton, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            SetGuide("");
            SetHomeStatus("发现：扫描识别卡，解锁动物互动体验");
            DisableArCamera();
            UpdateBottomNavSelection(discoverButton);
            RefreshUiInteractionState();
        }

        private void EnterLearnView()
        {
            isModelView = false;
            SetPanelActive(homePanel, false);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, true);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(cameraPreviewPanel, false);
            SetModelChatVisible(false);
            SetPanelActive(bottomNav, true);
            SetPanelActive(missionPanel, false);
            SetPanelActive(cardPanel, false);
            SetPanelActive(animalPlaceholder, false);
            SetButtonVisible(backHomeButton, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            UpdateLearnContent();
            DisableArCamera();
            UpdateBottomNavSelection(learnButton);
            RefreshUiInteractionState();
        }

        private void StartScanMode()
        {
            isModelView = false;
            SetPanelActive(homePanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, true);
            SetPanelActive(scanPanel, true);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(cameraPreviewPanel, true);
            SetModelChatVisible(false);
            SetPanelActive(bottomNav, false);
            SetPanelActive(missionPanel, false);
            SetPanelActive(cardPanel, false);
            SetButtonVisible(scanButton, true);
            SetButtonVisible(backHomeButton, true);
            SetButtonVisible(missionButton, false);
            SetButtonVisible(cardButton, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            SetPanelActive(animalPlaceholder, false);
            SetButtonLabel(scanButton, "手动识别动物");
            SetButtonLabel(backHomeButton, "‹");
            SetButtonRect(backHomeButton, new Vector2(-430, 790), new Vector2(76, 76));
            SetText(statusText, "正在扫描动物识别卡");
            SetText(chatText, "将识别卡放进绿色框内。识别成功后，动物伙伴会自动出现；现场光线不稳时也可以手动进入演示。");
            SetText(cameraScanHintText, "将动物识别卡放入绿色框内");
            SetGuide("");
            EnableArCamera();
            StartScanFallbackHintTimer();
            RefreshUiInteractionState();
        }

        private void EnterChatView()
        {
            isModelView = false;
            SetPanelActive(homePanel, false);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, true);
            SetPanelActive(profilePanel, false);
            SetPanelActive(cameraPreviewPanel, false);
            SetModelChatVisible(false);
            SetPanelActive(bottomNav, true);
            SetPanelActive(missionPanel, false);
            SetPanelActive(cardPanel, false);
            SetPanelActive(animalPlaceholder, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            SetButtonVisible(backHomeButton, false);
            DisableArCamera();
            SetText(chatPageText, isAnimalUnlocked ? chatTranscript : "先去发现页识别动物伙伴，再来和它聊天吧。");
            if (chatInput != null)
            {
                chatInput.text = "";
            }

            SetChatInputEnabled(isAnimalUnlocked && !isChatThinking);
            SetGuide("");
            ScrollChatToBottom();
            RefreshUiInteractionState();
        }

        private void EnterProfileView()
        {
            isModelView = false;
            SetPanelActive(homePanel, false);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, true);
            SetPanelActive(cameraPreviewPanel, false);
            SetModelChatVisible(false);
            SetPanelActive(bottomNav, true);
            SetPanelActive(missionPanel, false);
            SetPanelActive(cardPanel, false);
            SetPanelActive(animalPlaceholder, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            SetButtonVisible(backHomeButton, false);
            UpdateProfileContent();
            DisableArCamera();
            SetGuide("");
            UpdateBottomNavSelection(profileButton);
            RefreshUiInteractionState();
        }

        private void EnterMissionView()
        {
            SetPanelActive(homePanel, false);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(cameraPreviewPanel, false);
            SetModelChatVisible(false);
            SetPanelActive(bottomNav, false);
            SetPanelActive(cardPanel, false);
            SetPanelActive(missionPanel, true);
            SetPanelActive(animalPlaceholder, true);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            missionController.StartFoodMission();
            SetText(missionTitleText, CurrentAnimal.FoodMissionText);
            SetText(missionStatusText, $"{CurrentAnimal.FoodMissionText}开始啦。请选择最适合的选项。");
            SetText(badgeText, missionCompleted ? "已获得：生态守护者徽章 +20" : "奖励：生态守护者徽章待解锁");
            SetGuide("");
            PulseModel();
            RefreshUiInteractionState();
        }

        private void ShowCardPanel()
        {
            SetPanelActive(homePanel, false);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(cameraPreviewPanel, false);
            SetModelChatVisible(false);
            SetPanelActive(bottomNav, false);
            SetPanelActive(missionPanel, false);
            SetPanelActive(cardPanel, true);
            SetPanelActive(animalPlaceholder, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            UpdateCardContent();
            SetGuide("");
            RefreshUiInteractionState();
        }

        private AnimalProfile GetNextSimulatedAnimal()
        {
            var profiles = GetValidAnimalProfiles();
            if (profiles.Length == 0)
            {
                return AnimalProfile.DefaultSensen;
            }

            var profile = profiles[simulatedAnimalIndex % profiles.Length];
            simulatedAnimalIndex = (simulatedAnimalIndex + 1) % profiles.Length;
            return profile;
        }

        private AnimalProfile[] GetValidAnimalProfiles()
        {
            if (animalProfiles == null || animalProfiles.Length == 0)
            {
                return new[] { AnimalProfile.DefaultSensen };
            }

            return animalProfiles;
        }

        private AnimalProfile FindAnimalProfile(string animalId)
        {
            var profiles = GetValidAnimalProfiles();
            foreach (var profile in profiles)
            {
                if (profile != null && string.Equals(profile.AnimalId, animalId, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            foreach (var profile in profiles)
            {
                if (profile != null && string.Equals(profile.AnimalId, DefaultAnimalId, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return AnimalProfile.DefaultSensen;
        }

        private void SetCurrentAnimal(string animalId, bool resetProgress)
        {
            var profile = FindAnimalProfile(animalId);
            var animalChanged = !string.Equals(currentAnimalId, profile.AnimalId, StringComparison.OrdinalIgnoreCase);
            currentAnimalId = profile.AnimalId;
            chatTranscript = $"{profile.ShortName}：{profile.IntroText}";
            lastLearnedFact = profile.PrimaryFact;

            if (resetProgress || animalChanged)
            {
                isAnimalUnlocked = false;
                missionCompleted = false;
                earnedBadge = false;
                chatHistory.Clear();
            }
        }

        private void SimulateScan()
        {
            StopScanFallbackHintTimer();
            var animal = GetNextSimulatedAnimal();
            SetText(statusText, $"已识别{animal.DisplayName}卡片");
            SetText(cameraScanHintText, $"识别成功，{animal.ShortName}正在出现...");
            if (scanController != null)
            {
                scanController.SimulateMarkerDetected(animal.AnimalId);
                return;
            }

            SetCurrentAnimal(animal.AnimalId, false);
            EnterModelView();
        }

        private void ShowAnimal(string animalId)
        {
            if (isModelView)
            {
                return;
            }

            SetCurrentAnimal(animalId, false);
            SetPanelActive(animalPlaceholder, true);
            SetText(statusText, $"{CurrentAnimal.ShortName}出现了！");
        }

        private void PlaceAnimalOnMarker(string animalId, Transform markerTransform)
        {
            SetCurrentAnimal(animalId, false);
            EnterModelView();
        }

        private void EnterModelView()
        {
            isModelView = true;
            StopScanFallbackHintTimer();
            UnlockAnimalChat();
            SetPanelActive(homePanel, false);
            SetPanelActive(scanPanel, false);
            SetPanelActive(scanRoot == null ? null : scanRoot.gameObject, false);
            SetPanelActive(learnPanel, false);
            SetPanelActive(chatPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(cameraPreviewPanel, false);
            SetModelChatVisible(true);
            SetPanelActive(bottomNav, false);
            SetPanelActive(missionPanel, false);
            SetPanelActive(cardPanel, false);
            SetButtonVisible(scanButton, false);
            SetButtonVisible(backHomeButton, false);
            SetButtonVisible(missionButton, true);
            SetButtonVisible(cardButton, true);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            SetChatInputEnabled(!isChatThinking);
            DisableArCamera();

            if (displayCamera != null)
            {
                displayCamera.clearFlags = CameraClearFlags.SolidColor;
                displayCamera.backgroundColor = new Color(0.78f, 0.88f, 0.68f);
                displayCamera.transform.position = new Vector3(0f, 1.2f, -5f);
                displayCamera.transform.rotation = Quaternion.LookRotation(new Vector3(-0.72f, 0.1f, 0f) - displayCamera.transform.position);
            }

            if (animalPlaceholder != null)
            {
                animalPlaceholder.transform.SetPositionAndRotation(new Vector3(-1.02f, -0.13f, 0f), Quaternion.identity);
                if (animalPlaceholder.transform.localScale.x < 0.95f || animalPlaceholder.transform.localScale.x > 1.25f)
                {
                    animalPlaceholder.transform.localScale = Vector3.one;
                }

                animalPlaceholder.SetActive(true);
                var loader = animalPlaceholder.GetComponent<SensenGlbLoader>();
                loader?.ConfigureModel(CurrentAnimal.ModelPath);
                var gesture = animalPlaceholder.GetComponent<AnimalGestureController>();
                gesture?.RefreshBaseScale();
                modelRestPosition = animalPlaceholder.transform.position;
                StartModelMotion();
            }

            SetButtonLabel(missionButton, CurrentAnimal.FoodMissionText);
            SetText(missionTitleText, CurrentAnimal.FoodMissionText);
            SetText(missionStatusText, $"森林餐桌打开啦。请选择适合{CurrentAnimal.ShortName}的食物。");
            SetText(statusText, $"{CurrentAnimal.ShortName}：谢谢你找到我！我们一起守护森林吧。");
            SetText(chatText, "");
            SetModelChatText($"{CurrentAnimal.IntroText}\n\n你可以用手指旋转、缩放观察我，也可以直接问我问题。");
            SetGuide("");
            StartCoroutine(WelcomeAfterReveal());
            RefreshUiInteractionState();
        }

        private IEnumerator WelcomeAfterReveal()
        {
            yield return new WaitForSeconds(0.4f);
            AddAssistantMessage($"你找到我啦！我是{CurrentAnimal.ShortName}。你可以直接在下面输入问题。", false);
        }

        private void UnlockAnimalChat()
        {
            if (isAnimalUnlocked)
            {
                return;
            }

            isAnimalUnlocked = true;
            chatTranscript = $"{CurrentAnimal.ShortName}：{CurrentAnimal.IntroText}";
            chatHistory.Clear();
            AddHistory("assistant", CurrentAnimal.IntroText);
        }

        private void AskLocal(string message)
        {
            if (!isAnimalUnlocked)
            {
                SetText(chatPageText, "先去发现页识别动物伙伴，再来和它聊天吧。");
                SetModelChatText("先扫描识别卡，就可以和我聊天啦。");
                return;
            }

            if (isChatThinking)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                AppendChatLine("提示", $"请输入一个想问{CurrentAnimal.ShortName}的问题。");
                return;
            }

            if (chatInput != null)
            {
                chatInput.text = "";
            }

            isChatThinking = true;
            SetChatInputEnabled(false);
            AppendChatLine("你", message);
            AppendChatLine(CurrentAnimal.ShortName, ThinkingLine);
            SetModelChatText(ThinkingLine);
            ScrollChatToBottom();
            StartCloudAnswerTimeout(message);

            if (chatApiClient == null)
            {
                FinishCloudAnswer(message, BuildFallbackReply(message), $"云端还没配置好，{CurrentAnimal.ShortName}先用本地知识陪你继续。");
                return;
            }

            StartCoroutine(chatApiClient.SendMessage(
                currentAnimalId,
                message,
                chatHistory.ToArray(),
                response => StartCoroutine(FinishCloudAnswerAfterDelay(message, response.reply, response.missionHint)),
                error => StartCoroutine(FinishCloudAnswerAfterDelay(message, BuildFallbackReply(message), $"云端暂时不稳定，{CurrentAnimal.ShortName}先用本地知识陪你继续。"))
            ));
        }

        private void AskModelPanel(string message)
        {
            SetText(chatText, $"你：{message}\n{CurrentAnimal.ShortName}：{ThinkingLine}");
            AskLocal(message);
            PulseModel();
        }

        private IEnumerator CloudAnswerTimeout(string userMessage)
        {
            yield return new WaitForSeconds(CloudAnswerTimeoutSeconds);
            cloudAnswerTimeoutRoutine = null;

            if (!isChatThinking)
            {
                yield break;
            }

            FinishCloudAnswer(userMessage, BuildFallbackReply(userMessage), $"网络有点慢，{CurrentAnimal.ShortName}先用本地知识回答你。");
        }

        private void StartCloudAnswerTimeout(string userMessage)
        {
            if (cloudAnswerTimeoutRoutine != null)
            {
                StopCoroutine(cloudAnswerTimeoutRoutine);
            }

            cloudAnswerTimeoutRoutine = StartCoroutine(CloudAnswerTimeout(userMessage));
        }

        private void StopCloudAnswerTimeout()
        {
            if (cloudAnswerTimeoutRoutine == null)
            {
                return;
            }

            StopCoroutine(cloudAnswerTimeoutRoutine);
            cloudAnswerTimeoutRoutine = null;
        }

        private IEnumerator FinishCloudAnswerAfterDelay(string userMessage, string reply, string missionHint = "")
        {
            yield return new WaitForSeconds(0.45f);
            FinishCloudAnswer(userMessage, reply, missionHint);
        }

        private void FinishCloudAnswer(string userMessage, string reply, string missionHint = "")
        {
            if (!isChatThinking && chatTranscript.LastIndexOf(ThinkingLine, StringComparison.Ordinal) < 0)
            {
                return;
            }

            StopCloudAnswerTimeout();

            if (string.IsNullOrWhiteSpace(reply))
            {
                reply = BuildFallbackReply(userMessage);
            }

            reply = SanitizeUserFacingReply(reply, userMessage);

            if (!string.IsNullOrWhiteSpace(missionHint))
            {
                reply = $"{reply}\n{missionHint}";
            }

            ReplaceLastThinkingLine($"{CurrentAnimal.ShortName}：{reply}");
            SetText(chatPageText, chatTranscript);
            SetText(chatText, "");
            SetModelChatText(reply);
            AddHistory("user", userMessage);
            AddHistory("assistant", reply);
            lastLearnedFact = ExtractLearnedFact(userMessage, reply);
            isChatThinking = false;
            SetChatInputEnabled(true);
            ScrollChatToBottom();
            PulseModel();
        }

        private string SanitizeUserFacingReply(string reply, string userMessage)
        {
            if (LooksTechnical(reply))
            {
                return BuildFallbackReply(userMessage);
            }

            return reply.Trim();
        }

        private static bool LooksTechnical(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IndexOf("HTTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("URL:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("UnityWebRequest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("api.moonshot", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildFallbackReply(string message)
        {
            if (!string.Equals(currentAnimalId, DefaultAnimalId, StringComparison.OrdinalIgnoreCase))
            {
                return $"我先用本地知识告诉你：{CurrentAnimal.PrimaryFact}\n你愿意继续帮我完成“{CurrentAnimal.FoodMissionText}”任务吗？";
            }

            var answer = localChatService == null
                ? new ChatAnswer("我还在整理这个问题，不过保护森林、拒绝投喂野生动物，就是帮我的好办法。", Array.Empty<string>(), false)
                : localChatService.Answer(message);

            var reply = answer.Reply;
            if (!reply.Contains("森森") && !reply.Contains("我"))
            {
                reply = $"我悄悄告诉你：{reply}";
            }

            return $"{reply}\n你愿意继续帮我完成“{CurrentAnimal.FoodMissionText}”任务吗？";
        }

        private void SelectFood(string option)
        {
            var result = missionController.SelectFood(option);
            SetText(missionStatusText, result.Feedback);
            lastLearnedFact = result.LearnedFact;

            if (result.Success)
            {
                missionCompleted = true;
                earnedBadge = true;
                SetText(badgeText, "已获得：生态守护者徽章 +20");
                AddAssistantMessage($"太棒啦！你帮{CurrentAnimal.ShortName}完成了任务。现在你已经是小小生态守护者了，可以生成一张科普卡片带走这段记录。", true);
                UpdateCardContent();
                UpdateProfileContent();
                PulseModel();
                return;
            }

            SetText(badgeText, "再试一次：野生动物需要天然食物。");
            PulseModel();
        }

        private void SaveKnowledgeCard()
        {
            StartCoroutine(SaveKnowledgeCardNextFrame());
        }

        private IEnumerator SaveKnowledgeCardNextFrame()
        {
            UpdateCardContent();
            SetText(cardSaveStatusText, "正在生成 PNG...");
            yield return new WaitForEndOfFrame();

            var screenTexture = ScreenCapture.CaptureScreenshotAsTexture();
            var rect = GetScreenRect(cardCaptureRect);
            var width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, screenTexture.width);
            var height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, screenTexture.height);
            var x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, screenTexture.width - width);
            var y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, screenTexture.height - height);

            var cardTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            cardTexture.SetPixels(screenTexture.GetPixels(x, y, width, height));
            cardTexture.Apply();

            var fileName = $"{currentAnimalId}_card_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(path, cardTexture.EncodeToPNG());
            Destroy(screenTexture);
            Destroy(cardTexture);
            SetText(cardSaveStatusText, $"已保存 PNG：{fileName}\n位置：应用数据目录");
        }

        private static Rect GetScreenRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return new Rect(0, 0, Screen.width, Screen.height);
            }

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var xMin = Mathf.Min(corners[0].x, corners[2].x);
            var xMax = Mathf.Max(corners[0].x, corners[2].x);
            var yMin = Mathf.Min(corners[0].y, corners[2].y);
            var yMax = Mathf.Max(corners[0].y, corners[2].y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private void StartModelMotion()
        {
            if (modelMotionRoutine != null)
            {
                StopCoroutine(modelMotionRoutine);
            }

            modelMotionRoutine = StartCoroutine(ModelBreathingLoop());
        }

        private IEnumerator ModelBreathingLoop()
        {
            while (animalPlaceholder != null && animalPlaceholder.activeInHierarchy)
            {
                var wave = Mathf.Sin(Time.time * 1.8f);
                animalPlaceholder.transform.position = modelRestPosition + Vector3.up * (wave * 0.025f);
                yield return null;
            }
        }

        private void PositionModelForCard()
        {
            if (animalPlaceholder == null)
            {
                return;
            }

            animalPlaceholder.transform.SetPositionAndRotation(new Vector3(-1.02f, -0.13f, 0f), Quaternion.identity);
            if (animalPlaceholder.transform.localScale.x < 0.75f)
            {
                animalPlaceholder.transform.localScale = Vector3.one * 0.9f;
            }

            var gesture = animalPlaceholder.GetComponent<AnimalGestureController>();
            gesture?.RefreshBaseScale();
            modelRestPosition = animalPlaceholder.transform.position;
            StartModelMotion();
        }

        private void PulseModel()
        {
            if (animalPlaceholder == null)
            {
                return;
            }

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(PulseModelRoutine());
        }

        private IEnumerator PulseModelRoutine()
        {
            var startScale = animalPlaceholder.transform.localScale;
            for (var i = 0; i < 18; i++)
            {
                var t = i / 17f;
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
                animalPlaceholder.transform.localScale = startScale * scale;
                animalPlaceholder.transform.Rotate(Vector3.up, Mathf.Sin(t * Mathf.PI * 2f) * 1.5f, Space.World);
                yield return null;
            }

            animalPlaceholder.transform.localScale = startScale;
        }

        private void AddAssistantMessage(string reply, bool addToHistory)
        {
            AppendChatLine(CurrentAnimal.ShortName, reply);
            SetModelChatText(reply);
            if (addToHistory)
            {
                AddHistory("assistant", reply);
            }
        }

        private void AppendChatLine(string speaker, string message)
        {
            chatTranscript += $"\n\n{speaker}：{message}";
            SetText(chatPageText, chatTranscript);
        }

        private void ReplaceLastThinkingLine(string replacement)
        {
            var thinkingLine = $"{CurrentAnimal.ShortName}：{ThinkingLine}";
            var index = chatTranscript.LastIndexOf(thinkingLine, StringComparison.Ordinal);
            if (index >= 0)
            {
                chatTranscript = chatTranscript.Remove(index, thinkingLine.Length).Insert(index, replacement);
                return;
            }

            chatTranscript += $"\n\n{replacement}";
        }

        private void AddHistory(string role, string content)
        {
            chatHistory.Add(new ChatMessage { role = role, content = content });
            while (chatHistory.Count > MaxHistoryMessages)
            {
                chatHistory.RemoveAt(0);
            }
        }

        private string ExtractLearnedFact(string userMessage, string reply)
        {
            userMessage = userMessage ?? string.Empty;
            reply = reply ?? string.Empty;

            if (!string.Equals(currentAnimalId, DefaultAnimalId, StringComparison.OrdinalIgnoreCase))
            {
                return CurrentAnimal.PrimaryFact;
            }

            if (userMessage.Contains("吃") || reply.Contains("嫩叶"))
            {
                return "缨冠灰叶猴主要吃嫩叶、果实和花朵，不能随意投喂人类零食。";
            }

            if (userMessage.Contains("森林") || userMessage.Contains("家") || reply.Contains("栖息"))
            {
                return "连续完整的森林能帮助森森找到食物、躲避危险并遇到同伴。";
            }

            if (userMessage.Contains("保护") || reply.Contains("保护"))
            {
                return "减少浪费、支持自然保护、传播正确知识，都是保护濒危动物的行动。";
            }

            return "认识濒危动物，是参与生态保护的第一步。";
        }

        private void UpdateLearnContent()
        {
            var texts = learnPanel == null ? Array.Empty<Text>() : learnPanel.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
            {
                if (text.name.Contains("Learn Progress"))
                {
                    text.text = earnedBadge ? "生态守护者 Lv.2    已完成今日任务" : "生态守护者 Lv.1    今日学习进度 60%";
                }
                else if (text.name.Contains("Species Card"))
                {
                    text.text = $"濒危档案\n{CurrentAnimal.DisplayName}\n{CurrentAnimal.GetFact(0)}";
                }
                else if (text.name.Contains("Mission Card"))
                {
                    text.text = $"任务与栖息地\n{CurrentAnimal.FoodMissionText}\n{CurrentAnimal.GetFact(1)}";
                }
                else if (text.name.Contains("Crisis Card"))
                {
                    text.text = $"生存威胁与今日知识\n{CurrentAnimal.GetFact(2)}\n今日知识：{lastLearnedFact}";
                }
                else if (text.name.Contains("Fun Card"))
                {
                    text.text = $"趣味知识\n观察 3D 模型时，可以单指旋转、双指缩放。\n当前伙伴：{CurrentAnimal.ShortName}";
                }
            }
        }

        private void UpdateCardContent()
        {
            if (cardContentText == null)
            {
                return;
            }

            var missionLine = missionCompleted
                ? $"已完成：{CurrentAnimal.FoodMissionText}"
                : isAnimalUnlocked
                    ? $"进行中：{CurrentAnimal.FoodMissionText}"
                    : $"待开始：先扫描{CurrentAnimal.ShortName}识别卡";
            var badgeLine = earnedBadge ? "生态守护者徽章已解锁" : "完成食物任务后解锁";

            SetText(cardHeaderText, $"今日认识了{CurrentAnimal.ShortName}");
            SetText(cardModelHintText, $"{CurrentAnimal.ShortName}：谢谢你愿意了解我的森林");
            cardContentText.text =
                $"今日认识了{CurrentAnimal.ShortName}\n\n"
                + $"体验：扫描识别卡，观察{CurrentAnimal.DisplayName}的 3D 形态。\n"
                + $"学习：{lastLearnedFact}\n"
                + $"任务：{missionLine}\n"
                + $"徽章：{badgeLine}\n\n"
                + "分享行动：不投喂野生动物，把森林保护知识告诉更多人。";
        }

        private void UpdateProfileContent()
        {
            var unlockedCount = isAnimalUnlocked ? 1 : 0;
            var missionPoints = missionController == null ? 0 : missionController.Points;
            var totalPoints = missionPoints + (isAnimalUnlocked ? 10 : 0);
            var level = earnedBadge ? 2 : 1;
            var nextAction = earnedBadge
                ? "今日行动：把一条森林保护知识告诉朋友，继续解锁新的动物伙伴。"
                : isAnimalUnlocked
                    ? $"今日行动：完成“{CurrentAnimal.FoodMissionText}”任务，领取生态守护者徽章。"
                    : $"今日行动：先去发现页识别{CurrentAnimal.ShortName}卡片，开启你的第一段 AR 科普记录。";

            SetText(profileNameText, $"生态守护者 Lv.{level}");
            SetText(profileStatsText, $"积分 {totalPoints}    已解锁动物 {unlockedCount}/{GetValidAnimalProfiles().Length}\n学习进度 {(earnedBadge ? "100%" : isAnimalUnlocked ? "60%" : "20%")}");
            SetText(profileBadgeText, earnedBadge
                ? $"徽章卡片\n已获得：生态守护者\n完成{CurrentAnimal.ShortName}的互动挑战。"
                : "徽章卡片\n待解锁：生态守护者\n完成今日任务后可获得。");
            SetText(profileCollectionText, isAnimalUnlocked
                ? $"动物图鉴卡片\n已解锁：{CurrentAnimal.DisplayName}\n可继续聊天、做任务、生成科普卡片。"
                : $"动物图鉴卡片\n未解锁：{CurrentAnimal.DisplayName}\n扫描识别卡后加入收藏。");
            SetText(profileActionText, $"{nextAction}\n今日知识：{lastLearnedFact}");
        }

        private void UseAvatarFromInputPath()
        {
            var path = profileAvatarPathInput == null ? string.Empty : profileAvatarPathInput.text;
            if (string.IsNullOrWhiteSpace(path))
            {
                SetText(profileAvatarStatusText, $"请输入图片路径，或把图片保存为：{GetSavedAvatarPath()}");
                return;
            }

            if (!File.Exists(path))
            {
                SetText(profileAvatarStatusText, $"找不到图片：{path}");
                return;
            }

            if (!TryLoadAvatarSprite(path, out var sprite))
            {
                SetText(profileAvatarStatusText, "头像加载失败，请使用 PNG 或 JPG 图片。");
                return;
            }

            var targetPath = GetSavedAvatarPath();
            try
            {
                File.WriteAllBytes(targetPath, File.ReadAllBytes(path));
            }
            catch (Exception exception)
            {
                SetText(profileAvatarStatusText, $"头像保存失败：{exception.Message}");
                return;
            }

            SetProfileAvatar(sprite);
            SetText(profileAvatarStatusText, $"已保存自定义头像：{targetPath}");
        }

        private void LoadSavedCustomAvatar()
        {
            var path = GetSavedAvatarPath();
            if (!File.Exists(path))
            {
                UpdateAvatarStatusIfEmpty();
                return;
            }

            if (TryLoadAvatarSprite(path, out var sprite))
            {
                SetProfileAvatar(sprite);
                SetText(profileAvatarStatusText, $"已加载自定义头像：{path}");
                return;
            }

            SetText(profileAvatarStatusText, $"已保存头像无法读取，请重新选择图片：{path}");
        }

        private void ResetCustomAvatar()
        {
            var path = GetSavedAvatarPath();
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception exception)
                {
                    SetText(profileAvatarStatusText, $"恢复默认失败：{exception.Message}");
                    return;
                }
            }

            var defaultSprite = LoadUiSprite("Characters/character-sensen-avatar");
            SetProfileAvatar(defaultSprite);
            if (profileAvatarPathInput != null)
            {
                profileAvatarPathInput.text = string.Empty;
            }

            SetText(profileAvatarStatusText, $"已恢复默认头像。自定义头像保存位置：{path}");
        }

        private void SetProfileAvatar(Sprite sprite)
        {
            if (profileAvatarImage == null || sprite == null)
            {
                return;
            }

            profileAvatarImage.sprite = sprite;
            profileAvatarImage.preserveAspect = true;
            profileAvatarImage.color = Color.white;
        }

        private void UpdateAvatarStatusIfEmpty()
        {
            if (profileAvatarStatusText == null || !string.IsNullOrWhiteSpace(profileAvatarStatusText.text))
            {
                return;
            }

            SetText(profileAvatarStatusText, $"自定义头像支持 PNG/JPG。保存位置：{GetSavedAvatarPath()}");
        }

        private static bool TryLoadAvatarSprite(string path, out Sprite sprite)
        {
            sprite = null;
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch
            {
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return false;
            }

            texture.name = Path.GetFileNameWithoutExtension(path);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            return true;
        }

        private static string GetSavedAvatarPath()
        {
            return Path.Combine(Application.persistentDataPath, UserAvatarFileName);
        }

        private void EnableArCamera()
        {
            if (cameraPermissionRoutine != null)
            {
                StopCoroutine(cameraPermissionRoutine);
            }

            cameraPermissionRoutine = StartCoroutine(EnableCameraScanningWhenReady());

            if (arSession != null)
            {
                arSession.enabled = true;
            }

            if (arCameraManager != null)
            {
                arCameraManager.enabled = true;
            }

            if (arCameraBackground != null)
            {
                arCameraBackground.enabled = true;
            }
        }

        private void StartScanFallbackHintTimer()
        {
            StopScanFallbackHintTimer();
            scanFallbackHintRoutine = StartCoroutine(ShowScanFallbackHintAfterDelay());
        }

        private void StopScanFallbackHintTimer()
        {
            if (scanFallbackHintRoutine == null)
            {
                return;
            }

            StopCoroutine(scanFallbackHintRoutine);
            scanFallbackHintRoutine = null;
        }

        private IEnumerator ShowScanFallbackHintAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(1f, scanFallbackHintSeconds));
            scanFallbackHintRoutine = null;

            if (isModelView || cameraPreviewPanel == null || !cameraPreviewPanel.activeInHierarchy)
            {
                yield break;
            }

            SetText(statusText, "还没有识别到动物卡片");
            SetText(cameraScanHintText, "可以继续对准识别卡，或点击“手动识别动物”");
            SetText(chatText, "现场光线、反光或距离会影响识别。为了保证比赛演示连续，可点击底部按钮进入动物互动。");
        }

        private IEnumerator EnableCameraScanningWhenReady()
        {
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                SetText(cameraScanHintText, "请允许相机权限，用于扫描动物识别卡");
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            if (cameraPreviewPanel == null || !cameraPreviewPanel.activeInHierarchy)
            {
                yield break;
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                SetText(cameraScanHintText, "相机权限未开启，可点击“手动识别”继续演示");
                SetText(statusText, "相机权限未开启");
                SetText(chatText, "请在系统设置中允许相机权限，或点击“手动识别”进入动物互动。");
                yield break;
            }

            var cameraStarted = scanController != null && scanController.BeginCameraScanning(cameraPreviewImage);
            if (!cameraStarted)
            {
                SetText(cameraScanHintText, "未检测到可用相机，可点击“手动识别”继续演示");
                SetText(statusText, "相机未启动");
                SetText(chatText, "请检查相机权限，或点击“手动识别”进入动物互动。");
            }
        }

        private void DisableArCamera()
        {
            StopScanFallbackHintTimer();

            if (cameraPermissionRoutine != null)
            {
                StopCoroutine(cameraPermissionRoutine);
                cameraPermissionRoutine = null;
            }

            scanController?.StopCameraScanning();

            if (arCameraBackground != null)
            {
                arCameraBackground.enabled = false;
            }

            if (arCameraManager != null)
            {
                arCameraManager.enabled = false;
            }

            if (arSession != null)
            {
                arSession.enabled = false;
            }
        }

        private void SetChatInputEnabled(bool enabled)
        {
            SetButtonInteractable(sendLocalChatButton, enabled);
            SetButtonInteractable(quickFoodButton, enabled);
            SetButtonInteractable(quickDangerButton, enabled);
            SetButtonInteractable(quickProtectButton, enabled);
            if (chatInput != null)
            {
                chatInput.interactable = enabled;
            }
        }

        private void ScrollChatToBottom()
        {
            if (chatScrollRect == null)
            {
                return;
            }

            StartCoroutine(ScrollChatToBottomNextFrame());
        }

        private IEnumerator ScrollChatToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            chatScrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }

        private void SetGuide(string value)
        {
            if (appGuideText != null)
            {
                appGuideText.text = value;
                appGuideText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
            }
        }

        private void SetHomeStatus(string value)
        {
            SetText(homeStatusText, value);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetPanelActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private void SetModelChatVisible(bool visible)
        {
            SetPanelActive(modelChatBubble, visible);
            SetPanelActive(modelChatInputBar, visible);
            SetButtonVisible(modelBackButton, visible);
        }

        private void SetModelChatText(string value)
        {
            SetText(modelChatBubbleText, value);
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button == null ? null : button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private void SetButtonRect(Button button, Vector2 anchoredPosition, Vector2 size)
        {
            if (button == null)
            {
                return;
            }

            var rect = button.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                StyleButton(button, image.color);
            }

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.sizeDelta = size;
            }
        }

        private void StylePanel(GameObject panel, Color color)
        {
            if (panel == null)
            {
                return;
            }

            var image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = true;
                image.color = color;
            }
        }

        private void ApplyRoundedPanel(GameObject panel, Color color, float radius)
        {
            if (panel == null)
            {
                return;
            }

            var image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }

            image.enabled = true;
            image.color = color;
            image.sprite = GetRoundedSprite(panel.name, 128, Mathf.RoundToInt(radius));
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
        }

        private void RestoreImagePanel(GameObject panel, Color color)
        {
            var image = panel == null ? null : panel.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = true;
                image.color = color;
            }
        }

        private void StyleButton(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            var targetGraphic = button.targetGraphic;
            var image = button.GetComponent<Image>();
            if (image == null)
            {
                image = targetGraphic as Image;
            }

            if (image == null && targetGraphic == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            targetGraphic = image != null ? image : targetGraphic;
            if (targetGraphic == null)
            {
                return;
            }

            if (image != null)
            {
                image.enabled = true;
                image.color = color;
                image.sprite = GetRoundedSprite(button.name, 128, Mathf.RoundToInt(GetButtonRadius(button)));
                image.type = Image.Type.Sliced;
                image.raycastTarget = true;
            }

            targetGraphic.raycastTarget = true;
            targetGraphic.color = color;
            button.targetGraphic = targetGraphic;

            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
            colors.disabledColor = new Color(0.25f, 0.32f, 0.28f);
            button.colors = colors;

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 18;
                label.resizeTextMaxSize = Mathf.Max(label.fontSize, 30);
                label.color = Color.white;
                label.fontStyle = FontStyle.Bold;

                if (button == modelBackButton)
                {
                    label.color = Moss;
                    label.fontSize = 42;
                    label.resizeTextMaxSize = 42;
                }
            }
        }

        private GameObject CreateFixedPanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color, int radius)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = color;
            image.sprite = GetRoundedSprite(name, 128, Mathf.Clamp(radius, 0, 64));
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return obj;
        }

        private static float GetButtonRadius(Button button)
        {
            var rect = button == null ? null : button.GetComponent<RectTransform>();
            if (rect == null)
            {
                return 28f;
            }

            return Mathf.Min(rect.sizeDelta.x, rect.sizeDelta.y) * 0.5f;
        }

        private Sprite GetRoundedSprite(string key, int size, int radius)
        {
            var cacheKey = $"rounded-{key}-{size}-{radius}";
            if (spriteCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var center = radius - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x < radius ? center - x : x >= size - radius ? x - (size - radius) - center : 0f;
                    var dy = y < radius ? center - y : y >= size - radius ? y - (size - radius) - center : 0f;
                    var alpha = dx > 0f || dy > 0f
                        ? Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy))
                        : 1f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            spriteCache[cacheKey] = sprite;
            return sprite;
        }

        private void SetPanelBackground(GameObject panel, string assetKey, Color tint)
        {
            if (panel == null)
            {
                return;
            }

            var sprite = LoadUiSprite(assetKey);
            if (sprite == null)
            {
                return;
            }

            var image = panel.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = tint;
        }

        private void AddButtonIcon(Button button, string assetKey, float size)
        {
            if (button == null || button.transform.Find("Icon") != null)
            {
                return;
            }

            var sprite = LoadUiSprite(assetKey);
            if (sprite == null)
            {
                return;
            }

            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(button.transform, false);
            var icon = iconObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = Color.white;

            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(size, size);

            var buttonRect = button.GetComponent<RectTransform>();
            var iconOnly = buttonRect != null && buttonRect.sizeDelta.x <= 120f;
            if (iconOnly)
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(Mathf.Max(28f, size * 0.75f), 0f);
            }

            var label = button.GetComponentInChildren<Text>(true);
            if (label == null || label.transform.parent != button.transform)
            {
                return;
            }

            if (iconOnly)
            {
                label.gameObject.SetActive(false);
                return;
            }

            var labelRect = label.GetComponent<RectTransform>();
            labelRect.offsetMin = new Vector2(size + 34f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            label.alignment = TextAnchor.MiddleCenter;
        }

        private void AddLearnCardIcon(string cardTextName, string assetKey)
        {
            if (learnPanel == null)
            {
                return;
            }

            var texts = learnPanel.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
            {
                if (!text.name.Contains(cardTextName))
                {
                    continue;
                }

                var parent = text.transform.parent;
                if (parent == null || parent.Find("Card Icon") != null)
                {
                    return;
                }

                CreateImage(parent, "Card Icon", assetKey, new Vector2(-350f, 18f), new Vector2(68f, 68f), Color.white, true);
                var rect = text.GetComponent<RectTransform>();
                rect.offsetMin = new Vector2(92f, rect.offsetMin.y);
                return;
            }
        }

        private Image CreateImage(Transform parent, string name, string assetKey, Vector2 anchoredPosition, Vector2 size, Color color, bool preserveAspect)
        {
            var sprite = LoadUiSprite(assetKey);
            if (sprite == null)
            {
                return null;
            }

            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;

            var rect = image.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return image;
        }

        private static Image CreateSolidImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = image.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return image;
        }

        private Sprite LoadUiSprite(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                return null;
            }

            if (spriteCache.TryGetValue(assetKey, out var cached))
            {
                return cached;
            }

            var texture = LoadUiTexture(assetKey);
            if (texture == null)
            {
                spriteCache[assetKey] = null;
                return null;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sprite.name = assetKey;
            spriteCache[assetKey] = sprite;
            return sprite;
        }

        private static Texture2D LoadUiTexture(string assetKey)
        {
            var resourceTexture = Resources.Load<Texture2D>($"UI/{assetKey}");
            if (resourceTexture != null)
            {
                return resourceTexture;
            }

            var relativePath = Path.Combine("Art", "UI", $"{assetKey}.png");
            var path = Path.Combine(Application.dataPath, relativePath);
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            texture.name = assetKey;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = color;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return obj;
        }

        private Text CreateText(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.text = value;
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(16, fontSize - 12);
            text.resizeTextMaxSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return text;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = color;
            image.sprite = GetRoundedSprite(name, 128, Mathf.RoundToInt(Mathf.Min(size.x, size.y) * 0.28f));
            image.type = Image.Type.Sliced;
            var button = obj.AddComponent<Button>();
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var labelSize = size.y >= 80f ? 30 : 26;
            var labelText = CreateText(obj.transform, "Text", label, Vector2.zero, size, labelSize, Color.white, TextAnchor.MiddleCenter);
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            StyleButton(button, color);
            return button;
        }

        private InputField CreateInputField(Transform parent, string name, string placeholderValue, Vector2 anchoredPosition, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.97f, 0.99f, 0.95f, 0.96f);
            image.sprite = GetRoundedSprite(name, 128, Mathf.RoundToInt(Mathf.Min(size.x, size.y) * 0.5f));
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            var input = obj.AddComponent<InputField>();
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            input.targetGraphic = image;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(obj.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 15;
            text.resizeTextMaxSize = 22;
            text.color = new Color(0.03f, 0.1f, 0.07f);
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18, 6);
            textRect.offsetMax = new Vector2(-18, -6);

            var placeholderObject = new GameObject("Placeholder");
            placeholderObject.transform.SetParent(obj.transform, false);
            var placeholder = placeholderObject.AddComponent<Text>();
            placeholder.text = placeholderValue;
            placeholder.font = text.font;
            placeholder.fontSize = 22;
            placeholder.resizeTextForBestFit = true;
            placeholder.resizeTextMinSize = 15;
            placeholder.resizeTextMaxSize = 22;
            placeholder.color = new Color(0.22f, 0.32f, 0.27f, 0.72f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;

            var placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(18, 6);
            placeholderRect.offsetMax = new Vector2(-18, -6);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 260;
            return input;
        }
    }

    [Serializable]
    public class AnimalProfile
    {
        public AnimalProfile()
        {
        }

        public AnimalProfile(string animalId, string displayName, string modelPath, string markerName, string introText, string foodMissionText, string[] knowledgeFacts)
        {
            this.animalId = animalId;
            this.displayName = displayName;
            this.modelPath = modelPath;
            this.markerName = markerName;
            this.introText = introText;
            this.foodMissionText = foodMissionText;
            this.knowledgeFacts = knowledgeFacts;
        }

        [SerializeField] private string animalId;
        [SerializeField] private string displayName;
        [SerializeField] private string modelPath;
        [SerializeField] private string markerName;
        [SerializeField] private string introText;
        [SerializeField] private string foodMissionText;
        [SerializeField] private string[] knowledgeFacts;

        public static AnimalProfile DefaultSensen => new AnimalProfile(
            "sensen",
            "缨冠灰叶猴 森森",
            "Models/Sensen/sensen.glb",
            "sensen_marker",
            "你好呀！我是缨冠灰叶猴森森。谢谢你愿意来到我的森林，今天我们一起认识我的食物、家和保护方法吧。",
            "帮森森寻找食物",
            new[]
            {
                "缨冠灰叶猴主要吃嫩叶、果实和花朵。",
                "完整森林能给缨冠灰叶猴提供食物、庇护和迁徙通道。",
                "栖息地破碎、非法捕猎和种群隔离会让它们更加濒危。"
            });

        public string AnimalId => string.IsNullOrWhiteSpace(animalId) ? "sensen" : animalId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? AnimalId : displayName;
        public string ShortName => DisplayName.Contains(" ") ? DisplayName.Substring(DisplayName.LastIndexOf(" ", StringComparison.Ordinal) + 1) : DisplayName;
        public string ModelPath => string.IsNullOrWhiteSpace(modelPath) ? string.Empty : modelPath;
        public string MarkerName => string.IsNullOrWhiteSpace(markerName) ? AnimalId : markerName;
        public string IntroText => string.IsNullOrWhiteSpace(introText) ? "你好，很高兴见到你。我们一起认识濒危动物和保护行动吧。" : introText;
        public string FoodMissionText => string.IsNullOrWhiteSpace(foodMissionText) ? "完成保护任务" : foodMissionText;
        public string PrimaryFact => GetFact(0);

        public string GetFact(int index)
        {
            if (knowledgeFacts == null || knowledgeFacts.Length == 0)
            {
                return "认识濒危动物，是参与生态保护的第一步。";
            }

            return knowledgeFacts[Mathf.Clamp(index, 0, knowledgeFacts.Length - 1)];
        }
    }
}
