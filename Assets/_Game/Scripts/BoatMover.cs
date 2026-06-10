using UnityEngine;

/// <summary>
/// 배(또는 AI 고스트)를 자기 forward 방향으로 Speed(m/s) 만큼 전진시킨다.
/// 레일 직진(회전 없음) 기준 — 명세 1번 5항(객체 이동) 결정 반영.
///
/// Speed 의 출처는 외부에서 주입한다(이 컴포넌트는 "어떻게 빨라지는지"를 모른다):
///   - 플레이어 배: 나중에 SpeedController 가 매 프레임 SetTargetSpeed() (콤보 기반).
///   - AI 고스트:   나중에 GhostRacer 가 SetTargetSpeed() (AI 페이스).
///   - 지금:        useCruiseSpeed = true 면 cruiseSpeed 상수로 일정 전진
///                  (컴포트/FPS 스모크 테스트 + 토대).
///
/// ⚠️ 플레이어 배라면 XR Origin(헤드셋+컨트롤러)이 이 오브젝트와 함께 움직여야 한다.
///    권장: "PlayerBoat" 빈 루트에 이 스크립트 + 자식으로 [배 모델] + [XR Origin].
///    그러면 Striker.referenceFrame = XR Origin 이 배와 함께 움직여 타격 판정이 정확히 유지됨.
///
/// 재사용: 플레이어와 고스트가 같은 컴포넌트를 쓴다(속도 주입자만 다름).
/// </summary>
public class BoatMover : MonoBehaviour
{
    [Header("속도 (m/s)")]
    [Tooltip("외부 컨트롤러가 없을 때 쓰는 기본 순항 속도. 테스트/토대용.")]
    [SerializeField] private float cruiseSpeed = 6f;

    [Tooltip("켜면 항상 cruiseSpeed 로 전진(테스트용). 끄면 외부 SetTargetSpeed 값 사용.")]
    [SerializeField] private bool useCruiseSpeed = true;

    [Tooltip("목표 속도로 수렴하는 부드러움 계수(클수록 빠르게 반응). 0 이면 즉시. 멀미 방지(명세 4번).")]
    [SerializeField] private float speedSmoothing = 3f;

    // ── 외부에서 읽는 상태 (읽기 전용) ──────────────────────────────
    /// 현재 실제 속도 (m/s).
    public float Speed { get; private set; }
    /// 누적 전진 거리 (m). RaceManager 가 순위 계산에 사용 예정.
    public float DistanceTraveled { get; private set; }

    private float targetSpeed;

    /// 정지(결승 통과 등) 래치. true 면 부드럽게 0 으로 감속하고 SetTargetSpeed 를 무시한다.
    public bool Stopped { get; private set; }

    /// 외부(SpeedController / GhostRacer)가 목표 속도를 설정한다.
    /// 호출하면 순항 모드는 자동 해제된다. (정지 상태면 무시)
    public void SetTargetSpeed(float speed)
    {
        if (Stopped) return;
        useCruiseSpeed = false;
        targetSpeed = Mathf.Max(0f, speed);
    }

    /// 배를 멈춘다(목표 0 으로 부드럽게 감속). 결승 통과 시 등. 이후 SetTargetSpeed 무시.
    public void Stop()
    {
        Stopped = true;
        useCruiseSpeed = false;
        targetSpeed = 0f;
    }

    /// 정지 해제(레이스 재시작 시).
    public void Resume() => Stopped = false;

    /// 거리 누적 리셋 (레이스 재시작 시).
    public void ResetDistance() => DistanceTraveled = 0f;

    private void OnEnable()
    {
        if (useCruiseSpeed)
        {
            targetSpeed = cruiseSpeed;
            Speed = cruiseSpeed; // 시작부터 순항 속도(테스트가 자연스럽게)
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (Stopped) targetSpeed = 0f;
        else if (useCruiseSpeed) targetSpeed = cruiseSpeed;

        // 급가속 금지: 목표 속도로 부드럽게 수렴 (프레임레이트 무관 지수 보간).
        Speed = speedSmoothing > 0f
            ? Mathf.Lerp(Speed, targetSpeed, 1f - Mathf.Exp(-speedSmoothing * dt))
            : targetSpeed;

        // 자기 forward 로 전진.
        float step = Speed * dt;
        transform.position += transform.forward * step;
        DistanceTraveled += step;
    }
}
