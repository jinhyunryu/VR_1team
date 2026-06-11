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
    private float lastDiagLog;        // 카운트다운 진단 로그 간격용 (개발 빌드 전용)

    private bool IsHumanOwner => !IsAi.Value && IsOwner;
    private bool DrivenByNetwork => !IsHumanOwner && !(IsAi.Value && IsServer);

    public override void OnNetworkSpawn()
    {
        mover = GetComponent<BoatMover>();
        DisableForeignComponents();

        var coord = NetRaceCoordinator.Instance;
        if (IsServer && coord != null && !IsAi.Value)
            Lane.Value = coord.ClaimLane();

        ApplyRole();

        // IsAi/Lane 이 스폰 페이로드에 못 실리거나 늦게 도착해도 역할/레인을 다시 적용 (NGO 사전-스폰 쓰기 경고 대비).
        IsAi.OnValueChanged += (_, _) => ApplyRole();
        Lane.OnValueChanged += (_, v) =>
        {
            if (IsHumanOwner && NetRaceCoordinator.Instance != null)
                NetRaceCoordinator.Instance.SetLocalLane(v);
            SnapToLane();
        };

        Debug.Log($"[NetRacer] spawn — IsAi={IsAi.Value} Lane={Lane.Value} ownerId={OwnerClientId} " +
                  $"IsOwner={IsOwner} IsServer={IsServer} pos={transform.position}");
        TryRegister();
    }

    /// 역할(나/원격/AI)에 따른 셋업. IsAi 가 늦게 도착해도 다시 호출되므로 멱등이어야 함.
    private void ApplyRole()
    {
        var coord = NetRaceCoordinator.Instance;

        if (IsHumanOwner)
        {
            // 나: 표시는 기존 PlayerBoat 가 담당 — 이 인스턴스는 발행 전용.
            // ⚠️ avatarRoot 는 끄지 않는다 — NetAvatar.Update 가 포즈를 발행해야 함.
            //    (NetAvatar 가 owner 비주얼만 스스로 숨김)
            if (hullVisual != null) hullVisual.SetActive(false);
            mover.enabled = false;
            localSource = coord != null ? coord.LocalPlayerBoat : null;
            if (coord != null) coord.SetLocalLane(Lane.Value);
            return;
        }

        if (hullVisual != null) hullVisual.SetActive(true);
        if (avatarRoot != null) avatarRoot.SetActive(!IsAi.Value);
        mover.enabled = true;

        if (ghostRacer != null) ghostRacer.enabled = IsAi.Value && IsServer; // 호스트의 AI 만 구동

        // 시작 위치: 내 레인 기준 상대 오프셋 (현재 진행 거리 유지).
        if (coord != null)
        {
            transform.rotation = coord.RaceRotation;
            transform.position = RacePosition(coord, mover.DistanceTraveled);
        }

        // 원격(비owner) mover 는 즉시 네트워크 구동 래치 — 스폰 직후 cruise 자가 전진 방지.
        if (DrivenByNetwork) mover.ApplyNetworkDistance(NetDistance.Value);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 진단: 카운트다운 중 원격 보트가 움직이는지 0.5초 간격 위치 기록.
        var diag = NetRaceCoordinator.Instance;
        if (diag != null && diag.CountdownRemaining > 0f && Time.time - lastDiagLog > 0.5f)
        {
            lastDiagLog = Time.time;
            Debug.Log($"[NetRacer] 카운트다운 중 — IsAi={IsAi.Value} Lane={Lane.Value} " +
                      $"netDist={NetDistance.Value:F2} dispDist={displayDistance:F2} pos={transform.position}");
        }
#endif

        // 원격: 수신 거리 보간 → mover 적용.
        float dt = Time.deltaTime;
        displayDistance = Mathf.Lerp(displayDistance, NetDistance.Value,
                                     1f - Mathf.Exp(-distanceLerp * dt));
        mover.ApplyNetworkDistance(displayDistance);
    }

    /// 프리팹에 실수로 카메라/리스너/게임 입력이 섞여 들어와도 씬을 점령하지 못하게 차단.
    /// (PlayerBoat 를 XR Origin 째 복사하는 실수 방지 — 화면 점프/오디오리스너 중복의 원인)
    private void DisableForeignComponents()
    {
        foreach (var cam in GetComponentsInChildren<Camera>(true))
        {
            cam.enabled = false;
            Debug.LogWarning($"[NetRacer] 프리팹에 Camera 가 들어 있음({cam.name}) — 비활성 처리. NetRacer 프리팹에서 XR Origin/카메라를 제거하세요.");
        }
        foreach (var al in GetComponentsInChildren<AudioListener>(true)) al.enabled = false;
        foreach (var sc in GetComponentsInChildren<SpeedController>(true)) sc.enabled = false;
        foreach (var st in GetComponentsInChildren<Striker>(true)) st.enabled = false;
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

    /// 표시 위치를 현재 레인 기준으로 재계산. 내 레인(LocalLane)이 늦게 정해질 때 코디네이터가 전체 호출.
    public void SnapToLane()
    {
        var coord = NetRaceCoordinator.Instance;
        if (coord == null || IsHumanOwner || mover == null) return;
        transform.position = RacePosition(coord, mover.DistanceTraveled);
    }

    private Vector3 RacePosition(NetRaceCoordinator coord, float dist)
    {
        // 씬에 배치된 레인 앵커(PlayerBoat+Ghost×3) 우선 — 코스에 맞춘 실제 간격 사용.
        if (!coord.TryGetLaneOffset(Lane.Value, coord.LocalLane, out Vector3 side))
            side = coord.RaceRotation * Vector3.right * ((Lane.Value - coord.LocalLane) * laneWidth);

        return coord.RaceOrigin
             + coord.RaceRotation * Vector3.forward * dist
             + side;
    }
}
