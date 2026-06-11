using UnityEngine;

/// <summary>
/// 음악(AudioSource)에 싱크 맞춰 BPM 박자에 노트를 스폰한다.
/// 스폰 자체는 ProtoNoteSpawner.SpawnNote() 재사용(랜덤 위치/3종류).
///
/// 정밀 싱크: AudioSettings.dspTime + PlayScheduled 사용(명세의 dspTime 클럭 방식).
/// 선행 스폰: 노트가 박자에 "도달"하도록, (박자시각 − TravelTime) 에 미리 스폰.
///
/// ★프로토/임시★ — 정식 RhythmEngine + 비트맵(팀메이트)으로 교체될 것.
///   지금은 BPM·확률로 노트를 깔아 난이도/밀도/음악 핏을 테스트하는 용도.
///
/// 붙이는 법:
///   1) NoteSpawner(ProtoNoteSpawner 붙은 것)에 이 컴포넌트도 추가(또는 noteSpawner 에 드래그).
///   2) music = AudioSource (clip = Stormwake Run, Play On Awake 끄기).
///   3) bpm 128, firstBeatOffset 1.0 (이 곡 기준).
///   4) subdivisions/noteChance 로 난이도 조절.
///   ※ ProtoNoteSpawner 의 autoSpawn 은 자동으로 꺼짐(중복 방지).
///   ※ 씬에 AudioListener(카메라) 있어야 소리 남.
/// </summary>
public class ProtoBeatmapSpawner : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비우면 같은 GameObject 의 ProtoNoteSpawner 를 자동 사용.")]
    [SerializeField] private ProtoNoteSpawner noteSpawner;
    [SerializeField] private AudioSource music;

    [Header("박자")]
    [SerializeField] private float bpm = 128f;
    [Tooltip("첫 박자가 들어오는 시각(초). Stormwake Run ≈ 1.0")]
    [SerializeField] private float firstBeatOffset = 1.0f;
    [Tooltip("한 박을 몇 등분해 노트 슬롯을 만들지. 1=4분음표, 2=8분음표, 4=16분.")]
    [SerializeField] private int subdivisions = 1;
    [Tooltip("각 슬롯에 노트를 둘 확률(난이도/밀도). 1=꽉참, 0.5=듬성.")]
    [Range(0f, 1f)][SerializeField] private float noteChance = 0.6f;

    [Header("재생")]
    [Tooltip("재생 예약 선행(초).")]
    [SerializeField] private double playLead = 0.2;

    private double songStartDsp;
    private int nextSlot;
    private bool running;
    private bool seeded;   // 멀티 동기 채보 모드 (RestartSong(delay, seed) 로 진입. 싱글은 false 그대로)
    private int raceSeed;

    private double SecondsPerSlot => (60.0 / Mathf.Max(1f, bpm)) / Mathf.Max(1, subdivisions);
    private double SongTime => AudioSettings.dspTime - songStartDsp;

    private void Start()
    {
        if (noteSpawner == null) noteSpawner = GetComponent<ProtoNoteSpawner>();
        if (noteSpawner != null) noteSpawner.AutoSpawn = false; // 간격 스폰 끄기(중복 방지)

        if (music != null && music.clip != null)
        {
            songStartDsp = AudioSettings.dspTime + playLead;
            music.PlayScheduled(songStartDsp);
        }
        else
        {
            songStartDsp = AudioSettings.dspTime;
            Debug.LogWarning("[ProtoBeatmapSpawner] AudioSource/clip 없음 — 박자만 진행(무음).");
        }
        running = true;
    }

    /// 멀티: 음악/박자 진행을 멈춘다(대기실 진입). RestartSong 으로 재개.
    public void StopSong()
    {
        running = false;
        if (music != null) music.Stop();
    }

    /// 멀티: 시드 고정 재시작 — 전 기기 동일 채보. 슬롯 번호별 결정적 난수라
    /// 한 기기가 프레임 끊김으로 슬롯을 건너뛰어도 나머지 슬롯은 어긋나지 않는다.
    public void RestartSong(double delaySeconds, int seed)
    {
        seeded = true;
        raceSeed = seed;
        RestartSong(delaySeconds);
    }

    /// 멀티: delaySeconds 후 곡을 처음부터 다시 시작(전 기기 동시 출발).
    /// dspTime 기준 예약이라 호출 시점 프레임 편차에 둔감. 0.2 이상 권장(PlayScheduled 선행).
    public void RestartSong(double delaySeconds)
    {
        if (music != null) music.Stop();
        nextSlot = 0;
        songStartDsp = AudioSettings.dspTime + System.Math.Max(0.2, delaySeconds);
        if (music != null && music.clip != null) music.PlayScheduled(songStartDsp);
        running = true;
    }

    private void Update()
    {
        if (!running || noteSpawner == null) return;

        double lead = noteSpawner.TravelTime;
        double now = SongTime;

        // 도달시각 − lead 가 지난 슬롯들을 스폰(한 프레임 폭주 방지 가드 16).
        int guard = 0;
        while (guard++ < 16)
        {
            double slotHitTime = firstBeatOffset + nextSlot * SecondsPerSlot;
            double spawnTime = slotHitTime - lead;
            if (spawnTime > now) break;

            // 시작 백로그/프레임 끊김으로 한참 지난 슬롯은 스폰 생략 → 시작 시 노트 폭주 방지.
            if (now - spawnTime <= SecondsPerSlot * 1.5)
            {
                if (seeded)
                {
                    // 슬롯 번호로 시드 파생 → 전 기기에서 같은 슬롯 = 같은 노트(유무/종류/위치).
                    var slotRng = new System.Random(unchecked(raceSeed * 486187739 + nextSlot));
                    if (slotRng.NextDouble() <= noteChance) noteSpawner.SpawnNote(slotRng);
                }
                else if (Random.value <= noteChance) noteSpawner.SpawnNote();
            }
            nextSlot++;
        }
    }
}
