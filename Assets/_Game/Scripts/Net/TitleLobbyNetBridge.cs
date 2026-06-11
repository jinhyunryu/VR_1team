using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 타이틀/로비 UI(04_CreatureUI 의 TatleLobbyCanvasController)와 멀티 네트워크를 잇는 다리.
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
    [SerializeField] private TatleLobbyCanvasController controller;
    [SerializeField] private SessionConnector connector;

    [Header("설정")]
    [Tooltip("로비 START 가 로드할 레이스 씬 이름 (Build Settings 등록 필수).")]
    [SerializeField] private string raceSceneName = "WaterTemplate 1";

    [Tooltip("전원 Ready 일 때만 START 활성. 끄면 호스트가 언제든 시작 가능.")]
    [SerializeField] private bool requireAllReady = true;

    private void Awake()
    {
        if (controller == null) controller = FindFirstObjectByType<TatleLobbyCanvasController>();
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

        if (controller.tatleStartButton != null) controller.tatleStartButton.onClick.AddListener(OnHostClicked);
        if (controller.tatleJoinButton != null) controller.tatleJoinButton.onClick.AddListener(OnJoinClicked);
        if (controller.lobbyStartButton != null) controller.lobbyStartButton.onClick.AddListener(OnLobbyStartClicked);
        if (controller.lobbyReadyButton != null) controller.lobbyReadyButton.onClick.AddListener(OnReadyClicked);
        if (controller.lobbyExitButton != null) controller.lobbyExitButton.onClick.AddListener(OnLobbyExitClicked);
    }

    private void OnHostClicked() => connector.StartLanHost();
    private void OnJoinClicked() => connector.StartLanClient();

    private void OnReadyClicked()
    {
        var mine = FindLocalRacer();
        if (mine != null) mine.ToggleReady();
    }

    private void OnLobbyExitClicked() => connector.Disconnect();

    private void OnLobbyStartClicked()
    {
        if (!connector.IsHost) return;
        if (requireAllReady && !AllHumansReady())
        {
            Debug.Log("[TitleLobbyNetBridge] 전원 레디 전 — 시작 보류");
            return;
        }
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
        {
            Debug.Log($"[TitleLobbyNetBridge] 레이스 씬 로드 → {raceSceneName} (전원 이동)");
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
            controller.ShowTatleCanvas();
            return;
        }

        if (connector.State != SessionConnector.ConnState.InSession) return;

        DriveCards();

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
                    ? TatleLobbyCanvasController.PlayerLobbyState.ReadyOn
                    : TatleLobbyCanvasController.PlayerLobbyState.ReadyOff);
            }
            else
            {
                controller.SetPlayerState(playerNum, TatleLobbyCanvasController.PlayerLobbyState.None);
            }
        }
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
