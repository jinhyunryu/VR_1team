using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 레이스 총괄: 플레이어 + 고스트들의 누적 거리 추적, 결승 통과/순위 산정, Results 씬 전환.
/// finishDistance 의 단일 진실 공급원. (명세 5.8)
///
/// 의존성: 리듬과 무관 — 각 레이서의 BoatMover.DistanceTraveled 만 본다.
/// 종료 조건:
///   - 기본: 플레이어가 결승선 통과 시 종료(자체완결, 테스트 가능).
///   - 확장: 곡이 끝나면(마스터 클럭) 팀메이트의 리듬 시스템이 EndRaceNow() 를 호출 → 거리순 정산.
///
/// 붙이는 법(에디터):
///   1) 빈 GameObject "RaceManager" 에 추가.
///   2) racers 리스트에 플레이어 + 고스트 3척의 BoatMover 등록(이름/ isPlayer 체크).
///   3) finishDistance = 1800 (각 GhostRacer 의 finishDistance 도 같은 값으로 맞출 것).
///   4) resultsSceneName = "Results" (Build Settings 에 추가 필요). 비우면 콘솔 정산만.
/// </summary>
public class RaceManager : MonoBehaviour
{
    [System.Serializable]
    public class RaceEntry
    {
        public string name = "Racer";
        public BoatMover mover;
        public bool isPlayer;

        [System.NonSerialized] public int finishOrder;      // 0 = 미통과, 1.. = 통과 순서
        [System.NonSerialized] public float recordedDistance; // 통과 순간의 거리(정산 고정용)
        public float Distance => mover != null ? mover.DistanceTraveled : 0f;
    }

    [Header("레이스")]
    [Tooltip("결승 거리(m). 모든 GhostRacer 의 finishDistance 와 동일하게.")]
    [SerializeField] private float finishDistance = 1800f;

    [Tooltip("플레이어 + 고스트들. isPlayer 는 1명만 체크.")]
    [SerializeField] private List<RaceEntry> racers = new();

    [Header("종료/전환")]
    [Tooltip("플레이어가 결승선 통과하면 레이스 종료.")]
    [SerializeField] private bool endWhenPlayerFinishes = true;

    [Tooltip("각 배가 결승선 통과 시 부드럽게 정지(끝없이 항해해 가는 것 방지).")]
    [SerializeField] private bool stopBoatOnFinish = true;

    [Tooltip("종료 시 이동할 씬. Build Settings 에 있어야 함. 비우면 콘솔 정산만.")]
    [SerializeField] private string resultsSceneName = "Results";

    public float FinishDistance => finishDistance;
    public bool RaceEnded { get; private set; }

    private int nextFinishOrder = 1;

    private void Update()
    {
        if (RaceEnded) return;

        // 결승 통과 감지 + 순서 기록.
        foreach (var r in racers)
        {
            if (r.finishOrder == 0 && r.Distance >= finishDistance)
            {
                r.finishOrder = nextFinishOrder++;
                r.recordedDistance = r.Distance; // 통과 순간 거리 고정 → 이후 드리프트 무시
                if (stopBoatOnFinish && r.mover != null) r.mover.Stop(); // 통과 시 부드럽게 정지
                Debug.Log($"[RaceManager] '{r.name}' 결승 통과 ({r.finishOrder}번째)");

                if (endWhenPlayerFinishes && r.isPlayer)
                {
                    EndRaceNow();
                    return;
                }
            }
        }
    }

    /// 외부(리듬 시스템의 곡 종료 등)에서 호출 가능. 현재 상태로 즉시 정산.
    public void EndRaceNow()
    {
        if (RaceEnded) return;
        RaceEnded = true;

        var standings = BuildStandings();

        // 콘솔 정산(Results UI 없어도 검증).
        Debug.Log("===== 레이스 결과 =====");
        foreach (var s in standings)
            Debug.Log($"  {s.place}등  {s.name}{(s.isPlayer ? " (나)" : "")}  " +
                      $"거리 {s.distance:F0}m  {(s.finished ? "완주" : "미완주")}");

        // 씬 간 전달용 정적 보관.
        RaceResult.Standings = standings;
        RaceResult.PlayerPlace = standings.FirstOrDefault(s => s.isPlayer)?.place ?? 0;

        // Results 씬 전환(가능할 때만).
        if (!string.IsNullOrEmpty(resultsSceneName) &&
            Application.CanStreamedLevelBeLoaded(resultsSceneName))
        {
            SceneManager.LoadScene(resultsSceneName);
        }
        else if (!string.IsNullOrEmpty(resultsSceneName))
        {
            Debug.LogWarning($"[RaceManager] '{resultsSceneName}' 씬을 못 찾음(Build Settings 확인). 콘솔 정산만 함.");
        }
    }

    /// 현재 순위(라이브). HUD 등에서 사용 가능.
    /// 정렬: 완주자(통과 순서 빠른 순) → 미완주자(거리 많은 순).
    public List<RaceStanding> BuildStandings()
    {
        var ordered = racers
            .OrderBy(r => r.finishOrder == 0 ? int.MaxValue : r.finishOrder)
            .ThenByDescending(r => EffectiveDistance(r))
            .ToList();

        var result = new List<RaceStanding>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i];
            result.Add(new RaceStanding
            {
                name = r.name,
                isPlayer = r.isPlayer,
                place = i + 1,
                distance = EffectiveDistance(r),
                finished = r.finishOrder != 0,
            });
        }
        return result;
    }

    // 완주자는 통과 순간 거리로 고정, 미완주자는 현재 거리.
    private static float EffectiveDistance(RaceEntry r)
        => r.finishOrder != 0 ? r.recordedDistance : r.Distance;
}

/// 한 레이서의 최종 순위 정보(Results 표시용 DTO).
public class RaceStanding
{
    public string name;
    public bool isPlayer;
    public int place;
    public float distance;
    public bool finished;
}

/// 씬 전환 간 결과 전달용 정적 보관소. Results 씬이 읽는다.
public static class RaceResult
{
    public static List<RaceStanding> Standings;
    public static int PlayerPlace;
}
