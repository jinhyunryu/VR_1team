using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// HUD를 항상 최상단에 그린다(ZTest Always). 월드공간은 그대로 유지 → VR 안전.
/// 물/지형에 안 가려지므로, 계기판을 수평선 아래(물 위 낮은 위치)에 둘 수 있다.
///
/// Canvas(또는 그 상위)에 붙이면 자식의 모든 TMP_Text / Image / RawImage 를 처리:
///   - TMP_Text : SDF 셰이더의 _ZTestMode 를 Always 로.
///   - Image/RawImage : "UI/AlwaysOnTop" 셰이더 머티리얼로 교체.
///
/// ⚠️ 항상 위에 = 보트 구조물도 뚫고 보임. 계기판엔 보통 OK지만, 너무 남발하면
///    깊이감이 깨지니 꼭 필요한 HUD 에만.
///
/// 붙이는 법: World Space Canvas 에 추가 → imageOverlayShader 에 "UI/AlwaysOnTop" 연결(비우면 자동 탐색).
/// </summary>
[DisallowMultipleComponent]
public class HudAlwaysOnTop : MonoBehaviour
{
    [Tooltip("Image/RawImage 용 ZTest Always 셰이더. 'UI/AlwaysOnTop'. 비우면 Shader.Find 로 자동 탐색.")]
    [SerializeField] private Shader imageOverlayShader;

    [Tooltip("렌더 큐(클수록 늦게=위에). Overlay(4000)보다 크게.")]
    [SerializeField] private int renderQueue = 5000;

    private void Awake() => Apply();

    /// 자식의 모든 UI 그래픽을 항상-위에로 전환. 런타임에 UI를 추가했으면 다시 호출.
    public void Apply()
    {
        // TMP: 자체 SDF 셰이더의 깊이 테스트를 Always 로.
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            var mat = text.fontMaterial; // 인스턴스 — 공유 머티리얼은 안 건드림
            mat.SetFloat("_ZTestMode", (float)CompareFunction.Always);
            mat.renderQueue = renderQueue;
        }

        // Image/RawImage: ZTest Always 셰이더 머티리얼로 교체.
        var shader = imageOverlayShader != null ? imageOverlayShader : Shader.Find("UI/AlwaysOnTop");
        if (shader == null)
        {
            Debug.LogWarning("[HudAlwaysOnTop] 'UI/AlwaysOnTop' 셰이더를 못 찾음. Image 는 그대로 둠.");
            return;
        }

        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is TMP_Text) continue; // 위에서 처리함
            if (graphic is Image || graphic is RawImage)
                graphic.material = new Material(shader) { renderQueue = renderQueue };
        }
    }
}
