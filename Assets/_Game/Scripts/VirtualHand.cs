using UnityEngine;

/// <summary>
/// 컨트롤러 앞쪽에 떠 있는 "가상 손" 비주얼 + 입력 색 피드백.
///   기본 = 파랑, 트리거 = 빨강, 그랩 = 초록.
/// 판정 지점(Striker.hitPoint)을 이 Transform 으로 연결하면 팔이 짧거나 노트가 멀어도
/// 가상 손 위치로 타격 판정 → 리치 연장.
///
/// 붙이는 법(에디터):
///   1) 좌/우 컨트롤러 GameObject 아래 빈 자식 "VirtualHand" 생성, Position (0, 0, 0.5) 정도 (앞쪽).
///   2) 이 컴포넌트 부착 — striker 비우면 부모에서 자동 탐색, visual 비우면 구체 자동 생성.
///   3) ★핵심★ 그 컨트롤러의 Striker → Hit Point 에 이 Transform 드래그 (판정점 연장).
///   4) 거리/크기/색은 인스펙터에서 라이브 튜닝.
/// 표현 only — 판정·입력 로직은 기존 그대로 (Striker 가 hitPoint 를 추적할 뿐).
/// </summary>
public class VirtualHand : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("입력 상태를 읽을 Striker. 비우면 부모에서 자동 탐색.")]
    [SerializeField] private Striker striker;

    [Tooltip("색을 입힐 렌더러. 비우면 작은 구체를 자동 생성.")]
    [SerializeField] private Renderer visual;

    [Header("비주얼")]
    [Tooltip("자동 생성 구체의 지름(m).")]
    [SerializeField] private float visualScale = 0.12f;

    [Tooltip("기본(대기) 색 — 파랑.")]
    [SerializeField] private Color idleColor = new Color(0.25f, 0.55f, 1f);

    [Tooltip("트리거 누름 색 — 빨강.")]
    [SerializeField] private Color triggerColor = new Color(1f, 0.25f, 0.2f);

    [Tooltip("그랩(그립) 누름 색 — 초록.")]
    [SerializeField] private Color grabColor = new Color(0.2f, 1f, 0.35f);

    private Material mat;

    private void Awake()
    {
        if (striker == null) striker = GetComponentInParent<Striker>();

        if (visual == null)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "VirtualHandVisual";
            Destroy(sphere.GetComponent<Collider>()); // 물리 간섭 금지 (판정은 Striker 거리 기반)
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * visualScale;
            visual = sphere.GetComponent<Renderer>();
        }

        // 조명 무관하게 색이 항상 보이게 Unlit 으로 (Always Included 에 등록돼 있어 빌드 안전).
        mat = visual.material; // 인스턴스 — 공유 머티리얼 오염 방지
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null) mat.shader = unlit;
        ApplyColor(idleColor);
    }

    private void Update()
    {
        if (striker == null || mat == null) return;

        if (striker.TriggerHeld) ApplyColor(triggerColor);
        else if (striker.GrabHeld) ApplyColor(grabColor);
        else ApplyColor(idleColor);
    }

    private void ApplyColor(Color c)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        else mat.color = c;
    }
}
