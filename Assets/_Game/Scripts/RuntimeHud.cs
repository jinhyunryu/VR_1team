using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 월드공간 HUD를 코드로 생성(에디터 캔버스 세팅 불필요).
///   좌상단: 진행도 Fill 바.  우상단: 콤보 텍스트.
///
/// ★Play 중 인스펙터 값이 실시간 반영★ — 매 프레임 레이아웃을 다시 적용하므로,
///   Play 상태로 distance/위치/크기/색을 돌려 맞추면 즉시 보인다.
///   (Play 중 맞춘 값을 유지하려면: 컴포넌트 우클릭 → Copy Component → 정지 후 Paste Component Values)
///
/// VR 규칙: Screen Overlay 금지 → World Space + layer=Default(카메라가 렌더). 데이터만 읽어 표시.
///
/// 붙이는 법: 빈 GameObject 에 추가 → speedController/playerBoat/raceManager 연결.
///   (선택) attachTo 에 Camera Offset → body-lock(머리 안 따라옴). 비우면 Main Camera.
/// </summary>
public class RuntimeHud : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private SpeedController speedController;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private BoatMover playerBoat;

    [Header("패널 배치")]
    [SerializeField] private Transform attachTo;
    [SerializeField] private float distance = 1.5f;
    [SerializeField] private float heightOffset = 0f;
    [SerializeField] private float horizontalOffset = 0f;
    [SerializeField] private Vector2 panelSize = new Vector2(1200, 700);
    [SerializeField] private float worldScale = 0.0012f;

    [Header("좌상단 진행 바")]
    [SerializeField] private Vector2 barOffset = new Vector2(40, -40);
    [SerializeField] private Vector2 barSize = new Vector2(520, 70);
    [SerializeField] private Color barColor = new Color(0.3f, 0.85f, 1f, 1f);
    [SerializeField] private Color barBgColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("우상단 콤보 텍스트")]
    [SerializeField] private Vector2 comboOffset = new Vector2(-40, -40);
    [SerializeField] private Vector2 comboSize = new Vector2(600, 130);
    [SerializeField] private float comboFontSize = 90f;
    [SerializeField] private Color textColor = Color.white;

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset font;

    private RectTransform canvasRt, barBgRt, comboRt;
    private Image barBg, progressFill;
    private TMP_Text comboText;
    private static Sprite sWhite;

    private void Start()
    {
        Build();
        ApplyLayout();
    }

    private void Update()
    {
        ApplyLayout();   // 인스펙터 값 실시간 반영(튜닝 편의)
        ApplyData();
    }

    // ── 1회 생성 ──
    private void Build()
    {
        Transform parent = attachTo != null ? attachTo
                         : (Camera.main != null ? Camera.main.transform : null);

        var canvasGo = new GameObject("RuntimeHud", typeof(RectTransform), typeof(Canvas));
        canvasGo.layer = 0;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        canvasRt = (RectTransform)canvasGo.transform;
        if (parent != null) canvasRt.SetParent(parent, false);
        canvasRt.localRotation = Quaternion.identity;

        barBg = NewImage("ProgressBg", canvasRt);
        barBgRt = barBg.rectTransform;

        progressFill = NewImage("ProgressFill", barBgRt);
        var frt = progressFill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;

        var comboGo = new GameObject("ComboText", typeof(RectTransform));
        comboGo.layer = 0;
        comboGo.transform.SetParent(canvasRt, false);
        comboText = comboGo.AddComponent<TextMeshProUGUI>();
        comboText.font = font != null ? font : TMP_Settings.defaultFontAsset;
        comboText.text = "COMBO 0";
        comboText.alignment = TextAlignmentOptions.TopRight;
        comboText.enableWordWrapping = false;
        comboRt = (RectTransform)comboGo.transform;
    }

    // ── 매 프레임 레이아웃 적용(라이브 튜닝) ──
    private void ApplyLayout()
    {
        if (canvasRt == null) return;

        canvasRt.localScale = Vector3.one * worldScale;
        canvasRt.localPosition = new Vector3(horizontalOffset, heightOffset, distance);
        canvasRt.sizeDelta = panelSize;

        Place(barBgRt, new Vector2(0, 1), barOffset, barSize);
        barBg.color = barBgColor;
        progressFill.color = barColor;

        Place(comboRt, new Vector2(1, 1), comboOffset, comboSize);
        comboText.color = textColor;
        comboText.fontSize = comboFontSize;
    }

    // ── 데이터 갱신 ──
    private void ApplyData()
    {
        if (comboText != null && speedController != null)
            comboText.text = $"COMBO {speedController.Combo}";

        if (progressFill != null)
        {
            float p = 0f;
            if (raceManager != null && raceManager.FinishDistance > 0f && playerBoat != null)
                p = Mathf.Clamp01(playerBoat.DistanceTraveled / raceManager.FinishDistance);
            progressFill.fillAmount = p;
        }
    }

    private static void Place(RectTransform rt, Vector2 corner, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = corner;
        rt.anchorMax = corner;
        rt.pivot = corner;
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }

    private static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = WhiteSprite();
        img.type = Image.Type.Simple;
        return img;
    }

    private static Sprite WhiteSprite()
    {
        if (sWhite == null)
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            sWhite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
        return sWhite;
    }
}
