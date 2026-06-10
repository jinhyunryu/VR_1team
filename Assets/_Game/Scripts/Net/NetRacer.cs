using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 레이서 1명. NetworkManager 의 PlayerPrefab 으로 자동 스폰(접속자당 1개) +
/// 호스트가 AI 채움용으로 추가 스폰.
///
/// 역할 분기:
///   - 사람 + owner(나):    비주얼 숨김. 씬의 내 PlayerBoat 거리를 NetDistance 에 발행만.
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
            // ⚠️ avatarRoot 는 끄지 않는다 — NetAvatar.Update 가 포즈를 발행해야 함.
            //    (NetAvatar 가 owner 비주얼만 스스로 숨김)
            if (hullVisual != null) hullVisual.SetActive(false);
            mover.enabled = false;
            localSource = coord != null ? coord.LocalPlayerBoat : null;
            if (coord != null) coord.SetLocalLane(Lane.Value);
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

        Lane.OnValueChanged += (_, v) =>
        {
            if (IsHumanOwner && NetRaceCoordinator.Instance != null)
                NetRaceCoordinator.Instance.SetLocalLane(v);
            SnapToLane();
        };
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
