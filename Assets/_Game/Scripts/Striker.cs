using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// 노트를 "쳐내는 도구"의 타격 지점 운동을 추적한다.
/// 도구가 맨손인지 / 후라이팬인지 / 노(oar)인지는 이 코드가 모른다 —
/// hitPoint 가 어디에 붙느냐로만 결정된다(에디터에서 Transform 교체 = 코드 0 변경).
///
/// 역할: 순수 입력 추적만. 판정(슬랩/펀치/등급)은 하지 않는다.
///       NoteJudge 가 이 컴포넌트의 Velocity/Speed/LocalPosition 을 읽어 해석한다.
///
/// 속도 기준: referenceFrame(플레이어 Rig) 로컬 공간에서 계산한다.
///   → 배(Rig)가 전진해도 "도구 자체의 운동"만 잡힌다.
///   → 방향이 플레이어 기준 좌/우/전후 축이라 레인 판정에 그대로 쓰기 좋다.
///   referenceFrame 이 비어 있으면 월드 기준으로 폴백(Rig 붙기 전 단독 테스트용).
///
/// 의존성: 없음 (GameEvents 계약에 의존하지 않음) → 단독으로 테스트 가능.
///
/// 붙이는 법(에디터):
///   1) 씬에 XR Origin (XR Rig) 배치.
///   2) 이 스크립트를 좌/우 컨트롤러(또는 도구) GameObject 에 추가.
///   3) side = Left / Right.
///   4) hitPoint = 타격 지점(맨손=컨트롤러, 도구=도구 끝). 비우면 자기 transform.
///   5) referenceFrame = XR Origin 루트 Transform.
///   6) Play → 빠르게 휘두르면 Console 에 타격 후보 로그.
/// </summary>
public class Striker : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("설정")]
    [Tooltip("이 타격기가 어느 쪽인지")]
    [SerializeField] private Side side = Side.Right;

    [Tooltip("추적할 타격 지점. 맨손=컨트롤러 Transform, 도구=도구 끝(예: 후라이팬 면) Transform. 비우면 이 컴포넌트의 transform.")]
    [SerializeField] private Transform hitPoint;

    [Tooltip("속도를 이 기준의 로컬 공간에서 계산. 보통 XR Origin(플레이어 Rig) 루트. 배가 움직여도 도구 자체 속도만 잡힌다. 비우면 월드 기준 폴백.")]
    [SerializeField] private Transform referenceFrame;

    [Header("검증용 (판정 자체는 NoteJudge 가 담당)")]
    [Tooltip("이 속도(m/s) 이상이면 '타격 후보'로 보고 로그만 찍는다. 판정 아님.")]
    [SerializeField] private float strikeSpeedThreshold = 1.5f;

    [Tooltip("켜면 임계값 초과 시 Console 에 속도/방향 로그 출력.")]
    [SerializeField] private bool debugLog = true;

    [Tooltip("그랩/트리거를 '눌림'으로 인정하는 아날로그 임계값(0~1). 낮추면 더 민감.")]
    [SerializeField] private float pressThreshold = 0.4f;

    // ── 외부에서 읽는 추적 결과 (읽기 전용, referenceFrame 로컬 기준) ──────────
    public Side WhichSide => side;
    public Vector3 LocalPosition { get; private set; } // referenceFrame 로컬
    public Vector3 Velocity { get; private set; }       // referenceFrame 로컬, m/s
    public float Speed { get; private set; }            // Velocity.magnitude (m/s)

    /// 타격 지점의 월드 위치(노트 거리 판정 등 외부용).
    public Vector3 WorldPosition => Point.position;
    /// 타격 지점의 정면 방향(레이 포인터 등 외부용).
    public Vector3 Forward => Point.forward;

    /// 이 손의 그랩(그립)이 눌려있나. 아날로그 값 ≥ 임계값(둔한 bool 버튼 대신).
    public bool GrabHeld => ReadAxis(CommonUsages.grip) >= pressThreshold;
    /// 이 손의 트리거가 눌려있나. 아날로그 값 ≥ 임계값.
    public bool TriggerHeld => ReadAxis(CommonUsages.trigger) >= pressThreshold;

    private float ReadAxis(InputFeatureUsage<float> usage)
    {
        var node = side == Side.Left ? XRNode.LeftHand : XRNode.RightHand;
        var device = InputDevices.GetDeviceAtXRNode(node);
        return device.isValid && device.TryGetFeatureValue(usage, out float value) ? value : 0f;
    }

    /// 이 손 컨트롤러에 진동(햅틱)을 보낸다.
    public void SendHaptic(float amplitude, float duration)
    {
        var node = side == Side.Left ? XRNode.LeftHand : XRNode.RightHand;
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
    }

    private Vector3 lastLocalPosition;
    private bool hasLastPosition;

    private Transform Point => hitPoint != null ? hitPoint : transform;

    // 월드 위치를 referenceFrame 로컬로 변환(Rig 의 이동·회전 제거). 없으면 월드 그대로.
    private Vector3 ToReferenceLocal(Vector3 worldPos)
        => referenceFrame != null ? referenceFrame.InverseTransformPoint(worldPos) : worldPos;

    private void OnEnable()
    {
        // 활성화 직후 첫 프레임 속도 튐 방지: 기준점 초기화.
        LocalPosition = ToReferenceLocal(Point.position);
        lastLocalPosition = LocalPosition;
        Velocity = Vector3.zero;
        Speed = 0f;
        hasLastPosition = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        LocalPosition = ToReferenceLocal(Point.position);

        if (hasLastPosition)
        {
            // referenceFrame 로컬 위치의 유한 차분 = Rig 운동을 뺀 "도구 자체" 속도.
            Velocity = (LocalPosition - lastLocalPosition) / dt;
            Speed = Velocity.magnitude;

            if (debugLog && Speed >= strikeSpeedThreshold)
            {
                Vector3 dir = Velocity.normalized;
                Debug.Log($"[Striker:{side}] 타격 후보  speed={Speed:F2} m/s  dir=({dir.x:F2},{dir.y:F2},{dir.z:F2})");
            }
        }

        lastLocalPosition = LocalPosition;
        hasLastPosition = true;
    }
}
