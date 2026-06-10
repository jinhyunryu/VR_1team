# 4인 멀티플레이 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quest 4대가 같은 레이스에서 서로의 보트·아바타를 보며 경주 (빈자리 AI).

**Architecture:** NGO + Unity Multiplayer Services(Session API, Relay) — 동기화는 보트 거리(float)와 아바타 포즈(머리+양손)뿐. 리듬/판정/속도 계산은 전부 로컬. 신규 코드는 `Assets/_Game/Scripts/Net/`, 기존 코드 변경 4곳(전부 하위호환).

**Tech Stack:** Unity 6000.4.10f1, com.unity.netcode.gameobjects, com.unity.services.multiplayer, com.unity.multiplayer.playmode (테스트)

**검증 방식 (프로젝트 관례 — CLAUDE.md 우선):** 자동 테스트 프레임워크 없음. 각 태스크 = 코드 작성 → Unity 컴파일(Console 0 에러, 사용자) → 동작 확인(MPPM/플레이, 사용자) → 커밋. ⚠️ Unity 열린 채 씬/프리팹 파일 직접 편집 금지 — 코드만 작성, 에디터 작업은 사용자.

**스펙:** `docs/superpowers/specs/2026-06-10-multiplayer-design.md` (기존 코드 변경이 3곳→4곳으로 늘었음: GhostRacer.InitForNetwork 추가 — AI 러버밴딩/페이스 주입용. 스펙에 반영됨)

---

### Task 0: 패키지 추가

**Files:**
- Modify: `Packages/manifest.json`

- [ ] **Step 0.1: manifest.json dependencies 에 3줄 추가** (알파벳 순서 위치에)

```json
    "com.unity.multiplayer.playmode": "1.4.0",
    "com.unity.netcode.gameobjects": "2.4.0",
    "com.unity.services.multiplayer": "1.1.2",
```

- [ ] **Step 0.2: [사용자 Unity] 패키지 resolve 확인**

Unity 포커스 → 패키지 임포트 대기 → Console 에러 0. 버전 resolve 실패 시: Package Manager(Window > Package Manager) 에서 이름으로 검색해 최신 호환 버전 설치 후 manifest 버전을 그 값으로 갱신.

- [ ] **Step 0.3: 커밋**

```powershell
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "chore: NGO + Multiplayer Services + MPPM 패키지 추가"
```

---

### Task 1: ProtoBeatmapSpawner — StopSong / RestartSong

**Files:**
- Modify: `Assets/_Game/Scripts/ProtoBeatmapSpawner.cs` (클래스 끝에 메서드 2개 추가)

싱글 경로 무변경 — `Start()` 자동재생 그대로. 멀티가 대기실 진입 시 Stop, 카운트다운 후 Restart.

- [ ] **Step 1.1: 메서드 추가** (`running = true;` 가 있는 `Start()` 뒤, `Update()` 앞에)

```csharp
    /// 멀티: 음악/박자 진행을 멈춘다(대기실 진입). RestartSong 으로 재개.
    public void StopSong()
    {
        running = false;
        if (music != null) music.Stop();
    }

    /// 멀티: delaySeconds 후 곡을 처음부터 다시 시작(전 기기 동시 출발).
    /// dspTime 기준 예약이라 호출 시점 프레임 편차에 둔감. 0.2 이상 권장(PlayScheduled 선행).
    public void RestartSong(double delaySeconds)
    {
        if (music != null) music.Stop();
        nextSlot = 0;
        songStartDsp = AudioSettings.dspTime + System.Math.Max(0.2, delaySeconds);
        if (music != null && music.clip != null) music.PlayScheduled(songStartDsp);
        running = true;
    }
```

- [ ] **Step 1.2: [사용자 Unity] 컴파일 확인 + 싱글 회귀** — 플레이 시 기존처럼 음악/노트 정상

- [ ] **Step 1.3: 커밋**

```powershell
git add Assets/_Game/Scripts/ProtoBeatmapSpawner.cs
git commit -m "feat: ProtoBeatmapSpawner StopSong/RestartSong (멀티 동시 출발용, 싱글 무변경)"
```

---

### Task 2: BoatMover — ApplyNetworkDistance

**Files:**
- Modify: `Assets/_Game/Scripts/BoatMover.cs`

- [ ] **Step 2.1: NetworkDriven 프로퍼티 + 메서드 추가** (`Resume()` 아래)

```csharp
    /// 멀티: 이 mover 가 네트워크 수신 거리로 구동되는 중인가(원격 보트). true 면 Update 적분 중단.
    public bool NetworkDriven { get; private set; }

    /// 멀티: 네트워크로 받은 누적 거리를 직접 적용(원격 보트용). 위치도 forward 축으로 함께 이동.
    /// 보간은 호출자(NetRacer)가 한 뒤 결과만 넘긴다 — 이 컴포넌트는 단순 적용만.
    public void ApplyNetworkDistance(float distance)
    {
        NetworkDriven = true;
        float delta = distance - DistanceTraveled;
        if (delta != 0f)
        {
            transform.position += transform.forward * delta;
            DistanceTraveled = distance;
        }
    }
```

- [ ] **Step 2.2: Update() 맨 앞에 가드 추가** (`float dt = Time.deltaTime;` 윗줄)

```csharp
        if (NetworkDriven) return; // 멀티: 원격 보트는 ApplyNetworkDistance 가 구동
```

- [ ] **Step 2.3: [사용자 Unity] 컴파일 + 싱글 회귀(배 전진 정상)**

- [ ] **Step 2.4: 커밋**

```powershell
git add Assets/_Game/Scripts/BoatMover.cs
git commit -m "feat: BoatMover.ApplyNetworkDistance (원격 보트 거리 직접 구동)"
```

---

### Task 3: RaceManager — RegisterRacer / UnregisterRacer

**Files:**
- Modify: `Assets/_Game/Scripts/RaceManager.cs`

- [ ] **Step 3.1: 메서드 추가** (`EndRaceNow()` 위)

```csharp
    /// 멀티: 런타임 레이서 등록(원격 플레이어/AI). 인스펙터 racers 와 공존, 같은 mover 중복 방지.
    public void RegisterRacer(string racerName, BoatMover mover, bool isPlayer)
    {
        if (mover == null) return;
        foreach (var r in racers)
            if (r.mover == mover) return;
        racers.Add(new RaceEntry { name = racerName, mover = mover, isPlayer = isPlayer });
    }

    /// 멀티: 이탈 레이서 제거(이미 완주했으면 기록 보존을 위해 유지).
    public void UnregisterRacer(BoatMover mover)
    {
        racers.RemoveAll(r => r.mover == mover && r.finishOrder == 0);
    }
```

- [ ] **Step 3.2: [사용자 Unity] 컴파일 확인**

- [ ] **Step 3.3: 커밋**

```powershell
git add Assets/_Game/Scripts/RaceManager.cs
git commit -m "feat: RaceManager 런타임 레이서 등록/해제 API"
```

---

### Task 4: GhostRacer — InitForNetwork

**Files:**
- Modify: `Assets/_Game/Scripts/GhostRacer.cs`

AI NetRacer 프리팹은 씬의 PlayerBoat 를 미리 참조할 수 없으므로 런타임 주입 메서드가 필요.

- [ ] **Step 4.1: 메서드 추가** (`Awake()` 아래)

```csharp
    /// 멀티: 호스트가 AI 레이서 스폰 시 참조/페이스를 주입한다(프리팹은 씬 참조 불가).
    public void InitForNetwork(BoatMover player, float finish, float endPaceOverride)
    {
        playerBoat = player;
        finishDistance = finish;
        endPace = endPaceOverride;
    }
```

- [ ] **Step 4.2: [사용자 Unity] 컴파일 확인 → 커밋**

```powershell
git add Assets/_Game/Scripts/GhostRacer.cs
git commit -m "feat: GhostRacer.InitForNetwork (멀티 AI 스폰 시 참조 주입)"
```

---

### Task 5: SessionConnector

**Files:**
- Create: `Assets/_Game/Scripts/Net/SessionConnector.cs`

- [ ] **Step 5.1: 파일 작성**

```csharp
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
```

(`Connect()` 의 `SetState(ConnState.InSession);` 바로 윗줄에 `HookNgo();` 1줄 추가 — 이후 Task 11 의 LAN 두 경로에서도 `SetState(ConnState.InSession);` 직전에 `HookNgo();` 호출)

- [ ] **Step 5.2: [사용자 Unity] 컴파일 확인.** API 가 패키지 버전과 다르면(메서드명 등) Console 에러 기준으로 수정 — `Unity.Services.Multiplayer` 의 Session API 문서 참조 (Multiplayer Center 의 Quickstart 에 현재 버전 예제 있음)

- [ ] **Step 5.3: 커밋**

```powershell
git add Assets/_Game/Scripts/Net/SessionConnector.cs
git commit -m "feat: SessionConnector (UGS 익명로그인 + CreateOrJoin 세션 + Relay 자동)"
```

---

### Task 6: NetRacer (+ 프리팹 구성은 사용자)

**Files:**
- Create: `Assets/_Game/Scripts/Net/NetRacer.cs`

- [ ] **Step 6.1: 파일 작성**

```csharp
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 레이서 1명. NetworkManager 의 PlayerPrefab 으로 자동 스폰(접속자당 1개) +
/// 호스트가 AI 채움용으로 추가 스폰.
///
/// 역할 분기:
///   - 사람 + owner(나):    비주얼 숨김. 씬의 내 PlayerBoat 거리를 netDistance 에 발행만.
///   - 사람 + 비owner(남):  수신 거리(보간)로 내 BoatMover 구동 + 레인 오프셋 표시 + RaceManager 등록.
///   - AI + 서버(owner):    GhostRacer 가 BoatMover 구동, 그 거리를 발행. 표시/등록도 함.
///   - AI + 클라:           사람 비owner 와 동일(수신 구동).
///
/// 레인 표시는 "상대 레인" — 내 레인과의 차 × laneWidth 를 내 보트 기준 우측 오프셋으로.
/// (각 기기에서 절대좌표는 달라도 상대 간격/순서는 동일 — 표시 전용이라 무해)
/// </summary>
[RequireComponent(typeof(BoatMover))]
public class NetRacer : NetworkBehaviour
{
    [Header("비주얼 (프리팹 자식)")]
    [Tooltip("원격에서 보이는 배 모델 루트. 로컬 자신은 숨김.")]
    [SerializeField] private GameObject hullVisual;
    [Tooltip("NetAvatar 루트. AI 는 숨김.")]
    [SerializeField] private GameObject avatarRoot;
    [Tooltip("AI 모드에서 켤 GhostRacer (프리팹에 비활성으로 부착).")]
    [SerializeField] private GhostRacer ghostRacer;

    [Header("표시")]
    [SerializeField] private float laneWidth = 4f;
    [Tooltip("수신 거리 보간 계수(클수록 즉각).")]
    [SerializeField] private float distanceLerp = 8f;

    public NetworkVariable<float> NetDistance = new(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> Lane = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsAi = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private BoatMover mover;          // 이 오브젝트의 mover (원격 표시/AI 용)
    private BoatMover localSource;    // 사람 owner 일 때 읽는 씬의 내 PlayerBoat
    private float displayDistance;
    private bool registered;

    private bool IsHumanOwner => !IsAi.Value && IsOwner;
    private bool DrivenByNetwork => !IsHumanOwner && !(IsAi.Value && IsServer);

    public override void OnNetworkSpawn()
    {
        mover = GetComponent<BoatMover>();

        var coord = NetRaceCoordinator.Instance;
        if (IsServer && coord != null && !IsAi.Value)
            Lane.Value = coord.ClaimLane();

        if (IsHumanOwner)
        {
            // 나: 표시는 기존 PlayerBoat 가 담당 — 이 인스턴스는 발행 전용.
            if (hullVisual != null) hullVisual.SetActive(false);
            if (avatarRoot != null) avatarRoot.SetActive(false);
            mover.enabled = false;
            localSource = coord != null ? coord.LocalPlayerBoat : null;
        }
        else
        {
            if (avatarRoot != null) avatarRoot.SetActive(!IsAi.Value);

            if (IsAi.Value && IsServer)
            {
                // 호스트의 AI: GhostRacer 가 mover 를 직접 구동.
                if (ghostRacer != null) ghostRacer.enabled = true;
            }

            // 시작 위치: 내 레인 기준 상대 오프셋.
            if (coord != null)
            {
                transform.rotation = coord.RaceRotation;
                transform.position = RacePosition(coord, 0f);
            }

            // 원격(비owner) mover 는 즉시 네트워크 구동 래치 — 스폰 직후 cruise 자가 전진 방지.
            if (DrivenByNetwork) mover.ApplyNetworkDistance(NetDistance.Value);
        }

        Lane.OnValueChanged += (_, _) => SnapToLane();
        TryRegister();
    }

    public override void OnNetworkDespawn()
    {
        var coord = NetRaceCoordinator.Instance;
        if (registered && coord != null && coord.RaceManager != null)
            coord.RaceManager.UnregisterRacer(mover);
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsHumanOwner)
        {
            if (localSource != null) NetDistance.Value = localSource.DistanceTraveled;
            return;
        }

        if (IsAi.Value && IsServer)
        {
            NetDistance.Value = mover.DistanceTraveled; // GhostRacer 구동 결과 발행
            return;
        }

        // 원격: 수신 거리 보간 → mover 적용.
        float dt = Time.deltaTime;
        displayDistance = Mathf.Lerp(displayDistance, NetDistance.Value,
                                     1f - Mathf.Exp(-distanceLerp * dt));
        mover.ApplyNetworkDistance(displayDistance);
    }

    /// 레인 표시명("P1"/"AI 3")로 RaceManager 등록. owner 자신은 등록 안 함(내 PlayerBoat 가 이미 있음).
    private void TryRegister()
    {
        if (registered || IsHumanOwner) return;
        var coord = NetRaceCoordinator.Instance;
        if (coord == null || coord.RaceManager == null) return;
        string n = IsAi.Value ? $"AI {Lane.Value + 1}" : $"P{Lane.Value + 1}";
        coord.RaceManager.RegisterRacer(n, mover, isPlayer: false);
        registered = true;
    }

    private void SnapToLane()
    {
        var coord = NetRaceCoordinator.Instance;
        if (coord == null || IsHumanOwner) return;
        transform.position = RacePosition(coord, mover.DistanceTraveled);
    }

    private Vector3 RacePosition(NetRaceCoordinator coord, float dist)
    {
        int myLane = coord.LocalLane;
        float side = (Lane.Value - myLane) * laneWidth;
        return coord.RaceOrigin
             + coord.RaceRotation * Vector3.forward * dist
             + coord.RaceRotation * Vector3.right * side;
    }
}
```

- [ ] **Step 6.2: [사용자 Unity] 컴파일은 Task 7(NetRaceCoordinator) 작성 후 확인** (상호 참조)

---

### Task 7: NetRaceCoordinator

**Files:**
- Create: `Assets/_Game/Scripts/Net/NetRaceCoordinator.cs`

- [ ] **Step 7.1: 파일 작성**

```csharp
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 레이스 동기화 총괄(씬 배치 NetworkObject, 호스트 권한).
///   - 세션 합류 → 로비: 음악 정지 + 내 보트 정지/리셋/원위치.
///   - 레인 배정(접속순), 호스트 START → AI 채움 스폰 → 전원 카운트다운 → 동시 출발.
///
/// 붙이는 법: 빈 GameObject "Multiplayer" 에 SessionConnector 와 함께 추가 + NetworkObject 부착.
///   연결: sessionConnector / raceManager / localPlayerBoat(씬 PlayerBoat의 BoatMover) /
///         beatmapSpawner / netRacerPrefab(AI 채움용 — NetworkManager PlayerPrefab 과 같은 프리팹).
/// </summary>
public class NetRaceCoordinator : NetworkBehaviour
{
    public static NetRaceCoordinator Instance { get; private set; }

    [Header("참조")]
    [SerializeField] private SessionConnector sessionConnector;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private BoatMover localPlayerBoat;
    [SerializeField] private ProtoBeatmapSpawner beatmapSpawner;
    [Tooltip("AI 채움용 NetRacer 프리팹 (NetworkManager 의 PlayerPrefab 과 동일 프리팹).")]
    [SerializeField] private NetRacer netRacerPrefab;

    [Header("레이스")]
    [SerializeField] private int totalRacers = 4;
    [SerializeField] private float countdownSeconds = 3f;
    [Tooltip("AI 채움 endPace 후보(스폰 순서대로). GhostRacer 등수 바.")]
    [SerializeField] private float[] aiEndPaces = { 12f, 14f, 16f };

    [Tooltip("싱글용 씬 고스트 루트들(Ghost×3). 멀티 로비 진입 시 비활성(AI 채움이 대체).")]
    [SerializeField] private GameObject[] singleplayerGhosts;

    public RaceManager RaceManager => raceManager;
    public BoatMover LocalPlayerBoat => localPlayerBoat;
    public Vector3 RaceOrigin { get; private set; }
    public Quaternion RaceRotation { get; private set; }
    /// 내(로컬 플레이어) 레인. 내 NetRacer 의 Lane 이 정해지면 갱신.
    public int LocalLane { get; private set; }
    public bool RaceStarted { get; private set; }
    /// 카운트다운 표시용(0 이하면 비표시). HUD 가 읽음.
    public float CountdownRemaining { get; private set; }

    private int nextLane;

    private void Awake()
    {
        Instance = this;
        // 보트가 움직이기 전(Start 의 cruise/SpeedController 이전) 시작 기준 캡처.
        if (localPlayerBoat != null)
        {
            RaceOrigin = localPlayerBoat.transform.position;
            RaceRotation = localPlayerBoat.transform.rotation;
        }
    }

    private void OnEnable()
    {
        if (sessionConnector != null) sessionConnector.SessionJoined += EnterLobby;
    }

    private void OnDisable()
    {
        if (sessionConnector != null) sessionConnector.SessionJoined -= EnterLobby;
    }

    /// 서버: 사람 레이서에게 레인 배정(접속순). NetRacer.OnNetworkSpawn 에서 호출.
    public int ClaimLane() => nextLane++;

    /// 내 NetRacer 가 자기 레인을 알게 되면 호출(원격 표시 기준점).
    public void SetLocalLane(int lane) => LocalLane = lane;

    /// 로비 진입(각 클라 로컬): 음악 정지 + 보트 정지/리셋/원위치 + 싱글 고스트 제거.
    private void EnterLobby()
    {
        if (beatmapSpawner != null) beatmapSpawner.StopSong();
        if (localPlayerBoat != null)
        {
            localPlayerBoat.Stop();
            localPlayerBoat.ResetDistance();
            localPlayerBoat.transform.SetPositionAndRotation(RaceOrigin, RaceRotation);
        }
        // 싱글용 씬 고스트는 멀티에서 AI 채움(NetRacer)이 대체 — 끄고 순위에서 제거.
        if (singleplayerGhosts != null && raceManager != null)
        {
            foreach (var g in singleplayerGhosts)
            {
                if (g == null) continue;
                var m = g.GetComponent<BoatMover>();
                if (m != null) raceManager.UnregisterRacer(m);
                g.SetActive(false);
            }
        }
    }

    /// 호스트 HUD 의 START 버튼이 호출.
    public void RequestStartRace()
    {
        if (!IsServer || RaceStarted) return;
        SpawnAiFillers();
        StartRaceClientRpc();
    }

    private readonly System.Collections.Generic.List<BoatMover> aiMovers = new();

    private void SpawnAiFillers()
    {
        if (netRacerPrefab == null) return;
        int humans = NetworkManager.Singleton.ConnectedClientsList.Count;
        int need = Mathf.Max(0, totalRacers - humans);
        for (int i = 0; i < need; i++)
        {
            var ai = Instantiate(netRacerPrefab);
            ai.IsAi.Value = true;
            ai.Lane.Value = nextLane++;
            float pace = aiEndPaces.Length > 0 ? aiEndPaces[i % aiEndPaces.Length] : 14f;
            var ghost = ai.GetComponent<GhostRacer>();
            if (ghost != null && raceManager != null)
                ghost.InitForNetwork(localPlayerBoat, raceManager.FinishDistance, pace);

            // 카운트다운 동안 출발 금지(부정출발 방지) — CountdownAndGo 에서 Resume.
            var aiMover = ai.GetComponent<BoatMover>();
            aiMover.Stop();
            aiMovers.Add(aiMover);

            ai.GetComponent<NetworkObject>().Spawn();
        }
    }

    [ClientRpc]
    private void StartRaceClientRpc()
    {
        StartCoroutine(CountdownAndGo());
    }

    private IEnumerator CountdownAndGo()
    {
        RaceStarted = true;
        CountdownRemaining = countdownSeconds;
        while (CountdownRemaining > 0f)
        {
            yield return null;
            CountdownRemaining -= Time.deltaTime;
        }
        CountdownRemaining = 0f;

        if (localPlayerBoat != null) localPlayerBoat.Resume();
        // 호스트: AI 도 동시 출발.
        foreach (var m in aiMovers)
            if (m != null) { m.Resume(); m.ResetDistance(); }
        if (beatmapSpawner != null) beatmapSpawner.RestartSong(0.3);
    }
}
```

- [ ] **Step 7.2: NetRacer 에 LocalLane 통지 연결** — `NetRacer.OnNetworkSpawn` 의 `IsHumanOwner` 분기 끝에 1줄, `Lane.OnValueChanged` 에서도 갱신:

```csharp
            // (IsHumanOwner 분기 안, localSource 설정 다음 줄)
            if (coord != null) coord.SetLocalLane(Lane.Value);
```

```csharp
        // 기존 Lane.OnValueChanged 줄을 다음으로 교체:
        Lane.OnValueChanged += (_, v) =>
        {
            if (IsHumanOwner && NetRaceCoordinator.Instance != null)
                NetRaceCoordinator.Instance.SetLocalLane(v);
            SnapToLane();
        };
```

- [ ] **Step 7.3: [사용자 Unity] 컴파일 확인 (Task 6+7 함께)**

- [ ] **Step 7.4: 커밋**

```powershell
git add Assets/_Game/Scripts/Net/NetRacer.cs Assets/_Game/Scripts/Net/NetRaceCoordinator.cs
git commit -m "feat: NetRacer(거리 동기화+레인) + NetRaceCoordinator(로비/START/카운트다운/AI 스폰)"
```

---

### Task 8: NetAvatar

**Files:**
- Create: `Assets/_Game/Scripts/Net/NetAvatar.cs`

- [ ] **Step 8.1: 파일 작성**

```csharp
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 머리+양손 포즈 동기화(보트 기준 로컬 좌표 — 원격 보트 위치와 합성됨).
/// owner: 내 XR rig(메인카메라 + Striker 좌/우)에서 읽어 NetworkVariable 에 기록.
/// 원격: 수신 포즈로 비주얼(구체 플레이스홀더 — 팀메이트가 모델 교체) 보간 이동.
///
/// 프리팹: NetRacer 자식 "Avatar" 루트에 부착. headVisual/leftHandVisual/rightHandVisual =
///         자식 구체 3개. AI 레이서는 NetRacer 가 Avatar 루트를 꺼버림.
/// </summary>
public class NetAvatar : NetworkBehaviour
{
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftHandVisual;
    [SerializeField] private Transform rightHandVisual;
    [SerializeField] private float lerpSpeed = 14f;

    private struct PoseData : INetworkSerializable
    {
        public Vector3 HeadP, LeftP, RightP;
        public Quaternion HeadR, LeftR, RightR;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref HeadP); s.SerializeValue(ref HeadR);
            s.SerializeValue(ref LeftP); s.SerializeValue(ref LeftR);
            s.SerializeValue(ref RightP); s.SerializeValue(ref RightR);
        }
    }

    private readonly NetworkVariable<PoseData> pose = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Transform srcHead, srcLeft, srcRight;
    private Transform boatRef; // 내 보트 기준 좌표계

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        var coord = NetRaceCoordinator.Instance;
        boatRef = coord != null && coord.LocalPlayerBoat != null
                ? coord.LocalPlayerBoat.transform : null;
        srcHead = Camera.main != null ? Camera.main.transform : null;

        // Striker 좌/우 구분: 머리 기준 로컬 x 부호.
        var strikers = FindObjectsByType<Striker>(FindObjectsSortMode.None);
        foreach (var s in strikers)
        {
            if (srcHead == null) { srcLeft = s.transform; continue; }
            float x = srcHead.InverseTransformPoint(s.transform.position).x;
            if (x < 0f) srcLeft = s.transform; else srcRight = s.transform;
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            if (boatRef == null || srcHead == null) return;
            pose.Value = new PoseData
            {
                HeadP = boatRef.InverseTransformPoint(srcHead.position),
                HeadR = Quaternion.Inverse(boatRef.rotation) * srcHead.rotation,
                LeftP = srcLeft != null ? boatRef.InverseTransformPoint(srcLeft.position) : Vector3.zero,
                LeftR = srcLeft != null ? Quaternion.Inverse(boatRef.rotation) * srcLeft.rotation : Quaternion.identity,
                RightP = srcRight != null ? boatRef.InverseTransformPoint(srcRight.position) : Vector3.zero,
                RightR = srcRight != null ? Quaternion.Inverse(boatRef.rotation) * srcRight.rotation : Quaternion.identity,
            };
            return;
        }

        // 원격: 수신 포즈를 보트(NetRacer 루트) 로컬로 적용 + 보간.
        var p = pose.Value;
        float k = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        Apply(headVisual, p.HeadP, p.HeadR, k);
        Apply(leftHandVisual, p.LeftP, p.LeftR, k);
        Apply(rightHandVisual, p.RightP, p.RightR, k);
    }

    private static void Apply(Transform t, Vector3 localPos, Quaternion localRot, float k)
    {
        if (t == null) return;
        t.localPosition = Vector3.Lerp(t.localPosition, localPos, k);
        t.localRotation = Quaternion.Slerp(t.localRotation, localRot, k);
    }
}
```

- [ ] **Step 8.2: [사용자 Unity] 컴파일 확인**

- [ ] **Step 8.3: 커밋**

```powershell
git add Assets/_Game/Scripts/Net/NetAvatar.cs
git commit -m "feat: NetAvatar (머리+양손 포즈 동기화, 보트 로컬 좌표)"
```

---

### Task 9: MultiplayerHud

**Files:**
- Create: `Assets/_Game/Scripts/Net/MultiplayerHud.cs`

RaceResultScreen 의 코드 생성 + "근접 터치/레이+트리거" 버튼 패턴 재사용.

- [ ] **Step 9.1: 파일 작성**

```csharp
using TMPro;
using Unity.Netcode;
using UnityEngine;
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

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject root;
    private RectTransform crt;
    private TMP_Text statusText, countdownText;
    private RectTransform buttonRt;
    private Image buttonImg;
    private TMP_Text buttonLabel;
    private float lastPress;
    private static Sprite sWhite;

    private void Start() => Build();

    private void Update()
    {
        if (root == null || connector == null) return;

        // 레이스 시작 후엔 카운트다운만 보여주고 끝나면 숨김.
        bool counting = coordinator != null && coordinator.CountdownRemaining > 0f;
        bool raceRunning = coordinator != null && coordinator.RaceStarted && !counting;
        root.SetActive(!raceRunning);
        if (raceRunning) return;

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
                statusText.text = $"PLAYERS {connector.PlayerCount}/4";
                if (connector.IsHost && !counting) btn = "START";
                break;
        }
        countdownText.text = counting ? Mathf.CeilToInt(coordinator.CountdownRemaining).ToString() : "";

        bool showButton = btn != null;
        buttonRt.gameObject.SetActive(showButton);
        if (!showButton) return;
        buttonLabel.text = btn;

        // 버튼 입력 (RaceResultScreen 패턴).
        bool hovering = false;
        if (hands != null && Time.time - lastPress > pressCooldown)
        {
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                if (Vector3.Distance(hand.WorldPosition, buttonRt.position) <= buttonTouchRadius)
                { Press(btn); return; }
                if (PointingAtButton(hand))
                {
                    hovering = true;
                    if (hand.TriggerHeld) { Press(btn); return; }
                }
            }
        }
        buttonImg.color = hovering ? buttonHoverColor : buttonColor;
    }

    private void Press(string btn)
    {
        lastPress = Time.time;
        switch (btn)
        {
            case "MULTI":
            case "RETRY":
                connector.Connect();
                break;
            case "START":
                if (coordinator != null) coordinator.RequestStartRace();
                break;
        }
    }

    private bool PointingAtButton(Striker hand)
    {
        Vector3 n = root.transform.forward;
        Vector3 o = hand.WorldPosition;
        Vector3 d = hand.Forward;
        float denom = Vector3.Dot(d, n);
        if (Mathf.Abs(denom) < 1e-5f) return false;
        float t = Vector3.Dot(buttonRt.position - o, n) / denom;
        if (t < 0f) return false;
        Vector3 hit = o + d * t;
        Vector2 local = buttonRt.InverseTransformPoint(hit);
        return buttonRt.rect.Contains(local);
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
        var lrt = buttonLabel.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
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
```

- [ ] **Step 9.2: [사용자 Unity] 컴파일 확인**

- [ ] **Step 9.3: 커밋**

```powershell
git add Assets/_Game/Scripts/Net/MultiplayerHud.cs
git commit -m "feat: MultiplayerHud (참가/대기실/START/카운트다운 월드 UI)"
```

---

### Task 10: [사용자 Unity] 씬/프리팹 구성 + UGS 연결 + MPPM 검증

코드 작업 없음 — 전부 에디터. 체크리스트:

- [ ] **10.1 UGS 연결(1회):** Edit > Project Settings > Services → Unity 프로젝트 생성/연결 → cloud.unity.com 대시보드에서 Relay·Lobby(Multiplayer) 서비스 활성화
- [ ] **10.2 NetworkManager:** 씬에 빈 GameObject "NetworkManager" → `NetworkManager` + `UnityTransport` 컴포넌트 (Protocol 은 기본값 — Relay 가 자동 설정)
- [ ] **10.3 NetRacer 프리팹:** `Assets/_Game/Prefabs/NetRacer.prefab`
  - 루트: `NetworkObject` + `NetRacer` + `BoatMover` + `GhostRacer`(**컴포넌트 체크박스 OFF** — AI 모드에서만 코드가 켬)
  - 자식 "Hull": 임시 박스(Scale 예: 1.5×0.8×4) — `hullVisual` 연결
  - 자식 "Avatar": `NetAvatar` + 자식 구체 3개(Head 0.25, HandL/HandR 0.12) — `avatarRoot`/각 visual 연결
  - NetworkManager 의 **Player Prefab** 으로 지정
- [ ] **10.4 Multiplayer GameObject:** 씬에 "Multiplayer" → `SessionConnector` + `NetRaceCoordinator`(+`NetworkObject`) + `MultiplayerHud` → 인스펙터 연결: raceManager / localPlayerBoat(PlayerBoat 의 BoatMover) / beatmapSpawner(NoteSpawner 의 ProtoBeatmapSpawner) / netRacerPrefab / **singleplayerGhosts(씬의 Ghost×3 루트)** / hands(Striker×2) / attachTo(Camera Offset)
- [ ] **10.5 MPPM 검증:** Window > Multiplayer Play Mode → 가상 플레이어 1개 활성 → Play → 양쪽 MULTI 버튼(에디터에선 Scene 뷰에서 손 위치로 터치 어려우면 임시로 `connector.Connect()` 를 Inspector 우클릭 ContextMenu 또는 메인 에디터만 접속해 확인) → **확인 항목:** ① 둘 다 InSession + PLAYERS 2/4 ② 호스트 START → 양쪽 카운트다운 → 음악 동시 시작 ③ 서로의 보트가 옆 레인에서 전진 ④ 아바타 구체가 머리/손 따라 움직임 ⑤ 결승 후 RaceResultScreen 순위에 P1/P2(+AI) 표시
- [ ] **10.6 커밋** (씬/프리팹 변경)

```powershell
git add Assets/Scenes/ Assets/_Game/Prefabs/
git commit -m "scene: 멀티플레이 씬 구성 (NetworkManager/NetRacer 프리팹/Multiplayer 오브젝트)"
```

---

### Task 11: LAN 폴백 (시연 당일 인터넷 불능 대비)

**Files:**
- Create: `Assets/_Game/Scripts/Net/LanDiscovery.cs`
- Modify: `Assets/_Game/Scripts/Net/SessionConnector.cs` (LAN 모드 추가)
- Modify: `Assets/_Game/Scripts/Net/MultiplayerHud.cs` (LAN 버튼)

핫스팟(같은 서브넷)에서 UDP 브로드캐스트로 호스트를 찾아 IP 입력 없이 접속.

- [ ] **Step 11.1: LanDiscovery 작성**

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// LAN 호스트 발견(UDP 브로드캐스트). 호스트: 1초마다 포트 47878 로 신호 송출.
/// 클라: 같은 포트 수신 → 호스트 IP 획득. 핫스팟 시연용(IP 타이핑 제거).
/// </summary>
public class LanDiscovery : MonoBehaviour
{
    private const int Port = 47878;
    private const string Signature = "VRBOAT";

    public string DiscoveredHostIp { get; private set; }

    private UdpClient udp;
    private float lastBroadcast;
    private bool isHost;

    public void StartHostBroadcast()
    {
        StopAll();
        isHost = true;
        udp = new UdpClient { EnableBroadcast = true };
    }

    public void StartClientListen()
    {
        StopAll();
        isHost = false;
        DiscoveredHostIp = null;
        udp = new UdpClient(Port);
        udp.BeginReceive(OnReceive, null);
    }

    public void StopAll()
    {
        try { udp?.Close(); } catch { }
        udp = null;
        isHost = false;
    }

    private void Update()
    {
        if (!isHost || udp == null || Time.time - lastBroadcast < 1f) return;
        lastBroadcast = Time.time;
        byte[] data = Encoding.ASCII.GetBytes(Signature);
        try { udp.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port)); }
        catch (Exception e) { Debug.LogWarning($"[LanDiscovery] 송출 실패: {e.Message}"); }
    }

    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            var from = new IPEndPoint(IPAddress.Any, Port);
            byte[] data = udp.EndReceive(ar, ref from);
            if (Encoding.ASCII.GetString(data) == Signature)
                DiscoveredHostIp = from.Address.ToString();
            udp.BeginReceive(OnReceive, null);
        }
        catch { /* 소켓 닫힘 — 정상 종료 경로 */ }
    }

    private void OnDestroy() => StopAll();
}
```

- [ ] **Step 11.2: SessionConnector 에 LAN 경로 추가** — 필드/메서드 추가:

```csharp
    // (필드 추가)
    [Tooltip("LAN 폴백용. 같은 GameObject 에 LanDiscovery 부착 시 자동 사용.")]
    [SerializeField] private LanDiscovery lanDiscovery;
    [SerializeField] private ushort lanPort = 7777;

    /// LAN 호스트 시작(클라우드 불능 폴백). UnityTransport 직결.
    public void StartLanHost()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        var utp = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (utp != null) utp.SetConnectionData("0.0.0.0", lanPort);
        if (nm.StartHost())
        {
            if (lanDiscovery != null) lanDiscovery.StartHostBroadcast();
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
        if (nm.StartClient()) { SetState(ConnState.InSession); SessionJoined?.Invoke(); }
        else SetState(ConnState.Failed);
    }
```

(LAN 모드에선 `PlayerCount` 가 세션 기반이라 0 — `MultiplayerHud` 표시는 다음 스텝에서 NGO 기준으로 변경)

- [ ] **Step 11.3: MultiplayerHud — LAN 버튼 2개 추가 + 인원 표시를 NGO 기준으로**

(a) 필드 추가 (`buttonLabel` 선언 아래):

```csharp
    private RectTransform lanHostRt, lanJoinRt;
    private Image lanHostImg, lanJoinImg;
```

(b) `Build()` 끝(buttonLabel 생성 다음)에 LAN 버튼 2개 생성:

```csharp
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
```

(c) 헬퍼 추가 (`Place` 옆):

```csharp
    private static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
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
```

(d) `PointingAtButton(Striker hand)` 을 대상 rect 를 받는 형태로 교체:

```csharp
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
```

(e) `Update()` 의 상태 분기/버튼 입력부 교체 — Offline 분기와 InSession 분기, 그리고 기존 "버튼 입력" 블록 전체:

```csharp
            case SessionConnector.ConnState.Offline:
                statusText.text = "SINGLE MODE";
                btn = "MULTI";          // 클라우드 경로(기본). LAN 버튼은 아래에서 별도 표시.
                break;
```

```csharp
            case SessionConnector.ConnState.InSession:
                // 호스트는 ConnectedClientsList, 클라는 세션 인원(NGO 목록이 서버 전용이라 0일 수 있음).
                int n = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                    ? NetworkManager.Singleton.ConnectedClientsList.Count
                    : connector.PlayerCount;
                n = Mathf.Max(n, connector.PlayerCount);
                statusText.text = $"PLAYERS {n}/4";
                if (connector.IsHost && !counting) btn = "START";
                break;
```

```csharp
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
```

(기존 `Press(string)` 메서드와 옛 버튼 입력 루프는 삭제 — 위 블록이 대체)

- [ ] **Step 11.4: [사용자 Unity] 컴파일 + MPPM 으로 LAN 호스트/조인 확인** (에디터+가상플레이어는 localhost 라 브로드캐스트 수신 동작)

- [ ] **Step 11.5: 커밋**

```powershell
git add Assets/_Game/Scripts/Net/
git commit -m "feat: LAN 폴백 (UDP 브로드캐스트 발견 + UnityTransport 직결)"
```

---

### Task 12: [사용자] Quest 빌드 스모크

- [ ] 12.1 Quest 2대에 빌드 설치 (기존 빌드 세팅)
- [ ] 12.2 교실 와이파이: 양쪽 MULTI → 같은 세션 → START → 레이스 (체크: 보트/아바타/음악 동시성)
- [ ] 12.3 핫스팟: LAN HOST / LAN JOIN 경로 동일 체크
- [ ] 12.4 한쪽 강제 종료 → 남은 쪽 레이스 계속(이탈 보트 사라짐) 확인
- [ ] 12.5 멀티 미사용(MULTI 안 누름) 싱글 플레이 회귀 확인
- [ ] 12.6 발견 이슈 기록 → 다음 세션에서 수정

---

## 실행 중 수정 기록

- **아바타 포즈 발행 버그 fix (Task 6/8 코드에 반영됨)**: 로컬 owner 의 `avatarRoot.SetActive(false)` 는 NetAvatar.Update(포즈 발행)까지 죽임 → NetRacer 는 사람 owner 의 avatarRoot 를 끄지 않고, NetAvatar 가 owner 일 때 비주얼 3개만 스스로 숨기는 방식으로 변경. 위 코드 블록과 실제 커밋이 다른 부분은 이 항목뿐.

## 리스크 메모 (실행자용)

- **Multiplayer Services API 표면**: `CreateOrJoinSessionAsync`/`WithRelayNetwork`/`ISession.Players` 는 1.x Session API 기준. 설치된 버전과 시그니처가 다르면 패키지 문서(Package Manager > 패키지 선택 > Documentation) 의 Quickstart 예제로 보정 — 아키텍처는 불변, 호출부만 조정.
- **NGO 2.x NetworkVariable 쓰기 빈도**: 기본 tick(30/s)으로 충분. 떨림 보이면 NetworkManager Tick Rate 확인.
- **MPPM 가상 플레이어에서 XR 미동작**: 가상 플레이어는 헤드셋 없음 — 보트/거리/세션 검증용. 아바타 포즈는 실기 2대에서 확인.
- **씬의 NetworkObject(코디네이터)**: 씬 배치 NetworkObject 는 서버 시작 시 자동 스폰됨. 접속 전 ClientRpc 호출 금지(START 는 InSession 후에만 노출되므로 안전).
- **스폰 전 NetworkVariable 쓰기**(AI 의 IsAi/Lane): NGO 2.x 에서 지원되는 초기화 패턴이나 일부 버전은 경고 로그를 찍음 — 동작엔 문제 없음. 경고가 거슬리면 서버 전용 초기화를 OnNetworkSpawn 으로 옮기는 대안 있음.
- **늦게 합류한 클라이언트**: 레이스 시작 후 합류하면 카운트다운 ClientRpc 를 못 받아 관전 상태가 됨 — 데모에선 "전원 합류 후 START" 운영으로 회피(스펙 비목표).
