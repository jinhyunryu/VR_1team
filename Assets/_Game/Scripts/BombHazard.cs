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
