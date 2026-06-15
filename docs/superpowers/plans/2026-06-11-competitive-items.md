# 카트라이더식 견제 아이템 5종 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 자기강화 3종 → 견제 포함 5종(부스트/무지개별/자석/폭탄/우주선) 아이템으로 확장, 획득 즉시 자동 발동, 공격형은 RPC→로컬 효과.

**Architecture:** 기존 `ProtoItemSystem`/`SpeedController`/`ProtoNoteSpawner` 확장 + 신규 `ItemDistributor`(등수 가중)/`ItemHud`/`BombHazard`/`ItemNetworkRelay`. 공격형은 NGO RPC 로 대상에게 신호만 보내고 효과는 대상 기기가 로컬 적용(네트워크 물리 0). 싱글은 RPC 대신 직접 로컬 적용 폴백.

**Tech Stack:** Unity 6000.4.10f1, NGO(com.unity.netcode.gameobjects), 기존 _Game/Scripts 패턴(namespace 없음, SerializeField 옵셔널).

**검증 방식 (프로젝트 관례 — CLAUDE.md):** 자동 테스트 프레임워크 없음. 각 태스크 = 코드 작성 → Unity 컴파일(Console 0 에러, 사용자) → 플레이 확인(사용자) → 커밋. ⚠️ Unity 열린 채 씬/프리팹 직접 편집 금지 — 코드만, 에디터 작업은 사용자. 공격형 네트워크는 자동 빌드/테스트 루프(`DevBuildScript` + `-lanhost`/`-lanjoin`)로도 검증 가능.

**스펙:** `docs/superpowers/specs/2026-06-11-competitive-items-design.md`

**브랜치:** `multiplay` (공격형이 멀티 인프라 의존).

---

## 파일 구조

| 파일 | 책임 | 신규/수정 |
|---|---|---|
| `ProtoItemSystem.cs` | ItemType 5종 + Activate 디스패치 + ActivateRandom(획득) + 발동 이벤트 | 수정 |
| `ItemDistributor.cs` | 등수 가중 아이템 선택 (우주선 1등 제외) | 신규 |
| `SpeedController.cs` | Invincible(무적) + ApplyExternalSlow(외부 감속) | 수정 |
| `GhostRacer.cs` | ApplyExternalSlow (AI 감속 대칭) | 수정 |
| `ProtoNote.cs` | 무적 시 자동 명중 + 획득은 ActivateRandom | 수정 |
| `ProtoNoteSpawner.cs` | 자석 모드(파란 노트만) | 수정 |
| `BombHazard.cs` | 레인 폭탄 함정 (치면 감속, 피하면 통과) | 신규 |
| `ItemNetworkRelay.cs` | 폭탄/우주선 RPC + 솔로 폴백 | 신규 |
| `ItemHud.cs` | 활성 효과/발동 배너/폭탄 경고 표시 | 신규 |

---

### Task 1: ItemType 5종 확장 + ProtoItemSystem 디스패치

**Files:**
- Modify: `Assets/_Game/Scripts/ProtoItemSystem.cs`

- [ ] **Step 1.1: ItemType enum 교체** (파일 상단 enum 블록 전체)

```csharp
/// 아이템 종류 — 자기강화 3 + 견제 2.
public enum ItemType
{
    Boost,        // 속도 급상승 (자기강화)
    RainbowStar,  // 무적: 노트 자동 명중 + 폭탄 무시 + 견제 면역 (자기강화)
    Magnet,       // 파란(터치) 노트만 스폰 = 쉬운 콤보 (자기강화)
    Bomb,         // 상대 레인에 폭탄 함정 (공격)
    Spaceship,    // 현재 1등 감속 (공격)
}
```

- [ ] **Step 1.2: ProtoItemSystem 본문 교체** (클래스 전체를 아래로)

```csharp
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
```

(주의: 기존 `NoteSpeedMultiplier`/SlowMo 필드·Update 는 제거됨 — ProtoNoteSpawner 가 더 이상 NoteSpeedMultiplier 를 읽지 않도록 Task 4 에서 정리.)

- [ ] **Step 1.3: [사용자 Unity] 컴파일은 Task 2~4 후** (ItemDistributor/ItemNetworkRelay/SpeedController 신규 멤버 의존 — 묶어서 확인). 지금은 빨간 줄 정상.

---

### Task 2: ItemDistributor (등수 가중 분배)

**Files:**
- Create: `Assets/_Game/Scripts/ItemDistributor.cs`

- [ ] **Step 2.1: 파일 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등수 가중 아이템 분배 (카트라이더식). 각 플레이어가 자기 아이템 노트 명중 시 로컬로 뽑음
/// (개인 보상이라 네트워크 동기화 불필요).
///   1등: 부스트/자석 위주(약함)  중위권: 무지개별/폭탄  꼴찌권: 우주선↑
///   우주선은 1등이면 드롭 안 됨(앞에 1등 없으면 무의미).
/// </summary>
public static class ItemDistributor
{
    /// place: 내 등수(1=선두). totalRacers: 전체 레이서 수. 반환: 발동할 아이템.
    public static ItemType Pick(int place, int totalRacers)
    {
        bool isLeader = place <= 1;
        bool nearBack = totalRacers > 1 && place > (totalRacers + 1) / 2;

        var pool = new List<(ItemType type, int weight)>
        {
            (ItemType.Boost, 3),
            (ItemType.Magnet, isLeader ? 4 : 2),
        };
        if (isLeader)
        {
            pool.Add((ItemType.RainbowStar, 1)); // 선두는 견제 풀 약하게
        }
        else
        {
            pool.Add((ItemType.RainbowStar, 2));
            pool.Add((ItemType.Bomb, 3));
            pool.Add((ItemType.Spaceship, nearBack ? 4 : 2)); // 1등 제외 + 뒤일수록↑
        }
        return WeightedPick(pool);
    }

    private static ItemType WeightedPick(List<(ItemType type, int weight)> pool)
    {
        int total = 0;
        foreach (var e in pool) total += e.weight;
        int roll = Random.Range(0, total);
        foreach (var e in pool)
        {
            roll -= e.weight;
            if (roll < 0) return e.type;
        }
        return pool[0].type;
    }
}
```

- [ ] **Step 2.2: 커밋** (Task 4 컴파일 통과 후 일괄 — 아래 Task 4 Step 4.4 에서 함께)

---

### Task 3: SpeedController — 무적 + 외부 감속

**Files:**
- Modify: `Assets/_Game/Scripts/SpeedController.cs`

- [ ] **Step 3.1: 필드 추가** (`private float shieldTimer;` 아래)

```csharp
    private float invincibleTimer;   // 무지개별(무적): 자동 명중 + 폭탄/감속 면역
    private float externalSlowMult = 1f; // 우주선/폭탄 피격 감속 배율
    private float externalSlowTimer;

    /// 무적 상태 (ProtoNote 자동 명중 / BombHazard 무효 / 외부 감속 면역).
    public bool Invincible => invincibleTimer > 0f;
    /// 부스트 활성 (ItemHud 표시용).
    public bool BoostActive => boostTimer > 0f;
```

- [ ] **Step 3.2: 메서드 추가** (`public void ActivateShield(...)` 아래)

```csharp
    /// 무지개별: N초 무적. 진행 중 외부 감속 제거.
    public void SetInvincible(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, duration);
        externalSlowMult = 1f;
        externalSlowTimer = 0f;
    }

    /// 우주선/폭탄 피격: 외부 감속(배율<1) N초. 무적이면 무시.
    public void ApplyExternalSlow(float multiplier, float duration)
    {
        if (Invincible) return;
        externalSlowMult = Mathf.Clamp01(multiplier);
        externalSlowTimer = duration;
    }
```

- [ ] **Step 3.3: RegisterMiss 가드** (메서드 첫 줄에 추가)

```csharp
    public void RegisterMiss()
    {
        if (Invincible) return;           // 무적 중 미스 무시
        if (shieldTimer > 0f) return;     // 실드 중 미스 무시
        Combo = 0;
        speedLevel = Mathf.Max(baseSpeed, speedLevel - missSpeedPenalty);
    }
```

- [ ] **Step 3.4: Update 타이머/감속 적용** (`if (shieldTimer > 0f) shieldTimer -= Time.deltaTime;` 아래에 타이머, 그리고 target 계산부 교체)

```csharp
        if (shieldTimer > 0f) shieldTimer -= Time.deltaTime;
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
        if (externalSlowTimer > 0f)
        {
            externalSlowTimer -= Time.deltaTime;
            if (externalSlowTimer <= 0f) externalSlowMult = 1f;
        }

        float target = Mathf.Min(speedLevel + boostSpeed, maxSpeed + boostExtraCap);
        if (!Invincible) target *= externalSlowMult; // 무적이면 감속 면역
        CurrentTargetSpeed = target;
        boatMover.SetTargetSpeed(target);
```

(기존 `float target = ...; CurrentTargetSpeed = target; boatMover.SetTargetSpeed(target);` 3줄을 위 블록으로 대체.)

- [ ] **Step 3.5: ResetForRace 에 무적/감속 리셋 추가** (기존 ResetForRace 본문에)

```csharp
    public void ResetForRace()
    {
        Combo = 0;
        speedLevel = baseSpeed;
        boostSpeed = 0f;
        boostTimer = 0f;
        shieldTimer = 0f;
        invincibleTimer = 0f;
        externalSlowMult = 1f;
        externalSlowTimer = 0f;
    }
```

---

### Task 4: 로컬 효과 완성 (무적 자동명중 + 자석) + 컴파일 통과

**Files:**
- Modify: `Assets/_Game/Scripts/ProtoNote.cs`
- Modify: `Assets/_Game/Scripts/ProtoNoteSpawner.cs`

- [ ] **Step 4.1: ProtoNote 무적 자동 명중 + 획득 ActivateRandom**

(a) `Update()` 의 `if (resolved) return;` 바로 아래에 자동 명중 추가:

```csharp
        if (resolved) return;

        // 무지개별(무적): 일반 노트는 자동 명중 (콤보·속도 자동 상승). 아이템 노트는 제외.
        if (speed != null && speed.Invincible && type != ProtoNoteType.Item)
        {
            hitHand = null;
            Resolve(true);
            return;
        }
```

(b) `Resolve(bool hit)` 의 아이템 분기 교체 — 고정 itemType 대신 획득 시점 등수 가중:

```csharp
        if (hit)
        {
            speed?.RegisterHit();
            if (type == ProtoNoteType.Item) itemSystem?.ActivateRandom(); // 등수 가중 획득
            PlayHitFeedback();
            Debug.Log($"[ProtoNote] HIT ({type}) → 콤보 {(speed != null ? speed.Combo.ToString() : "?")}");
            if (bounceOnHit) { StartFly(); return; }
        }
```

(기존 `if (type == ProtoNoteType.Item) itemSystem?.Activate(itemType);` 한 줄을 `itemSystem?.ActivateRandom();` 으로.)

- [ ] **Step 4.2: ProtoNoteSpawner 자석 모드**

(a) 필드 추가 (`private float timer;` 아래):

```csharp
    private float magnetTimer; // 자석: 진행 중 파란(터치) 노트만 스폰
    /// 자석 활성 (ItemHud 표시용).
    public bool MagnetActive => magnetTimer > 0f;
    /// 자석 발동 — N초간 파란 노트만.
    public void ActivateMagnet(float duration) => magnetTimer = Mathf.Max(magnetTimer, duration);
```

(b) `Update()` 맨 앞에 타이머 틱 (autoSpawn 반환보다 위):

```csharp
    private void Update()
    {
        if (magnetTimer > 0f) magnetTimer -= Time.deltaTime;
        if (!autoSpawn || notePrefab == null) return;
        // (기존 timer 스폰 로직 그대로)
        timer -= Time.deltaTime;
        if (timer <= 0f) { SpawnNote(); timer = Mathf.Max(0.1f, spawnInterval); }
    }
```

(c) `SpawnNote(System.Random rng)` 에서 자석 시 아이템 노트 스킵 + 타입 고정. `if (SpawnBlocked) return;` 다음에:

```csharp
        bool magnet = magnetTimer > 0f;
```

아이템 노트 분기 조건에 `!magnet` 추가:

```csharp
        if (!magnet && itemNotePrefab != null && RandValue(rng) < itemNoteChance)
        {
```

일반 타입 선택을 자석 시 Touch 고정:

```csharp
        var type = magnet ? ProtoNoteType.Touch : (ProtoNoteType)RandInt(rng, 3);
```

(d) **SlowMo 잔재 제거**: `float speed = approachSpeed * (itemSystem != null ? itemSystem.NoteSpeedMultiplier : 1f);` → `float speed = approachSpeed;` (NoteSpeedMultiplier 는 Task 1 에서 삭제됨)

- [ ] **Step 4.3: [사용자 Unity] 컴파일 확인** — Task 1~4 + ItemDistributor 합쳐 Console 0 에러. (ItemNetworkRelay 는 아직 없음 → ProtoItemSystem 의 `itemRelay` 필드 타입 때문에 Task 8 전까지 에러날 수 있으므로, **Task 8 작성 후 최종 컴파일**. 그 전까지는 itemRelay 호출부가 null-safe 라 타입만 존재하면 됨 → Task 8 을 먼저 만들거나, 임시로 itemRelay 관련 2줄을 주석 처리 후 Task 8 에서 해제.)

> 실행 순서 권장: Task 1~7 작성 → Task 8(ItemNetworkRelay) 작성 → 그때 일괄 컴파일. 로컬 3종(부스트/무지개별/자석)은 이 시점에 솔로 플레이로 검증 가능.

- [ ] **Step 4.4: 커밋 (로컬 효과 묶음)**

```powershell
git add Assets/_Game/Scripts/ProtoItemSystem.cs Assets/_Game/Scripts/ItemDistributor.cs Assets/_Game/Scripts/SpeedController.cs Assets/_Game/Scripts/ProtoNote.cs Assets/_Game/Scripts/ProtoNoteSpawner.cs
git commit -m "feat: 아이템 5종 enum + 등수가중 분배 + 무적/외부감속 + 자석(로컬 3종)"
```

---

### Task 5: GhostRacer — 외부 감속 (AI 대칭)

**Files:**
- Modify: `Assets/_Game/Scripts/GhostRacer.cs`

- [ ] **Step 5.1: 필드 + 메서드 추가** (`private BoatMover boatMover;` 아래)

```csharp
    private float extSlowMult = 1f; // 폭탄/우주선 피격 시 AI 감속
    private float extSlowTimer;

    /// 외부 감속(배율<1) N초 — 폭탄/우주선 대상이 AI 일 때.
    public void ApplyExternalSlow(float multiplier, float duration)
    {
        extSlowMult = Mathf.Clamp01(multiplier);
        extSlowTimer = duration;
    }
```

- [ ] **Step 5.2: Update 에 감속 적용** (`boatMover.SetTargetSpeed(Mathf.Max(0f, target));` 한 줄 교체)

```csharp
        if (extSlowTimer > 0f)
        {
            extSlowTimer -= Time.deltaTime;
            if (extSlowTimer <= 0f) extSlowMult = 1f;
        }
        boatMover.SetTargetSpeed(Mathf.Max(0f, target) * extSlowMult);
```

- [ ] **Step 5.3: 커밋**

```powershell
git add Assets/_Game/Scripts/GhostRacer.cs
git commit -m "feat: GhostRacer.ApplyExternalSlow (폭탄/우주선 AI 대상 감속)"
```

---

### Task 6: BombHazard (레인 폭탄 함정)

**Files:**
- Create: `Assets/_Game/Scripts/BombHazard.cs`

- [ ] **Step 6.1: 파일 작성**

```csharp
using UnityEngine;

/// <summary>
/// 폭탄 함정. 폭탄 아이템 발동 시 "피해자" 노트 레인에 로컬 스폰(노트와 동일하게 부모 로컬 -Z 접근).
///   손이 닿으면(=치면) 큰 감속. 안 치고 지나가면 무사 통과. 무적(무지개별)이면 무효.
/// 노트가 아니라 별도 함정 — 02_RhythmScore 노트 시스템과 무관, "치면 안 되는 것" 직관.
/// </summary>
public class BombHazard : MonoBehaviour
{
    private Striker[] hands;
    private SpeedController speed;
    private float approachSpeed;
    private float hitRadius;
    private float missLocalZ;
    private float slowMult;
    private float slowDuration;
    private NoteFeedback feedback;
    private bool resolved;

    public void Init(Striker[] hands, SpeedController speed, float approachSpeed,
                     float hitRadius, float missLocalZ, float slowMult, float slowDuration,
                     NoteFeedback feedback = null)
    {
        this.hands = hands;
        this.speed = speed;
        this.approachSpeed = approachSpeed;
        this.hitRadius = hitRadius;
        this.missLocalZ = missLocalZ;
        this.slowMult = slowMult;
        this.slowDuration = slowDuration;
        this.feedback = feedback;
    }

    private void Update()
    {
        if (resolved) return;

        transform.localPosition += Vector3.back * (approachSpeed * Time.deltaTime);

        // 손 접촉 = 폭발(감속). 무적이면 무효.
        if (hands != null && !(speed != null && speed.Invincible))
        {
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                if (Vector3.Distance(transform.position, hand.WorldPosition) <= hitRadius)
                {
                    Explode();
                    return;
                }
            }
        }

        // 안 치고 지나감 = 무사 통과.
        if (transform.localPosition.z <= missLocalZ)
        {
            resolved = true;
            Destroy(gameObject);
        }
    }

    private void Explode()
    {
        resolved = true;
        speed?.ApplyExternalSlow(slowMult, slowDuration);
        if (feedback != null) feedback.PlayAt(transform.position);
        Debug.Log("[Bomb] 피격 — 감속");
        Destroy(gameObject);
    }
}
```

- [ ] **Step 6.2: 커밋**

```powershell
git add Assets/_Game/Scripts/BombHazard.cs
git commit -m "feat: BombHazard (레인 폭탄 함정 — 치면 감속, 피하면 통과)"
```

---

### Task 7: ItemHud (효과/발동/경고 표시)

**Files:**
- Create: `Assets/_Game/Scripts/ItemHud.cs`

- [ ] **Step 7.1: 파일 작성** (RuntimeHud 코드 생성 패턴)

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 아이템 체감 HUD(코드 생성 월드 UI). 활성 효과(부스트/무적/자석) 상태 + 발동 배너 + 폭탄 경고.
/// 표현 only — 상태는 SpeedController/ProtoNoteSpawner 폴링 + 발동 이벤트 구독.
/// 붙이는 법: 빈 GameObject → speedController/noteSpawner/itemSystem/attachTo(Camera Offset) 연결.
/// </summary>
public class ItemHud : MonoBehaviour
{
    [SerializeField] private SpeedController speedController;
    [SerializeField] private ProtoNoteSpawner noteSpawner;
    [SerializeField] private ProtoItemSystem itemSystem;

    [Header("배치")]
    [SerializeField] private Transform attachTo;
    [SerializeField] private float distance = 1.6f;
    [SerializeField] private float heightOffset = 0.35f;
    [SerializeField] private Vector2 panelSize = new Vector2(900, 300);
    [SerializeField] private float worldScale = 0.0012f;
    [SerializeField] private float bannerSeconds = 1.5f;
    [SerializeField] private TMP_FontAsset font;

    private GameObject root;
    private RectTransform crt;
    private TMP_Text statusText, bannerText, warnText;
    private float bannerTimer;

    private void Start()
    {
        Build();
        if (itemSystem != null) itemSystem.OnItemActivated += OnActivated;
    }

    private void OnDestroy()
    {
        if (itemSystem != null) itemSystem.OnItemActivated -= OnActivated;
    }

    private void OnActivated(ItemType type, float duration)
    {
        if (bannerText != null) bannerText.text = $"{KorName(type)} 발동!";
        bannerTimer = bannerSeconds;
    }

    private void Update()
    {
        if (crt == null) return;
        crt.localPosition = new Vector3(0f, heightOffset, distance);
        crt.localScale = Vector3.one * worldScale;

        // 활성 효과.
        var active = new List<string>();
        if (speedController != null && speedController.Invincible) active.Add("무적");
        if (speedController != null && speedController.BoostActive) active.Add("부스트");
        if (noteSpawner != null && noteSpawner.MagnetActive) active.Add("자석");
        statusText.text = active.Count > 0 ? string.Join("  ", active) : "";

        // 발동 배너.
        if (bannerTimer > 0f) bannerTimer -= Time.deltaTime;
        bannerText.gameObject.SetActive(bannerTimer > 0f);

        // 폭탄 경고 — 레인에 BombHazard 존재 시.
        bool bombIncoming = FindObjectsByType<BombHazard>(FindObjectsSortMode.None).Length > 0;
        warnText.gameObject.SetActive(bombIncoming);
        warnText.text = bombIncoming ? "⚠ 폭탄! 피해!" : "";
    }

    private static string KorName(ItemType t) => t switch
    {
        ItemType.Boost => "부스트",
        ItemType.RainbowStar => "무지개별",
        ItemType.Magnet => "자석",
        ItemType.Bomb => "폭탄",
        ItemType.Spaceship => "우주선",
        _ => t.ToString(),
    };

    private void Build()
    {
        Transform parent = attachTo != null ? attachTo
                         : (Camera.main != null ? Camera.main.transform : null);
        var go = new GameObject("ItemHud", typeof(RectTransform), typeof(Canvas));
        go.layer = 0;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        root = go;
        crt = (RectTransform)go.transform;
        crt.sizeDelta = panelSize;
        if (parent != null) crt.SetParent(parent, false);
        crt.localRotation = Quaternion.identity;

        statusText = NewText("Status", 56f, new Vector2(0.5f, 0f), new Vector2(0, 20), Color.cyan);
        bannerText = NewText("Banner", 72f, new Vector2(0.5f, 0.5f), new Vector2(0, 60), Color.yellow);
        warnText = NewText("Warn", 64f, new Vector2(0.5f, 1f), new Vector2(0, -20), new Color(1f, 0.3f, 0.2f));
        bannerText.gameObject.SetActive(false);
        warnText.gameObject.SetActive(false);
    }

    private TMP_Text NewText(string name, float size, Vector2 anchor, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.transform.SetParent(crt, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : TMP_Settings.defaultFontAsset;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        var rt = t.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.sizeDelta = new Vector2(880, 120); rt.anchoredPosition = pos;
        return t;
    }
}
```

- [ ] **Step 7.2: 커밋**

```powershell
git add Assets/_Game/Scripts/ItemHud.cs
git commit -m "feat: ItemHud (활성효과/발동배너/폭탄경고 — 아이템 체감)"
```

---

### Task 8: ItemNetworkRelay (폭탄/우주선 RPC + 솔로 폴백)

**Files:**
- Create: `Assets/_Game/Scripts/Net/ItemNetworkRelay.cs`

- [ ] **Step 8.1: 파일 작성**

```csharp
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 공격형 아이템(폭탄/우주선)의 네트워크 중계. RPC 로 대상에게 "신호"만 보내고 효과는 대상 기기 로컬 적용.
///   폭탄: 다른 모든 사람 기기에 폭탄 함정 로컬 스폰. (MP 의 AI 는 노트 없으니 제외)
///   우주선: 현재 1등(사람=ClientRpc, AI=서버 직접) 감속.
/// 멀티 미접속(솔로)이면 RPC 대신 직접 로컬 적용 (AI 대상).
/// 붙이는 법: "Multiplayer" GameObject 에 NetworkObject 와 함께 + noteSpawner/hands/speedController/bombPrefab 연결.
/// </summary>
public class ItemNetworkRelay : NetworkBehaviour
{
    [Header("폭탄 함정")]
    [SerializeField] private ProtoNoteSpawner noteSpawner; // 폭탄 함정을 띄울 내 레인
    [SerializeField] private Striker[] hands;
    [SerializeField] private SpeedController speedController;
    [SerializeField] private BombHazard bombPrefab;
    [SerializeField] private NoteFeedback noteFeedback;
    [SerializeField] private float bombApproachSpeed = 2f;
    [SerializeField] private float bombHitRadius = 0.18f;
    [SerializeField] private float bombMissLocalZ = 0.1f;
    [SerializeField] private float bombSpawnDistance = 4f;
    [Range(0.1f, 1f)][SerializeField] private float bombSlowMult = 0.35f;
    [SerializeField] private float bombSlowDuration = 3f;

    [Header("우주선 (1등 감속)")]
    [Range(0.1f, 1f)][SerializeField] private float spaceshipSlowMult = 0.5f;
    [SerializeField] private float spaceshipSlowDuration = 3f;

    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    // ── 폭탄 ──────────────────────────────────────────────
    public void SendBomb()
    {
        if (Online) RequestBombServerRpc(NetworkManager.Singleton.LocalClientId);
        else SoloBomb();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBombServerRpc(ulong senderId) => ApplyBombClientRpc(senderId);

    [ClientRpc]
    private void ApplyBombClientRpc(ulong senderId)
    {
        if (NetworkManager.Singleton.LocalClientId == senderId) return; // 발신자 제외
        SpawnLocalBomb();
    }

    private void SpawnLocalBomb()
    {
        if (bombPrefab == null || noteSpawner == null) return;
        var bomb = Instantiate(bombPrefab, noteSpawner.transform, false);
        bomb.transform.localPosition = new Vector3(0f, 0f, bombSpawnDistance);
        bomb.Init(hands, speedController, bombApproachSpeed, bombHitRadius, bombMissLocalZ,
                  bombSlowMult, bombSlowDuration, noteFeedback);
    }

    private void SoloBomb()
    {
        // 솔로: 내 앞 가장 가까운 AI 를 직접 감속 (노트 없으니 함정 대신).
        var target = NearestAiAhead();
        if (target != null) SlowBoat(target, bombSlowMult, bombSlowDuration);
    }

    // ── 우주선 ────────────────────────────────────────────
    public void SendSpaceship()
    {
        if (Online) RequestSpaceshipServerRpc();
        else SoloSpaceship();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpaceshipServerRpc()
    {
        var leader = LeaderRacer();
        if (leader == null) return;
        if (leader.IsAi.Value)
            SlowBoat(leader.gameObject, spaceshipSlowMult, spaceshipSlowDuration); // 서버가 AI 직접
        else
            ApplySlowClientRpc(spaceshipSlowMult, spaceshipSlowDuration, leader.OwnerClientId);
    }

    [ClientRpc]
    private void ApplySlowClientRpc(float mult, float duration, ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
            speedController?.ApplyExternalSlow(mult, duration);
    }

    private void SoloSpaceship()
    {
        // 솔로: 1등(보통 AI 고스트) 직접 감속.
        var leaderBoat = SoloLeaderBoat();
        if (leaderBoat != null) SlowBoat(leaderBoat, spaceshipSlowMult, spaceshipSlowDuration);
    }

    // ── 헬퍼 ──────────────────────────────────────────────
    // MP: NetDistance 최댓값 NetRacer.
    private NetRacer LeaderRacer()
    {
        NetRacer best = null;
        float max = float.NegativeInfinity;
        foreach (var r in FindObjectsByType<NetRacer>(FindObjectsSortMode.None))
            if (r.NetDistance.Value > max) { max = r.NetDistance.Value; best = r; }
        return best;
    }

    // 솔로: 내 PlayerBoat 포함 모든 BoatMover 중 최장거리. 내 보트면 무시(null).
    private GameObject SoloLeaderBoat()
    {
        BoatMover best = null;
        float max = float.NegativeInfinity;
        foreach (var m in FindObjectsByType<BoatMover>(FindObjectsSortMode.None))
            if (m.DistanceTraveled > max) { max = m.DistanceTraveled; best = m; }
        if (best == null) return null;
        return best.GetComponent<GhostRacer>() != null ? best.gameObject : null; // 1등이 나면 무효
    }

    private GameObject NearestAiAhead()
    {
        var myBoat = speedController != null ? speedController.GetComponent<BoatMover>() : null;
        float myDist = myBoat != null ? myBoat.DistanceTraveled : 0f;
        GameObject best = null;
        float bestGap = float.PositiveInfinity;
        foreach (var g in FindObjectsByType<GhostRacer>(FindObjectsSortMode.None))
        {
            var m = g.GetComponent<BoatMover>();
            if (m == null) continue;
            float gap = m.DistanceTraveled - myDist;
            if (gap > 0f && gap < bestGap) { bestGap = gap; best = g.gameObject; }
        }
        return best;
    }

    private static void SlowBoat(GameObject go, float mult, float duration)
    {
        var sc = go.GetComponent<SpeedController>();
        if (sc != null) sc.ApplyExternalSlow(mult, duration);
        var gr = go.GetComponent<GhostRacer>();
        if (gr != null) gr.ApplyExternalSlow(mult, duration);
    }
}
```

- [ ] **Step 8.2: [사용자 Unity] 전체 컴파일 확인** — 이제 모든 타입(ItemNetworkRelay 포함) 존재 → Console 0 에러여야 함. 에러 시 메시지 기준 보정 (특히 NetRacer.NetDistance/IsAi/OwnerClientId 접근성 — 전부 public 확인됨).

- [ ] **Step 8.3: 커밋**

```powershell
git add Assets/_Game/Scripts/Net/ItemNetworkRelay.cs
git commit -m "feat: ItemNetworkRelay (폭탄/우주선 RPC→로컬 효과 + 솔로 폴백)"
git push origin multiplay
```

---

### Task 9: [사용자 Unity] 씬 배선 + 밸런스 + 검증

코드 작업 없음 — 에디터. 체크리스트:

- [ ] **9.1 ProtoItemSystem 배선 (미연결 수정):** 씬에 `ProtoItemSystem` 컴포넌트가 있는 GameObject 확인(없으면 빈 GameObject "ItemSystem" 생성 + 부착). 연결: `speedController`(PlayerBoat) / `noteSpawner`(NoteSpawner) / `itemRelay`(아래 9.3) / `raceManager`
- [ ] **9.2 NoteSpawner 연결 확인:** `ProtoNoteSpawner` 의 `itemSystem` 필드에 위 ProtoItemSystem 연결 (아이템 노트가 ActivateRandom 호출하도록)
- [ ] **9.3 ItemNetworkRelay 배치:** "Multiplayer" GameObject 에 `ItemNetworkRelay` + (이미 있는)`NetworkObject` 추가. 연결: `noteSpawner`/`hands`(Striker×2)/`speedController`(PlayerBoat)/`bombPrefab`(아래 9.4)/`noteFeedback`
- [ ] **9.4 폭탄 프리팹:** `Assets/_Game/Prefabs/BombHazard.prefab` — 검은 구/폭탄 메시(Collider 제거) + `BombHazard.cs`. ItemNetworkRelay 의 `bombPrefab` 에 연결
- [ ] **9.5 ItemHud 배치:** 빈 GameObject "ItemHud" + `ItemHud.cs` → `speedController`/`noteSpawner`/`itemSystem`/`attachTo`(Camera Offset)/`font`(NanumGothicBold SDF) 연결
- [ ] **9.6 아이템 아이콘/사운드(선택):** 발동 SFX(SoundManager 또는 noteFeedback). 아이콘은 플레이스홀더 가능
- [ ] **9.7 솔로 검증(에디터):** 아이템 노트 반복 명중 → 5종 발동 로그 + HUD 표시. 부스트(가속)/무지개별(자동명중·무적)/자석(파란 노트만)/폭탄(앞 AI 감속)/우주선(1등 AI 감속) 확인
- [ ] **9.8 멀티 검증(빌드 2~4인 또는 자동 루프):** 폭탄이 상대 레인에 뜨고 회피/피격 분기, 우주선이 1등만 감속, 등수 가중 분배(꼴찌가 우주선 잘 나옴), 무지개별 견제 면역
- [ ] **9.9 밸런스 튜닝:** 지속시간/감속률/등수 가중치 인스펙터·`ItemDistributor` 조정
- [ ] **9.10 커밋** (씬/프리팹)

```powershell
git add Assets/Scenes/ Assets/_Game/Prefabs/
git commit -m "scene: 아이템 5종 배선 (ItemSystem/Relay/Hud/BombHazard 프리팹)"
```

---

## 리스크 메모 (실행자용)

- **컴파일 순서**: ProtoItemSystem 이 `ItemNetworkRelay` 타입을 참조 → Task 8 작성 전엔 컴파일 에러. Task 1~8 을 한 묶음으로 작성 후 첫 컴파일 권장(중간 검증 불가 구간). 로컬 3종만 먼저 보고 싶으면 ProtoItemSystem 의 `itemRelay` 필드+2호출을 임시 주석 → Task 8 에서 해제.
- **MP 의 AI 폭탄 제외**: 멀티에서 AI 슬롯은 노트가 없어 폭탄 대상에서 빠짐(사람만 함정). 의도된 단순화 — 스펙 명시.
- **우주선 솔로 1등=나일 때**: `SoloLeaderBoat` 가 내 보트면 null 반환(무효). 정상 — 1등이면 우주선 드롭도 안 됨(ItemDistributor).
- **무적 중 자동 명중**: ProtoNote 가 매 프레임 Resolve(true) — 무적 동안 콤보/속도 자동 상승. 아이템 노트는 제외(무한 아이템 방지).
- **NetDistance 권한**: NetRacer.NetDistance 는 Everyone read. LeaderRacer 는 서버에서 호출(RequestSpaceshipServerRpc 내부)이라 모든 NetRacer 의 최신 NetDistance 접근 OK.
- **DevBuildScript 자동 루프**: 공격형은 `VR1Team.exe -lanhost`/`-lanjoin` 2 인스턴스로 폭탄 RPC 도달·우주선 1등 식별 로그 검증 가능.
