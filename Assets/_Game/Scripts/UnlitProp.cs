using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 소품(골라인 게이트 등)을 광원 영향에서 분리한다 — 노트의 자동 Unlit 와 같은 방식.
///   1) 자식 렌더러 전부의 머티리얼을 URP/Unlit 로 교체 (텍스처·색 보존) → 그림자·광원 무시, 항상 또렷
///   2) 그림자 드리우기(cast) 끔 → 주변에 큰 그림자 안 생김
///
/// 붙이는 법: 광원 영향을 빼고 싶은 에셋의 루트에 부착. 끝.
///   - convertToUnlit 을 끄면 라이팅은 유지하고 그림자 cast/receive 만 끈다 (절충안).
/// 표현 only — 게임 로직 무관.
/// </summary>
public class UnlitProp : MonoBehaviour
{
    [Tooltip("켜면 머티리얼을 URP/Unlit 로 교체 — 광원/그림자 완전 무시 (노트와 동일한 톤). " +
             "끄면 라이팅은 유지하고 그림자만 끔.")]
    [SerializeField] private bool convertToUnlit = true;

    [Tooltip("이 오브젝트가 그림자를 드리우지 않게.")]
    [SerializeField] private bool disableShadowCasting = true;

    [Tooltip("(convertToUnlit 꺼져 있을 때) 이 오브젝트가 그림자를 받지 않게.")]
    [SerializeField] private bool disableShadowReceiving = true;

    private void Start()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (disableShadowCasting) r.shadowCastingMode = ShadowCastingMode.Off;
            if (!convertToUnlit && disableShadowReceiving) r.receiveShadows = false;
        }

        if (!convertToUnlit) return;

        var unlit = Shader.Find("Universal Render Pipeline/Unlit"); // Always Included 등록됨 — 빌드 안전
        if (unlit == null) return;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.materials) // 인스턴스 — 공유 머티리얼/에셋 오염 없음
            {
                var tex = m.mainTexture;
                Color col = Color.white;
                if (m.HasProperty("_BaseColor")) col = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color")) col = m.color;

                m.shader = unlit;
                m.mainTexture = tex;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            }
        }
    }
}
