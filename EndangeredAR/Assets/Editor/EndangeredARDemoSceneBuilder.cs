using EndangeredAR.API;
using EndangeredAR.AI;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using EndangeredAR.Models;
using EndangeredAR.Missions;
using EndangeredAR.Progress;
using EndangeredAR.UI;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EndangeredARDemoSceneBuilder
{
    [MenuItem("Endangered AR/Set Local API To Mac LAN IP")]
    public static void SetLocalApiToLanIp()
    {
        var config = CreateApiConfig();
        config.baseUrl = $"http://{GetLanIpAddress()}:8000";
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Endangered AR", $"LocalApiConfig 已设置为：\n{config.baseUrl}", "OK");
    }

    [MenuItem("Endangered AR/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camera = new GameObject("Main Camera");
        var cameraComponent = camera.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = SensenDesignTokens.Forest950;
        cameraComponent.nearClipPlane = 0.1f;
        cameraComponent.farClipPlane = 20f;
        camera.transform.position = new Vector3(0f, 1.2f, -5f);
        camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.44f, 0f) - camera.transform.position);
        camera.tag = "MainCamera";

        var light = new GameObject("Directional Light");
        var lightComponent = light.AddComponent<Light>();
        lightComponent.type = LightType.Directional;
        lightComponent.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        var animal = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        animal.name = "Sensen Placeholder";
        animal.transform.position = new Vector3(0, 0.72f, 0);
        animal.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        animal.GetComponent<Renderer>().sharedMaterial = CreateMaterial();
        animal.AddComponent<SensenGlbLoader>();
        animal.SetActive(false);

        var scanObject = new GameObject("AR Image Scan Controller");
        var scanController = scanObject.AddComponent<ARImageScanController>();
        ConfigureScanner(scanController);

        var apiConfig = CreateApiConfig();
        var apiObject = new GameObject("Chat API Client");
        var chatApiClient = apiObject.AddComponent<ChatApiClient>();
        var apiSerialized = new SerializedObject(chatApiClient);
        apiSerialized.FindProperty("config").objectReferenceValue = apiConfig;
        apiSerialized.ApplyModifiedPropertiesWithoutUndo();

        var localChatObject = new GameObject("Local Knowledge Chat Service");
        var localChatService = localChatObject.AddComponent<LocalKnowledgeChatService>();
        var aiManager = CreateAIManager(chatApiClient, localChatService);

        var missionObject = new GameObject("Mission Controller");
        var missionController = missionObject.AddComponent<MissionController>();

        var sensenDefinition = AssetDatabase.LoadAssetAtPath<AnimalDefinition>("Assets/Resources/Animals/Sensen.asset");
        var catalogObject = new GameObject("Animal Catalog Service");
        var animalCatalog = catalogObject.AddComponent<AnimalCatalogService>();
        var catalogSerialized = new SerializedObject(animalCatalog);
        var definitions = catalogSerialized.FindProperty("definitions");
        definitions.arraySize = 1;
        definitions.GetArrayElementAtIndex(0).objectReferenceValue = sensenDefinition;
        catalogSerialized.FindProperty("defaultAnimalId").stringValue = "sensen";
        catalogSerialized.ApplyModifiedPropertiesWithoutUndo();

        var progressObject = new GameObject("Animal Progress Service");
        var animalProgress = progressObject.AddComponent<AnimalProgressService>();

        var experienceObject = new GameObject("Animal Experience Controller");
        var animalExperience = experienceObject.AddComponent<AnimalExperienceController>();
        var experienceSerialized = new SerializedObject(animalExperience);
        experienceSerialized.FindProperty("animalCatalogService").objectReferenceValue = animalCatalog;
        experienceSerialized.FindProperty("animalProgressService").objectReferenceValue = animalProgress;
        experienceSerialized.FindProperty("missionController").objectReferenceValue = missionController;
        experienceSerialized.FindProperty("modelLoader").objectReferenceValue = animal.GetComponent<AnimalModelLoader>();
        experienceSerialized.FindProperty("experienceHostTransform").objectReferenceValue = animal.transform;
        experienceSerialized.ApplyModifiedPropertiesWithoutUndo();

        var canvas = CreateCanvas();
        var homePanel = CreateHomePanel(canvas.transform);
        var homeTitle = CreateText(homePanel.transform, "Home Title", "濒危动物 AR 科普", new Vector2(0, 360), 54);
        homeTitle.fontStyle = FontStyle.Bold;
        var homeStatusText = CreateText(homePanel.transform, "Home Status Text", "发现：扫描识别卡，解锁森森互动体验", new Vector2(0, 190), 30);
        var bottomNav = CreateBottomNav(canvas.transform);
        var learnButton = CreateButton(bottomNav.transform, "Learn Button", "学习", new Vector2(-285, 0), new Vector2(230, 78));
        var discoverButton = CreateButton(bottomNav.transform, "Discover Button", "扫描", new Vector2(0, 22), new Vector2(176, 118));
        Button chatButton = null;
        var profileButton = CreateButton(bottomNav.transform, "Profile Button", "我的", new Vector2(285, 0), new Vector2(230, 78));

        var learnPanel = CreateFullPanel(canvas.transform, "Learn Panel");
        learnPanel.gameObject.SetActive(false);
        var learnTitle = CreateText(learnPanel.transform, "Learn Title", "Learn", new Vector2(0, 650), 56);
        learnTitle.fontStyle = FontStyle.Bold;
        CreateText(learnPanel.transform, "Learn Progress", "环保等级 Lv.1    学习进度 20%", new Vector2(0, 535), 32);
        CreateCardText(learnPanel.transform, "Species Card", "物种百科库\n缨冠灰叶猴 森森\n濒危等级：濒危\n栖息地：热带和亚热带森林", new Vector2(0, 330));
        CreateCardText(learnPanel.transform, "Mission Card", "每日守护任务\n帮助森森找到适合的食物\n奖励：森林小助手徽章 +20", new Vector2(0, 85));
        CreateCardText(learnPanel.transform, "Crisis Card", "生态危机演练区\n模拟森林砍伐对栖息地的影响", new Vector2(0, -160));
        var learnBackButton = CreateButton(learnPanel.transform, "Learn Back Button", "返回首页", new Vector2(0, -430), new Vector2(760, 82));

        var chatPanel = CreateFullPanel(canvas.transform, "Chat Panel");
        chatPanel.gameObject.SetActive(false);
        var chatTitle = CreateText(chatPanel.transform, "Chat Title", "Chat", new Vector2(0, 650), 56);
        chatTitle.fontStyle = FontStyle.Bold;
        var chatPageText = CreateChatScrollText(
            chatPanel.transform,
            "Chat Message List",
            "森森：你好！我是缨冠灰叶猴森森。你可以问我吃什么、住在哪里、为什么濒危。",
            new Vector2(0, 245),
            out var chatScrollRect
        );
        var chatInput = CreateInputField(chatPanel.transform, "Chat Input", new Vector2(-145, -125), new Vector2(520, 82));
        var sendLocalChatButton = CreateButton(chatPanel.transform, "Send Chat Button", "发送", new Vector2(285, -125), new Vector2(240, 82));
        Button quickFoodButton = null;
        Button quickDangerButton = null;
        Button quickProtectButton = null;
        var chatBackButton = CreateButton(chatPanel.transform, "Chat Back Button", "返回首页", new Vector2(0, -430), new Vector2(760, 82));

        var panel = CreatePanel(canvas.transform);
        panel.gameObject.name = "Scan And Model Panel";
        panel.gameObject.SetActive(false);
        var statusText = CreateText(panel.transform, "Status Text", "请将摄像头对准森森识别卡", new Vector2(0, 235), 40);
        var chatText = CreateText(panel.transform, "Chat Text", "识别成功后进入互动展示", new Vector2(0, 85), 32);
        chatText.alignment = TextAnchor.UpperCenter;

        var scanButton = CreateButton(panel.transform, "Scan Button", "模拟识别", new Vector2(0, -95), new Vector2(760, 96));
        var backHomeButton = CreateButton(panel.transform, "Back Home Button", "返回首页", new Vector2(0, -350), new Vector2(760, 76));
        Button foodButton = null;
        Button protectButton = null;

        var controllerObject = new GameObject("Demo App Controller");
        var demoController = controllerObject.AddComponent<DemoAppController>();
        var demoSerialized = new SerializedObject(demoController);
        demoSerialized.FindProperty("scanController").objectReferenceValue = scanController;
        demoSerialized.FindProperty("chatApiClient").objectReferenceValue = chatApiClient;
        demoSerialized.FindProperty("aiManager").objectReferenceValue = aiManager;
        demoSerialized.FindProperty("localChatService").objectReferenceValue = localChatService;
        demoSerialized.FindProperty("missionController").objectReferenceValue = missionController;
        demoSerialized.FindProperty("animalCatalog").objectReferenceValue = animalCatalog;
        demoSerialized.FindProperty("animalProgress").objectReferenceValue = animalProgress;
        demoSerialized.FindProperty("animalExperience").objectReferenceValue = animalExperience;
        demoSerialized.FindProperty("animalPlaceholder").objectReferenceValue = animal;
        demoSerialized.FindProperty("displayCamera").objectReferenceValue = cameraComponent;
        demoSerialized.FindProperty("homePanel").objectReferenceValue = homePanel.gameObject;
        demoSerialized.FindProperty("scanPanel").objectReferenceValue = panel.gameObject;
        demoSerialized.FindProperty("learnPanel").objectReferenceValue = learnPanel.gameObject;
        demoSerialized.FindProperty("chatPanel").objectReferenceValue = chatPanel.gameObject;
        demoSerialized.FindProperty("bottomNav").objectReferenceValue = bottomNav.gameObject;
        demoSerialized.FindProperty("statusText").objectReferenceValue = statusText;
        demoSerialized.FindProperty("chatText").objectReferenceValue = chatText;
        demoSerialized.FindProperty("chatPageText").objectReferenceValue = chatPageText;
        demoSerialized.FindProperty("homeStatusText").objectReferenceValue = homeStatusText;
        demoSerialized.FindProperty("chatScrollRect").objectReferenceValue = chatScrollRect;
        demoSerialized.FindProperty("chatInput").objectReferenceValue = chatInput;
        demoSerialized.FindProperty("discoverButton").objectReferenceValue = discoverButton;
        demoSerialized.FindProperty("learnButton").objectReferenceValue = learnButton;
        demoSerialized.FindProperty("chatButton").objectReferenceValue = chatButton;
        demoSerialized.FindProperty("profileButton").objectReferenceValue = profileButton;
        demoSerialized.FindProperty("scanButton").objectReferenceValue = scanButton;
        demoSerialized.FindProperty("backHomeButton").objectReferenceValue = backHomeButton;
        demoSerialized.FindProperty("learnBackButton").objectReferenceValue = learnBackButton;
        demoSerialized.FindProperty("chatBackButton").objectReferenceValue = chatBackButton;
        demoSerialized.FindProperty("sendLocalChatButton").objectReferenceValue = sendLocalChatButton;
        demoSerialized.FindProperty("quickFoodButton").objectReferenceValue = quickFoodButton;
        demoSerialized.FindProperty("quickDangerButton").objectReferenceValue = quickDangerButton;
        demoSerialized.FindProperty("quickProtectButton").objectReferenceValue = quickProtectButton;
        demoSerialized.FindProperty("askFoodButton").objectReferenceValue = foodButton;
        demoSerialized.FindProperty("askProtectButton").objectReferenceValue = protectButton;
        demoSerialized.FindProperty("uiFont").objectReferenceValue = GetUiFont();
        demoSerialized.ApplyModifiedPropertiesWithoutUndo();

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DemoScene.unity");
        EditorUtility.DisplayDialog("Endangered AR", "DemoScene 已生成：Assets/Scenes/DemoScene.unity", "OK");
    }

    private static void ConfigureScanner(ARImageScanController scanController)
    {
        var scannerSerialized = new SerializedObject(scanController);
        scannerSerialized.FindProperty("defaultAnimalId").stringValue = "sensen";
        var mappings = scannerSerialized.FindProperty("markerAnimals");
        mappings.arraySize = 1;
        mappings.GetArrayElementAtIndex(0).FindPropertyRelative("markerName").stringValue = "sensen_marker";
        mappings.GetArrayElementAtIndex(0).FindPropertyRelative("animalId").stringValue = "sensen";
        scannerSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ApiConfig CreateApiConfig()
    {
        const string path = "Assets/Config/LocalApiConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<ApiConfig>(path);
        if (config != null)
        {
            return config;
        }

        config = ScriptableObject.CreateInstance<ApiConfig>();
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        return config;
    }

    private static AIManager CreateAIManager(
        ChatApiClient chatApiClient,
        LocalKnowledgeChatService localChatService)
    {
        const string path = "Assets/Config/LocalAIConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<AIConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<AIConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
        }

        var managerObject = new GameObject("AI Manager");
        var manager = managerObject.AddComponent<AIManager>();
        var managerSerialized = new SerializedObject(manager);
        managerSerialized.FindProperty("aiConfig").objectReferenceValue = config;
        managerSerialized.FindProperty("chatApiClient").objectReferenceValue = chatApiClient;
        managerSerialized.FindProperty("localKnowledgeService").objectReferenceValue = localChatService;
        managerSerialized.ApplyModifiedPropertiesWithoutUndo();
        return manager;
    }

    private static string GetLanIpAddress()
    {
        foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                continue;
            }

            var value = address.ToString();
            if (!value.StartsWith("127."))
            {
                return value;
            }
        }

        return "127.0.0.1";
    }

    private static Material CreateMaterial()
    {
        const string path = "Assets/Config/SensenPlaceholder.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Standard"));
        material.color = SensenDesignTokens.Leaf500;
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static Image CreatePanel(Transform parent)
    {
        var panelObject = new GameObject("Bottom Panel");
        panelObject.transform.SetParent(parent, false);
        var image = panelObject.AddComponent<Image>();
        image.color = SensenDesignTokens.WithAlpha(SensenDesignTokens.Forest900, 0.82f);
        var rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.04f, 0.02f);
        rect.anchorMax = new Vector2(0.96f, 0.42f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static Image CreateHomePanel(Transform parent)
    {
        var panelObject = new GameObject("Home Panel");
        panelObject.transform.SetParent(parent, false);
        var image = panelObject.AddComponent<Image>();
        image.color = SensenDesignTokens.WithAlpha(SensenDesignTokens.Forest900, 0.94f);
        var rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.04f, 0.18f);
        rect.anchorMax = new Vector2(0.96f, 0.88f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static Image CreateBottomNav(Transform parent)
    {
        var panelObject = new GameObject("Bottom Navigation");
        panelObject.transform.SetParent(parent, false);
        var image = panelObject.AddComponent<Image>();
        image.color = SensenDesignTokens.WithAlpha(SensenDesignTokens.Forest950, 0.96f);
        var rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.12f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static Image CreateFullPanel(Transform parent, string name)
    {
        var panelObject = new GameObject(name);
        panelObject.transform.SetParent(parent, false);
        var image = panelObject.AddComponent<Image>();
        image.color = SensenDesignTokens.WithAlpha(SensenDesignTokens.Forest950, 0.98f);
        var rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.04f, 0.14f);
        rect.anchorMax = new Vector2(0.96f, 0.96f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static Text CreateCardText(Transform parent, string name, string value, Vector2 anchoredPosition)
    {
        var cardObject = new GameObject(name);
        cardObject.transform.SetParent(parent, false);
        var image = cardObject.AddComponent<Image>();
        image.color = SensenDesignTokens.Forest800;
        var rect = cardObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(820, 190);
        rect.anchoredPosition = anchoredPosition;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(cardObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = 30;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 20;
        text.resizeTextMaxSize = 30;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(36, 18);
        textRect.offsetMax = new Vector2(-36, -18);
        return text;
    }

    private static Text CreateChatScrollText(Transform parent, string name, string value, Vector2 anchoredPosition, out ScrollRect scrollRect)
    {
        var cardObject = new GameObject(name);
        cardObject.transform.SetParent(parent, false);
        var image = cardObject.AddComponent<Image>();
        image.color = SensenDesignTokens.Forest800;
        scrollRect = cardObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 28f;

        var rect = cardObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(820, 520);
        rect.anchoredPosition = anchoredPosition;

        var viewportObject = new GameObject("Viewport");
        viewportObject.transform.SetParent(cardObject.transform, false);
        var viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(28, 22);
        viewportRect.offsetMax = new Vector2(-28, -22);
        viewportObject.AddComponent<RectMask2D>();

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(viewportObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = 30;
        text.resizeTextForBestFit = false;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;

        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 1);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.pivot = new Vector2(0.5f, 1);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0, 500);
        var fitter = textObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = textRect;
        return text;
    }

    private static Text CreateText(Transform parent, string name, string value, Vector2 anchoredPosition, int fontSize)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(18, fontSize - 12);
        text.resizeTextMaxSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        var rect = text.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(820, 140);
        rect.anchoredPosition = anchoredPosition;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.AddComponent<Image>();
        image.color = SensenDesignTokens.Moss650;
        var button = buttonObject.AddComponent<Button>();

        var rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var labelObject = new GameObject("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);
        var text = labelObject.AddComponent<Text>();
        text.text = label;
        text.font = GetUiFont();
        text.fontSize = 32;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 22;
        text.resizeTextMaxSize = 32;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private static InputField CreateInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        var inputObject = new GameObject(name);
        inputObject.transform.SetParent(parent, false);
        var image = inputObject.AddComponent<Image>();
        image.color = new Color(0.95f, 0.98f, 0.96f, 0.96f);
        var input = inputObject.AddComponent<InputField>();

        var rect = input.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(inputObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.font = GetUiFont();
        text.fontSize = 30;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 22;
        text.resizeTextMaxSize = 30;
        text.color = new Color(0.02f, 0.08f, 0.06f);
        text.alignment = TextAnchor.MiddleLeft;
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(22, 8);
        textRect.offsetMax = new Vector2(-22, -8);

        var placeholderObject = new GameObject("Placeholder");
        placeholderObject.transform.SetParent(inputObject.transform, false);
        var placeholder = placeholderObject.AddComponent<Text>();
        placeholder.text = "输入问题";
        placeholder.font = GetUiFont();
        placeholder.fontSize = 30;
        placeholder.resizeTextForBestFit = true;
        placeholder.resizeTextMinSize = 22;
        placeholder.resizeTextMaxSize = 30;
        placeholder.color = new Color(0.25f, 0.35f, 0.32f, 0.75f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        var placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(22, 8);
        placeholderRect.offsetMax = new Vector2(-22, -8);

        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 80;
        return input;
    }

    private static Font GetUiFont()
    {
        const string embeddedFontPath = "Assets/Fonts/ArialUnicode.ttf";
        AssetDatabase.ImportAsset(embeddedFontPath, ImportAssetOptions.ForceUpdate);
        var embeddedFont = AssetDatabase.LoadAssetAtPath<Font>(embeddedFontPath);
        if (embeddedFont != null)
        {
            return embeddedFont;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
