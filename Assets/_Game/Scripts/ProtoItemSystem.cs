using UnityEngine;

/// 아이템 종류 — 전부 자기강화(self-buff).
public enum ItemType
{
    Boost,   // 잠깐 속도 ↑
    Shield,  // 잠깐 미스 무시(콤보 보호)
    SlowMo,  // 잠깐 노트 접근 느려짐(치기 쉬움)
}

/// <summary>
/// 아이템 노트 성공 시 효과를 발동(자동). 명세 5.6 self-buff 프로토 버전.
///   Boost  → SpeedController.AddBoost
///   Shield → SpeedController.ActivateShield (미스 무시)
///   SlowMo → NoteSpeedMultiplier 를 낮춰 새 노트가 천천히 옴(ProtoNoteSpawner 가 읽음)
///
/// 프로토/임시 — 정식 ItemSystem(팀메이트)으로 교체 가능.
/// 붙이는 법: 아무 GameObject 에 추가 → speedController 연결. ProtoNoteSpawner 의 itemSystem 에 연결.
/// </summary>
public class ProtoItemSystem : MonoBehaviour
{
    [SerializeField] private SpeedController speedController;

    [Header("부스트")]
    [SerializeField] private float boostSpeed = 5f;
    [SerializeField] private float boostDuration = 4f;

    [Header("실드")]
    [SerializeField] private float shieldDuration = 4f;

    [Header("슬로우모")]
    [SerializeField] private float slowMoDuration = 4f;
    [Range(0.1f, 1f)][SerializeField] private float slowMoMultiplier = 0.5f;

    /// 새로 스폰되는 노트의 접근속도 배율(슬로우모 중 < 1). ProtoNoteSpawner 가 읽는다.
    public float NoteSpeedMultiplier { get; private set; } = 1f;

    private float slowMoTimer;

    public void Activate(ItemType type)
    {
        switch (type)
        {
            case ItemType.Boost:
                speedController?.AddBoost(boostSpeed, boostDuration);
                break;
            case ItemType.Shield:
                speedController?.ActivateShield(shieldDuration);
                break;
            case ItemType.SlowMo:
                NoteSpeedMultiplier = slowMoMultiplier;
                slowMoTimer = slowMoDuration;
                break;
        }
        Debug.Log($"[Item] {type} 발동");
    }

    private void Update()
    {
        if (slowMoTimer > 0f)
        {
            slowMoTimer -= Time.deltaTime;
            if (slowMoTimer <= 0f) NoteSpeedMultiplier = 1f;
        }
    }
}
