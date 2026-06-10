using UnityEngine;

/// <summary>
/// 프로토타입 노트 스포너. 일정 간격으로 스폰 범위 안 랜덤 위치에 3종류 노트를 스폰한다.
/// (나중에 비트맵/리듬엔진으로 스폰 타이밍 교체 가능 — 지금은 일정 간격 플레이스홀더.)
///
/// ★배치★: 이 오브젝트를 플레이어 리그의 'Camera Offset' 자식으로, 손 닿는 높이(앞쪽)에.
///   노트는 이 오브젝트의 자식으로 스폰돼 로컬 -Z 로 접근 → 보트 속도와 무관하게 반응시간 일정.
///
/// 붙이는 법:
///   1) 빈 GameObject "NoteSpawner" 를 Camera Offset 자식으로. Pos 예: (0, 1.2, 0), Rotation (0,0,0).
///   2) speedController = PlayerBoat 의 SpeedController.
///   3) hands = 좌/우 Striker(컨트롤러에 붙은 것) 2개 드래그.  ← Transform 아님, Striker!
///   4) notePrefab = ProtoNote 붙은 작은 프리팹(Renderer 있어야 색 구분됨).
///   5) Play → 범위 안 랜덤 위치에 흰(터치)/초록(그랩)/빨강(트리거) 노트가 다가옴.
/// </summary>
public class ProtoNoteSpawner : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SpeedController speedController;
    [Tooltip("좌/우 손 Striker. 거리 + 버튼 상태로 판정.")]
    [SerializeField] private Striker[] hands;
    [Tooltip("(폴백) 타입별 프리팹 없을 때 쓰는 단일 노트 + 색 틴트.")]
    [SerializeField] private ProtoNote notePrefab;
    [Tooltip("Touch(파랑) 모델 — nana_B")]
    [SerializeField] private ProtoNote touchNotePrefab;
    [Tooltip("Grab(초록) 모델 — nana_G")]
    [SerializeField] private ProtoNote grabNotePrefab;
    [Tooltip("Trigger(빨강) 모델 — nana_R")]
    [SerializeField] private ProtoNote triggerNotePrefab;
    [Tooltip("아이템 노트(흰 새) 프리팹 — Bird. 비우면 아이템 노트 안 나옴.")]
    [SerializeField] private ProtoNote itemNotePrefab;
    [Tooltip("아이템 효과 발동 시스템. 비우면 아이템 노트 안 나옴.")]
    [SerializeField] private ProtoItemSystem itemSystem;
    [Tooltip("연결하면 레이스 종료(완주) 시 노트 스폰을 멈춘다.")]
    [SerializeField] private RaceManager raceManager;
    [Tooltip("타격 피드백(이펙트/사운드/진동). 비우면 피드백 없음.")]
    [SerializeField] private NoteFeedback noteFeedback;

    [Header("스폰 타이밍/범위")]
    [Tooltip("스폰 간격(초). autoSpawn 켜진 경우.")]
    [SerializeField] private float spawnInterval = 1.2f;
    [Tooltip("켜면 이 간격으로 자동 스폰. ProtoBeatmapSpawner 가 음악 박자로 구동하면 자동으로 꺼짐.")]
    [SerializeField] private bool autoSpawn = true;
    [Tooltip("스폰 거리(로컬 +Z, m).")]
    [SerializeField] private float spawnDistance = 4f;
    [Tooltip("좌우 랜덤 범위 ±X(m).")]
    [SerializeField] private float spawnRangeX = 0.5f;
    [Tooltip("상하 랜덤 범위 ±Y(m).")]
    [SerializeField] private float spawnRangeY = 0.3f;
    [Tooltip("스폰 중심 높이(로컬 Y).")]
    [SerializeField] private float noteHeight = 0f;
    [Tooltip("노트 크기 배율(프리팹 스케일 × 이 값). 날치 작게 하려면 0.5.")]
    [SerializeField] private float noteScale = 1f;

    [Header("종류별 색")]
    [SerializeField] private Color touchColor = Color.white;
    [SerializeField] private Color grabColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color triggerColor = new Color(1f, 0.3f, 0.2f);
    [Tooltip("스폰이 아이템 노트(갈매기)일 확률.")]
    [Range(0f, 1f)][SerializeField] private float itemNoteChance = 0.12f;
    [SerializeField] private Color itemColor = Color.white;

    [Header("판정")]
    [SerializeField] private float approachSpeed = 2f;
    [SerializeField] private float hitRadius = 0.15f;
    [Tooltip("노트가 이 로컬 Z 에서 사라짐(미스). 손 구역을 지나간 직후가 적당. 크면 손 전에 사라짐(안 닿음), 작으면 얼굴까지.")]
    [SerializeField] private float missLocalZ = 0.1f;

    private float timer;

    /// 간격 자동 스폰 on/off. ProtoBeatmapSpawner 가 음악 구동 시 false 로 끈다.
    public bool AutoSpawn { get => autoSpawn; set => autoSpawn = value; }

    /// 노트가 스폰→히트존까지 걸리는 시간(s). 비트맵 스포너가 선행시간(lead)으로 사용.
    public float TravelTime => approachSpeed > 0f ? spawnDistance / approachSpeed : 0f;

    private void Update()
    {
        if (!autoSpawn || notePrefab == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnNote();
            timer = Mathf.Max(0.1f, spawnInterval);
        }
    }

    /// 범위 안 랜덤 위치에 랜덤 종류 노트 1개 스폰. 외부(비트맵 스포너)도 호출.
    public void SpawnNote()
    {
        if (raceManager != null && raceManager.RaceEnded) return; // 완주/종료 후 스폰 중단

        // 범위 안 랜덤 위치.
        float x = Random.Range(-spawnRangeX, spawnRangeX);
        float y = noteHeight + Random.Range(-spawnRangeY, spawnRangeY);
        var pos = new Vector3(x, y, spawnDistance);

        // 슬로우모 아이템 반영(새 노트 접근속도 배율).
        float speed = approachSpeed * (itemSystem != null ? itemSystem.NoteSpeedMultiplier : 1f);

        // 일정 확률로 아이템 노트(흰 새, 양손 터치). itemSystem 없으면 효과만 생략(노트는 나옴).
        if (itemNotePrefab != null && Random.value < itemNoteChance)
        {
            var itemType = (ItemType)Random.Range(0, 3);
            var item = Instantiate(itemNotePrefab, transform, false); // 프리팹 로컬 회전/스케일 유지
            item.transform.localPosition = pos;
            item.transform.localScale *= noteScale;
            item.Init(speedController, hands, ProtoNoteType.Item, speed, hitRadius, missLocalZ,
                      itemColor, itemSystem, itemType, applyTint: false, feedback: noteFeedback); // 모델 색 유지
            return;
        }

        // 일반 3종 — 타입별 모델 프리팹 우선, 없으면 단일 폴백 + 색 틴트.
        var type = (ProtoNoteType)Random.Range(0, 3);
        ProtoNote prefab = type switch
        {
            ProtoNoteType.Grab    => grabNotePrefab,
            ProtoNoteType.Trigger => triggerNotePrefab,
            _                     => touchNotePrefab,
        };
        bool hasModel = prefab != null;
        if (!hasModel) prefab = notePrefab;
        if (prefab == null) return;

        Color color = type switch
        {
            ProtoNoteType.Grab    => grabColor,
            ProtoNoteType.Trigger => triggerColor,
            _                     => touchColor,
        };

        var note = Instantiate(prefab, transform, false); // 프리팹 로컬 회전/스케일 유지
        note.transform.localPosition = pos;
        note.transform.localScale *= noteScale;
        note.Init(speedController, hands, type, speed, hitRadius, missLocalZ, color, applyTint: !hasModel, feedback: noteFeedback);
    }
}
