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
    [Tooltip("같은 이름끼리 같은 방. 교실 데모는 고정값으로 충분(코드 입력 없음).")]
    [SerializeField] private string sessionName = "vrboat-race";
    [SerializeField] private int maxPlayers = 4;

    [Header("LAN 폴백")]
    [Tooltip("LAN 폴백용. 같은 GameObject 에 LanDiscovery 부착 시 자동 사용.")]
    [SerializeField] private LanDiscovery lanDiscovery;
    [SerializeField] private ushort lanPort = 7777;

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
        try { if (Session != null) await Session.LeaveAsync(); }
        catch (Exception e) { Debug.LogWarning($"[SessionConnector] 퇴장 오류(무시): {e.Message}"); }
        Session = null;
        SetState(ConnState.Offline);
    }

    /// LAN 호스트 시작(클라우드 불능 폴백). UnityTransport 직결 + 브로드캐스트 송출.
    public void StartLanHost()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
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
        if (lanDiscovery == null) { LastError = "LanDiscovery 없음"; SetState(ConnState.Failed); return; }
        SetState(ConnState.Connecting);
        lanDiscovery.StartClientListen();
        StartCoroutine(JoinWhenDiscovered());
    }

    private System.Collections.IEnumerator JoinWhenDiscovered()
    {
        float timeout = 10f;
        while (lanDiscovery.DiscoveredHostIp == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (lanDiscovery.DiscoveredHostIp == null)
        {
            LastError = "LAN 호스트 못 찾음";
            SetState(ConnState.Failed);
            yield break;
        }
        var nm = NetworkManager.Singleton;
        var utp = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (utp != null) utp.SetConnectionData(lanDiscovery.DiscoveredHostIp, lanPort);
        if (nm.StartClient())
        {
            HookNgo();
            SetState(ConnState.InSession);
            SessionJoined?.Invoke();
        }
        else SetState(ConnState.Failed);
    }

    /// 호스트 이탈 등으로 NGO 연결이 끊기면 Failed 표시(스펙: "세션 종료" 안내).
    private void HookNgo()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNgoDisconnect;
    }

    private void UnhookNgo()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNgoDisconnect;
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
