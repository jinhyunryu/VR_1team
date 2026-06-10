using UnityEngine;

/// <summary>
/// 보트 속도에 비례해 워프/스피드라인 파티클 강도를 조절한다(표현 only).
///   느릴 땐 안 보이고, startSpeed 부터 나타나 fullSpeed 에서 최대.
///
/// 대상: 임포트한 워프 효과 프리팹(Style 1/2) 인스턴스 — 자식 ParticleSystem 들 전부 제어.
///
/// ⚠️ VR 멀미: 주변시야 빠른 스트릭은 벡션 멀미를 키운다(명세 2번 "속도감 연출 절제").
///    → startSpeed 를 높게(빠를 때만), 강도 maxIntensity 낮게, 너무 빽빽하지 않게.
///
/// 붙이는 법: 아무 오브젝트에 추가 → boat(플레이어 BoatMover) + warpEffect(Style 인스턴스) 연결.
/// </summary>
public class SpeedLines : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("플레이어 보트(현재 속도 읽음).")]
    [SerializeField] private BoatMover boat;
    [Tooltip("워프 효과 프리팹 인스턴스(자식 ParticleSystem 들).")]
    [SerializeField] private GameObject warpEffect;

    [Header("속도 → 강도 (m/s)")]
    [Tooltip("이 속도부터 라인이 보이기 시작.")]
    [SerializeField] private float startSpeed = 12f;
    [Tooltip("이 속도에서 최대 강도.")]
    [SerializeField] private float fullSpeed = 20f;
    [Tooltip("최대 방출 배율(1 = 프리팹 기본량). VR이면 낮게 시작 권장.")]
    [SerializeField] private float maxIntensity = 1f;

    [Tooltip("스트릭 흐름 속도도 보트 속도에 비례시킬지.")]
    [SerializeField] private bool scaleStreakSpeed = false;

    [Tooltip("워프 효과의 Light(이 프리팹 32개!) 를 끈다. Quest엔 매우 무거우니 VR이면 켜둘 것.")]
    [SerializeField] private bool disableLights = true;

    [Header("색 (툰)")]
    [Tooltip("켜면 모든 스트릭을 lineColor 로 통일(기본 붉은 기 제거).")]
    [SerializeField] private bool forceColor = true;
    [SerializeField] private Color lineColor = Color.white;

    private ParticleSystem[] systems;

    private void Awake()
    {
        if (warpEffect == null) return;

        systems = warpEffect.GetComponentsInChildren<ParticleSystem>(true);

        if (disableLights)
            foreach (var light in warpEffect.GetComponentsInChildren<Light>(true))
                light.enabled = false; // 32개 실시간 라이트 = 모바일 VR 프레임 폭락 방지

        if (forceColor)
            ApplyColor();
    }

    // 모든 파티클의 Start Color + Color over Lifetime 을 lineColor 로 통일(알파는 1→0 페이드 유지).
    private void ApplyColor()
    {
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(lineColor, 0f), new GradientColorKey(lineColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

        foreach (var ps in systems)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.startColor = lineColor;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }
    }

    private void Update()
    {
        if (boat == null || systems == null) return;

        // 속도 → 0..1 강도.
        float t = Mathf.Clamp01(Mathf.InverseLerp(startSpeed, fullSpeed, boat.Speed));
        float intensity = t * maxIntensity;

        foreach (var ps in systems)
        {
            if (ps == null) continue;
            var emission = ps.emission;
            emission.rateOverTimeMultiplier = intensity;     // 시간 기반 방출
            emission.rateOverDistanceMultiplier = intensity; // 거리 기반 방출(프리팹이 이걸 쓰면)

            if (!ps.isPlaying) ps.Play();                    // 멈춰 있으면 재생 보장

            if (scaleStreakSpeed)
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Lerp(0.6f, 1.5f, t);
            }
        }
    }
}
