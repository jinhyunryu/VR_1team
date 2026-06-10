using UnityEngine;

/// <summary>
/// RaceManager 의 finishDistance 에 맞춰 결승선(게이트/배너/배경)을 자동 배치한다.
///   위치 = 플레이어 시작 위치 + 진행방향 × finishDistance.
///   이 오브젝트의 '자식'으로 게이트·배너·배경 모델을 넣으면 그 위치로 함께 이동·정렬된다.
///
/// ★에디트 모드 미리보기★: Play 안 해도 보트/ finishDistance 바꾸면 결승선이 실시간으로 따라감.
/// ★Play★: 레이스 시작 위치를 고정해, 보트가 전진해도 결승선은 월드에 박혀 있음.
///
/// 붙이는 법:
///   1) 씬 '루트'에 빈 GameObject "FinishLine" (★PlayerBoat 자식 X — 월드 고정이어야 함).
///   2) 자식으로 게이트/배너/배경 모델 배치.
///   3) raceManager / playerBoat(PlayerBoat 루트) 연결.
///   → finishDistance 위치에 자동 정렬. RaceManager 의 실제 통과 판정과 위치가 일치.
/// </summary>
[ExecuteAlways]
public class FinishLine : MonoBehaviour
{
    [SerializeField] private RaceManager raceManager;
    [Tooltip("플레이어 보트 루트(시작 위치/진행방향 기준).")]
    [SerializeField] private Transform playerBoat;
    [Tooltip("게이트가 진행방향을 바라보게 자동 정렬.")]
    [SerializeField] private bool alignToTravel = true;

    private Vector3 startPos;
    private Vector3 startForward = Vector3.forward;

    private void OnEnable()
    {
        // Play 시작 시점의 보트 위치를 결승선 기준으로 고정.
        if (Application.isPlaying && playerBoat != null)
        {
            startPos = playerBoat.position;
            startForward = playerBoat.forward;
        }
    }

    private void Update()
    {
        if (raceManager == null || playerBoat == null) return;

        // 에디트 모드: 보트 현재(=시작) 위치를 실시간 추적해 미리보기.
        if (!Application.isPlaying)
        {
            startPos = playerBoat.position;
            startForward = playerBoat.forward;
        }

        transform.position = startPos + startForward * raceManager.FinishDistance;
        if (alignToTravel && startForward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(startForward, Vector3.up);
    }
}
