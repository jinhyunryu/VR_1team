using UnityEngine;

/// <summary>
/// 노트 타격 피드백 — 이펙트(VFX) + 사운드(SFX) + 컨트롤러 진동(햅틱).
/// ProtoNote 가 히트 시 호출한다. 전부 표현 only(로직 비의존).
///
/// 붙이는 법: 아무 GameObject 에 추가 → ProtoNoteSpawner 의 noteFeedback 에 연결.
///   hitEffectPrefab(파티클 버스트) / hitClip(타격음) / 진동 세기·길이 설정.
/// </summary>
public class NoteFeedback : MonoBehaviour
{
    [Header("이펙트(VFX)")]
    [Tooltip("히트 위치에 생성할 파티클/이펙트 프리팹.")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("이펙트 자동 삭제 시간(s). 0 이면 안 지움.")]
    [SerializeField] private float effectLifetime = 2f;

    [Header("사운드(SFX)")]
    [SerializeField] private AudioClip hitClip;
    [Tooltip("재생용 AudioSource. 비우면 위치에서 PlayClipAtPoint.")]
    [SerializeField] private AudioSource audioSource;
    [Range(0f, 1f)][SerializeField] private float volume = 0.8f;

    [Header("진동(햅틱)")]
    [Range(0f, 1f)][SerializeField] private float hapticAmplitude = 0.5f;
    [SerializeField] private float hapticDuration = 0.1f;

    /// 히트 위치에 이펙트 + 사운드.
    public void PlayAt(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            var fx = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            if (effectLifetime > 0f) Destroy(fx, effectLifetime);
        }

        if (hitClip != null)
        {
            if (audioSource != null) audioSource.PlayOneShot(hitClip, volume);
            else AudioSource.PlayClipAtPoint(hitClip, position, volume);
        }
    }

    /// 해당 손 진동.
    public void Vibrate(Striker hand)
    {
        if (hand != null) hand.SendHaptic(hapticAmplitude, hapticDuration);
    }
}
