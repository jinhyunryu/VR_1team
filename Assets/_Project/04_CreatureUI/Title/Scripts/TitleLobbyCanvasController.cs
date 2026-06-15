using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleLobbyCanvasController : MonoBehaviour
{
    const string DefaultTitleStartPulseTargetName = "Title_Start_1";

    public enum PlayerLobbyState
    {
        None,
        Connect,
        ReadyOff,
        ReadyOn
    }

    [System.Serializable]
    public class PlayerStateObjects
    {
        public int playerNumber;
        public GameObject connect;
        public GameObject readyOff;
        public GameObject readyOn;
    }

    [Header("Canvas Roots")]
    public GameObject titleCanvas;
    public GameObject lobbyCanvas;

    [Header("Camera Lock")]
    public Transform cameraLockedRoot;
    public Camera targetCamera;
    public bool lockToCamera = true;
    public bool yawOnlyCameraLock = true;

    [Header("UI Audio")]
    public TitleUIAudioMaster audioMaster;
    public bool autoInstallButtonFeedback = true;

    [Header("Title Menu")]
    public GameObject titleTouchPrompt;
    public string titleStartPulseTargetName = DefaultTitleStartPulseTargetName;
    public TitleStartPulseEffect titleStartPulseEffect;
    public GameObject titleMenuRoot;
    public float titleMenuDelay = 0.5f;
    public float titleMenuFadeDuration = 0.35f;
    public Button titleTouchButton;
    public Button titleStartButton;
    public Button titleJoinButton;
    public Button titleSettingButton;
    public Button titleExitButton;

    [Header("Lobby Buttons")]
    public Button lobbyStartButton;
    public Button lobbyReadyButton;
    public Button lobbyExitButton;

    [Header("Playlist")]
    public Button playlistButton;
    public GameObject playlistPopupRoot;
    public Button playlistConfirmButton;
    public Button playlistCancelButton;
    public Button[] playlistSongButtons;
    public GameObject[] playlistSongSelectedHighlights;
    public string[] playlistSongNames =
    {
        "Ocean Beat",
        "Coral Pop",
        "Wave Runner",
        "Starfish Melody",
        "Blue Horizon"
    };
    public GameObject currentSongBannerRoot;
    public TMP_Text currentSongBannerText;

    [Header("Player States")]
    public int localPlayerNumber = 1;
    public PlayerStateObjects[] playerStates =
    {
        new PlayerStateObjects { playerNumber = 1 },
        new PlayerStateObjects { playerNumber = 2 },
        new PlayerStateObjects { playerNumber = 3 },
        new PlayerStateObjects { playerNumber = 4 },
    };

    [SerializeField] bool showTitleOnAwake = true;
    [SerializeField] int selectedPlaylistSongIndex;

    CanvasGroup titleMenuCanvasGroup;
    Coroutine titleMenuTransition;
    PlayerLobbyState[] currentPlayerStates;
    bool buttonFeedbackInstalled;
    bool cameraLockPoseCaptured;
    Vector3 cameraLockedOffset;
    Quaternion cameraLockedLocalRotation;

    void Awake()
    {
        ResolveCameraLockedRoot();
        EnsureAudioMaster();
        AutoBindPlayerStates();
        EnsureTitleStartPulseEffect();
        EnsureButtonFeedback();
        ClearAllPlayerStates();
        SetActive(playlistPopupRoot, false);
        ResolveCurrentSongBannerReferences();
        RefreshPlaylistSelectionVisuals();
        RefreshCurrentSongBanner();

        if (showTitleOnAwake)
            ShowTitleCanvas();
    }

    void LateUpdate()
    {
        if (!lockToCamera)
            return;

        ResolveCameraLockedRoot();

        if (cameraLockedRoot == null)
            return;

        var camera = ResolveTargetCamera();
        if (camera == null)
            return;

        AssignCanvasCamera(camera);

        if (yawOnlyCameraLock)
            ApplyYawOnlyCameraLock(camera.transform);
        else
            AttachRootToCamera(camera.transform);
    }

    public void ShowLobbyCanvas()
    {
        StopTitleMenuTransition();
        SetCanvasState(showTitle: false);
        SelectButton(lobbyReadyButton != null ? lobbyReadyButton : lobbyStartButton);
    }

    public void ShowTitleCanvas()
    {
        StopTitleMenuTransition();
        SetCanvasState(showTitle: true);
        SetTitleMenuVisibleImmediate(false);
        SetActive(playlistPopupRoot, false);
        SelectButton(titleTouchButton);
    }

    public void ShowTitleMenu()
    {
        EnsureAudioMaster();
        audioMaster.PlayTouchToStartClick();
        StartTitleMenuTransition();
    }

    public void CreateHostRoom()
    {
        Debug.Log("Host room creation requested.");
        SetPlayerState(localPlayerNumber, PlayerLobbyState.ReadyOff);
        ShowLobbyCanvas();
    }

    public void JoinHostRoom()
    {
        Debug.Log("Host room join requested.");
        SetPlayerState(localPlayerNumber, PlayerLobbyState.Connect);
        ShowLobbyCanvas();
    }

    public void OpenTitleSettings()
    {
        Debug.Log("Title settings requested.");
    }

    public void ToggleLobbyReady()
    {
        TogglePlayerReady(localPlayerNumber);
    }

    public void StartLobbyGame()
    {
        Debug.Log($"Lobby start requested. Selected song: {GetSelectedPlaylistSongName()}.");
    }

    public void ShowPlaylistPopup()
    {
        EnsurePlaylistSelection();
        SetActive(playlistPopupRoot, true);
        RefreshPlaylistSelectionVisuals();
        SelectButton(GetSelectedPlaylistButton() != null ? GetSelectedPlaylistButton() : playlistConfirmButton);
    }

    public void HidePlaylistPopup()
    {
        SetActive(playlistPopupRoot, false);
        SelectButton(playlistButton != null ? playlistButton : lobbyReadyButton);
    }

    public void SelectPlaylistSong(int songIndex)
    {
        selectedPlaylistSongIndex = ClampPlaylistSongIndex(songIndex);
        RefreshPlaylistSelectionVisuals();
        RefreshCurrentSongBanner();
    }

    public void ConfirmPlaylistSelection()
    {
        EnsurePlaylistSelection();
        Debug.Log($"Playlist song selected: {GetSelectedPlaylistSongName()}.");
        RefreshCurrentSongBanner();
        HidePlaylistPopup();
    }

    public void RefreshCurrentSongBanner()
    {
        ResolveCurrentSongBannerReferences();

        if (currentSongBannerRoot != null)
            currentSongBannerRoot.SetActive(true);

        if (currentSongBannerText != null)
            currentSongBannerText.text = GetSelectedPlaylistSongName();
    }

    public string GetSelectedPlaylistSongName()
    {
        EnsurePlaylistSelection();

        var selectedButton = GetSelectedPlaylistButton();
        if (selectedButton != null)
        {
            var selectedButtonSongName = ReadPlaylistButtonSongName(selectedButton);
            if (!string.IsNullOrWhiteSpace(selectedButtonSongName))
                return selectedButtonSongName;
        }

        if (playlistSongNames != null
            && selectedPlaylistSongIndex >= 0
            && selectedPlaylistSongIndex < playlistSongNames.Length
            && !string.IsNullOrWhiteSpace(playlistSongNames[selectedPlaylistSongIndex]))
        {
            return playlistSongNames[selectedPlaylistSongIndex];
        }

        return $"Song {selectedPlaylistSongIndex + 1}";
    }

    void ResolveCurrentSongBannerReferences()
    {
        if (currentSongBannerRoot == null)
            currentSongBannerRoot = FindChildGameObject(lobbyCanvas != null ? lobbyCanvas.transform : transform, "PlaySongBanner");

        if (currentSongBannerText != null || currentSongBannerRoot == null)
            return;

        var texts = currentSongBannerRoot.GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
        {
            if (text.name == "CurrentSongNameText")
            {
                currentSongBannerText = text;
                return;
            }
        }

        if (texts.Length > 0)
            currentSongBannerText = texts[0];
    }

    static string ReadPlaylistButtonSongName(Button button)
    {
        if (button == null)
            return null;

        var songItem = button.GetComponent<TitlePlaylistSongItemUI>();
        if (songItem != null && !string.IsNullOrWhiteSpace(songItem.SongName))
            return songItem.SongName;

        var texts = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
        {
            if (text.name == "SongName" && !string.IsNullOrWhiteSpace(text.text))
                return text.text;
        }

        return null;
    }

    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetCanvasState(bool showTitle)
    {
        if (titleCanvas != null)
            titleCanvas.SetActive(showTitle);

        if (lobbyCanvas != null)
            lobbyCanvas.SetActive(!showTitle);
    }

    void EnsurePlaylistSelection()
    {
        selectedPlaylistSongIndex = ClampPlaylistSongIndex(selectedPlaylistSongIndex);
    }

    int ClampPlaylistSongIndex(int songIndex)
    {
        var songCount = 0;

        if (playlistSongButtons != null && playlistSongButtons.Length > 0)
            songCount = playlistSongButtons.Length;
        else if (playlistSongNames != null && playlistSongNames.Length > 0)
            songCount = playlistSongNames.Length;

        if (songCount <= 0)
            return Mathf.Max(0, songIndex);

        return Mathf.Clamp(songIndex, 0, songCount - 1);
    }

    void RefreshPlaylistSelectionVisuals()
    {
        EnsurePlaylistSelection();

        if (playlistSongSelectedHighlights == null)
            return;

        for (var i = 0; i < playlistSongSelectedHighlights.Length; i++)
            SetActive(playlistSongSelectedHighlights[i], i == selectedPlaylistSongIndex);
    }

    Button GetSelectedPlaylistButton()
    {
        if (playlistSongButtons == null
            || selectedPlaylistSongIndex < 0
            || selectedPlaylistSongIndex >= playlistSongButtons.Length)
        {
            return null;
        }

        return playlistSongButtons[selectedPlaylistSongIndex];
    }

    void SetTitleMenuVisibleImmediate(bool visible)
    {
        if (titleTouchPrompt != null)
            titleTouchPrompt.SetActive(!visible);

        if (titleMenuRoot != null)
        {
            titleMenuRoot.SetActive(visible);
            var canvasGroup = EnsureTitleMenuCanvasGroup();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }
    }

    void StartTitleMenuTransition()
    {
        StopTitleMenuTransition();

        if (titleTouchPrompt != null)
            titleTouchPrompt.SetActive(false);

        if (titleMenuRoot == null)
            return;

        var canvasGroup = EnsureTitleMenuCanvasGroup();
        titleMenuRoot.SetActive(true);

        if (canvasGroup == null)
        {
            SelectButton(titleStartButton);
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        titleMenuTransition = StartCoroutine(FadeTitleMenuIn(canvasGroup));
    }

    IEnumerator FadeTitleMenuIn(CanvasGroup canvasGroup)
    {
        var delay = Mathf.Max(0f, titleMenuDelay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        var duration = Mathf.Max(0.01f, titleMenuFadeDuration);
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        titleMenuTransition = null;
        SelectButton(titleStartButton);
    }

    void StopTitleMenuTransition()
    {
        if (titleMenuTransition == null)
            return;

        StopCoroutine(titleMenuTransition);
        titleMenuTransition = null;
    }

    CanvasGroup EnsureTitleMenuCanvasGroup()
    {
        if (titleMenuRoot == null)
            return null;

        if (titleMenuCanvasGroup == null)
            titleMenuCanvasGroup = titleMenuRoot.GetComponent<CanvasGroup>();

        if (titleMenuCanvasGroup == null)
            titleMenuCanvasGroup = titleMenuRoot.AddComponent<CanvasGroup>();

        return titleMenuCanvasGroup;
    }

    void EnsureTitleStartPulseEffect()
    {
        if (titleStartPulseEffect != null)
            return;

        var targetName = string.IsNullOrWhiteSpace(titleStartPulseTargetName)
            ? DefaultTitleStartPulseTargetName
            : titleStartPulseTargetName;

        var pulseTarget = FindChildGameObject(
            titleCanvas != null ? titleCanvas.transform : transform,
            targetName);

        if (pulseTarget == null)
            return;

        titleStartPulseEffect = pulseTarget.GetComponent<TitleStartPulseEffect>();
        if (titleStartPulseEffect == null)
            titleStartPulseEffect = pulseTarget.AddComponent<TitleStartPulseEffect>();
    }

    void EnsureAudioMaster()
    {
        if (audioMaster == null)
            audioMaster = TitleUIAudioMaster.EnsureInstance();
    }

    void EnsureButtonFeedback()
    {
        if (!autoInstallButtonFeedback)
            return;

        if (buttonFeedbackInstalled)
            return;

        EnsureAudioMaster();
        InstallButtonFeedback(titleCanvas);
        InstallButtonFeedback(lobbyCanvas);
        buttonFeedbackInstalled = true;
    }

    public void RefreshButtonFeedback()
    {
        buttonFeedbackInstalled = false;
        EnsureButtonFeedback();
    }

    void InstallButtonFeedback(GameObject root)
    {
        if (root == null)
            return;

        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            var feedback = button.GetComponent<TitleUIButtonFeedback>();
            if (feedback == null)
                feedback = button.gameObject.AddComponent<TitleUIButtonFeedback>();

            feedback.SetAudioMaster(audioMaster);

            if (IsTitleTouchButton(button))
                feedback.SetClickSound(TitleUIButtonFeedback.ButtonClickSound.None);
            else
                feedback.SetClickSound(TitleUIButtonFeedback.ButtonClickSound.ButtonClick);
        }
    }

    bool IsTitleTouchButton(Button button)
    {
        if (button == null)
            return false;

        if (button == titleTouchButton)
            return true;

        if (titleTouchPrompt != null && button.gameObject == titleTouchPrompt)
            return true;

        return button.name == "TitleTouchToStartButton";
    }

    void ResolveCameraLockedRoot()
    {
        if (cameraLockedRoot != null)
            return;

        if (titleCanvas != null && titleCanvas.transform.parent != null)
            cameraLockedRoot = titleCanvas.transform.parent;
    }

    Camera ResolveTargetCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        return targetCamera;
    }

    void AssignCanvasCamera(Camera camera)
    {
        AssignCanvasCamera(titleCanvas, camera);
        AssignCanvasCamera(lobbyCanvas, camera);
    }

    void AttachRootToCamera(Transform cameraTransform)
    {
        if (cameraLockedRoot.parent == cameraTransform)
            return;

        cameraLockedRoot.SetParent(cameraTransform, false);
    }

    void ApplyYawOnlyCameraLock(Transform cameraTransform)
    {
        CaptureCameraLockPose();

        if (cameraLockedRoot.parent == cameraTransform)
            cameraLockedRoot.SetParent(null, true);

        var cameraYaw = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
        cameraLockedRoot.SetPositionAndRotation(
            cameraTransform.position + cameraYaw * cameraLockedOffset,
            cameraYaw * cameraLockedLocalRotation);
    }

    void CaptureCameraLockPose()
    {
        if (cameraLockPoseCaptured || cameraLockedRoot == null)
            return;

        cameraLockedOffset = cameraLockedRoot.localPosition;
        cameraLockedLocalRotation = cameraLockedRoot.localRotation;
        cameraLockPoseCaptured = true;
    }

    public void ClearAllPlayerStates()
    {
        AutoBindPlayerStates();

        foreach (var playerState in playerStates)
        {
            if (playerState != null)
                SetCurrentPlayerState(playerState.playerNumber, PlayerLobbyState.None);

            ApplyPlayerState(playerState, PlayerLobbyState.None);
        }
    }

    public void SetPlayerConnecting(int playerNumber)
    {
        SetPlayerState(playerNumber, PlayerLobbyState.Connect);
    }

    public void SetLocalPlayerConnecting()
    {
        SetPlayerConnecting(localPlayerNumber);
    }

    public void SetPlayerConnected(int playerNumber)
    {
        SetPlayerState(playerNumber, PlayerLobbyState.ReadyOff);
    }

    public void SetLocalPlayerConnected()
    {
        SetPlayerConnected(localPlayerNumber);
    }

    public void SetPlayerReady(int playerNumber)
    {
        SetPlayerState(playerNumber, PlayerLobbyState.ReadyOn);
    }

    public void TogglePlayerReady(int playerNumber)
    {
        var currentState = GetPlayerState(playerNumber);
        SetPlayerState(
            playerNumber,
            currentState == PlayerLobbyState.ReadyOn
                ? PlayerLobbyState.ReadyOff
                : PlayerLobbyState.ReadyOn);
    }

    public void SetLocalPlayerReady()
    {
        SetPlayerReady(localPlayerNumber);
    }

    public void ClearPlayerState(int playerNumber)
    {
        SetPlayerState(playerNumber, PlayerLobbyState.None);
    }

    public void ClearLocalPlayerState()
    {
        ClearPlayerState(localPlayerNumber);
    }

    public void SetPlayerState(int playerNumber, PlayerLobbyState state)
    {
        AutoBindPlayerStates();

        var playerState = GetPlayerStateObjects(playerNumber);
        if (playerState == null)
            return;

        SetCurrentPlayerState(playerNumber, state);
        ApplyPlayerState(playerState, state);
    }

    void AutoBindPlayerStates()
    {
        EnsurePlayerStateArray();

        foreach (var playerState in playerStates)
        {
            if (playerState == null)
                continue;

            var prefix = $"Player{playerState.playerNumber}_state_";
            playerState.connect = ResolveStateObject(playerState.connect, prefix + "Connect");
            playerState.readyOff = ResolveStateObject(playerState.readyOff, prefix + "ReadyOff");
            playerState.readyOn = ResolveStateObject(playerState.readyOn, prefix + "ReadyOn");
        }
    }

    void EnsurePlayerStateArray()
    {
        if (playerStates != null && playerStates.Length > 0)
        {
            EnsureCurrentPlayerStates();
            return;
        }

        playerStates = new PlayerStateObjects[4];
        for (var i = 0; i < playerStates.Length; i++)
        {
            playerStates[i] = new PlayerStateObjects
            {
                playerNumber = i + 1
            };
        }

        EnsureCurrentPlayerStates();
    }

    void EnsureCurrentPlayerStates()
    {
        if (playerStates == null)
            return;

        if (currentPlayerStates != null && currentPlayerStates.Length == playerStates.Length)
            return;

        currentPlayerStates = new PlayerLobbyState[playerStates.Length];
    }

    GameObject ResolveStateObject(GameObject current, string objectName)
    {
        if (current != null)
            return current;

        return FindChildGameObject(lobbyCanvas != null ? lobbyCanvas.transform : transform, objectName);
    }

    PlayerStateObjects GetPlayerStateObjects(int playerNumber)
    {
        foreach (var playerState in playerStates)
        {
            if (playerState != null && playerState.playerNumber == playerNumber)
                return playerState;
        }

        return null;
    }

    PlayerLobbyState GetPlayerState(int playerNumber)
    {
        EnsurePlayerStateArray();

        for (var i = 0; i < playerStates.Length; i++)
        {
            var playerState = playerStates[i];
            if (playerState == null || playerState.playerNumber != playerNumber)
                continue;

            if (currentPlayerStates != null && i < currentPlayerStates.Length && currentPlayerStates[i] != PlayerLobbyState.None)
                return currentPlayerStates[i];

            return ReadPlayerStateFromObjects(playerState);
        }

        return PlayerLobbyState.None;
    }

    void SetCurrentPlayerState(int playerNumber, PlayerLobbyState state)
    {
        EnsureCurrentPlayerStates();

        if (currentPlayerStates == null)
            return;

        for (var i = 0; i < playerStates.Length; i++)
        {
            var playerState = playerStates[i];
            if (playerState != null && playerState.playerNumber == playerNumber)
            {
                currentPlayerStates[i] = state;
                return;
            }
        }
    }

    static PlayerLobbyState ReadPlayerStateFromObjects(PlayerStateObjects playerState)
    {
        if (playerState.readyOn != null && playerState.readyOn.activeSelf)
            return PlayerLobbyState.ReadyOn;

        if (playerState.readyOff != null && playerState.readyOff.activeSelf)
            return PlayerLobbyState.ReadyOff;

        if (playerState.connect != null && playerState.connect.activeSelf)
            return PlayerLobbyState.Connect;

        return PlayerLobbyState.None;
    }

    static void ApplyPlayerState(PlayerStateObjects playerState, PlayerLobbyState state)
    {
        if (playerState == null)
            return;

        SetActive(playerState.connect, state == PlayerLobbyState.Connect);
        SetActive(playerState.readyOff, state == PlayerLobbyState.ReadyOff);
        SetActive(playerState.readyOn, state == PlayerLobbyState.ReadyOn);
    }

    static void AssignCanvasCamera(GameObject canvasObject, Camera camera)
    {
        if (canvasObject == null)
            return;

        var canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera != camera)
            canvas.worldCamera = camera;
    }

    static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    static GameObject FindChildGameObject(Transform root, string objectName)
    {
        if (root == null)
            return null;

        var transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in transforms)
        {
            if (child.name == objectName)
                return child.gameObject;
        }

        foreach (var child in transforms)
        {
            if (string.Equals(child.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return child.gameObject;
        }

        return null;
    }

    static void SelectButton(Button button)
    {
        if (button == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
