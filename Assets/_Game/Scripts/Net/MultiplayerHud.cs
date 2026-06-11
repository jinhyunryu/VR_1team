using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 멀티 진입/대기실 월드 HUD(코드 생성 — RaceResultScreen 패턴).
///   미접속: [MULTI] 버튼.  접속중: 상태 텍스트.  대기실: 인원(N/4) + 호스트만 [START].
///   카운트다운 중: 큰 숫자.  레이스 시작 후: 패널 숨김.
/// 버튼: 근접 터치 or 레이+트리거 (RaceResultScreen 과 동일 로직).
///
/// 붙이는 법: "Multiplayer" GameObject 에 추가. connector/coordinator/hands(Striker×2) 연결.
///   attachTo = Camera Offset 권장. (font 에 한글 TMP 폰트 없으면 영문 유지)
/// </summary>
public class MultiplayerHud : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private SessionConnector connector;
    [SerializeField] private NetRaceCoordinator coordinator;
    [SerializeField] private Striker[] hands;

    [Header("배치")]
    [SerializeField] private Transform attachTo;
    [SerializeField] private float distance = 1.8f;
    [SerializeField] private float heightOffset = -0.2f;
    [SerializeField] private Vector2 panelSize = new Vector2(900, 600);
    [SerializeField] private float worldScale = 0.0012f;

    [Header("버튼")]
    [SerializeField] private float buttonTouchRadius = 0.18f;
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color buttonHoverColor = new Color(0.5f, 0.85f, 1f, 1f);
    [Tooltip("연타 방지(초).")]
    [SerializeField] private float pressCooldown = 0.6f;

    [Tooltip("미접속 상태로 이만큼(m) 전진하면 패널 숨김 — 싱글로 달리기 시작한 것으로 간주. 0 이면 항상 표시.")]
    [SerializeField] private float hideAfterDistance = 20f;

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject root;
    private RectTransform crt;
    private TMP_Text statusText, countdownText;
    private RectTransform buttonRt;
    private Image buttonImg;
    private TMP_Text buttonLabel;
    private RectTransform lanHostRt, lanJoinRt;
    private Image lanHostImg, lanJoinImg;
    private float lastPress;
    private static Sprite sWhite;

    private void Start()
    {
        Build();
        Debug.Log($"[MultiplayerHud] 시작 — connector={(connector != null)} coordinator={(coordinator != null)}"
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            + " / F1~F4 단축키 활성"
#endif
        );
        TryAutoConnectByMppmTag();
        TryAutoConnectByCommandLine();
    }

    /// 커맨드라인 자동 접속 — 빌드를 "VR1Team.exe -lanhost" / "-lanjoin" 으로 실행하면
    /// 키 입력 없이 자동 호스트/조인 (자동화 테스트·시연 바로가기용).
    private void TryAutoConnectByCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        bool host = false, join = false, autoStart = false;
        foreach (var a in args)
        {
            if (a == "-lanhost") host = true;
            else if (a == "-lanjoin") join = true;
            else if (a == "-autostart") autoStart = true; // 호스트가 15초 후 자동 START (자동화 테스트용)
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
        Debug.Log($"[MultiplayerHud] 커맨드라인 자동 접속 — {(asHost ? "LAN HOST" : "LAN JOIN")}");
        if (connector == null) yield break;
        if (asHost) connector.StartLanHost();
        else connector.StartLanClient();
    }

    /// MPPM 태그 기반 자동 접속 — 가상 플레이어 창은 키 입력이 불안정하므로 태그로 역할 지정.
    /// MPPM 창에서 Main Editor 에 "host", Player 2~4 에 "client" 태그를 달면 Play 시 자동 접속.
    /// 태그 없으면 아무것도 안 함(수동 F1~F4/버튼 그대로).
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
        // 호스트가 먼저 떠야 클라가 발견함 — 클라는 여유를 두고 시작.
        yield return new WaitForSeconds(asHost ? 0.5f : 2.5f);
        if (connector == null) yield break;
        Debug.Log($"[MultiplayerHud] MPPM 태그 자동 접속 — {(asHost ? "LAN HOST" : "LAN JOIN")}");
        if (asHost) connector.StartLanHost();
        else connector.StartLanClient();
    }
#endif

    private void Update()
    {
        if (root == null || connector == null) return;

        HandleEditorKeys(); // 에디터/MPPM 가상 플레이어용 (VR 손 없이 접속 테스트)

        // 레이스 시작 후엔 카운트다운만 보여주고 끝나면 숨김.
        bool counting = coordinator != null && coordinator.CountdownRemaining > 0f;
        bool raceRunning = coordinator != null && coordinator.RaceStarted && !counting;

        // 미접속(싱글) 상태로 일정 거리 전진했으면 숨김 — 싱글 플레이 방해 금지.
        bool offline = connector.State == SessionConnector.ConnState.Offline
                    || connector.State == SessionConnector.ConnState.Failed;
        var boat = coordinator != null ? coordinator.LocalPlayerBoat : null;
        bool singleRunning = offline && hideAfterDistance > 0f
                          && boat != null && boat.DistanceTraveled > hideAfterDistance;

        bool hidden = raceRunning || singleRunning;
        root.SetActive(!hidden);
        if (hidden) return;

        crt.localPosition = new Vector3(0f, heightOffset, distance);
        crt.localScale = Vector3.one * worldScale;
        crt.sizeDelta = panelSize;

        // 상태/버튼 라벨 갱신.
        string btn = null;
        switch (connector.State)
        {
            case SessionConnector.ConnState.Offline:
                statusText.text = "SINGLE MODE";
                btn = "MULTI";
                break;
            case SessionConnector.ConnState.Connecting:
                statusText.text = "CONNECTING...";
                break;
            case SessionConnector.ConnState.Failed:
                statusText.text = "FAILED - RETRY?";
                btn = "RETRY";
                break;
            case SessionConnector.ConnState.InSession:
                // 호스트는 NGO 목록, 클라는 세션 인원(NGO 목록이 서버 전용이라 0일 수 있음).
                int n = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                    ? NetworkManager.Singleton.ConnectedClientsList.Count
                    : connector.PlayerCount;
                n = Mathf.Max(n, connector.PlayerCount);
                // 호스트는 자기 IP 표시 — 브로드캐스트 발견 실패 시 상대가 hostip.txt 에 적을 수 있게.
                statusText.text = connector.IsHost
                    ? $"PLAYERS {n}/4  ({LanDiscovery.GetLocalIpHint()})"
                    : $"PLAYERS {n}/4";
                if (connector.IsHost && !counting) btn = "START";
                break;
        }
        countdownText.text = counting ? Mathf.CeilToInt(coordinator.CountdownRemaining).ToString() : "";

        // 메인 버튼.
        bool showButton = btn != null;
        buttonRt.gameObject.SetActive(showButton);
        if (showButton)
        {
            buttonLabel.text = btn;
            if (ButtonInteract(buttonRt, buttonImg))
            {
                if (btn == "START") { if (coordinator != null) coordinator.RequestStartRace(); }
                else connector.Connect(); // MULTI / RETRY
            }
        }

        // LAN 폴백 버튼(미접속 상태에서만).
        bool showLan = connector.State == SessionConnector.ConnState.Offline
                    || connector.State == SessionConnector.ConnState.Failed;
        if (lanHostRt != null) lanHostRt.gameObject.SetActive(showLan);
        if (lanJoinRt != null) lanJoinRt.gameObject.SetActive(showLan);
        if (showLan)
        {
            if (ButtonInteract(lanHostRt, lanHostImg)) connector.StartLanHost();
            else if (ButtonInteract(lanJoinRt, lanJoinImg)) connector.StartLanClient();
        }
    }

    /// 에디터/개발빌드 테스트용 키보드 단축키 — F1=클라우드 / F2=LAN 호스트 / F3=LAN 조인 / F4=START.
    /// 데스크탑 개발 빌드를 두 번째 플레이어로 쓰는 2인 테스트용 (VR 손 없이 접속). 릴리즈 빌드 제외.
    /// ⚠️ H/J/S 등 글자 키 금지 — SpeedController 디버그 키(H=히트, J=미스)와 충돌함.
    private void HandleEditorKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame) { Debug.Log("[MultiplayerHud] F1 → 클라우드 접속"); connector.Connect(); }
        if (kb.f2Key.wasPressedThisFrame) { Debug.Log("[MultiplayerHud] F2 → LAN HOST"); connector.StartLanHost(); }
        if (kb.f3Key.wasPressedThisFrame) { Debug.Log("[MultiplayerHud] F3 → LAN JOIN"); connector.StartLanClient(); }
        if (kb.f4Key.wasPressedThisFrame && coordinator != null) { Debug.Log("[MultiplayerHud] F4 → START"); coordinator.RequestStartRace(); }
#endif
    }

    /// 한 버튼의 hover/터치/트리거 입력 처리. 눌렸으면 true.
    private bool ButtonInteract(RectTransform rt, Image img)
    {
        bool hovering = false;
        if (hands != null && Time.time - lastPress > pressCooldown)
        {
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                if (Vector3.Distance(hand.WorldPosition, rt.position) <= buttonTouchRadius)
                { lastPress = Time.time; return true; }
                if (PointingAt(hand, rt))
                {
                    hovering = true;
                    if (hand.TriggerHeld) { lastPress = Time.time; return true; }
                }
            }
        }
        if (img != null) img.color = hovering ? buttonHoverColor : buttonColor;
        return false;
    }

    private bool PointingAt(Striker hand, RectTransform rt)
    {
        Vector3 n = root.transform.forward;
        Vector3 o = hand.WorldPosition;
        Vector3 d = hand.Forward;
        float denom = Vector3.Dot(d, n);
        if (Mathf.Abs(denom) < 1e-5f) return false;
        float t = Vector3.Dot(rt.position - o, n) / denom;
        if (t < 0f) return false;
        Vector3 hit = o + d * t;
        Vector2 local = rt.InverseTransformPoint(hit);
        return rt.rect.Contains(local);
    }

    private void Build()
    {
        Transform parent = attachTo != null ? attachTo
                         : (Camera.main != null ? Camera.main.transform : null);

        var go = new GameObject("MultiplayerHud", typeof(RectTransform), typeof(Canvas));
        go.layer = 0;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        root = go;
        crt = (RectTransform)go.transform;
        crt.sizeDelta = panelSize;
        if (parent != null) crt.SetParent(parent, false);
        crt.localScale = Vector3.one * worldScale;
        crt.localPosition = new Vector3(0f, heightOffset, distance);
        crt.localRotation = Quaternion.identity;

        var bg = NewImage("Bg", crt, new Color(0f, 0f, 0f, 0.55f));
        var brt = bg.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        statusText = NewText("Status", crt, 70f);
        Place(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(800, 140));

        countdownText = NewText("Countdown", crt, 220f);
        Place(countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(800, 300));

        buttonImg = NewImage("Button", crt, buttonColor);
        buttonRt = buttonImg.rectTransform;
        Place(buttonRt, new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(460, 140));
        buttonLabel = NewText("ButtonLabel", buttonRt, 56f);
        StretchFill(buttonLabel.rectTransform);

        lanHostImg = NewImage("LanHost", crt, buttonColor);
        lanHostRt = lanHostImg.rectTransform;
        Place(lanHostRt, new Vector2(0.5f, 0f), new Vector2(-240, 230), new Vector2(420, 110));
        var lh = NewText("LanHostLabel", lanHostRt, 44f);
        StretchFill(lh.rectTransform);
        lh.text = "LAN HOST";

        lanJoinImg = NewImage("LanJoin", crt, buttonColor);
        lanJoinRt = lanJoinImg.rectTransform;
        Place(lanJoinRt, new Vector2(0.5f, 0f), new Vector2(240, 230), new Vector2(420, 110));
        var lj = NewText("LanJoinLabel", lanJoinRt, 44f);
        StretchFill(lj.rectTransform);
        lj.text = "LAN JOIN";
    }

    private static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private TMP_Text NewText(string name, Transform parent, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : TMP_Settings.defaultFontAsset;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        return t;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = color;
        return img;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    private static Sprite WhiteSprite()
    {
        if (sWhite == null)
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            sWhite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
        return sWhite;
    }
}
