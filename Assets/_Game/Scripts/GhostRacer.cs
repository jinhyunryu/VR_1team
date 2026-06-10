using UnityEngine;

/// <summary>
/// 고스트(AI 배) 한 척의 페이스를 제어한다. 각 고스트 배에 1개씩 붙인다.
///
/// 페이스 모델 (명세 5.7 + 밸런스):
///   1) 레이스 진행도(자기 거리 / finishDistance)에 따라 startPace → endPace 로 램프업.
///      → 플레이어가 콤보로 6 에서 18~20 까지 가속하는 아크를 흉내내, 초반 압살/후반 무의미를 방지.
///   2) 그 위에 "가벼운 러버밴딩": 플레이어와의 거리차로 속도를 ±maxRubberbandAdjust 만큼만 미세조정.
///      (앞서면 살짝 봐주고 뒤처지면 살짝 따라붙음 — 드라마용. 실력 결과를 뒤집을 만큼 강하지 않게.)
///
///   ※ 고스트 속도를 "플레이어 콤보/현재속도"에 직접 묶지 않는다 — 그러면 AI가 미러링돼
///     잘 쳐도 앞서지지 않아 실력 보상이 사라진다. 페이스는 "난이도 바"로 고정 램프.
///
/// 의존성: 리듬과 무관. 플레이어 BoatMover 의 누적 거리만 참조.
///
/// 붙이는 법(에디터):
///   1) 고스트 배 = [배 모델] + BoatMover + GhostRacer.  playerBoat = 플레이어 BoatMover 드래그.
///   2) 3척이면 endPace 를 다르게(예: 12 / 14 / 16) → 등수 바. startPace 는 플레이어 시작과 비슷(~6).
///   3) 시작 위치 X(레인) 다르게. finishDistance 는 RaceManager 의 값과 동일하게 맞춤(나중에 RaceManager가 주입 가능).
///
/// ⚠️ 숫자(start/end/러버밴딩)는 Phase 7 튜닝 영역. 플레이어 콤보→속도(SpeedController, 팀메이트 이벤트)
///    가 붙어 실제 속도 곡선이 생긴 뒤 같이 맞추는 게 정확하다. 지금은 합리적 기본값.
/// </summary>
[RequireComponent(typeof(BoatMover))]
public class GhostRacer : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("플레이어 배의 BoatMover (거리 비교용). 비우면 러버밴딩 없이 램프 페이스로만 달림.")]
    [SerializeField] private BoatMover playerBoat;

    [Header("페이스 램프 (레이스 동안 가속)")]
    [Tooltip("레이스 시작 페이스(m/s). 플레이어 시작 속도와 비슷하게(~6).")]
    [SerializeField] private float startPace = 6f;

    [Tooltip("결승 부근 페이스(m/s). 고스트마다 다르게 = 등수 바 (예: 12/14/16).")]
    [SerializeField] private float endPace = 14f;

    [Tooltip("진행도 0→1 을 페이스 보간에 매핑하는 곡선(램프 모양). 기본 선형.")]
    [SerializeField] private AnimationCurve paceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("가속 앞당김(>1이면 초반부터 빨라짐 = AI 더 어려움). 1=곡선 그대로.")]
    [SerializeField] private float accelerationPower = 1.8f;

    [Tooltip("진행도 계산용 결승 거리(m). RaceManager 값과 동일하게. 0 이면 램프 없이 endPace 고정.")]
    [SerializeField] private float finishDistance = 1800f;

    [Header("러버밴딩 (약중간 — 드라마용)")]
    [Tooltip("플레이어와의 거리차 1m당 속도 보정(m/s).")]
    [SerializeField] private float rubberbandGainPerMeter = 0.05f;

    [Tooltip("러버밴딩 보정 최대치(±m/s). 작을수록 약함(실력 결과 보존).")]
    [SerializeField] private float maxRubberbandAdjust = 1.2f;

    private BoatMover boatMover;

    private void Awake()
    {
        boatMover = GetComponent<BoatMover>();
    }

    /// 멀티: 호스트가 AI 레이서 스폰 시 참조/페이스를 주입한다(프리팹은 씬 참조 불가).
    public void InitForNetwork(BoatMover player, float finish, float endPaceOverride)
    {
        playerBoat = player;
        finishDistance = finish;
        endPace = endPaceOverride;
    }

    private void Update()
    {
        // 1) 진행도에 따른 램프 페이스.
        float basePace;
        if (finishDistance > 0f)
        {
            float progress = Mathf.Clamp01(boatMover.DistanceTraveled / finishDistance);
            float curved = Mathf.Pow(paceCurve.Evaluate(progress), 1f / Mathf.Max(0.1f, accelerationPower));
            basePace = Mathf.Lerp(startPace, endPace, curved);
        }
        else
        {
            basePace = endPace;
        }

        // 2) 가벼운 러버밴딩 (플레이어 거리차).
        float target = basePace;
        if (playerBoat != null)
        {
            float gap = playerBoat.DistanceTraveled - boatMover.DistanceTraveled; // >0: 고스트 뒤처짐
            float adjust = Mathf.Clamp(gap * rubberbandGainPerMeter,
                                       -maxRubberbandAdjust, maxRubberbandAdjust);
            target = basePace + adjust;
        }

        boatMover.SetTargetSpeed(Mathf.Max(0f, target));
    }
}
