using System;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// 멀티 세션 접속 담당. UGS 익명 로그인 + 고정 이름 세션 CreateOrJoin(교실 데모: 전원 같은 방).
/// SessionOptions.WithRelayNetwork() — Multiplayer Services 가 릴레이 할당 + NGO NetworkManager
/// 시작(호스트/클라)까지 자동 처리하므로 릴레이 보일러플레이트가 없다.
///
/// 붙이는 법: 빈 GameObject "Multiplayer" 에 추가. 씬에 NetworkManager(+UnityTransport) 필요.
/// 사용: MultiplayerHud 가 Connect()/Disconnect() 호출, State/이벤트 표시.
/// </summary>
public class SessionConnector : MonoBehaviour
{
    /// 영속 인스턴스 (타이틀 씬 부트스트랩). 씬 로컬 사본은 Awake 에서 스스로 물러난다.
    public static SessionConnector Instance { get; private set; }

    [Tooltip("같은 이름끼리 같은 방. 교실 데모는 고정값으로 충분(코드 입력 없음).")]
    [SerializeField] private string sessionName = "vrboat-race";
    [SerializeField] private int maxPlayers = 4;

    [Tooltip("씬 전환에도 유지 (타이틀 씬의 부트스트랩만 체크 — 레이스 씬 사본은 끔).")]
    [SerializeField] private bool persistAcrossScenes = false;

    [Header("LAN 폴백")]
    [Tooltip("LAN 폴백용. 비워두면 Awake 에서 자동 확보(GetComponent → 없으면 AddComponent).")]
    [SerializeField] private LanDiscovery lanDiscovery;
    [SerializeField] private ushort lanPort = 7777;

    private void Awake()
    {
        // 타이틀에서 온 영속 인스턴스가 이미 있으면 씬 로컬 사본은 물러남 (컴포넌트만 제거 — 같은 GO 의 코디네이터/HUD 는 유지).
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        // 인스펙터 연결 누락에 면역 — 씬 연결이 비어 있어도 스스로 확보한다.
        // (2026-06-11: lanDiscovery 미연결로 LAN 발견/수동 IP 폴백이 통째로 침묵 실패했던 사고)
        if (lanDiscovery == null)
        {
            lanDiscovery = GetComponent<LanDiscovery>();
            if (lanDiscovery == null)
            {
                lanDiscovery = gameObject.AddComponent<LanDiscovery>();
                Debug.Log("[SessionConnector] LanDiscovery 자동 부착 (인스펙터 미연결)");
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public enum ConnState { Offline, Connecting, InSession, Failed }
    public ConnState State { get; private set; } = ConnState.Offline;
    public string LastError { get; private set; } = "";
    public ISession Session { get; private set; }

    /// 세션 인원(자기 포함). 미접속이면 0.
    public int PlayerCount => Session != null ? Session.Players.Count : 0;
    public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

    /// 상태 변화(접속/실패/퇴장) 알림 — HUD 갱신용.
    public event Action StateChanged;
    /// 세션 합류 완료(로비 진입 트리거) — NetRaceCoordinator 가 구독.
    public event Action SessionJoined;

    public async void Connect()
    {
        if (State == ConnState.Connecting || State == ConnState.InSession) return;
        SetState(ConnState.Connecting);
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            Session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionName, options);

            HookNgo();
            SetState(ConnState.InSession);
            SessionJoined?.Invoke();
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogWarning($"[SessionConnector] 접속 실패: {e}");
            SetState(ConnState.Failed);
        }
    }

    public async void Disconnect()
    {
        UnhookNgo();
        if (lanDiscovery != null) lanDiscovery.StopAll();
        try { if (Session != null) await Session.LeaveAsync(); }
        catch (Exception e) { Debug.LogWarning($"[SessionConnector] 퇴장 오류(무시): {e.Message}"); }
        Session = null;
        // LAN 경로 정리 — NGO 를 내리지 않으면 재호스트/재조인이 막힘 (포트 점유 + instance running).
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();
        SetState(ConnState.Offline);
    }

    /// LAN 호스트 시작(클라우드 불능 폴백). UnityTransport 직결 + 브로드캐스트 송출.
    public void StartLanHost()
    {
        if (State == ConnState.Connecting || State == ConnState.InSession)
        { Debug.Log("[SessionConnector] 이미 접속 중/접속됨 — LAN HOST 무시"); return; }
        var nm = NetworkManager.Singleton;
        if (nm == null)
        { Debug.LogWarning("[SessionConnector] NetworkManager.Singleton 이 null — 씬에 NetworkManager 없음/비활성"); return; }
        if (nm.IsListening)
        { Debug.Log("[SessionConnector] NetworkManager 가 이미 동작 중 — LAN HOST 무시"); return; }
        Debug.Log("[SessionConnector] LAN HOST 시작");
        var utp = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (utp != null) utp.SetConnectionData("0.0.0.0", lanPort);
        if (nm.StartHost())
        {
            if (lanDiscovery != null) lanDiscovery.StartHostBroadcast();
            HookNgo();
            SetState(ConnState.InSession);
            SessionJoined?.Invoke();
        }
        else SetState(ConnState.Failed);
    }

    /// LAN 클라 시작: 브로드캐스트로 호스트 IP 발견 후 접속.
    public void StartLanClient()
    {
        if (State == ConnState.Connecting || State == ConnState.InSession)
        { Debug.Log("[SessionConnector] 이미 접속 중/접속됨 — LAN JOIN 무시"); return; }
        var nmGuard = NetworkManager.Singleton;
        if (nmGuard == null)
        { Debug.LogWarning("[SessionConnector] NetworkManager.Singleton 이 null — 씬에 NetworkManager 없음/비활성"); return; }
        if (nmGuard.IsListening)
        { Debug.Log("[SessionConnector] NetworkManager 가 이미 동작 중 — LAN JOIN 무시"); return; }
        Debug.Log("[SessionConnector] LAN JOIN 시작");
        SetState(ConnState.Connecting);
        if (lanDiscovery != null) lanDiscovery.StartClientListen();
        StartCoroutine(JoinWhenDiscovered());
    }

    private System.Collections.IEnumerator JoinWhenDiscovered()
    {
        // 발견 가능하면 최대 10초 대기, 불가하면 곧장 수동 IP 폴백.
        float timeout = lanDiscovery != null ? 10f : 0f;
        while (timeout > 0f && (lanDiscovery == null || lanDiscovery.DiscoveredHostIp == null))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // 브로드캐스트 발견 실패 시 수동 IP 폴백 (커맨드라인 -hostip 또는 exe 옆 hostip.txt).
        string hostIp = (lanDiscovery != null ? lanDiscovery.DiscoveredHostIp : null) ?? GetManualHostIp();
        if (string.IsNullOrEmpty(hostIp))
        {
            LastError = "LAN HOST NOT FOUND";
            SetState(ConnState.Failed);
            yield break;
        }
        Debug.Log($"[SessionConnector] LAN 접속 시도 → {hostIp}:{lanPort} (발견={(lanDiscovery != null && lanDiscovery.DiscoveredHostIp != null ? "브로드캐스트" : "수동 IP")})");

        var nm = NetworkManager.Singleton;
        var utp = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (utp != null) utp.SetConnectionData(hostIp, lanPort);
        if (nm.StartClient())
        {
            HookNgo();
            SetState(ConnState.InSession);
            SessionJoined?.Invoke();
        }
        else SetState(ConnState.Failed);
    }

    /// 수동 호스트 IP — ① 커맨드라인 "-hostip 192.168.x.x" ② 실행파일 옆 hostip.txt (IP 한 줄).
    private static string GetManualHostIp()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-hostip") return args[i + 1].Trim();
        try
        {
            string p = System.IO.Path.Combine(Application.dataPath, "..", "hostip.txt");
            if (System.IO.File.Exists(p))
            {
                string s = System.IO.File.ReadAllText(p).Trim();
                if (s.Length > 0) return s;
            }
        }
        catch { }
        return null;
    }

    /// 호스트 이탈 등으로 NGO 연결이 끊기면 Failed 표시(스펙: "세션 종료" 안내).
    private void HookNgo()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNgoDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback += OnNgoConnected;
        }
    }

    private void OnNgoConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        int n = nm != null && nm.IsServer ? nm.ConnectedClientsList.Count : -1;
        Debug.Log($"[SessionConnector] NGO 접속됨 — clientId={clientId} (서버 기준 인원 {n})");
    }

    private void UnhookNgo()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNgoDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNgoConnected;
        }
    }

    private void OnNgoDisconnect(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && clientId == nm.LocalClientId && State == ConnState.InSession)
        {
            LastError = "SESSION ENDED";
            Session = null;
            SetState(ConnState.Failed);
        }
    }

    private void SetState(ConnState s)
    {
        State = s;
        StateChanged?.Invoke();
    }
}
