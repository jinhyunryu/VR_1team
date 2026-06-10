using UnityEngine;

/// <summary>
/// 보트 속도를 제어한다. (명세 5.4 — 콤보/속도 분리 버전)
///
/// ★콤보와 속도를 분리★:
///   - Combo : 리듬 카운터. 미스 시 0으로 리셋(리듬게임 정석).
///   - speedLevel : 속도 누적값. 히트마다 +hitSpeedGain, 미스마다 -missSpeedPenalty(만큼만).
///   → 미스해도 콤보는 0이 되지만 속도는 "정해진 만큼만" 떨어져 급추락하지 않는다.
///
/// 리듬 계약(GameEvents) 비의존. 입력 API: RegisterHit / RegisterMiss / SetCombo / AddBoost / ActivateShield.
///   지금(프로토): ProtoNote 가 호출.  나중(정식): OnNoteJudged 어댑터가 호출.
///
/// 붙이는 법: 플레이어 PlayerBoat(BoatMover 있는 곳)에 추가.
/// 에디터 테스트: debugKeys → H=히트, J=미스.
/// </summary>
[RequireComponent(typeof(BoatMover))]
public class SpeedController : MonoBehaviour
{
    [Header("속도 (m/s)")]
    [Tooltip("최소(시작) 속도. 속도는 이 아래로 안 떨어짐.")]
    [SerializeField] private float baseSpeed = 6f;
    [Tooltip("최대 속도.")]
    [SerializeField] private float maxSpeed = 20f;
    [Tooltip("히트당 오르는 속도.")]
    [SerializeField] private float hitSpeedGain = 0.5f;
    [Tooltip("미스당 떨어지는 속도. 콤보는 0이 되지만 속도는 이만큼만 깎임.")]
    [SerializeField] private float missSpeedPenalty = 3f;

    [Header("부스트(아이템)")]
    [Tooltip("부스트가 maxSpeed 위로 더 허용하는 여유분.")]
    [SerializeField] private float boostExtraCap = 4f;

    [Header("디버그(에디터)")]
    [SerializeField] private bool debugKeys = true;

    public int Combo { get; private set; }
    public float CurrentTargetSpeed { get; private set; }
    public bool ShieldActive => shieldTimer > 0f;

    private BoatMover boatMover;
    private float speedLevel;
    private float boostSpeed;
    private float boostTimer;
    private float shieldTimer;

    private void Awake()
    {
        boatMover = GetComponent<BoatMover>();
        speedLevel = baseSpeed;
    }

    // ── 입력 API ──────────────────────────────────────────────
    public void RegisterHit()
    {
        Combo++;
        speedLevel = Mathf.Min(maxSpeed, speedLevel + hitSpeedGain); // 속도 누적
    }

    public void RegisterMiss()
    {
        if (shieldTimer > 0f) return;                                // 실드 중 미스 무시
        Combo = 0;                                                   // 콤보는 정석대로 0
        speedLevel = Mathf.Max(baseSpeed, speedLevel - missSpeedPenalty); // 속도는 일부만 감소
    }

    public void SetCombo(int combo) => Combo = Mathf.Max(0, combo);

    public void AddBoost(float extraSpeed, float duration)
    {
        boostSpeed = Mathf.Max(boostSpeed, extraSpeed);
        boostTimer = Mathf.Max(boostTimer, duration);
    }

    public void ActivateShield(float duration) => shieldTimer = Mathf.Max(shieldTimer, duration);

    // ──────────────────────────────────────────────────────────
    private void Update()
    {
#if UNITY_EDITOR
        if (debugKeys)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.hKey.wasPressedThisFrame) RegisterHit();
                if (kb.jKey.wasPressedThisFrame) RegisterMiss();
            }
        }
#endif
        if (boostTimer > 0f)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0f) boostSpeed = 0f;
        }
        if (shieldTimer > 0f) shieldTimer -= Time.deltaTime;

        float target = Mathf.Min(speedLevel + boostSpeed, maxSpeed + boostExtraCap);
        CurrentTargetSpeed = target;
        boatMover.SetTargetSpeed(target); // 급가속 방지 보간은 BoatMover 가 처리
    }
}
