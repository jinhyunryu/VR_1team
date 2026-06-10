using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보트가 지나가는 부표·바위·등을 진행 경로 앞쪽에 일정 간격으로 스폰하고,
/// 충분히 지나간 것은 제거한다(속도감 연출). 표현 only — 로직/판정 영향 없음.
///
/// 객체 이동 모델 전제: 보트가 월드를 전진 → 월드 고정 사물을 "지나간다".
///   고정 공간 간격(spacingMeters)으로 깔면 "빠를수록 더 자주 지나감"이 자동 성립.
///   (명세 5.10 의 '속도 비례 빈도'를 별도 계산 없이 만족)
///
/// VR 멀미 규칙(명세 2번):
///   - 사물은 경로 '옆(측면)'에 둠 → 주행 방해 X, 중앙 시야 안 가림.
///   - 주변시야로 너무 가깝게/빽빽이 스쳐가면 벡션 멀미 → lateralRange 키우거나 perRow 줄여 튜닝.
///
/// 붙이는 법(에디터):
///   1) 빈 GameObject "SpeedScenery" 에 추가.
///   2) boat = PlayerBoat Transform, boatMover = 그 BoatMover.
///   3) sceneryPrefabs[] 에 부표/바위 등 프리팹 등록(여러 개면 랜덤).
///   4) yOffset 으로 수면 높이 맞춤(보트 데크가 수면보다 높으면 음수).
/// </summary>
public class SpeedScenery : MonoBehaviour
{
    [Header("기준")]
    [Tooltip("기준 플레이어 보트(진행 위치/방향). 보통 PlayerBoat.")]
    [SerializeField] private Transform boat;
    [Tooltip("보트의 BoatMover(이동 거리 기준).")]
    [SerializeField] private BoatMover boatMover;

    [Header("스폰 대상")]
    [Tooltip("랜덤으로 고를 사물 프리팹들(부표/바위/...).")]
    [SerializeField] private GameObject[] sceneryPrefabs;

    [Header("배치")]
    [Tooltip("진행 방향으로 몇 m 마다 한 줄 스폰.")]
    [SerializeField] private float spacingMeters = 15f;
    [Tooltip("한 줄에 스폰할 최대 개수(좌우 흩뿌림).")]
    [SerializeField] private int perRow = 2;
    [Tooltip("한 칸을 채울 확률(1=항상, 0.7=가끔 비움). 규칙적 격자 느낌을 깸.")]
    [Range(0f, 1f)][SerializeField] private float spawnChance = 0.75f;
    [Tooltip("개체별 랜덤 크기 배율(최소~최대). 같은 프리팹도 달라 보이게 — 종류 적어도 OK.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.6f, 1.7f);
    [Tooltip("보트보다 몇 m 앞에 스폰(갑툭튀 방지).")]
    [SerializeField] private float spawnAheadDistance = 120f;
    [Tooltip("보트 뒤로 몇 m 지나가면 제거.")]
    [SerializeField] private float despawnBehindDistance = 40f;
    [Tooltip("경로 좌우 측면 오프셋 범위(최소~최대). 경로 위에 안 깔리게.")]
    [SerializeField] private Vector2 lateralRange = new Vector2(12f, 45f);
    [Tooltip("(권장) 물 플레인 Transform 을 연결하면 그 Y 를 자동으로 수면 높이로 쓴다. 비우면 아래 숫자 사용.")]
    [SerializeField] private Transform waterSurface;
    [Tooltip("waterSurface 가 비었을 때 쓸 수면 절대 Y. 물 플레인 Y 와 맞출 것.")]
    [SerializeField] private float waterSurfaceY = 0f;
    [Tooltip("수면에서의 미세 조정(프리팹 피봇 보정용).")]
    [SerializeField] private float yOffset = 0f;

    [Header("프리팹 정리")]
    [Tooltip("스폰 시 Rigidbody=kinematic + Animator 끔 → '혼자 움직임' 방지. (커스텀 이동 스크립트가 있으면 프리팹에서 직접 제거)")]
    [SerializeField] private bool makeStatic = true;

    [Header("파도 흔들림 (물리 아님 — 가벼운 sine)")]
    [Tooltip("스폰된 사물을 파도 탄 듯 가볍게 흔든다. BoatBob 을 동적으로 붙이고 위상은 랜덤.")]
    [SerializeField] private bool bobOnWaves = true;
    [Tooltip("위아래 진폭(m).")]
    [SerializeField] private float bobAmplitude = 0.12f;
    [Tooltip("흔들림 빠르기(Hz 느낌).")]
    [SerializeField] private float bobFrequency = 0.5f;
    [Tooltip("미세 기울기(도).")]
    [SerializeField] private float bobTilt = 4f;

    private float nextSpawnDistance;
    private readonly List<Transform> active = new();

    private float Traveled => boatMover != null ? boatMover.DistanceTraveled : 0f;

    // 수면 높이: 물 플레인이 연결돼 있으면 그 Y, 아니면 숫자값.
    private float WaterY => waterSurface != null ? waterSurface.position.y : waterSurfaceY;

    private void Start()
    {
        if (boat == null && boatMover != null) boat = boatMover.transform;
        if (boat == null || sceneryPrefabs == null || sceneryPrefabs.Length == 0) return;

        // 시작 시 보이는 구간을 미리 채움(t=0 에 텅 빈 바다 방지).
        for (float d = -despawnBehindDistance; d <= spawnAheadDistance; d += Mathf.Max(1f, spacingMeters))
            SpawnRow(d);

        nextSpawnDistance = Traveled + Mathf.Max(1f, spacingMeters);
    }

    private void Update()
    {
        if (boat == null || sceneryPrefabs == null || sceneryPrefabs.Length == 0) return;

        // 일정 거리 전진할 때마다 앞쪽에 한 줄 추가.
        while (Traveled + 0.0001f >= nextSpawnDistance)
        {
            SpawnRow(spawnAheadDistance);
            nextSpawnDistance += Mathf.Max(1f, spacingMeters);
        }

        // 뒤로 충분히 지나간 것 제거.
        Vector3 fwd = boat.forward;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var t = active[i];
            if (t == null) { active.RemoveAt(i); continue; }
            float along = Vector3.Dot(t.position - boat.position, fwd);
            if (along < -despawnBehindDistance)
            {
                Destroy(t.gameObject);
                active.RemoveAt(i);
            }
        }
    }

    // 보트 앞 aheadDist 위치에 한 줄(perRow 개) 스폰. 월드 고정으로 둬서 보트가 지나간다.
    private void SpawnRow(float aheadDist)
    {
        Vector3 fwd = boat.forward;
        Vector3 right = boat.right;
        Vector3 ahead = boat.position + fwd * aheadDist;

        for (int i = 0; i < perRow; i++)
        {
            if (Random.value > spawnChance) continue; // 불규칙하게 비움

            var prefab = sceneryPrefabs[Random.Range(0, sceneryPrefabs.Length)];
            if (prefab == null) continue;

            float side = (Random.value < 0.5f) ? -1f : 1f;
            float lateral = side * Random.Range(lateralRange.x, lateralRange.y);
            float jitter = Random.Range(-spacingMeters * 0.4f, spacingMeters * 0.4f);

            Vector3 pos = ahead + right * lateral + fwd * jitter;
            pos.y = WaterY + yOffset; // 보트 높이와 무관하게 수면에 고정
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.transform.localScale *= Random.Range(scaleRange.x, scaleRange.y); // 랜덤 크기로 변화감
            if (makeStatic) MakeStatic(go);
            if (bobOnWaves) go.AddComponent<BoatBob>().Configure(bobAmplitude, bobFrequency, bobTilt, true);
            active.Add(go.transform);
        }
    }

    // 스폰된 사물이 '혼자 움직이지' 않게: 물리(Rigidbody)를 kinematic 으로, Animator 를 끔.
    // ※ Transform 을 직접 움직이는 커스텀 스크립트는 못 잡음 → 그건 프리팹에서 제거할 것.
    private static void MakeStatic(GameObject go)
    {
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (var anim in go.GetComponentsInChildren<Animator>())
            anim.enabled = false;
    }
}
