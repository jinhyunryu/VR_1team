using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

[InitializeOnLoad]
public static class CreateTitleLobbyScene
{
    const float CanvasWidth = 1672f;
    const float CanvasHeight = 941f;

    const string SceneFolder = "Assets/_Project/04_CreatureUI/Title/Scenes";
    const string ScenePath = SceneFolder + "/VR_Title_Lobby.unity";
    const string TitleFolder = "Assets/_Project/04_CreatureUI/Title/";
    const string LobbyFolder = "Assets/_Project/04_CreatureUI/Title/Lobby/";
    const string PlaylistFolder = LobbyFolder + "Playlist/";
    const string GuideFolder = LobbyFolder + "GuideUI/";
    const string DistanceBarFolder = "Assets/_Project/04_CreatureUI/PlaySceneUI/Distance_Bar/";
    const string XrOriginPrefabPath = "Assets/_Project/01_VRHands/XRHandsRig/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string RequestPath = "Temp/CreateTitleLobbyScene.request";
    const string InstallPlaylistRequestPath = "Temp/InstallTitlePlaylistUI.request";
    const string InstallPlaylistListBuilderRequestPath = "Temp/InstallTitlePlaylistListBuilder.request";
    const string InstallGuideRequestPath = "Temp/InstallTitleGuideUI.request";
    const string InstallDistanceBarPreviewRequestPath = "Temp/InstallTitleDistanceBarPreview.request";

    static readonly Color DeepBlue = new Color(0.02f, 0.1f, 0.48f, 1f);
    static readonly Color PlaylistHighlightColor = new Color(0.12f, 0.75f, 1f, 0.32f);
    static readonly Color PlaylistDimColor = new Color(0.01f, 0.06f, 0.18f, 0.42f);
    static readonly string[] PlaylistSongNames =
    {
        "Ocean Beat",
        "Coral Pop",
        "Wave Runner",
        "Starfish Melody",
        "Blue Horizon"
    };
    static readonly string[] PlaylistSongDifficulties =
    {
        "EASY",
        "NORMAL",
        "HARD",
        "EXPERT",
        "EXPERT+"
    };
    static readonly int[] PlaylistSongLevels = { 1, 2, 3, 4, 5 };

    static CreateTitleLobbyScene()
    {
        EditorApplication.delayCall += RunPendingRequest;
    }

    static void RunPendingRequest()
    {
        if (File.Exists(InstallPlaylistRequestPath))
        {
            File.Delete(InstallPlaylistRequestPath);
            InstallPlaylistUiInScene();
        }

        if (File.Exists(InstallPlaylistListBuilderRequestPath))
        {
            File.Delete(InstallPlaylistListBuilderRequestPath);
            InstallPlaylistListBuilderInScene();
        }

        if (File.Exists(InstallGuideRequestPath))
        {
            File.Delete(InstallGuideRequestPath);
            InstallGuideUiInScene();
        }

        if (File.Exists(InstallDistanceBarPreviewRequestPath))
        {
            File.Delete(InstallDistanceBarPreviewRequestPath);
            InstallDistanceBarPreviewInScene();
        }

        if (File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
            CreateScene();
        }
    }

    [MenuItem("Tools/Creature UI/Create Title Lobby Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory(SceneFolder);

        var previousActiveScene = SceneManager.GetActiveScene();
        var sceneMode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, sceneMode);
        EditorSceneManager.SetActiveScene(scene);
        scene.name = "VR_Title_Lobby";

        CreateLighting();
        CreateXrOrigin();
        CreateXrInteractionManager();
        CreateEventSystem();

        var audioMaster = CreateAudioMaster();
        var controllerObject = new GameObject("Title Lobby Canvas Controller");
        var controller = controllerObject.AddComponent<TitleLobbyCanvasController>();
        controller.audioMaster = audioMaster;

        var uiRoot = new GameObject("VR UI Root");
        uiRoot.transform.position = new Vector3(0f, 0f, 2.2f);
        controller.cameraLockedRoot = uiRoot.transform;
        controller.lockToCamera = true;
        controller.yawOnlyCameraLock = true;

        var titleCanvas = CreateWorldCanvas("TitleCanvas", uiRoot.transform, 10);
        var lobbyCanvas = CreateWorldCanvas("LobbyCanvas", uiRoot.transform, 20);

        controller.titleCanvas = titleCanvas.gameObject;
        controller.lobbyCanvas = lobbyCanvas.gameObject;

        BuildTitleCanvas(titleCanvas.transform, controller);
        BuildLobbyCanvas(lobbyCanvas.transform, controller);

        lobbyCanvas.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        if (!Application.isBatchMode && previousActiveScene.IsValid())
            EditorSceneManager.SetActiveScene(previousActiveScene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created Title/Lobby VR scene at {ScenePath}");
    }

    static void CreateLighting()
    {
        var lightObject = new GameObject("Directional Light");
        lightObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f), Quaternion.Euler(45f, -30f, 0f));

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
    }

    static void CreateXrOrigin()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "XR Origin (XR Rig)";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return;
        }

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.01f;
    }

    static void CreateXrInteractionManager()
    {
        var managerObject = new GameObject("XR Interaction Manager");
        managerObject.AddComponent<XRInteractionManager>();
    }

    static void CreateEventSystem()
    {
        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<XRUIInputModule>();
    }

    static TitleUIAudioMaster CreateAudioMaster()
    {
        var audioObject = new GameObject(TitleUIAudioMaster.DefaultObjectName);
        return audioObject.AddComponent<TitleUIAudioMaster>();
    }

    static Canvas CreateWorldCanvas(string name, Transform parent, int sortingOrder)
    {
        var canvasObject = new GameObject(name);
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = Vector3.zero;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = new Vector3(0.0016f, 0.0016f, 0.0016f);

        var rectTransform = canvasObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = sortingOrder;

        var mainCamera = Camera.main;
        if (mainCamera != null)
            canvas.worldCamera = mainCamera;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        return canvas;
    }

    static void BuildTitleCanvas(Transform canvasTransform, TitleLobbyCanvasController controller)
    {
        AddRawImage(canvasTransform, "Title_BG", TitleFolder + "Title_BG.png", Vector2.zero, new Vector2(CanvasWidth, CanvasHeight), false);
        AddRawImage(canvasTransform, "title_main", TitleFolder + "title_main.png", PixelCenter(392f, 430f), new Vector2(880f, 587f), false);

        var touchButton = AddButton(canvasTransform, "TitleTouchToStartButton", null, PixelCenter(836f, 816f), new Vector2(560f, 110f));
        touchButton.GetComponent<TitleUIButtonFeedback>().SetClickSound(TitleUIButtonFeedback.ButtonClickSound.None);
        var touchStartImage = AddRawImage(touchButton.transform, "Title_Start_1", TitleFolder + "Title_Start 1.png", Vector2.zero, new Vector2(450f, 45f), false);
        touchStartImage.gameObject.AddComponent<TitleStartPulseEffect>();

        var menuRoot = CreateRectObject(canvasTransform, "TitleMenuRoot", Vector2.zero, new Vector2(CanvasWidth, CanvasHeight));
        var menuCanvasGroup = menuRoot.gameObject.AddComponent<CanvasGroup>();
        menuCanvasGroup.alpha = 1f;
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;
        var menuVisualSize = new Vector2(500f, 166f);
        var menuHitSize = new Vector2(500f, 82f);
        var startButton = AddImageButton(menuRoot, "TitleStartButton", TitleFolder + "Title_Start.png", PixelCenter(836f, 548f), menuVisualSize, menuHitSize);
        var joinButton = AddImageButton(menuRoot, "TitleJoinGameButton", TitleFolder + "Title_Joingame.png", PixelCenter(836f, 646f), menuVisualSize, menuHitSize);
        var settingButton = AddImageButton(menuRoot, "TitleSettingButton", TitleFolder + "Title_Setting.png", PixelCenter(836f, 744f), menuVisualSize, menuHitSize);
        var exitButton = AddImageButton(menuRoot, "TitleExitButton", TitleFolder + "Title_Exit.png", PixelCenter(836f, 842f), menuVisualSize, menuHitSize);
        menuRoot.gameObject.SetActive(false);

        UnityEventTools.AddPersistentListener(touchButton.onClick, controller.ShowTitleMenu);
        UnityEventTools.AddPersistentListener(startButton.onClick, controller.CreateHostRoom);
        UnityEventTools.AddPersistentListener(joinButton.onClick, controller.JoinHostRoom);
        UnityEventTools.AddPersistentListener(settingButton.onClick, controller.OpenTitleSettings);
        UnityEventTools.AddPersistentListener(exitButton.onClick, controller.QuitApplication);

        controller.titleTouchPrompt = touchButton.gameObject;
        controller.titleMenuRoot = menuRoot.gameObject;
        controller.titleTouchButton = touchButton;
        controller.titleStartButton = startButton;
        controller.titleJoinButton = joinButton;
        controller.titleSettingButton = settingButton;
        controller.titleExitButton = exitButton;
    }

    static void BuildLobbyCanvas(Transform canvasTransform, TitleLobbyCanvasController controller)
    {
        AddRawImage(canvasTransform, "Lobby_BG", LobbyFolder + "Lobby_BG.png", Vector2.zero, new Vector2(CanvasWidth, CanvasHeight), false);

        AddButton(canvasTransform, "SettingsButton", LobbyFolder + "Lobby_Seeting-.png", PixelCenter(115f, 98f), new Vector2(120f, 120f));
        AddButton(canvasTransform, "EmotesButton", LobbyFolder + "Lobby_Emotes-.png", PixelCenter(230f, 98f), new Vector2(120f, 120f));

        AddRawImage(canvasTransform, "Lobby_Title", LobbyFolder + "Lobby_Title-.png", PixelCenter(836f, 116f), new Vector2(894f, 279f), false);
        AddRawImage(canvasTransform, "Lobby_Information", LobbyFolder + "Lobby_Information-.png", PixelCenter(836f, 287f), new Vector2(1000f, 250f), false);

        AddText(canvasTransform, "RoomCodeText", "FISH123", PixelCenter(585f, 286f), new Vector2(230f, 48f), 34f, DeepBlue, TextAlignmentOptions.MidlineLeft);
        AddText(canvasTransform, "PlayersText", "1 / 4", PixelCenter(852f, 286f), new Vector2(150f, 48f), 34f, DeepBlue, TextAlignmentOptions.Center);
        AddText(canvasTransform, "HostText", "Player 1", PixelCenter(1128f, 286f), new Vector2(220f, 48f), 32f, DeepBlue, TextAlignmentOptions.MidlineLeft);

        AddPlayerSlot(canvasTransform, 1, LobbyFolder + "Lobby_player1-.png", PixelCenter(585f, 426f), "Player 1", true);
        AddPlayerSlot(canvasTransform, 2, LobbyFolder + "Lobby_player2-.png", PixelCenter(1088f, 426f), "Player 2", false);
        AddPlayerSlot(canvasTransform, 3, LobbyFolder + "Lobby_Player3-.png", PixelCenter(585f, 624f), "Player 3", false);
        AddPlayerSlot(canvasTransform, 4, LobbyFolder + "Lobby_player4-.png", PixelCenter(1088f, 624f), "Player 4", false);

        var startButton = AddButton(canvasTransform, "LobbyStartButton", LobbyFolder + "Lobby_Start-.png", PixelCenter(500f, 782f), new Vector2(360f, 120f));
        var readyButton = AddButton(canvasTransform, "LobbyReadyButton", LobbyFolder + "Lobby_Ready-.png", PixelCenter(836f, 782f), new Vector2(360f, 120f));
        var exitButton = AddButton(canvasTransform, "LobbyExitButton", LobbyFolder + "Lobby_Exit-.png", PixelCenter(1172f, 782f), new Vector2(360f, 120f));

        UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartLobbyGame);
        UnityEventTools.AddPersistentListener(readyButton.onClick, controller.ToggleLobbyReady);
        UnityEventTools.AddPersistentListener(exitButton.onClick, controller.ShowTitleCanvas);

        controller.lobbyStartButton = startButton;
        controller.lobbyReadyButton = readyButton;
        controller.lobbyExitButton = exitButton;

        BuildPlaylistUI(canvasTransform, controller);
        BuildGuideUI(canvasTransform, controller);

        controller.localPlayerNumber = 1;
    }

    [MenuItem("Tools/Creature UI/Install Title Playlist UI")]
    public static void InstallPlaylistUiInScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"Title lobby scene not found at {ScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<TitleLobbyCanvasController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("TitleLobbyCanvasController was not found in the lobby scene.");
            return;
        }

        var lobbyCanvas = controller.lobbyCanvas != null
            ? controller.lobbyCanvas.transform
            : GameObject.Find("LobbyCanvas")?.transform;

        if (lobbyCanvas == null)
        {
            Debug.LogError("LobbyCanvas was not found in the lobby scene.");
            return;
        }

        DestroyDirectChild(lobbyCanvas, "PlaylistButton");
        DestroyDirectChild(lobbyCanvas, "PlaySongBanner");
        DestroyDirectChild(lobbyCanvas, "PlaylistPopupRoot");
        BuildPlaylistUI(lobbyCanvas, controller);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Installed Title playlist UI into the lobby scene.");
    }

    [MenuItem("Tools/Creature UI/Install Title Guide UI")]
    public static void InstallGuideUiInScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"Title lobby scene not found at {ScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<TitleLobbyCanvasController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("TitleLobbyCanvasController was not found in the lobby scene.");
            return;
        }

        var lobbyCanvas = controller.lobbyCanvas != null
            ? controller.lobbyCanvas.transform
            : GameObject.Find("LobbyCanvas")?.transform;

        if (lobbyCanvas == null)
        {
            Debug.LogError("LobbyCanvas was not found in the lobby scene.");
            return;
        }

        DestroyDirectChild(lobbyCanvas, "GuideButton");
        DestroyDirectChild(lobbyCanvas, "GuidePopupRoot");
        BuildGuideUI(lobbyCanvas, controller);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Installed Title guide UI into the lobby scene.");
    }

    [MenuItem("Tools/Creature UI/Install Title Distance Bar Preview")]
    public static void InstallDistanceBarPreviewInScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"Title lobby scene not found at {ScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<TitleLobbyCanvasController>(FindObjectsInactive.Include);
        var lobbyCanvas = controller != null && controller.lobbyCanvas != null
            ? controller.lobbyCanvas.transform
            : GameObject.Find("LobbyCanvas")?.transform;

        if (lobbyCanvas == null)
        {
            Debug.LogError("LobbyCanvas was not found in the lobby scene.");
            return;
        }

        DestroyDirectChild(lobbyCanvas, "DistanceBarPreviewRoot");
        BuildDistanceBarPreviewUI(lobbyCanvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Installed Distance Bar preview object into the title lobby scene.");
    }

    [MenuItem("Tools/Creature UI/Install Title Playlist List Builder")]
    public static void InstallPlaylistListBuilderInScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"Title lobby scene not found at {ScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<TitleLobbyCanvasController>(FindObjectsInactive.Include);
        var popupPanel = GameObject.Find("PlaylistPopupPanel");
        var templateButton = GameObject.Find("PlaylistSong1Button")?.GetComponent<Button>();

        if (popupPanel == null || templateButton == null)
        {
            Debug.LogError("PlaylistPopupPanel or PlaylistSong1Button was not found in the lobby scene.");
            return;
        }

        var builder = popupPanel.GetComponent<TitlePlaylistSongListBuilder>();
        if (builder == null)
            builder = popupPanel.AddComponent<TitlePlaylistSongListBuilder>();

        builder.Configure(templateButton, popupPanel.transform, controller);
        EditorUtility.SetDirty(builder);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Installed Title playlist list builder into the lobby scene.");
    }

    static void BuildGuideUI(Transform canvasTransform, TitleLobbyCanvasController controller)
    {
        var guideButton = AddButton(
            canvasTransform,
            "GuideButton",
            GuideFolder + "GuideUI_Icon.png",
            PixelCenter(500f, 890f),
            new Vector2(92f, 92f));

        var popupRoot = CreateRectObject(canvasTransform, "GuidePopupRoot", Vector2.zero, new Vector2(CanvasWidth, CanvasHeight));
        var dimBlocker = AddColorImage(popupRoot, "GuideDimBlocker", Vector2.zero, new Vector2(CanvasWidth, CanvasHeight), PlaylistDimColor, true);
        var dimButton = dimBlocker.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dimBlocker;
        dimBlocker.gameObject.AddComponent<TitleUIButtonFeedback>();

        var popupPanel = CreateRectObject(popupRoot, "GuidePopupPanel", PixelCenter(836f, 470f), new Vector2(920f, 690f));
        AddRawImage(popupPanel, "GuidePopupImage", GuideFolder + "GuideUI.png", Vector2.zero, new Vector2(920f, 690f), false);
        var closeButton = AddGuideCloseButton(popupPanel, new Vector2(410f, 300f));

        UnityEventTools.AddPersistentListener(guideButton.onClick, controller.ShowGuidePopup);
        UnityEventTools.AddPersistentListener(dimButton.onClick, controller.HideGuidePopup);
        UnityEventTools.AddPersistentListener(closeButton.onClick, controller.HideGuidePopup);

        controller.guideButton = guideButton;
        controller.guidePopupRoot = popupRoot.gameObject;
        controller.guideCloseButton = closeButton;

        var playlistPopupRoot = canvasTransform.Find("PlaylistPopupRoot");
        if (playlistPopupRoot != null)
            guideButton.transform.SetSiblingIndex(playlistPopupRoot.GetSiblingIndex());

        popupRoot.SetAsLastSibling();

        popupRoot.gameObject.SetActive(false);
    }

    static Button AddGuideCloseButton(Transform parent, Vector2 center)
    {
        var closeImage = AddColorImage(parent, "GuideCloseButton", center, new Vector2(74f, 74f), new Color(0.02f, 0.26f, 0.88f, 0.9f), true);
        var closeButton = closeImage.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeImage.gameObject.AddComponent<TitleUIButtonFeedback>();

        var slashA = AddColorImage(closeButton.transform, "GuideCloseButtonSlashA", Vector2.zero, new Vector2(44f, 8f), Color.white, false);
        slashA.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var slashB = AddColorImage(closeButton.transform, "GuideCloseButtonSlashB", Vector2.zero, new Vector2(44f, 8f), Color.white, false);
        slashB.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);

        return closeButton;
    }

    static void BuildDistanceBarPreviewUI(Transform canvasTransform)
    {
        var root = CreateRectObject(canvasTransform, "DistanceBarPreviewRoot", PixelCenter(836f, 470f), new Vector2(1300f, 430f));

        AddRawImage(root, "DistanceBarBG", DistanceBarFolder + "Distance_Bar_BG.png", Vector2.zero, new Vector2(1117f, 223f), false);
        AddRawImage(root, "DistanceBarFillSample", DistanceBarFolder + "Distance_Bar_Loading_player.png", new Vector2(-260f, 0f), new Vector2(540f, 108f), false);

        AddRawImage(root, "DistanceBarAnotherPlayerBar1", DistanceBarFolder + "Distance_Bar_AnotherPlayerBar1V2.png", new Vector2(-210f, 22f), new Vector2(54f, 126f), false);
        AddRawImage(root, "DistanceBarAnotherPlayerBar2", DistanceBarFolder + "Distance_Bar_AnotherPlayerBar2V2.png", new Vector2(118f, 22f), new Vector2(54f, 126f), false);
        AddRawImage(root, "DistanceBarAnotherPlayerBar3", DistanceBarFolder + "Distance_Bar_AnotherPlayerBar3V2.png", new Vector2(298f, 22f), new Vector2(54f, 126f), false);

        AddRawImage(root, "DistanceBarStartIcon", DistanceBarFolder + "Distance_Bar_Start_Icon.png", new Vector2(-558.5f, 0f), new Vector2(170f, 170f), false);
        AddRawImage(root, "DistanceBarFinishIcon", DistanceBarFolder + "Distance_Bar_Finish_Icon.png", new Vector2(558.5f, 0f), new Vector2(170f, 170f), false);
        AddRawImage(root, "DistanceBarPlayerIcon", DistanceBarFolder + "Distance_Bar_Player_Loading_Icon.png", new Vector2(0f, 36f), new Vector2(106f, 106f), false);
        AddRawImage(root, "DistanceBarTitle", DistanceBarFolder + "Distance_Bar_Title-removebg-preview.png", new Vector2(0f, 166f), new Vector2(520f, 173f), false);

        root.SetAsLastSibling();
    }

    static void BuildPlaylistUI(Transform canvasTransform, TitleLobbyCanvasController controller)
    {
        var playlistButton = AddButton(
            canvasTransform,
            "PlaylistButton",
            PlaylistFolder + "Playlist_Icon.png",
            PixelCenter(836f, 890f),
            new Vector2(92f, 92f));
        var currentSongBanner = AddRawImage(
            canvasTransform,
            "PlaySongBanner",
            PlaylistFolder + "PlaySong_banner.png",
            PixelCenter(1072f, 890f),
            new Vector2(420f, 140f),
            false);
        var currentSongText = AddText(
            currentSongBanner.transform,
            "CurrentSongNameText",
            PlaylistSongNames[0],
            new Vector2(36f, -10f),
            new Vector2(270f, 48f),
            26f,
            DeepBlue,
            TextAlignmentOptions.Center);

        var popupRoot = CreateRectObject(canvasTransform, "PlaylistPopupRoot", Vector2.zero, new Vector2(CanvasWidth, CanvasHeight));
        var dimBlocker = AddColorImage(popupRoot, "PlaylistDimBlocker", Vector2.zero, new Vector2(CanvasWidth, CanvasHeight), PlaylistDimColor, true);
        var dimButton = dimBlocker.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dimBlocker;
        dimBlocker.gameObject.AddComponent<TitleUIButtonFeedback>();

        var popupPanel = CreateRectObject(popupRoot, "PlaylistPopupPanel", PixelCenter(836f, 470f), new Vector2(580f, 724f));
        AddRawImage(popupPanel, "PlaylistPopupBG", PlaylistFolder + "Playlist_popup_BG.png", Vector2.zero, new Vector2(580f, 724f), false);
        AddRawImage(popupPanel, "PlaylistTitle", PlaylistFolder + "Playlist_title.png", new Vector2(0f, 318f), new Vector2(520f, 173f), false);

        var songButtons = new Button[PlaylistSongNames.Length];
        var selectedHighlights = new GameObject[PlaylistSongNames.Length];
        for (var i = 0; i < PlaylistSongNames.Length; i++)
        {
            var center = new Vector2(0f, 184f - i * 92f);
            songButtons[i] = AddPlaylistSongButton(
                popupPanel,
                i,
                PlaylistSongNames[i],
                PlaylistSongDifficulties[i],
                PlaylistSongLevels[i],
                center,
                out selectedHighlights[i]);
        }

        var selectButton = AddButton(popupPanel, "PlaylistSelectButton", PlaylistFolder + "Playlist_Select_Button.png", new Vector2(-142f, -304f), new Vector2(240f, 120f));
        var cancelButton = AddButton(popupPanel, "PlaylistCancelButton", PlaylistFolder + "Playlist_cancel_Button.png", new Vector2(142f, -304f), new Vector2(240f, 120f));

        UnityEventTools.AddPersistentListener(playlistButton.onClick, controller.ShowPlaylistPopup);
        UnityEventTools.AddPersistentListener(dimButton.onClick, controller.HidePlaylistPopup);
        UnityEventTools.AddPersistentListener(selectButton.onClick, controller.ConfirmPlaylistSelection);
        UnityEventTools.AddPersistentListener(cancelButton.onClick, controller.HidePlaylistPopup);

        for (var i = 0; i < songButtons.Length; i++)
            UnityEventTools.AddIntPersistentListener(songButtons[i].onClick, controller.SelectPlaylistSong, i);

        controller.playlistButton = playlistButton;
        controller.currentSongBannerRoot = currentSongBanner.gameObject;
        controller.currentSongBannerText = currentSongText;
        controller.playlistPopupRoot = popupRoot.gameObject;
        controller.playlistConfirmButton = selectButton;
        controller.playlistCancelButton = cancelButton;
        controller.playlistSongButtons = songButtons;
        controller.playlistSongSelectedHighlights = selectedHighlights;
        controller.playlistSongNames = PlaylistSongNames;
        controller.SelectPlaylistSong(0);
        // Keep the popup visible in edit mode so it can be positioned in the Scene view.
        // TitleLobbyCanvasController hides it again during Awake.
        var builder = popupPanel.gameObject.AddComponent<TitlePlaylistSongListBuilder>();
        builder.Configure(songButtons[0], popupPanel, controller);
        popupRoot.gameObject.SetActive(true);
    }

    static Button AddPlaylistSongButton(
        Transform parent,
        int index,
        string songName,
        string difficulty,
        int level,
        Vector2 center,
        out GameObject selectedHighlight)
    {
        var songButton = AddButton(parent, $"PlaylistSong{index + 1}Button", null, center, new Vector2(500f, 82f));

        var highlight = AddColorImage(songButton.transform, "SelectedHighlight", Vector2.zero, new Vector2(510f, 88f), PlaylistHighlightColor, false);
        selectedHighlight = highlight.gameObject;

        AddRawImage(songButton.transform, "SongBanner", PlaylistFolder + "Playlist_Songbanner.png", Vector2.zero, new Vector2(500f, 92f), false);
        AddText(songButton.transform, "SongNumber", (index + 1).ToString(), new Vector2(-228f, 7f), new Vector2(38f, 38f), 28f, Color.white, TextAlignmentOptions.Center);
        AddText(songButton.transform, "SongName", songName, new Vector2(-56f, 7f), new Vector2(250f, 42f), 25f, DeepBlue, TextAlignmentOptions.MidlineLeft);
        AddText(songButton.transform, "Difficulty", difficulty, new Vector2(170f, 22f), new Vector2(104f, 28f), 15f, Color.white, TextAlignmentOptions.Center);

        var starCount = Mathf.Clamp(level, 1, 5);
        for (var i = 0; i < starCount; i++)
            AddRawImage(songButton.transform, $"LevelStar{i + 1}", PlaylistFolder + "Playlist_Song_level_Icon.png", new Vector2(117f + i * 28f, -18f), new Vector2(25f, 25f), false);

        selectedHighlight.SetActive(false);
        return songButton;
    }

    static void DestroyDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return;

        var child = parent.Find(childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    static void AddPlayerSlot(Transform parent, int playerNumber, string cardPath, Vector2 center, string playerName, bool isHost)
    {
        var name = $"Player{playerNumber}";
        AddRawImage(parent, name + "_Card", cardPath, center, new Vector2(520f, 346f), false);
        AddText(parent, name + "_Name", playerName, center + new Vector2(108f, 43f), new Vector2(250f, 58f), 40f, DeepBlue, TextAlignmentOptions.MidlineLeft);

        if (isHost)
            AddRawImage(parent, name + "_HostBadge", LobbyFolder + "Lobby_player1_Host.png", center + new Vector2(164f, 43f), new Vector2(220f, 66f), false);

        var stateCenter = center + new Vector2(66f, -58f);
        var readyOn = AddRawImage(parent, name + "_state_ReadyOn", LobbyFolder + "Lobby_player_ReadyOn-.png", stateCenter, new Vector2(320f, 106f), false);
        var connect = AddRawImage(parent, name + "_state_Connect", LobbyFolder + "Lobby_player_Connecting.png", stateCenter, new Vector2(320f, 106f), false);
        var readyOff = AddRawImage(parent, name + "_state_ReadyOff", LobbyFolder + "Lobby_player_ReadyOff-.png", stateCenter, new Vector2(320f, 106f), false);

        readyOn.gameObject.SetActive(false);
        connect.gameObject.SetActive(false);
        readyOff.gameObject.SetActive(false);
    }

    static Button AddButton(Transform parent, string name, string texturePath, Vector2 center, Vector2 size)
    {
        var rectTransform = CreateRectObject(parent, name, center, size);
        var rawImage = rectTransform.gameObject.AddComponent<RawImage>();
        rawImage.texture = string.IsNullOrEmpty(texturePath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        rawImage.raycastTarget = true;

        if (string.IsNullOrEmpty(texturePath))
            rawImage.color = Color.clear;

        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rawImage;
        rectTransform.gameObject.AddComponent<TitleUIButtonFeedback>();
        return button;
    }

    static Button AddImageButton(Transform parent, string name, string texturePath, Vector2 center, Vector2 visualSize, Vector2 hitSize)
    {
        var button = AddButton(parent, name, null, center, hitSize);
        AddRawImage(button.transform, name + "_Visual", texturePath, Vector2.zero, visualSize, false);
        return button;
    }

    static RawImage AddRawImage(Transform parent, string name, string texturePath, Vector2 center, Vector2 size, bool raycastTarget)
    {
        var rectTransform = CreateRectObject(parent, name, center, size);
        var rawImage = rectTransform.gameObject.AddComponent<RawImage>();
        rawImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        rawImage.raycastTarget = raycastTarget;
        return rawImage;
    }

    static Image AddColorImage(Transform parent, string name, Vector2 center, Vector2 size, Color color, bool raycastTarget)
    {
        var rectTransform = CreateRectObject(parent, name, center, size);
        var image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    static TextMeshProUGUI AddText(Transform parent, string name, string value, Vector2 center, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var rectTransform = CreateRectObject(parent, name, center, size);
        var text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
            text.font = font;

        return text;
    }

    static RectTransform CreateRectObject(Transform parent, string name, Vector2 center, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = center;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;

        return rectTransform;
    }

    static Vector2 PixelCenter(float x, float y)
    {
        return new Vector2(x - CanvasWidth * 0.5f, CanvasHeight * 0.5f - y);
    }
}
