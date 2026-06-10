using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 레이스 동기화 총괄(씬 배치 NetworkObject, 호스트 권한).
///   - 세션 합류 → 로비: 음악 정지 + 내 보트 정지/리셋/원위치 + 싱글 고스트 제거.
///   - 레인 배정(접속순), 호스트 START → AI 채움 스폰 → 전원 카운트다운 → 동시 출발.
///
/// 붙이는 법: 빈 GameObject "Multiplayer" 에 SessionConnector 와 함께 추가 + NetworkObject 부착.
///   연결: sessionConnector / raceManager / localPlayerBoat(씬 PlayerBoat의 BoatMover) /
///         beatmapSpawner / netRacerPrefab(AI 채움용 — NetworkManager PlayerPrefab 과 같은 프리팹) /
///         singleplayerGhosts(씬의 Ghost×3 루트).
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
    private readonly List<BoatMover> aiMovers = new();

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
