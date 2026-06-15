using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct NoteData
{
    public float spawnTime; // [변경됨] 판정선 시간이 아닌, 실제로 '스폰되어야 할 시간'이 저장될 칸
    public int laneIndex;
}

public class NoteSpawner : MonoBehaviour
{
    [Header("노트 프리팹과 생성 위치")]
    public GameObject notePrefab;
    public Transform[] spawnPoints;     // 스폰 포인트 (시작 지점)

    // ----------- [새로 추가된 변수들] -----------
    [Header("판정 기준선 및 속도")]
    public Transform targetPoint;       // 노트를 맞춰야 하는 판정선 위치 (목적지)
    public float noteSpeed = 5.0f;      // 노트가 날아오는 속도
    // --------------------------------------------

    [Header("CSV 파일 이름")]
    public string csvFileName = "TestPattern";

    private List<NoteData> songPattern = new List<NoteData>();
    private int currentNoteIndex = 0;

    void Start()
    {
        LoadPatternFromCSV();
    }

    void Update()
    {
        if (MusicPlayer.Instance == null || currentNoteIndex >= songPattern.Count)
            return;

        float currentTime = MusicPlayer.Instance.GetSongTime();

        if (currentTime >= songPattern[currentNoteIndex].spawnTime)
        {
            SpawnNote(songPattern[currentNoteIndex].laneIndex);
            currentNoteIndex++;
        }
    }

    void LoadPatternFromCSV()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(csvFileName);

        if (csvFile == null)
        {
            Debug.LogError($"CSV 파일을 찾을 수 없습니다: Resources/{csvFileName}");
            return;
        }

        // [중요] 스폰포인트와 판정선 사이의 거리를 계산합니다.
        // 거리 = 스폰포인트(0번레인 기준) Y축/Z축 위치 - 판정선 위치
        float distance = Vector3.Distance(spawnPoints[0].position, targetPoint.position);

        // 노트가 시작점에서 판정선까지 도달하는 데 걸리는 시간 (시간 = 거리 / 속도)
        float dropTime = distance / noteSpeed;

        string[] lines = csvFile.text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] row = lines[i].Split(',');

            if (row.Length >= 2)
            {
                NoteData data = new NoteData();

                float targetTime = 0; // CSV에 적힌 '판정선에 도달해야 하는 진짜 시간'
                float.TryParse(row[0], out targetTime);
                int.TryParse(row[1], out data.laneIndex);

                // 🔥 [핵심 공식] 실제 생성 시간 = 판정선 도달 시간 - 날아오는 시간
                // 예: 1.5초에 터져야 하는데 날아오는데 1초 걸린다면 -> 0.5초에 미리 스폰!
                data.spawnTime = targetTime - dropTime;

                // 혹시 계산한 스폰 시간이 0초보다 작으면 게임 시작하자마자 나와야 하므로 예외 처리
                if (data.spawnTime < 0) data.spawnTime = 0;

                songPattern.Add(data);
            }
        }

        Debug.Log($"🎯 싱크 계산 완료! 총 {songPattern.Count}개의 노트를 배치했습니다. (노트 이동 시간: {dropTime:F2}초)");
    }

    void SpawnNote(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= spawnPoints.Length) return;

        GameObject note = Instantiate(notePrefab, spawnPoints[laneIndex].position, spawnPoints[laneIndex].rotation);

        // [참고] 혹시 기존 노트 이동 스크립트에 속도를 제어하는 부분이 있다면 
        // 여기서 속도를 맞추어 주면 인스펙터 속도 조절이 실시간으로 동기화됩니다.
    }
}