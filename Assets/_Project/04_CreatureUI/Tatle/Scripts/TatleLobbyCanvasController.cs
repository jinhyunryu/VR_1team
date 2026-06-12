using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TatleLobbyCanvasController : MonoBehaviour
{
    const string DefaultTatleStartPulseTargetName = "Tatle_Start_1";

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
    public GameObject tatleCanvas;
    public GameObject lobbyCanvas;

    [Header("Camera Lock")]
    public Transform cameraLockedRoot;
    public Camera targetCamera;
    public bool lockToCamera = true;
    public bool yawOnlyCameraLock = true;

    [Header("UI Audio")]
    public TatleUIAudioMaster audioMaster;
    public bool autoInstallButtonFeedback = true;

    [Header("Tatle Menu")]
    public GameObject tatleTouchPrompt;
    public string tatleStartPulseTargetName = DefaultTatleStartPulseTargetName;
    public TatleStartPulseEffect tatleStartPulseEffect;
    public GameObject tatleMenuRoot;
    public float tatleMenuDelay = 0.5f;
    public float tatleMenuFadeDuration = 0.35f;
    public Button tatleTouchButton;
    public Button tatleStartButton;
    public Button tatleJoinButton;
    public Button tatleSettingButton;
    public Button tatleExitButton;

    [Header("Lobby Buttons")]
    public Button lobbyStartButton;
    public Button lobbyReadyButton;
    public Button lobbyExitButton;

    [Header("Player States")]
    public int localPlayerNumber = 1;
    public PlayerStateObjects[] playerStates =
    {
        new PlayerStateObjects { playerNumber = 1 },
        new PlayerStateObjects { playerNumber = 2 },
        new PlayerStateObjects { playerNumber = 3 },
        new PlayerStateObjects { playerNumber = 4 },
    };

    [SerializeField] bool showTatleOnAwake = true;

    CanvasGroup tatleMenuCanvasGroup;
    Coroutine tatleMenuTransition;
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
        EnsureTatleStartPulseEffect();
        EnsureButtonFeedback();
        ClearAllPlayerStates();

        if (showTatleOnAwake)
            ShowTatleCanvas();
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
        StopTatleMenuTransition();
        SetCanvasState(showTatle: false);
        SelectButton(lobbyReadyButton != null ? lobbyReadyButton : lobbyStartButton);
    }

    public void ShowTatleCanvas()
    {
        StopTatleMenuTransition();
        SetCanvasState(showTatle: true);
        SetTatleMenuVisibleImmediate(false);
        SelectButton(tatleTouchButton);
    }

    public void ShowTatleMenu()
    {
        EnsureAudioMaster();
        audioMaster.PlayTouchToStartClick();
        StartTatleMenuTransition();
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

    public void OpenTatleSettings()
    {
        Debug.Log("Tatle settings requested.");
    }

    public void ToggleLobbyReady()
    {
        TogglePlayerReady(localPlayerNumber);
    }

    public void StartLobbyGame()
    {
        Debug.Log("Lobby start requested.");
    }

    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetCanvasState(bool showTatle)
    {
        if (tatleCanvas != null)
            tatleCanvas.SetActive(showTatle);

        if (lobbyCanvas != null)
            lobbyCanvas.SetActive(!showTatle);
    }

    void SetTatleMenuVisibleImmediate(bool visible)
    {
        if (tatleTouchPrompt != null)
            tatleTouchPrompt.SetActive(!visible);

        if (tatleMenuRoot != null)
        {
            tatleMenuRoot.SetActive(visible);
            var canvasGroup = EnsureTatleMenuCanvasGroup();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }
    }

    void StartTatleMenuTransition()
    {
        StopTatleMenuTransition();

        if (tatleTouchPrompt != null)
            tatleTouchPrompt.SetActive(false);

        if (tatleMenuRoot == null)
            return;

        var canvasGroup = EnsureTatleMenuCanvasGroup();
        tatleMenuRoot.SetActive(true);

        if (canvasGroup == null)
        {
            SelectButton(tatleStartButton);
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        tatleMenuTransition = StartCoroutine(FadeTatleMenuIn(canvasGroup));
    }

    IEnumerator FadeTatleMenuIn(CanvasGroup canvasGroup)
    {
        var delay = Mathf.Max(0f, tatleMenuDelay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        var duration = Mathf.Max(0.01f, tatleMenuFadeDuration);
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
        tatleMenuTransition = null;
        SelectButton(tatleStartButton);
    }

    void StopTatleMenuTransition()
    {
        if (tatleMenuTransition == null)
            return;

        StopCoroutine(tatleMenuTransition);
        tatleMenuTransition = null;
    }

    CanvasGroup EnsureTatleMenuCanvasGroup()
    {
        if (tatleMenuRoot == null)
            return null;

        if (tatleMenuCanvasGroup == null)
            tatleMenuCanvasGroup = tatleMenuRoot.GetComponent<CanvasGroup>();

        if (tatleMenuCanvasGroup == null)
            tatleMenuCanvasGroup = tatleMenuRoot.AddComponent<CanvasGroup>();

        return tatleMenuCanvasGroup;
    }

    void EnsureTatleStartPulseEffect()
    {
        if (tatleStartPulseEffect != null)
            return;

        var targetName = string.IsNullOrWhiteSpace(tatleStartPulseTargetName)
            ? DefaultTatleStartPulseTargetName
            : tatleStartPulseTargetName;

        var pulseTarget = FindChildGameObject(
            tatleCanvas != null ? tatleCanvas.transform : transform,
            targetName);

        if (pulseTarget == null)
            return;

        tatleStartPulseEffect = pulseTarget.GetComponent<TatleStartPulseEffect>();
        if (tatleStartPulseEffect == null)
            tatleStartPulseEffect = pulseTarget.AddComponent<TatleStartPulseEffect>();
    }

    void EnsureAudioMaster()
    {
        if (audioMaster == null)
            audioMaster = TatleUIAudioMaster.EnsureInstance();
    }

    void EnsureButtonFeedback()
    {
        if (!autoInstallButtonFeedback)
            return;

        if (buttonFeedbackInstalled)
            return;

        EnsureAudioMaster();
        InstallButtonFeedback(tatleCanvas);
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
            var feedback = button.GetComponent<TatleUIButtonFeedback>();
            if (feedback == null)
                feedback = button.gameObject.AddComponent<TatleUIButtonFeedback>();

            feedback.SetAudioMaster(audioMaster);

            if (IsTatleTouchButton(button))
                feedback.SetClickSound(TatleUIButtonFeedback.ButtonClickSound.None);
            else
                feedback.SetClickSound(TatleUIButtonFeedback.ButtonClickSound.ButtonClick);
        }
    }

    bool IsTatleTouchButton(Button button)
    {
        if (button == null)
            return false;

        if (button == tatleTouchButton)
            return true;

        if (tatleTouchPrompt != null && button.gameObject == tatleTouchPrompt)
            return true;

        return button.name == "TatleTouchToStartButton";
    }

    void ResolveCameraLockedRoot()
    {
        if (cameraLockedRoot != null)
            return;

        if (tatleCanvas != null && tatleCanvas.transform.parent != null)
            cameraLockedRoot = tatleCanvas.transform.parent;
    }

    Camera ResolveTargetCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        return targetCamera;
    }

    void AssignCanvasCamera(Camera camera)
    {
        AssignCanvasCamera(tatleCanvas, camera);
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
