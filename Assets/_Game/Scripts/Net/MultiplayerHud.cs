using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 레이스 시작 카운트다운 숫자 표시(코드 생성 월드 텍스트) + 에디터/커맨드라인 접속 테스트 헬퍼.
///   로비/호스트코드(IP)/MULTI·START·LAN 버튼 UI 는 타이틀(TitleLobbyCanvasController + TitleLobbyNetBridge)이 담당
///   → 여기선 제거(2026-06-16). 카운트다운 중에만 큰 숫자, 그 외 숨김.
/// 붙이는 법: "Multiplayer" GameObject 에 추가. coordinator/connector 연결. attachTo=Camera Offset.
///   ★font 를 다른 UI 와 같은 폰트로 지정하면 숫자 폰트 통일됨.
/// </summary>
public class MultiplayerHud : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private SessionConnector connector;
    [SerializeField] private NetRaceCoordinator coordinator;

    [Header("배치")]
    [SerializeField] private Transform attachTo;
    [SerializeField] private float distance = 1.8f;
    [SerializeField] private float heightOffset = 0.1f;
    [SerializeField] private float worldScale = 0.0012f;
    [SerializeField] private float countdownFontSize = 220f;
    [SerializeField] private Color countdownColor = Color.white;

    [Header("폰트 (다른 UI 와 통일)")]
    [SerializeField] private TMP_FontAsset font;

    private RectTransform crt;
    private TMP_Text countdownText;

    private void Awake()
    {
        // 타이틀 씬에서 온 영속 커넥터가 있으면 그걸 사용.
        if (SessionConnector.Instance != null) connector = SessionConnector.Instance;
    }

    private void Start()
    {
        Build();
        TryAutoConnectByMppmTag();
        TryAutoConnectByCommandLine();
    }

    private void Update()
    {
        if (countdownText == null) return;

        HandleEditorKeys(); // 에디터/개발빌드 접속 테스트 (F1~F4)

        bool counting = coordinator != null && coordinator.CountdownRemaining > 0f;
        if (crt != null)
        {
            crt.localPosition = new Vector3(0f, heightOffset, distance);
            crt.localScale = Vector3.one * worldScale;
        }
        countdownText.gameObject.SetActive(counting);
        if (counting)
            countdownText.text = Mathf.CeilToInt(coordinator.CountdownRemaining).ToString();
    }

    private void Build()
    {
        Transform parent = attachTo != null ? attachTo
                         : (Camera.main != null ? Camera.main.transform : null);

        var go = new GameObject("CountdownHud", typeof(RectTransform), typeof(Canvas));
        go.layer = 0;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        crt = (RectTransform)go.transform;
        crt.sizeDelta = new Vector2(800, 320);
        if (parent != null) crt.SetParent(parent, false);
        crt.localScale = Vector3.one * worldScale;
        crt.localPosition = new Vector3(0f, heightOffset, distance);
        crt.localRotation = Quaternion.identity;

        var tgo = new GameObject("Countdown", typeof(RectTransform));
        tgo.layer = 0;
        tgo.transform.SetParent(crt, false);
        var t = tgo.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : TMP_Settings.defaultFontAsset;
        t.fontSize = countdownFontSize;
        t.color = countdownColor;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        countdownText = t;
        countdownText.gameObject.SetActive(false);
    }

    // ── 접속 테스트 헬퍼 (비주얼 없음 — 자동화/2인 테스트용) ──

    /// 커맨드라인 자동 접속 — "VR1Team.exe -lanhost"/"-lanjoin"(+"-autostart").
    private void TryAutoConnectByCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        bool host = false, join = false, autoStart = false;
        foreach (var a in args)
        {
            if (a == "-lanhost") host = true;
            else if (a == "-lanjoin") join = true;
            else if (a == "-autostart") autoStart = true;
        }
        if (host) StartCoroutine(DelayedLanConnect(true));
        else if (join) StartCoroutine(DelayedLanConnect(false));
        if (autoStart) StartCoroutine(AutoStartRace());
    }

    private System.Collections.IEnumerator AutoStartRace()
    {
        yield return new WaitForSeconds(15f);
        if (coordinator != null && connector != null && connector.IsHost)
        {
            Debug.Log("[MultiplayerHud] 커맨드라인 자동 START");
            coordinator.RequestStartRace();
        }
    }

    private System.Collections.IEnumerator DelayedLanConnect(bool asHost)
    {
        yield return new WaitForSeconds(asHost ? 1f : 4f);
        if (connector == null) yield break;
        Debug.Log($"[MultiplayerHud] 커맨드라인 자동 접속 — {(asHost ? "LAN HOST" : "LAN JOIN")}");
        if (asHost) connector.StartLanHost();
        else connector.StartLanClient();
    }

    /// MPPM 태그 기반 자동 접속(가상 플레이어 테스트).
    private void TryAutoConnectByMppmTag()
    {
#if UNITY_EDITOR
        string[] tags = Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags();
        if (tags == null) return;
        foreach (var t in tags)
        {
            if (string.Equals(t, "host", System.StringComparison.OrdinalIgnoreCase))
            { StartCoroutine(AutoConnect(true)); return; }
            if (string.Equals(t, "client", System.StringComparison.OrdinalIgnoreCase))
            { StartCoroutine(AutoConnect(false)); return; }
        }
#endif
    }

#if UNITY_EDITOR
    private System.Collections.IEnumerator AutoConnect(bool asHost)
    {
        yield return new WaitForSeconds(asHost ? 0.5f : 2.5f);
        if (connector == null) yield break;
        Debug.Log($"[MultiplayerHud] MPPM 태그 자동 접속 — {(asHost ? "LAN HOST" : "LAN JOIN")}");
        if (asHost) connector.StartLanHost();
        else connector.StartLanClient();
    }
#endif

    /// 에디터/개발빌드 키보드 단축키 — F1=클라우드 / F2=LAN호스트 / F3=LAN조인 / F4=START.
    private void HandleEditorKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (connector == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame) { Debug.Log("[MultiplayerHud] F1 → 클라우드 접속"); connector.Connect(); }
        if (kb.f2Key.wasPressedThisFrame) { Debug.Log("[MultiplayerHud] F2 → LAN HOST"); connector.StartLanHost(); }
        if (kb.f3Key.wasPressedThisFrame) { Debug.Log("[MultiplayerHud] F3 → LAN JOIN"); connector.StartLanClient(); }
        if (kb.f4Key.wasPressedThisFrame && coordinator != null) { Debug.Log("[MultiplayerHud] F4 → START"); coordinator.RequestStartRace(); }
#endif
    }
}
