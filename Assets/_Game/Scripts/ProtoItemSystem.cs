using UnityEngine;

/// 아이템 종류 — 자기강화 3 + 견제 2.
public enum ItemType
{
    Boost,        // 속도 급상승 (자기강화)
    RainbowStar,  // 무적: 노트 자동 명중 + 폭탄 무시 + 견제 면역 (자기강화)
    Magnet,       // 파란(터치) 노트만 스폰 = 쉬운 콤보 (자기강화)
    Bomb,         // 상대 레인에 폭탄 함정 (공격)
    Spaceship,    // 현재 1등 감속 (공격)
}

/// <summary>
/// 아이템 발동 허브. 획득(ActivateRandom) 시 등수 가중으로 1종 선택 → Activate.
///   Boost       → SpeedController.AddBoost
///   RainbowStar → SpeedController.SetInvincible (무적)
///   Magnet      → ProtoNoteSpawner.ActivateMagnet (파란 노트만)
///   Bomb        → ItemNetworkRelay.SendBomb (상대 레인 함정)
///   Spaceship   → ItemNetworkRelay.SendSpaceship (1등 감속)
/// 발동 시 OnItemActivated 이벤트 → ItemHud 가 표시.
/// 붙이는 법: 아무 GameObject → speedController/noteSpawner/itemRelay/raceManager 연결.
/// </summary>
public class ProtoItemSystem : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SpeedController speedController;
    [SerializeField] private ProtoNoteSpawner noteSpawner;
    [SerializeField] private ItemNetworkRelay itemRelay;
    [Tooltip("등수 가중 분배용. 비우면 균등 랜덤.")]
    [SerializeField] private RaceManager raceManager;

    [Header("부스트")]
    [SerializeField] private float boostSpeed = 5f;
    [SerializeField] private float boostDuration = 4f;

    [Header("무지개별(무적)")]
    [SerializeField] private float rainbowDuration = 4f;

    [Header("자석")]
    [SerializeField] private float magnetDuration = 4f;

    /// 발동 알림 (type, 지속시간) — ItemHud 구독.
    public event System.Action<ItemType, float> OnItemActivated;

    /// 아이템 노트 명중 시 호출 — 등수 가중으로 뽑아 즉시 발동.
    public void ActivateRandom()
    {
        int place = 1, count = 1;
        if (raceManager != null)
        {
            count = Mathf.Max(1, raceManager.RacerCount());
            foreach (var s in raceManager.BuildStandings())
                if (s.isPlayer) { place = s.place; break; }
        }
        Activate(ItemDistributor.Pick(place, count));
    }

    public void Activate(ItemType type)
    {
        float duration = 0f;
        switch (type)
        {
            case ItemType.Boost:
                speedController?.AddBoost(boostSpeed, boostDuration);
                duration = boostDuration;
                break;
            case ItemType.RainbowStar:
                speedController?.SetInvincible(rainbowDuration);
                duration = rainbowDuration;
                break;
            case ItemType.Magnet:
                if (noteSpawner != null) noteSpawner.ActivateMagnet(magnetDuration);
                duration = magnetDuration;
                break;
            case ItemType.Bomb:
                itemRelay?.SendBomb();
                break;
            case ItemType.Spaceship:
                itemRelay?.SendSpaceship();
                break;
        }
        OnItemActivated?.Invoke(type, duration);
        Debug.Log($"[Item] {type} 발동 (place 기반 획득)");
    }
}
