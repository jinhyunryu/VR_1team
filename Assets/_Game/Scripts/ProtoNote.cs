using UnityEngine;

/// 노트 종류 — 종류마다 요구 입력이 다르다.
public enum ProtoNoteType
{
    Touch,    // 그냥 손 닿으면
    Grab,     // 그랩(그립) 버튼 누른 채 닿으면
    Trigger,  // 트리거 버튼 누른 채 닿으면
    Item,     // 양손 동시 터치 → 아이템 자동 발동
}

/// <summary>
/// 프로토타입 노트. 플레이어 쪽으로 다가오다가, "요구 조건"을 만족한 손이 닿으면 성공.
///   Touch  = 손 닿기만
///   Grab   = 그랩 버튼 누른 채 닿기
///   Trigger= 트리거 누른 채 닿기
/// 조건 안 맞고 손만 닿으면 통과시킴(관대) — 안 친 채 지나가면 미스.
///
/// 콜라이더 불필요(거리 판정). 종류는 색으로 구분(Init 에서 머티리얼 틴팅).
/// 정식 NoteJudge(팀메이트)로 교체 가능 — SpeedController 는 RegisterHit/Miss 만 받음.
/// </summary>
public class ProtoNote : MonoBehaviour
{
    private SpeedController speed;
    private Striker[] hands;
    private ProtoNoteType type;
    private float approachSpeed;
    private float hitRadius;
    private float missLocalZ;
    private bool resolved;

    private ProtoItemSystem itemSystem;
    private ItemType itemType;
    private NoteFeedback feedback;
    private Striker hitHand;

    [Header("타격 시 튕겨나가기")]
    [SerializeField] private bool bounceOnHit = true;

    private bool flying;
    private Vector3 flyVelocity;
    private Vector3 flySpin;
    private Vector3 flyStartScale;
    private float flyTimer;
    private const float FlyDuration = 0.45f; // 시야에 머무는 시간 최소화 (0.6 → 0.45)

    public void Init(SpeedController speed, Striker[] hands, ProtoNoteType type,
                     float approachSpeed, float hitRadius, float missLocalZ, Color color,
                     ProtoItemSystem itemSystem = null, ItemType itemType = ItemType.Boost,
                     bool applyTint = true, NoteFeedback feedback = null)
    {
        this.speed = speed;
        this.hands = hands;
        this.type = type;
        this.approachSpeed = approachSpeed;
        this.hitRadius = hitRadius;
        this.missLocalZ = missLocalZ;
        this.itemSystem = itemSystem;
        this.itemType = itemType;
        this.feedback = feedback;
        MakeUnlit();                // 빛 영향 제거(가까워도 안 검어짐) — 텍스처/색 유지
        if (applyTint) Tint(color); // 모델이 이미 색 있으면 false 로(색 안 덮음)
    }

    private void Update()
    {
        if (flying) { FlyAway(); return; }
        if (resolved) return;

        // 플레이어 쪽(부모 로컬 -Z)으로 등속 접근.
        transform.localPosition += Vector3.back * (approachSpeed * Time.deltaTime);

        // 히트 판정.
        if (type == ProtoNoteType.Item)
        {
            // 아이템: 양손 동시 터치.
            if (BothHandsTouching())
            {
                Resolve(true);
                return;
            }
        }
        else if (hands != null)
        {
            // 일반: 조건 만족한 손이 닿으면.
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                bool touching = Vector3.Distance(transform.position, hand.WorldPosition) <= hitRadius;
                if (touching && ConditionMet(hand))
                {
                    hitHand = hand; // 친 손(진동용)
                    Resolve(true);
                    return;
                }
            }
        }

        // 안 친 채 지나치면 미스.
        if (transform.localPosition.z <= missLocalZ)
            Resolve(false);
    }

    private bool BothHandsTouching()
    {
        if (hands == null) return false;
        int touching = 0;
        foreach (var hand in hands)
        {
            if (hand == null) continue;
            if (Vector3.Distance(transform.position, hand.WorldPosition) <= hitRadius) touching++;
        }
        return touching >= 2; // 양손
    }

    private bool ConditionMet(Striker hand)
    {
        return type switch
        {
            ProtoNoteType.Grab    => hand.GrabHeld,
            ProtoNoteType.Trigger => hand.TriggerHeld,
            _                     => !hand.GrabHeld && !hand.TriggerHeld, // Touch: 버튼 아무것도 안 눌림
        };
    }

    private void Tint(Color color)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var m = r.material; // 인스턴스
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); // URP
            if (m.HasProperty("_Color")) m.color = color;                     // 그 외
            // 빛/그림자로 검게 변하지 않도록 색을 발광시킴(가까워져도 색 유지).
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color);
            }
        }
    }

    // 노트 머티리얼을 URP/Unlit 로 바꿔 빛 영향을 없앤다(텍스처·색은 보존).
    private static Shader unlitShader;
    private void MakeUnlit()
    {
        if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) return;

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            foreach (var m in r.materials) // 인스턴스
            {
                var tex = m.mainTexture;
                Color col = Color.white;
                if (m.HasProperty("_BaseColor")) col = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color")) col = m.color;

                m.shader = unlitShader;        // Lit → Unlit
                m.mainTexture = tex;           // _BaseMap 유지
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col); // 색 유지
            }
        }
    }

    private void Resolve(bool hit)
    {
        resolved = true;
        if (hit)
        {
            speed?.RegisterHit();
            if (type == ProtoNoteType.Item) itemSystem?.Activate(itemType); // 아이템 자동 발동
            PlayHitFeedback();
            Debug.Log($"[ProtoNote] HIT ({type}) → 콤보 {(speed != null ? speed.Combo.ToString() : "?")}");
            if (bounceOnHit) { StartFly(); return; } // 튕겨나간 뒤 사라짐(파괴 보류)
        }
        else
        {
            speed?.RegisterMiss();
            Debug.Log($"[ProtoNote] MISS ({type}) → 콤보 리셋");
        }
        Destroy(gameObject);
    }

    // 맞은 노트를 "플레이어를 비껴 뒤쪽 대각선"으로 날려보낸다(작아지다 소멸).
    // 정면 앞+위로 날리면 시야를 가림 — 노트가 있던 쪽(좌/우) 어깨 뒤로 빠르게 스쳐 지나가게.
    private void StartFly()
    {
        flying = true;
        flyStartScale = transform.localScale;
        transform.SetParent(null, true); // 리그 따라가지 않게(월드 고정) — 배가 전진하며 더 빨리 멀어짐

        Vector3 back = Vector3.back;
        Vector3 side = Vector3.right;
        var cam = Camera.main;
        if (cam != null)
        {
            back = -cam.transform.forward; // 플레이어 뒤쪽
            // 노트가 시야 기준 어느 쪽에 있었나 → 그쪽 대각선으로 비껴 나감 (얼굴 정면 통과 방지)
            float sideSign = Mathf.Sign(Vector3.Dot(transform.position - cam.transform.position, cam.transform.right));
            if (sideSign == 0f) sideSign = Random.value < 0.5f ? -1f : 1f;
            side = cam.transform.right * sideSign;
        }

        // ⚠️ 뒤(back)를 주속도로 주면 얼굴 바로 옆을 통과하며 한순간 화면을 가림 —
        //    옆(side)을 주속도로 줘서 시야 원뿔을 측면으로 탈출시키고,
        //    배가 전진 중이라 옆으로 빠진 노트는 자연히 "뒤로 스쳐가는" 느낌이 됨.
        flyVelocity = side * Random.Range(4.5f, 6f)          // 옆으로 강하게 (시야 측면 탈출)
                    + back * Random.Range(1.5f, 2.5f)        // 뒤로는 적당히
                    + Vector3.up * Random.Range(0.3f, 0.8f); // 위는 아주 살짝
        flySpin = Random.insideUnitSphere.normalized * Random.Range(360f, 900f);
        flyTimer = FlyDuration;
    }

    private void FlyAway()
    {
        flyVelocity += Vector3.down * 9f * Time.deltaTime; // 중력 → 포물선
        transform.position += flyVelocity * Time.deltaTime;
        transform.Rotate(flySpin * Time.deltaTime, Space.World);

        flyTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(flyTimer / FlyDuration);
        transform.localScale = flyStartScale * k; // 줄며 사라짐
        if (flyTimer <= 0f) Destroy(gameObject);
    }

    // 히트 시 이펙트 + 사운드 + 진동(친 손, 아이템은 양손).
    private void PlayHitFeedback()
    {
        if (feedback == null) return;
        feedback.PlayAt(transform.position);
        if (type == ProtoNoteType.Item)
        {
            if (hands != null)
                foreach (var hand in hands) feedback.Vibrate(hand);
        }
        else
        {
            feedback.Vibrate(hitHand);
        }
    }
}
