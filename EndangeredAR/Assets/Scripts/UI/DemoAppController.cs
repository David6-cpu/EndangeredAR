using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EndangeredAR.AI;
using EndangeredAR.API;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using EndangeredAR.Missions;
using EndangeredAR.Models;
using EndangeredAR.Progress;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EndangeredAR.UI
{
    public class DemoAppController : MonoBehaviour
    {
        [SerializeField] private ARImageScanController scanController;
        [SerializeField] private AIManager aiManager;
        [SerializeField] private ChatApiClient chatApiClient;
        [SerializeField] private LocalKnowledgeChatService localChatService;
        [SerializeField] private MissionController missionController;
        [SerializeField] private AnimalCatalogService animalCatalog;
        [SerializeField] private AnimalProgressService animalProgress;
        [SerializeField] private AnimalExperienceController animalExperience;
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

        private const string DefaultAnimalId = "sensen";
        private const string ThinkingLine = "正在想一想...";
        private const string UserAvatarFileName = "user_avatar.png";
        private const float CloudAnswerTimeoutSeconds = 40f;
        private const int MaxHistoryMessages = 20;

        private static readonly Color ForestDark = SensenDesignTokens.WithAlpha(SensenDesignTokens.Forest950, 0.96f);
        private static readonly Color Leaf = SensenDesignTokens.Leaf500;
        private static readonly Color Moss = SensenDesignTokens.Moss650;
        private static readonly Color Cream = SensenDesignTokens.Cream100;

        private readonly List<ChatMessage> chatHistory = new List<ChatMessage>();
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private bool isModelView;
        private readonly ChatRequestState chatRequestState = new ChatRequestState();
        private int simulatedAnimalIndex;
        private string chatTranscript;
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
        private Text cardBadgeStatusText;
        private Text cardActionText;
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

        private AnimalDefinition CurrentAnimal =>
            animalExperience != null && animalExperience.CurrentAnimal != null
                ? animalExperience.CurrentAnimal
                : animalCatalog?.DefaultAnimal;

        private AnimalProgressRecord CurrentProgress => CurrentAnimal == null || animalProgress == null
            ? null
            : animalProgress.GetOrCreate(CurrentAnimal.AnimalId);

        private string CurrentAnimalId => CurrentAnimal?.AnimalId ?? string.Empty;
        private string CurrentShortName => string.IsNullOrWhiteSpace(CurrentAnimal?.ShortName)
            ? "动物伙伴"
            : CurrentAnimal.ShortName;
        private string CurrentDisplayName => string.IsNullOrWhiteSpace(CurrentAnimal?.DisplayName)
            ? CurrentShortName
            : CurrentAnimal.DisplayName;
        private string CurrentWelcomeText => string.IsNullOrWhiteSpace(CurrentAnimal?.WelcomeText)
            ? "你好，很高兴见到你。我们一起认识濒危动物和保护行动吧。"
            : CurrentAnimal.WelcomeText;
        private string CurrentMissionTitle => string.IsNullOrWhiteSpace(CurrentAnimal?.Mission?.Title)
            ? "完成保护任务"
            : CurrentAnimal.Mission.Title;
        private bool IsCurrentAnimalUnlocked => CurrentProgress != null && CurrentProgress.unlocked;
        private bool IsCurrentMissionCompleted => CurrentProgress != null && CurrentProgress.missionCompleted;
        private bool HasCurrentBadge => CurrentProgress != null && CurrentProgress.earnedBadgeIds.Count > 0;
        private bool IsChatThinking => chatRequestState.IsThinking;

        private void Awake()
        {
            InitializeAnimalArchitecture();

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

            rootCanvas = FindObjectOfType<Canvas>();
            ConfigureCanvasScaler();
            EnsureEventSystem();
            EnsureCanvasRaycaster();
            EnsureSafeAreaLayout();
            ApplyChineseFont();
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
            if (scanController != null)
            {
                scanController.AnimalMarkerDetected -= ShowAnimal;
                scanController.AnimalMarkerTracked -= PlaceAnimalOnMarker;
            }

            if (animalExperience != null)
            {
                animalExperience.CurrentAnimalChanged -= HandleCurrentAnimalChanged;
            }
        }

        private void InitializeAnimalArchitecture()
        {
            animalCatalog = animalCatalog != null ? animalCatalog : FindObjectOfType<AnimalCatalogService>();
            animalProgress = animalProgress != null ? animalProgress : FindObjectOfType<AnimalProgressService>();
            animalExperience = animalExperience != null ? animalExperience : FindObjectOfType<AnimalExperienceController>();

            if (animalCatalog == null || animalProgress == null || animalExperience == null)
            {
                Debug.LogError(
                    "DemoAppController requires Animal Catalog Service, Animal Progress Service, and Animal Experience Controller. " +
                    "Run Endangered AR/Migrate Demo Scene Animal Architecture and verify the three serialized references.",
                    this);
                chatTranscript = "动物伙伴：欢迎来到濒危动物科普体验。";
                return;
            }

            animalCatalog.Initialize();
            animalProgress.Initialize();
            animalExperience.Initialize();
            animalExperience.CurrentAnimalChanged += HandleCurrentAnimalChanged;

            var defaultAnimalId = animalCatalog.DefaultAnimal?.AnimalId;
            animalExperience.Prepare(string.IsNullOrWhiteSpace(defaultAnimalId) ? DefaultAnimalId : defaultAnimalId);
        }

        private void HandleCurrentAnimalChanged(AnimalDefinition animal)
        {
            if (chatRequestState.InvalidateForAnimalChange(animal?.AnimalId))
            {
                StopCloudAnswerTimeout();
                SetChatInputEnabled(IsCurrentAnimalUnlocked);
            }

            RestoreConversation(animal);
            UpdateLearnContent();
            UpdateCardContent();
            UpdateProfileContent();
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
            AddClick(leafyFoodButton, "Food Mission clicked", () => SelectMissionOption("leaf"));
            AddClick(snackFoodButton, "Food Mission clicked", () => SelectMissionOption("snack"));
            AddClick(flowerFoodButton, "Food Mission clicked", () => SelectMissionOption("flower"));
            AddClick(plasticFoodButton, "Food Mission clicked", () => SelectMissionOption("plastic"));
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

            missionButton = CreateButton(safeParent, "Food Mission Button", CurrentMissionTitle, new Vector2(-200, -585), new Vector2(320, 62), Leaf);
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
            modelChatBubble = CreatePanel(parent, "Model Chat Bubble", new Vector2(0.56f, 0.52f), new Vector2(0.91f, 0.66f), new Color(0.97f, 0.99f, 0.94f, 0.94f));
            ApplyRoundedPanel(modelChatBubble, new Color(0.97f, 0.99f, 0.94f, 0.94f), 34f);
            var bubbleImage = modelChatBubble.GetComponent<Image>();
            if (bubbleImage != null)
            {
                bubbleImage.raycastTarget = false;
            }

            modelChatBubbleText = CreateText(modelChatBubble.transform, "Model Chat Bubble Text", "你好！我是动物伙伴。\n你可以直接问我问题。", Vector2.zero, new Vector2(340, 280), 28, new Color(0.09f, 0.14f, 0.12f), TextAnchor.MiddleLeft);
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
            missionTitleText = CreateText(missionPanel.transform, "Mission Title", CurrentMissionTitle, new Vector2(0, 575), new Vector2(820, 72), 46, Color.white, TextAnchor.MiddleCenter);
            missionTitleText.fontStyle = FontStyle.Bold;
            missionStatusText = CreateText(missionPanel.transform, "Mission Status", CurrentMissionPrompt(), new Vector2(0, 450), new Vector2(820, 120), 30, Cream, TextAnchor.MiddleCenter);
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
            var panelColor = new Color(0.82f, 0.9f, 0.76f, 1f);
            var surfaceColor = new Color(0.965f, 0.975f, 0.91f, 1f);
            var primaryText = new Color(0.055f, 0.16f, 0.1f, 1f);
            var secondaryText = new Color(0.2f, 0.36f, 0.25f, 1f);

            cardPanel = CreatePanel(parent, "Knowledge Card Panel", Vector2.zero, Vector2.one, panelColor);
            var shareSurface = CreatePanel(cardPanel.transform, "Share Card Surface", new Vector2(0.065f, 0.24f), new Vector2(0.935f, 0.955f), surfaceColor);
            ApplyRoundedPanel(shareSurface, surfaceColor, 34f);
            cardCaptureRect = shareSurface.GetComponent<RectTransform>();

            CreateImage(shareSurface.transform, "Card Soft Glow", "Cards/card-soft-glow", new Vector2(320, 790), new Vector2(300, 300), new Color(1f, 1f, 1f, 0.32f), true);
            CreateImage(shareSurface.transform, "Card Leaf Corner", "Cards/card-leaf-corner", new Vector2(-365, -835), new Vector2(150, 150), new Color(1f, 1f, 1f, 0.48f), true);
            CreateImage(shareSurface.transform, "Card Sensen Avatar", "Characters/character-sensen-avatar", new Vector2(-330, 800), new Vector2(190, 190), Color.white, true);
            cardHeaderText = CreateText(shareSurface.transform, "Card Header", $"今日认识了{CurrentShortName}", new Vector2(30, 855), new Vector2(620, 72), 42, primaryText, TextAnchor.MiddleLeft);
            cardHeaderText.fontStyle = FontStyle.Bold;
            CreateText(shareSurface.transform, "Card Subtitle", CurrentDisplayName, new Vector2(30, 795), new Vector2(620, 42), 24, secondaryText, TextAnchor.MiddleLeft);
            cardModelHintText = CreateText(shareSurface.transform, "Card Model Hint", $"{CurrentShortName}：谢谢你愿意认识我的森林。", new Vector2(0, 670), new Vector2(850, 70), 25, secondaryText, TextAnchor.MiddleCenter);

            CreateSolidImage(shareSurface.transform, "Card Top Divider", new Vector2(0, 610), new Vector2(850, 2), new Color(0.32f, 0.5f, 0.34f, 0.28f));
            cardContentText = CreateText(shareSurface.transform, "Card Content", "", new Vector2(0, 345), new Vector2(820, 360), 27, primaryText, TextAnchor.UpperLeft);
            CreateSolidImage(shareSurface.transform, "Card Bottom Divider", new Vector2(0, 130), new Vector2(850, 2), new Color(0.32f, 0.5f, 0.34f, 0.28f));

            CreateImage(shareSurface.transform, "Card Badge Icon", "Badges/badge-eco-guardian", new Vector2(-335, -45), new Vector2(150, 150), Color.white, true);
            cardBadgeStatusText = CreateText(shareSurface.transform, "Card Badge Status", "", new Vector2(70, -20), new Vector2(650, 120), 27, primaryText, TextAnchor.MiddleLeft);
            cardActionText = CreateText(shareSurface.transform, "Card Action", "", new Vector2(0, -285), new Vector2(820, 180), 25, secondaryText, TextAnchor.UpperLeft);
            CreateText(shareSurface.transform, "Card Footer", "濒危动物 AR 科普 · 为森林多做一件小事", new Vector2(0, -860), new Vector2(820, 52), 20, new Color(0.28f, 0.43f, 0.3f, 1f), TextAnchor.MiddleCenter);

            cardSaveStatusText = CreateText(cardPanel.transform, "Card Save Status", "", Vector2.zero, new Vector2(780f, 72f), 19, secondaryText, TextAnchor.MiddleCenter);
            SetAnchoredRect(cardSaveStatusText.GetComponent<RectTransform>(), new Vector2(0.08f, 0.145f), new Vector2(0.92f, 0.185f));
            cardSaveButton = CreateButton(cardPanel.transform, "Save Card Button", "保存 PNG", Vector2.zero, new Vector2(350f, 76f), Leaf);
            cardBackButton = CreateButton(cardPanel.transform, "Card Back Button", "返回展示", Vector2.zero, new Vector2(350f, 76f), Moss);
            SetAnchoredRect(cardSaveButton.GetComponent<RectTransform>(), new Vector2(0.10f, 0.055f), new Vector2(0.48f, 0.115f));
            SetAnchoredRect(cardBackButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.055f), new Vector2(0.90f, 0.115f));
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
            SetPanelBackground(scanPanel, "Backgrounds/bg-discover-camera", new Color(1f, 1f, 1f, 0.78f));
            SetPanelBackground(chatPanel, "Backgrounds/bg-chat-forest", new Color(1f, 1f, 1f, 0.9f));
            SetPanelBackground(learnPanel, "Backgrounds/bg-learn-panel", new Color(1f, 1f, 1f, 0.86f));
            SetPanelBackground(profilePanel, "Backgrounds/bg-learn-panel", new Color(1f, 1f, 1f, 0.86f));
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
            SetText(chatPageText, IsCurrentAnimalUnlocked ? chatTranscript : "先去发现页识别动物伙伴，再来和它聊天吧。");
            if (chatInput != null)
            {
                chatInput.text = "";
            }

            SetChatInputEnabled(IsCurrentAnimalUnlocked && !IsChatThinking);
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
            SetButtonVisible(missionButton, false);
            SetButtonVisible(cardButton, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            missionController?.StartMission();
            SetText(missionTitleText, CurrentMissionTitle);
            SetText(missionStatusText, CurrentMissionPrompt());
            SetText(badgeText, IsCurrentMissionCompleted
                ? "徽章已收藏 · 本轮可再次挑战"
                : "答对后解锁：生态守护者徽章");
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
            SetButtonVisible(missionButton, false);
            SetButtonVisible(cardButton, false);
            SetButtonVisible(askFoodButton, false);
            SetButtonVisible(askProtectButton, false);
            UpdateCardContent();
            SetGuide("");
            RefreshUiInteractionState();
        }

        private AnimalDefinition GetNextSimulatedAnimal()
        {
            animalCatalog?.Initialize();
            var animals = animalCatalog?.Catalog?.Animals;
            if (animals == null || animals.Count == 0)
            {
                return null;
            }

            var animal = animals[simulatedAnimalIndex % animals.Count];
            simulatedAnimalIndex = (simulatedAnimalIndex + 1) % animals.Count;
            return animal;
        }

        private void SimulateScan()
        {
            StopScanFallbackHintTimer();
            var animal = GetNextSimulatedAnimal();
            if (animal == null)
            {
                SetText(statusText, "动物目录未配置，请返回首页后重试。");
                return;
            }

            SetText(statusText, $"已识别{animal.DisplayName}卡片");
            SetText(cameraScanHintText, $"识别成功，{animal.ShortName}正在出现...");
            if (scanController != null)
            {
                scanController.SimulateMarkerDetected(animal.AnimalId);
                return;
            }

            var result = animalExperience == null
                ? default
                : animalExperience.SelectFromScan(animal.AnimalId);
            if (result.IsSuccess)
            {
                EnterModelView();
            }
        }

        private void ShowAnimal(string animalId)
        {
            if (isModelView)
            {
                return;
            }

            var result = animalExperience == null
                ? default
                : animalExperience.Prepare(animalId);
            if (!result.IsSuccess)
            {
                return;
            }

            SetPanelActive(animalPlaceholder, true);
            SetText(statusText, $"{CurrentShortName}出现了！");
        }

        private void PlaceAnimalOnMarker(string animalId, Transform markerTransform)
        {
            _ = markerTransform;
            var result = animalExperience == null
                ? default
                : animalExperience.SelectFromScan(animalId);
            if (result.IsSuccess)
            {
                EnterModelView();
            }
        }

        public bool OpenAnimalFromCatalog(string animalId)
        {
            var result = animalExperience == null
                ? default
                : animalExperience.SelectFromCatalog(animalId);
            if (!result.IsSuccess)
            {
                return false;
            }

            EnterModelView();
            return true;
        }

        private void EnterModelView()
        {
            if (CurrentAnimal == null)
            {
                EnterHomeView();
                return;
            }

            isModelView = true;
            StopScanFallbackHintTimer();
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
            SetChatInputEnabled(!IsChatThinking);
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
                animalPlaceholder.SetActive(true);
                var gesture = animalPlaceholder.GetComponent<AnimalGestureController>();
                gesture?.RefreshBaseScale();
                modelRestPosition = animalPlaceholder.transform.position;
                StartModelMotion();
            }

            SetButtonLabel(missionButton, CurrentMissionTitle);
            SetText(missionTitleText, CurrentMissionTitle);
            SetText(missionStatusText, CurrentMissionPrompt());
            SetText(statusText, $"{CurrentShortName}：谢谢你找到我！我们一起守护森林吧。");
            SetText(chatText, "");
            SetModelChatText($"{CurrentWelcomeText}\n\n你可以用手指旋转、缩放观察我，也可以直接问我问题。");
            SetGuide("");
            StartCoroutine(WelcomeAfterReveal());
            RefreshUiInteractionState();
        }

        private IEnumerator WelcomeAfterReveal()
        {
            yield return new WaitForSeconds(0.4f);
            AddAssistantMessage($"你找到我啦！我是{CurrentShortName}。你可以直接在下面输入问题。", false);
        }

        private void AskLocal(string message)
        {
            if (!IsCurrentAnimalUnlocked)
            {
                SetText(chatPageText, "先去发现页识别动物伙伴，再来和它聊天吧。");
                SetModelChatText("先扫描识别卡，就可以和我聊天啦。");
                return;
            }

            if (IsChatThinking)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                AppendChatLine("提示", $"请输入一个想问{CurrentShortName}的问题。");
                return;
            }

            if (chatInput != null)
            {
                chatInput.text = "";
            }

            var request = chatRequestState.Begin(CurrentAnimalId);
            SetChatInputEnabled(false);
            AppendChatLine("你", message);
            AppendChatLine(CurrentShortName, ThinkingLine);
            SetModelChatText(ThinkingLine);
            ScrollChatToBottom();
            StartCloudAnswerTimeout(request, message);

            if (aiManager == null)
            {
                FinishCloudAnswer(request, message, BuildFallbackReply(message), $"AI 服务还没配置好，{CurrentShortName}先用本地知识陪你继续。");
                return;
            }

            var aiRequest = new AIRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                animalId = CurrentAnimalId,
                message = message,
                history = chatHistory.ToArray(),
                knowledgeProfile = CurrentAnimal?.Knowledge
            };

            StartCoroutine(aiManager.Send(
                aiRequest,
                response =>
                {
                    Debug.Log($"AI response source={response?.source ?? "unknown"}, routeReason={response?.routeReason ?? "unspecified"}", this);
                    StartCoroutine(FinishCloudAnswerAfterDelay(
                        request,
                        message,
                        response?.reply,
                        response?.missionHint));
                },
                error => StartCoroutine(FinishCloudAnswerAfterDelay(request, message, BuildFallbackReply(message), $"AI 服务暂时不稳定，{CurrentShortName}先用本地知识陪你继续。"))
            ));
        }

        private void AskModelPanel(string message)
        {
            SetText(chatText, $"你：{message}\n{CurrentShortName}：{ThinkingLine}");
            AskLocal(message);
            PulseModel();
        }

        private IEnumerator CloudAnswerTimeout(ChatRequestTicket request, string userMessage)
        {
            yield return new WaitForSeconds(CloudAnswerTimeoutSeconds);
            cloudAnswerTimeoutRoutine = null;

            if (!chatRequestState.CanComplete(request, CurrentAnimalId))
            {
                yield break;
            }

            FinishCloudAnswer(request, userMessage, BuildFallbackReply(userMessage), $"网络有点慢，{CurrentShortName}先用本地知识回答你。");
        }

        private void StartCloudAnswerTimeout(ChatRequestTicket request, string userMessage)
        {
            if (cloudAnswerTimeoutRoutine != null)
            {
                StopCoroutine(cloudAnswerTimeoutRoutine);
            }

            cloudAnswerTimeoutRoutine = StartCoroutine(CloudAnswerTimeout(request, userMessage));
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

        private IEnumerator FinishCloudAnswerAfterDelay(ChatRequestTicket request, string userMessage, string reply, string missionHint = "")
        {
            yield return new WaitForSeconds(0.45f);
            FinishCloudAnswer(request, userMessage, reply, missionHint);
        }

        private void FinishCloudAnswer(ChatRequestTicket request, string userMessage, string reply, string missionHint = "")
        {
            if (!chatRequestState.TryComplete(request, CurrentAnimalId))
            {
                return;
            }

            StopCloudAnswerTimeout();

            if (string.IsNullOrWhiteSpace(reply))
            {
                reply = BuildFallbackReply(userMessage);
            }

            reply = SanitizeUserFacingReply(reply, userMessage);

            if (!string.IsNullOrWhiteSpace(missionHint) && !LooksTechnical(missionHint))
            {
                reply = $"{reply}\n{missionHint}";
            }

            ReplaceLastThinkingLine($"{CurrentShortName}：{reply}");
            SetText(chatPageText, chatTranscript);
            SetText(chatText, "");
            SetModelChatText(reply);
            AddHistory("user", userMessage);
            AddHistory("assistant", reply);
            PersistConversation();
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
            var answer = localChatService == null
                ? new ChatAnswer("我还在整理这个问题，不过保护森林、拒绝投喂野生动物，就是帮我的好办法。", Array.Empty<string>(), false)
                : localChatService.Answer(CurrentAnimal?.Knowledge, message);

            var reply = answer.Reply;
            if (string.IsNullOrWhiteSpace(reply))
            {
                reply = "我暂时无法回答这个问题，不过我们可以继续了解栖息地和保护行动。";
            }
            else if (!reply.Contains(CurrentShortName) && !reply.Contains("我"))
            {
                reply = $"我悄悄告诉你：{reply}";
            }

            return $"{reply}\n你愿意继续帮我完成“{CurrentMissionTitle}”任务吗？";
        }

        private void SelectMissionOption(string optionId)
        {
            var rewardWasAlreadyClaimed = IsCurrentMissionCompleted;
            var result = missionController == null ? default : missionController.SelectOption(optionId);
            SetText(missionStatusText, result.Feedback);

            if (result.Success)
            {
                if (!rewardWasAlreadyClaimed)
                {
                    animalProgress?.MarkMissionCompleted(CurrentAnimalId, result.BadgeId, result.LearnedKnowledgeId);
                    SetText(badgeText, $"已获得：生态守护者徽章 +{result.PointsAwarded}");
                    AddAssistantMessage($"太棒啦！你帮{CurrentShortName}完成了任务。现在你已经是小小生态守护者了，可以生成一张科普卡片带走这段记录。", true);
                }
                else
                {
                    SetText(badgeText, "回答正确 · 徽章已收藏");
                    AddAssistantMessage($"又答对啦！你已经把保护{CurrentShortName}的知识记得很牢，徽章会一直为你保留。", true);
                }
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

            Texture2D screenTexture = null;
            Texture2D cardTexture = null;
            try
            {
                screenTexture = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenTexture == null)
                {
                    throw new InvalidOperationException("Screenshot capture returned no texture.");
                }

                var rect = GetScreenRect(cardCaptureRect);
                var width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, screenTexture.width);
                var height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, screenTexture.height);
                var x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, screenTexture.width - width);
                var y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, screenTexture.height - height);

                cardTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
                cardTexture.SetPixels(screenTexture.GetPixels(x, y, width, height));
                cardTexture.Apply();

                var fileName = $"{CurrentAnimalId}_card_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllBytes(path, cardTexture.EncodeToPNG());
                SetText(cardSaveStatusText, $"已保存：{fileName}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Knowledge card save failed: {exception.GetType().Name}");
                SetText(cardSaveStatusText, "保存失败，请稍后再试。");
            }
            finally
            {
                if (screenTexture != null)
                {
                    Destroy(screenTexture);
                }

                if (cardTexture != null)
                {
                    Destroy(cardTexture);
                }
            }
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
            AppendChatLine(CurrentShortName, reply);
            SetModelChatText(reply);
            if (addToHistory)
            {
                AddHistory("assistant", reply);
                PersistConversation();
            }
        }

        private void AppendChatLine(string speaker, string message)
        {
            chatTranscript += $"\n\n{speaker}：{message}";
            SetText(chatPageText, chatTranscript);
        }

        private void ReplaceLastThinkingLine(string replacement)
        {
            var thinkingLine = $"{CurrentShortName}：{ThinkingLine}";
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

        private void RestoreConversation(AnimalDefinition animal)
        {
            chatHistory.Clear();
            if (animal == null)
            {
                chatTranscript = "动物伙伴：欢迎来到濒危动物科普体验。";
                return;
            }

            var records = animalProgress?.GetConversation(animal.AnimalId);
            if (records != null)
            {
                foreach (var record in records)
                {
                    if (record == null || !IsSupportedConversationRole(record.role) ||
                        !IsPersistableConversationContent(record.content))
                    {
                        continue;
                    }

                    AddHistory(record.role, record.content.Trim());
                }
            }

            chatTranscript = BuildConversationTranscript(animal.ShortName, animal.WelcomeText, chatHistory);
        }

        private void PersistConversation()
        {
            if (animalProgress == null || string.IsNullOrWhiteSpace(CurrentAnimalId))
            {
                return;
            }

            animalProgress.ReplaceConversation(CurrentAnimalId, BuildConversationSnapshot(chatHistory));
        }

        internal static IReadOnlyList<ConversationRecord> BuildConversationSnapshot(IEnumerable<ChatMessage> history)
        {
            var snapshot = new List<ConversationRecord>();
            if (history != null)
            {
                foreach (var message in history)
                {
                    if (message == null || !IsSupportedConversationRole(message.role) ||
                        !IsPersistableConversationContent(message.content))
                    {
                        continue;
                    }

                    snapshot.Add(new ConversationRecord
                    {
                        role = message.role.Trim().ToLowerInvariant(),
                        content = message.content.Trim()
                    });
                }
            }

            if (snapshot.Count > MaxHistoryMessages)
            {
                snapshot.RemoveRange(0, snapshot.Count - MaxHistoryMessages);
            }

            return snapshot;
        }

        private static string BuildConversationTranscript(
            string animalName,
            string welcomeText,
            IEnumerable<ChatMessage> history)
        {
            var safeAnimalName = string.IsNullOrWhiteSpace(animalName) ? "动物伙伴" : animalName;
            var lines = new List<string>();
            if (history != null)
            {
                foreach (var message in history)
                {
                    if (message == null || !IsSupportedConversationRole(message.role) ||
                        string.IsNullOrWhiteSpace(message.content))
                    {
                        continue;
                    }

                    var speaker = string.Equals(message.role, "user", StringComparison.OrdinalIgnoreCase)
                        ? "你"
                        : safeAnimalName;
                    lines.Add($"{speaker}：{message.content.Trim()}");
                }
            }

            if (lines.Count == 0)
            {
                var safeWelcome = string.IsNullOrWhiteSpace(welcomeText)
                    ? "你好，很高兴见到你。"
                    : welcomeText.Trim();
                lines.Add($"{safeAnimalName}：{safeWelcome}");
            }

            return string.Join("\n\n", lines);
        }

        private static bool IsSupportedConversationRole(string role)
        {
            return string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPersistableConversationContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content) ||
                content.IndexOf(ThinkingLine, StringComparison.OrdinalIgnoreCase) >= 0 ||
                content.IndexOf("http://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                content.IndexOf("https://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                content.IndexOf("www.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return !LooksTechnical(content);
        }

        private string CurrentMissionPrompt()
        {
            return string.IsNullOrWhiteSpace(CurrentAnimal?.Mission?.Prompt)
                ? $"{CurrentMissionTitle}开始啦。请选择最适合的选项。"
                : CurrentAnimal.Mission.Prompt;
        }

        private string CurrentKnowledgeFact(int index)
        {
            var knowledge = CurrentAnimal?.Knowledge;
            if (knowledge == null)
            {
                return "认识濒危动物，是参与生态保护的第一步。";
            }

            if (index == 0)
            {
                return string.IsNullOrWhiteSpace(knowledge.EndangeredLevel)
                    ? knowledge.Habitat
                    : $"濒危等级：{knowledge.EndangeredLevel}；栖息地：{knowledge.Habitat}";
            }

            if (index == 1)
            {
                return string.IsNullOrWhiteSpace(knowledge.Habitat)
                    ? knowledge.Food
                    : knowledge.Habitat;
            }

            var threats = knowledge.Threats;
            return threats.Length > 0 && !string.IsNullOrWhiteSpace(threats[0])
                ? threats[0]
                : CurrentLearnedFact();
        }

        private string CurrentLearnedFact()
        {
            var mission = CurrentAnimal?.Mission;
            var progress = CurrentProgress;
            if (mission != null && progress != null &&
                !string.IsNullOrWhiteSpace(mission.LearnedKnowledgeId) &&
                progress.learnedKnowledgeIds.Contains(mission.LearnedKnowledgeId) &&
                !string.IsNullOrWhiteSpace(mission.LearnedFact))
            {
                return mission.LearnedFact;
            }

            var dailyFacts = CurrentAnimal?.Knowledge?.DailyFacts;
            if (dailyFacts != null && dailyFacts.Length > 0 && !string.IsNullOrWhiteSpace(dailyFacts[0]))
            {
                return dailyFacts[0];
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
                    text.text = HasCurrentBadge ? "生态守护者 Lv.2    已完成今日任务" : "生态守护者 Lv.1    今日学习进度 60%";
                }
                else if (text.name.Contains("Species Card"))
                {
                    text.text = $"濒危档案\n{CurrentDisplayName}\n{CurrentKnowledgeFact(0)}";
                }
                else if (text.name.Contains("Mission Card"))
                {
                    text.text = $"任务与栖息地\n{CurrentMissionTitle}\n{CurrentKnowledgeFact(1)}";
                }
                else if (text.name.Contains("Crisis Card"))
                {
                    text.text = $"生存威胁与今日知识\n{CurrentKnowledgeFact(2)}\n今日知识：{CurrentLearnedFact()}";
                }
                else if (text.name.Contains("Fun Card"))
                {
                    text.text = $"趣味知识\n观察 3D 模型时，可以单指旋转、双指缩放。\n当前伙伴：{CurrentShortName}";
                }
            }
        }

        private void UpdateCardContent()
        {
            if (cardContentText == null)
            {
                return;
            }

            var missionLine = IsCurrentMissionCompleted
                ? $"已完成：{CurrentMissionTitle}"
                : IsCurrentAnimalUnlocked
                    ? $"进行中：{CurrentMissionTitle}"
                    : $"待开始：先扫描{CurrentShortName}识别卡";
            var badgeLine = HasCurrentBadge ? "生态守护者徽章已解锁" : "完成任务后解锁";

            SetText(cardHeaderText, $"今日认识了{CurrentShortName}");
            SetText(cardModelHintText, $"{CurrentShortName}：谢谢你愿意认识我的森林。");
            cardContentText.text =
                $"栖息地与食物\n{CurrentKnowledgeFact(1)}\n\n"
                + $"今日知识\n{CurrentLearnedFact()}\n\n"
                + $"互动任务\n{missionLine}";
            SetText(cardBadgeStatusText, $"生态守护者徽章\n{badgeLine}");
            SetText(cardActionText, "今天的守护行动\n不投喂野生动物，把森林保护知识告诉更多人。");
        }

        private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private void UpdateProfileContent()
        {
            var unlockedCount = animalProgress == null ? 0 : animalProgress.UnlockedCount;
            var catalogCount = animalCatalog?.Catalog?.Animals.Count ?? 0;
            var missionPoints = IsCurrentMissionCompleted ? CurrentAnimal?.Mission?.Points ?? 0 : 0;
            var totalPoints = missionPoints + (IsCurrentAnimalUnlocked ? 10 : 0);
            var level = HasCurrentBadge ? 2 : 1;
            var nextAction = HasCurrentBadge
                ? "今日行动：把一条森林保护知识告诉朋友，继续解锁新的动物伙伴。"
                : IsCurrentAnimalUnlocked
                    ? $"今日行动：完成“{CurrentMissionTitle}”任务，领取生态守护者徽章。"
                    : $"今日行动：先去发现页识别{CurrentShortName}卡片，开启你的第一段 AR 科普记录。";

            SetText(profileNameText, $"生态守护者 Lv.{level}");
            SetText(profileStatsText, $"积分 {totalPoints}    已解锁动物 {unlockedCount}/{catalogCount}\n学习进度 {(HasCurrentBadge ? "100%" : IsCurrentAnimalUnlocked ? "60%" : "20%")}");
            SetText(profileBadgeText, HasCurrentBadge
                ? $"徽章卡片\n已获得：生态守护者\n完成{CurrentShortName}的互动挑战。"
                : "徽章卡片\n待解锁：生态守护者\n完成今日任务后可获得。");
            SetText(profileCollectionText, IsCurrentAnimalUnlocked
                ? $"动物图鉴卡片\n已解锁：{CurrentDisplayName}\n可继续聊天、做任务、生成科普卡片。"
                : $"动物图鉴卡片\n未解锁：{CurrentDisplayName}\n扫描识别卡后加入收藏。");
            SetText(profileActionText, $"{nextAction}\n今日知识：{CurrentLearnedFact()}");
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
            radius = Mathf.Clamp(radius, 0, Mathf.Max(0, size / 2 - 1));
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
                    var dx = x < radius ? center - x : x >= size - radius ? x - (size - radius - 0.5f) : 0f;
                    var dy = y < radius ? center - y : y >= size - radius ? y - (size - radius - 0.5f) : 0f;
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
            var hasFixedWidth = buttonRect != null && Mathf.Approximately(buttonRect.anchorMin.x, buttonRect.anchorMax.x);
            var iconOnly = hasFixedWidth && buttonRect.sizeDelta.x <= 120f;
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

}
