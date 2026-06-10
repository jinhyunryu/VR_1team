using UnityEngine;

/// <summary>
/// 배가 파도에 따라 살짝 출렁이는(위아래 + 미세 기울기) 느낌을 sine 으로 표현한다.
/// 표현(presentation) only — 게임 로직(속도/거리/판정)에 전혀 영향 없음.
///
/// 시작 시점의 로컬 포즈를 기준으로 오프셋만 더한다.
/// → BoatMover 가 부모(PlayerBoat)를 전진시켜도 출렁임은 그 위에 자연스럽게 합쳐진다.
///
/// ⚠️⚠️ VR 멀미 경고 (매우 중요) ⚠️⚠️
///   이 컴포넌트가 "플레이어 머리(카메라)를 포함한 오브젝트"에 붙으면
///   상하 흔들림 + 회전이 곧 카메라 가속 → 멀미를 유발한다(명세: 카메라 인위 가속 금지).
///
///   권장 구조:
///     PlayerBoat (BoatMover — 전진)
///     ├── BoatVisual (배 모델, ★여기에 BoatBob★ — 풀 출렁임+기울기, 시각만)
///     └── XR Origin (플레이어 — 흔들지 않음)
///
///   플레이어도 출렁임을 "살짝" 느끼게 하고 싶으면:
///     XR Origin 에 BoatBob 을 따로 하나 더 붙이되
///     → bobAmplitude 아주 작게(예: 0.02) + pitchAmplitude/rollAmplitude = 0 (회전 금지!)
///     회전(pitch/roll)은 VR에서 멀미 최악이라 카메라엔 절대 주지 말 것.
/// </summary>
public class BoatBob : MonoBehaviour
{
    [Header("상하 출렁임")]
    [Tooltip("위아래 진폭(m). 시각용 배는 0.1~0.3, 카메라엔 0.02 이하.")]
    [SerializeField] private float bobAmplitude = 0.15f;

    [Tooltip("출렁임 빠르기(Hz 느낌). 낮을수록 잔잔.")]
    [SerializeField] private float bobFrequency = 0.5f;

    [Header("미세 기울기 (★카메라에는 0 으로★)")]
    [Tooltip("앞뒤 끄덕임 각도(도). 시각용 배만. VR 카메라엔 0.")]
    [SerializeField] private float pitchAmplitude = 1.5f;

    [Tooltip("좌우 갸웃 각도(도). 시각용 배만. VR 카메라엔 0.")]
    [SerializeField] private float rollAmplitude = 2f;

    [Tooltip("기울기 빠르기(Hz 느낌).")]
    [SerializeField] private float tiltFrequency = 0.35f;

    [Header("기타")]
    [Tooltip("배마다 위상을 다르게 줘서 동기화돼 보이지 않게(여러 배일 때).")]
    [SerializeField] private float phaseOffset = 0f;

    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    private const float Tau = Mathf.PI * 2f;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        float t = Time.time + phaseOffset;

        // 상하: 단일 sine.
        float y = bobAmplitude * Mathf.Sin(t * bobFrequency * Tau);

        // 기울기: pitch/roll 에 서로 다른 주파수·위상을 줘서 "딱딱한 동기화"를 피함(파도 같은 organic 느낌).
        float pitch = pitchAmplitude * Mathf.Sin(t * tiltFrequency * Tau);
        float roll = rollAmplitude * Mathf.Sin(t * tiltFrequency * Tau * 0.83f + 1.3f);

        transform.localPosition = startLocalPosition + new Vector3(0f, y, 0f);
        transform.localRotation = startLocalRotation * Quaternion.Euler(pitch, 0f, roll);
    }

    /// 런타임에 동적으로 붙일 때(예: SpeedScenery) 파라미터 주입 + 위상 분산.
    /// 물리 없이 sine 흔들림만 — 장식용 수십 개에 가볍게 적용 가능.
    public void Configure(float bobAmp, float bobFreq, float tiltDegrees, bool randomPhase)
    {
        bobAmplitude = bobAmp;
        bobFrequency = bobFreq;
        pitchAmplitude = tiltDegrees;
        rollAmplitude = tiltDegrees;
        tiltFrequency = Mathf.Max(0.05f, bobFreq * 0.7f);
        if (randomPhase) phaseOffset = Random.Range(0f, 1000f); // 개체마다 다르게 흔들리게
    }
}
