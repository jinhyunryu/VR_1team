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

    [Tooltip("색을 입힐 렌더러. 비우면 작은 구체를 자동 생성. 손 모델이면 그 Renderer 연결.")]
    [SerializeField] private Renderer visual;

    [Tooltip("추가로 같은 색을 입힐 렌더러들(손 모델이 여러 파츠일 때). 비워도 됨.")]
    [SerializeField] private Renderer[] extraVisuals;

    [Tooltip("켜면 Unlit 으로 강제(구체용 — 단색). 손 모델은 끄면 고유 머티리얼 유지 + 색만 틴팅.")]
    [SerializeField] private bool forceUnlit = true;

    [Header("비주얼")]
    [Tooltip("자동 생성 구체의 지름(m).")]
    [SerializeField] private float visualScale = 0.12f;

    [Tooltip("기본(대기) 색 — 파랑.")]
    [SerializeField] private Color idleColor = new Color(0.25f, 0.55f, 1f);

    [Tooltip("트리거 누름 색 — 빨강.")]
    [SerializeField] private Color triggerColor = new Color(1f, 0.25f, 0.2f);

    [Tooltip("그랩(그립) 누름 색 — 초록.")]
    [SerializeField] private Color grabColor = new Color(0.2f, 1f, 0.35f);

    [Header("양손 모음 (아이템 노트 — 흰색)")]
    [Tooltip("반대쪽 손의 Striker. 비우면 자동 탐색.")]
    [SerializeField] private Striker otherStriker;

    [Tooltip("양손 타격점 거리가 이 값(m) 이하면 '모음' — 흰색. 0 이면 기능 끔.")]
    [SerializeField] private float handsTogetherDistance = 0.25f;

    [Tooltip("모음 해제 거리(m). together 보다 크게 — 경계에서 색 깜빡임 방지(히스테리시스).")]
    [SerializeField] private float handsApartDistance = 0.35f;

    [Tooltip("양손 모음 색 — 흰색 (아이템 노트와 동일).")]
    [SerializeField] private Color togetherColor = Color.white;

    private Material mat;
    private readonly System.Collections.Generic.List<Material> tintMats = new();
    private bool handsTogether;

    private void Awake()
    {
        if (striker == null) striker = GetComponentInParent<Striker>();
        if (otherStriker == null && striker != null)
            foreach (var s in FindObjectsByType<Striker>(FindObjectsSortMode.None))
                if (s != striker) { otherStriker = s; break; }

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

        // 틴팅 대상 머티리얼 수집 (visual + 추가 파츠). 인스턴스라 공유 머티리얼 오염 없음.
        mat = visual.material;
        tintMats.Clear();
        tintMats.Add(mat);
        if (extraVisuals != null)
            foreach (var r in extraVisuals)
                if (r != null) tintMats.Add(r.material);

        // 구체는 Unlit 강제(조명 무관 단색). 손 모델은 forceUnlit 끄면 고유 머티리얼 유지.
        if (forceUnlit)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
                foreach (var m in tintMats)
                    if (m != null) m.shader = unlit;
        }
        ApplyColor(idleColor);
    }

    private void Update()
    {
        if (striker == null || mat == null) return;

        // 양손 모음 감지 — 히스테리시스(켜질 땐 가깝게, 꺼질 땐 멀게)로 경계 깜빡임 방지.
        if (otherStriker != null && handsTogetherDistance > 0f)
        {
            float d = Vector3.Distance(striker.WorldPosition, otherStriker.WorldPosition);
            if (handsTogether) { if (d > handsApartDistance) handsTogether = false; }
            else if (d <= handsTogetherDistance) handsTogether = true;
        }
        else handsTogether = false;

        if (handsTogether) ApplyColor(togetherColor);          // 양손 모음 = 흰색 (아이템)
        else if (striker.TriggerHeld) ApplyColor(triggerColor); // 트리거 = 빨강
        else if (striker.GrabHeld) ApplyColor(grabColor);       // 그랩 = 초록
        else ApplyColor(idleColor);                             // 기본 = 파랑
    }

    private void ApplyColor(Color c)
    {
        foreach (var m in tintMats)
        {
            if (m == null) continue;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
        }
    }
}
