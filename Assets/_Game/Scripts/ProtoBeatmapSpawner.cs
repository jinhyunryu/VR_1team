using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CSV 채보 기반 노트 스포너 (2026-06-16 — BPM·확률 임시판에서 교체).
///   CSV 한 줄 = "판정도달시간(초),레인(0/1/2)". 레인 = 노트 X 위치(좌/중/우).
///   노트 종류/색(Touch/Grab/Trigger)은 시드 랜덤 — 레인 무관 (플레이어가 색 보고 손 맵핑 판단).
///   스폰 자체는 ProtoNoteSpawner.SpawnNote(rng, lane) 재사용 (보트/아이템/멀티 통합 유지).
///
/// 곡 선택: SongSelection.SelectedIndex 로 songs[] 중 하나 선택 → 그 곡(clip) 재생 + 그 CSV 로드.
/// 정밀 싱크: AudioSettings.dspTime + PlayScheduled. 노트는 TravelTime 만큼 선행 스폰(pre-roll).
/// 멀티: RestartSong(delay, seed) 로 전 기기 동시 출발 + 같은 seed → 같은 노트 색 (CSV 가 위치 고정).
///
/// 붙이는 법:
///   1) NoteSpawner(ProtoNoteSpawner) 와 같은 GameObject 에 추가(또는 noteSpawner 드래그).
///   2) music = AudioSource (Play On Awake 끄기).
///   3) songs[] 에 곡별 { 표시이름, clip(mp3), csvFileName(Resources 내, 확장자 제외) } 채움.
///      예: [0] Tavern Tide Turn / TestPattern, [1] Barnacle Beat / BarnacleBeat_smver
///   ※ ProtoNoteSpawner.AutoSpawn 자동 off. 씬에 AudioListener 필요.
/// </summary>
public class ProtoBeatmapSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SongEntry
    {
        [Tooltip("인스펙터 식별용 라벨(표시 아님 — 표시 이름은 타이틀 플레이리스트 UI 가 가짐).")]
        public string label;
        public AudioClip clip;
        [Tooltip("Resources 폴더 안 CSV 파일명 (확장자 제외). 예: TestPattern")]
        public string csvFileName;
        [Tooltip("이 곡의 결승 거리(m). 곡 길이/난이도에 맞춰 다르게. 0 이면 RaceManager 인스펙터 기본값 유지.")]
        public float finishDistance = 1800f;
    }

    [Header("참조")]
    [Tooltip("비우면 같은 GameObject 의 ProtoNoteSpawner 자동 사용.")]
    [SerializeField] private ProtoNoteSpawner noteSpawner;
    [SerializeField] private AudioSource music;
    [Tooltip("곡별 결승 거리 적용 대상. 비우면 씬에서 자동 탐색.")]
    [SerializeField] private RaceManager raceManager;

    [Tooltip("곡별 clip+CSV. 순서 = 타이틀 플레이리스트 버튼 순서. SongSelection.SelectedIndex 로 선택.")]
    [SerializeField] private SongEntry[] songs;

    [Header("재생")]
    [Tooltip("음악 예약 추가 선행(초). pre-roll = TravelTime + 이 값.")]
    [SerializeField] private double playLead = 0.2;
    [Tooltip("시작 직후 너무 지난(백로그) 노트 생략 임계(초). 0이면 전부 스폰.")]
    [SerializeField] private double backlogSkipSeconds = 0;

    private struct ChartNote { public double hitTime; public int lane; }
    private readonly List<ChartNote> chart = new();
    private int nextIndex;

    private double songStartDsp;   // 이 시각에 SongTime=0 (음악 실제 시작)
    private bool running;
    private bool seeded;           // 멀티 동기 모드
    private int raceSeed;

    private double SongTime => AudioSettings.dspTime - songStartDsp;

    private void Start()
    {
        if (noteSpawner == null) noteSpawner = GetComponent<ProtoNoteSpawner>();
        if (noteSpawner != null) noteSpawner.AutoSpawn = false; // 간격 스폰 끄기(중복 방지)
        if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();

        LoadSelectedSong();
        BeginPlay(0);
    }

    // SongSelection 인덱스로 곡/채보 선택.
    private void LoadSelectedSong()
    {
        if (songs == null || songs.Length == 0)
        {
            Debug.LogWarning("[ProtoBeatmapSpawner] songs 비어 있음 — 무음/무노트.");
            return;
        }
        int idx = Mathf.Clamp(SongSelection.SelectedIndex, 0, songs.Length - 1);
        var song = songs[idx];
        if (music != null) music.clip = song.clip;
        LoadChart(song.csvFileName);
        ApplySongFinishDistance(song.finishDistance);
    }

    // 곡별 결승 거리 적용 — RaceManager(결승선 + 멀티 AI 가 스폰 시 참조) + 싱글 씬 고스트 페이스 램프.
    //   distance <= 0 이면 (필드 미설정) RaceManager 인스펙터 기본값을 그대로 둔다(하위 호환).
    private void ApplySongFinishDistance(float distance)
    {
        if (distance <= 0f) return;
        if (raceManager != null) raceManager.SetFinishDistance(distance);
        foreach (var g in FindObjectsByType<GhostRacer>(FindObjectsSortMode.None))
            if (g != null) g.SetFinishDistance(distance);
    }

    // CSV ("시간,레인") → 정렬된 채보.
    private void LoadChart(string csvFileName)
    {
        chart.Clear();
        nextIndex = 0;
        if (string.IsNullOrEmpty(csvFileName)) return;

        var csv = Resources.Load<TextAsset>(csvFileName);
        if (csv == null)
        {
            Debug.LogWarning($"[ProtoBeatmapSpawner] CSV 못 찾음: Resources/{csvFileName}");
            return;
        }

        foreach (var raw in csv.text.Split('\n'))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 2) continue;
            if (double.TryParse(parts[0].Trim(), out double t) &&
                int.TryParse(parts[1].Trim(), out int lane))
                chart.Add(new ChartNote { hitTime = t, lane = lane });
        }
        chart.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
    }

    // 음악 예약 + 채보 시작. pre-roll(TravelTime) 만큼 먼저 시작해 첫 노트도 제 타이밍에 도달.
    private void BeginPlay(double extraDelay)
    {
        nextIndex = 0;
        double preRoll = (noteSpawner != null ? noteSpawner.TravelTime : 0) + playLead;
        double startDelay = System.Math.Max(0.2, extraDelay) + preRoll;

        songStartDsp = AudioSettings.dspTime + startDelay;
        if (music != null && music.clip != null)
            music.PlayScheduled(songStartDsp);
        running = true;
    }

    /// 멀티: 음악/채보 정지(로비 대기 진입). RestartSong 으로 재개.
    public void StopSong()
    {
        running = false;
        if (music != null) music.Stop();
    }

    /// 멀티: 시드 고정 재시작 — 전 기기 동일 노트 색(위치는 CSV 로 이미 고정).
    public void RestartSong(double delaySeconds, int seed)
    {
        seeded = true;
        raceSeed = seed;
        RestartSong(delaySeconds);
    }

    /// 멀티/단일: delaySeconds 후 곡을 처음부터 다시 시작. 선택 곡 다시 로드.
    public void RestartSong(double delaySeconds)
    {
        if (music != null) music.Stop();
        LoadSelectedSong();
        BeginPlay(delaySeconds);
    }

    private void Update()
    {
        if (!running || noteSpawner == null || nextIndex >= chart.Count) return;

        double lead = noteSpawner.TravelTime;
        double now = SongTime;

        int guard = 0;
        while (nextIndex < chart.Count && guard++ < 32)
        {
            var note = chart[nextIndex];
            double spawnTime = note.hitTime - lead;
            if (spawnTime > now) break;

            bool tooOld = backlogSkipSeconds > 0 && (now - spawnTime) > backlogSkipSeconds;
            if (!tooOld)
            {
                if (seeded)
                {
                    // 노트 인덱스로 시드 파생 → 전 기기 같은 노트 = 같은 종류/색.
                    var rng = new System.Random(unchecked(raceSeed * 486187739 + nextIndex));
                    noteSpawner.SpawnNote(rng, note.lane);
                }
                else
                {
                    noteSpawner.SpawnNote(null, note.lane);
                }
            }
            nextIndex++;
        }
    }
}
