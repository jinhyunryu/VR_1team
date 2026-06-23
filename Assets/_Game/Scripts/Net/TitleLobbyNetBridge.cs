using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 타이틀/로비 UI(04_CreatureUI 의 TitleLobbyCanvasController)와 멀티 네트워크를 잇는 다리.
/// 팀메이트 스크립트는 수정하지 않는다 — 컨트롤러가 노출한 public Button 들에 런타임으로
/// 네트워크 동작만 '추가'한다 (UI 전환·사운드는 기존 stub 가 그대로 처리).
///
/// 연결되는 흐름:
///   타이틀 Start  → LAN 호스트 (방 생성)
///   타이틀 Join   → LAN 조인 (브로드캐스트 발견)
///   로비 Ready    → 내 NetRacer.IsReady 토글 (전 기기 카드 동기화)
///   로비 START    → (호스트, 전원 레디) NGO 씬 전환으로 전원 레이스 씬 이동
///                   → NetRaceCoordinator 가 자동으로 카운트다운 → 레이스
///   로비 Exit     → 세션 해제 + 타이틀 복귀
///
/// 붙이는 법(타이틀 씬): 빈 GameObject "TitleNetBridge" 에 부착 (참조는 자동 탐색, 비워도 됨).
///   같은 씬에 NetworkManager(+UnityTransport, PlayerPrefab=NetRacer) 와
///   SessionConnector(persistAcrossScenes 체크) 가 있어야 한다.
/// </summary>
public class TitleLobbyNetBridge : MonoBehaviour
{
    [Header("참조 (비우면 자동 탐색)")]
    [SerializeField] private TitleLobbyCanvasController controller;
    [SerializeField] private SessionConnector connector;

    [Header("설정")]
    [Tooltip("로비 START 가 로드할 레이스 씬 이름 (Build Settings 등록 필수).")]
    [SerializeField] private string raceSceneName = "WaterTemplate 1";

    [Tooltip("전원 Ready 일 때만 START 활성. 끄면 호스트가 언제든 시작 가능.")]
    [SerializeField] private bool requireAllReady = true;

    // 로비 곡 동기: 호스트는 자기 선택이 바뀔 때만 발행, 클라는 호스트 값이 바뀔 때만 적용.
    private int lastBroadcastSong = -1;  // 호스트: 마지막으로 발행한 곡 인덱스
    private int lastAppliedSong = -1;    // 클라: 마지막으로 UI 에 반영한 곡 인덱스

    private void Awake()
    {
        if (controller == null) controller = FindFirstObjectByType<TitleLobbyCanvasController>();
        if (connector == null)
            connector = SessionConnector.Instance != null
                ? SessionConnector.Instance
                : FindFirstObjectByType<SessionConnector>();
    }

    private void Start()
    {
        if (controller == null || connector == null)
        {
            Debug.LogWarning("[TitleLobbyNetBridge] controller/connector 없음 — 비활성 (타이틀 씬 구성 확인)");
            enabled = false;
            return;
        }

        if (controller.titleStartButton != null) controller.titleStartButton.onClick.AddListener(OnHostClicked);
        if (controller.titleJoinButton != null) controller.titleJoinButton.onClick.AddListener(OnJoinClicked);
        if (controller.lobbyStartButton != null) controller.lobbyStartButton.onClick.AddListener(OnLobbyStartClicked);
        if (controller.lobbyReadyButton != null) controller.lobbyReadyButton.onClick.AddListener(OnReadyClicked);
        if (controller.lobbyExitButton != null) controller.lobbyExitButton.onClick.AddListener(OnLobbyExitClicked);
        // 타이틀 Exit: 컨트롤러의 QuitApplication(앱 종료)에 더해 세션 정리만 '추가' (팀메이트 스크립트 비수정).
        if (controller.titleExitButton != null) controller.titleExitButton.onClick.AddListener(OnTitleExitClicked);

        // 곡 선택은 컨트롤러가 추적(SelectedPlaylistSongIndex) — 빌더가 버튼 재생성해도 안전(구독 안 함).

        // 레이스 끝나고 세션 유지한 채 로비로 돌아온 경우 → 터치투스타트 건너뛰고 바로 로비 화면.
        if (connector.State == SessionConnector.ConnState.InSession)
            controller.ShowLobbyCanvas();
    }

    private void OnHostClicked() => connector.StartLanHost();
    private void OnJoinClicked() => connector.StartLanClient();

    private void OnReadyClicked()
    {
        var mine = FindLocalRacer();
        if (mine != null) mine.ToggleReady();
    }

    private void OnLobbyExitClicked() => connector.Disconnect();

    // 타이틀 Exit 클릭 시: 세션 중이면 종료 전에 깔끔하게 끊는다.
    //   QuitApplication 의 Application.Quit / EditorApplication.isPlaying=false 는 프레임 끝에 처리되므로,
    //   같은 클릭에서 먼저 실행되는 Disconnect 의 동기 정리(LAN: NetworkManager.Shutdown → 클라에 호스트 이탈 통지,
    //   LanDiscovery.StopAll)가 종료 전에 끝난다. (LAN 은 Session==null 이라 await 없이 완전 동기로 완료)
    private void OnTitleExitClicked()
    {
        if (connector.State == SessionConnector.ConnState.InSession
            || connector.State == SessionConnector.ConnState.Connecting)
            connector.Disconnect();
    }

    private void OnLobbyStartClicked()
    {
        if (!connector.IsHost) return;
        if (requireAllReady && !AllHumansReady())
        {
            Debug.Log("[TitleLobbyNetBridge] 전원 레디 전 — 시작 보류");
            return;
        }
        // 호스트가 고른 곡(컨트롤러의 실제 선택)을 전달. 멀티는 NetRaceCoordinator 가 전원에게 재동기.
        SongSelection.SelectedIndex = controller.SelectedPlaylistSongIndex;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
        {
            Debug.Log($"[TitleLobbyNetBridge] 레이스 씬 로드 → {raceSceneName} (곡 {controller.SelectedPlaylistSongIndex}, 전원 이동)");
            nm.SceneManager.LoadScene(raceSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void Update()
    {
        if (controller == null || connector == null) return;

        // 접속 실패 → 로비에 갇히지 않게 타이틀 복귀.
        if (connector.State == SessionConnector.ConnState.Failed
            && controller.lobbyCanvas != null && controller.lobbyCanvas.activeSelf)
        {
            controller.ShowTitleCanvas();
            return;
        }

        if (connector.State != SessionConnector.ConnState.InSession) return;

        DriveCards();
        SyncLobbySong();

        // START 버튼: 호스트 + (전원 레디) 일 때만.
        if (controller.lobbyStartButton != null)
            controller.lobbyStartButton.interactable =
                connector.IsHost && (!requireAllReady || AllHumansReady());
    }

    // 접속순(OwnerClientId)으로 카드 1~4 에 매핑 — 전 기기에서 같은 배치.
    private void DriveCards()
    {
        var racers = HumansSorted();
        for (int i = 0; i < 4; i++)
        {
            int playerNum = i + 1;
            if (i < racers.Count)
            {
                var r = racers[i];
                if (r.IsOwner) controller.localPlayerNumber = playerNum;
                controller.SetPlayerState(playerNum, r.IsReady.Value
                    ? TitleLobbyCanvasController.PlayerLobbyState.ReadyOn
                    : TitleLobbyCanvasController.PlayerLobbyState.ReadyOff);
            }
            else
            {
                controller.SetPlayerState(playerNum, TitleLobbyCanvasController.PlayerLobbyState.None);
            }
        }
    }

    // 로비 곡 선택을 전 기기에 동기 — 호스트가 NOW Playing 을 바꾸면 클라 UI(배너+하이라이트)도 갱신.
    //   곡 인덱스는 호스트 racer 의 LobbySongIndex(NetworkVariable)로 전파. 호스트가 권위.
    //   (버그 수리 2026-06-17: 이전엔 곡 인덱스가 START 시점 StartRaceClientRpc 로만 전송돼
    //    로비에서 호스트가 곡을 바꿔도 클라 NOW Playing 이 그대로였음.)
    private void SyncLobbySong()
    {
        var host = FindHostRacer();
        if (host == null) return;

        if (connector.IsHost)
        {
            int sel = controller.SelectedPlaylistSongIndex;
            if (sel != lastBroadcastSong)
            {
                host.LobbySongIndex.Value = sel;   // 호스트=서버 → Server 쓰기 권한 OK
                lastBroadcastSong = sel;
            }
        }
        else
        {
            int v = host.LobbySongIndex.Value;
            if (v != lastAppliedSong)
            {
                controller.SelectPlaylistSong(v);  // 클라 UI(하이라이트 + NOW Playing 배너) 갱신
                lastAppliedSong = v;
            }
        }
    }

    // 서버(호스트)가 소유한 사람 racer = 곡 선택의 단일 권위 인스턴스.
    // 호스트는 항상 가장 낮은 OwnerClientId(서버 id 0) → HumansSorted()[0].
    private NetRacer FindHostRacer()
    {
        var humans = HumansSorted();
        return humans.Count > 0 ? humans[0] : null;
    }

    private List<NetRacer> HumansSorted()
    {
        var list = new List<NetRacer>();
        foreach (var r in FindObjectsByType<NetRacer>(FindObjectsSortMode.None))
            if (!r.IsAi.Value) list.Add(r);
        list.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        return list;
    }

    private NetRacer FindLocalRacer()
    {
        foreach (var r in FindObjectsByType<NetRacer>(FindObjectsSortMode.None))
            if (r.IsOwner && !r.IsAi.Value) return r;
        return null;
    }

    private bool AllHumansReady()
    {
        var racers = HumansSorted();
        if (racers.Count == 0) return false;
        foreach (var r in racers)
            if (!r.IsReady.Value) return false;
        return true;
    }
}
