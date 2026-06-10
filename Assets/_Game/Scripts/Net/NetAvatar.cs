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

        // 내 아바타 비주얼은 숨김(내 머리/손은 실물이 있음) — 컴포넌트는 발행을 위해 계속 동작.
        if (headVisual != null) headVisual.gameObject.SetActive(false);
        if (leftHandVisual != null) leftHandVisual.gameObject.SetActive(false);
        if (rightHandVisual != null) rightHandVisual.gameObject.SetActive(false);

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
