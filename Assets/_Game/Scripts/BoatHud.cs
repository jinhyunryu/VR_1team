using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 보트 부착 대시보드 HUD. 머리고정 RuntimeHud/ItemHud 대체.
///   좌하단: 진행도 바   우하단: 콤보 + 속도   우상단: 활성 아이템(이름 + 남은 시간) + 발동 플래시
///
/// ★Edit 모드에서 바로 보임 + Transform 으로 배치★ — [ExecuteAlways] 로 씬 뷰에 캔버스 생성.
///   캔버스는 이 GameObject 의 자식 → **이 오브젝트의 Transform(위치/회전/스케일)이 곧 HUD 위치**.
///   배치: BoatHud GameObject 를 보트(PlayerBoat) 자식으로 두고, 씬 뷰에서 드래그/인스펙터로 옮기면 됨.
/// 표현 only(로직 비의존).
///
/// 붙이는 법: 빈 GameObject "BoatHud" → 이 컴포넌트 + speedController/playerBoat/raceManager/noteSpawner/itemSystem 연결.
///   보트 자식으로 두고 Transform 으로 대시보드 위치에 배치. World Scale 로 크기 조절.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class BoatHud : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private SpeedController speedController;
    [SerializeField] private BoatMover playerBoat;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private ProtoNoteSpawner noteSpawner;
    [SerializeField] private ProtoItemSystem itemSystem;

    [Header("크기 (위치는 이 오브젝트 Transform)")]
    [Tooltip("HUD 월드 크기. 키우면 커짐. 위치/회전은 이 GameObject 의 Transform 으로 조절.")]
    [SerializeField] private float worldScale = 0.0015f;
    [SerializeField] private Vector2 panelSize = new Vector2(1200, 700);

    [Header("진행도 바 (좌하단)")]
    [SerializeField] private Vector2 barOffset = new Vector2(40, 40);
    [SerializeField] private Vector2 barSize = new Vector2(520, 64);
    [SerializeField] private Color barColor = new Color(0.3f, 0.85f, 1f, 1f);
    [SerializeField] private Color barBgColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("콤보/속도 (우하단)")]
    [SerializeField] private Vector2 comboOffset = new Vector2(-40, 40);
    [SerializeField] private float comboFontSize = 90f;
    [SerializeField] private float speedFontSize = 50f;
    [SerializeField] private Color textColor = Color.white;

    [Header("아이템 (우상단)")]
    [SerializeField] private Vector2 itemOffset = new Vector2(-40, -40);
    [SerializeField] private float itemFontSize = 56f;
    [SerializeField] private float flashSeconds = 1.5f;
    [SerializeField] private Color itemColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset font;

    [Header("항상 위에 (배/지형에 안 가림)")]
    [Tooltip("켜면 ZTest Always — HUD 가 모든 것 위에 그려짐.")]
    [SerializeField] private bool alwaysOnTop = true;
    [Tooltip("렌더 큐(클수록 위에). Overlay(4000)보다 크게.")]
    [SerializeField] private int renderQueue = 5000;

    private const string CanvasName = "__BoatHudCanvas";

    private RectTransform canvasRt, barBgRt, comboRt, itemRt;
    private Image barBg, progressFill;
    private TMP_Text comboText, speedText, itemText;
    private static Sprite sWhite;

    private ItemType activeItem;
    private float itemActiveTimer;
    private float flashTimer;
    private ItemType flashItem;

    private void OnEnable()
    {
        Rebuild();
        if (Application.isPlaying && itemSystem != null) itemSystem.OnItemActivated += OnItemActivated;
    }

    private void OnDisable()
    {
        if (Application.isPlaying && itemSystem != null) itemSystem.OnItemActivated -= OnItemActivated;
    }

    private void OnItemActivated(ItemType type, float duration)
    {
        flashItem = type;
        flashTimer = flashSeconds;
        if (duration > 0f) { activeItem = type; itemActiveTimer = duration; }
    }

    private void Update()
    {
        if (canvasRt == null) Rebuild();
        ApplyLayout();
        ApplyData();
    }

    // ── 캔버스 (재)생성 — 이 오브젝트 자식, 저장 안 함(DontSave) ──
    private void Rebuild()
    {
        // 기존 생성본 정리 (도메인 리로드/재컴파일 후 중복 방지).
        var existing = transform.Find(CanvasName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
        Build();
    }

    private void Build()
    {
        var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas));
        go.layer = 0;
        go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable; // 씬에 안 저장, 보이기만
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        canvasRt = (RectTransform)go.transform;
        canvasRt.SetParent(transform, false);           // ★이 오브젝트 자식 = Transform 이 위치
        canvasRt.localPosition = Vector3.zero;
        canvasRt.localRotation = Quaternion.identity;

        barBg = NewImage("ProgressBg", canvasRt, barBgColor);
        barBgRt = barBg.rectTransform;
        progressFill = NewImage("ProgressFill", barBgRt, barColor);
        var frt = progressFill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0.3f;

        comboText = NewText("Combo", canvasRt, comboFontSize, TextAlignmentOptions.BottomRight, textColor);
        comboRt = comboText.rectTransform;
        speedText = NewText("Speed", canvasRt, speedFontSize, TextAlignmentOptions.BottomRight, textColor);
        itemText = NewText("Item", canvasRt, itemFontSize, TextAlignmentOptions.TopRight, itemColor);
        itemRt = itemText.rectTransform;

        if (alwaysOnTop) MakeAlwaysOnTop();
    }

    // 모든 것 위에 그리기 (배/지형 안 가림) — HudAlwaysOnTop 기법.
    private void MakeAlwaysOnTop()
    {
        foreach (var text in canvasRt.GetComponentsInChildren<TMP_Text>(true))
        {
            var mat = text.fontMaterial; // 인스턴스
            mat.SetFloat("_ZTestMode", (float)CompareFunction.Always);
            mat.renderQueue = renderQueue;
            mat.hideFlags = HideFlags.DontSave;
        }
        var shader = Shader.Find("UI/AlwaysOnTop");
        if (shader == null) return; // 못 찾으면 TMP 만 처리(Image 는 그대로)
        foreach (var g in canvasRt.GetComponentsInChildren<Graphic>(true))
        {
            if (g is TMP_Text) continue;
            if (g is Image || g is RawImage)
                g.material = new Material(shader) { renderQueue = renderQueue, hideFlags = HideFlags.DontSave };
        }
    }

    private void ApplyLayout()
    {
        if (canvasRt == null) return;
        canvasRt.localPosition = Vector3.zero;
        canvasRt.localRotation = Quaternion.identity;
        canvasRt.localScale = Vector3.one * worldScale;
        canvasRt.sizeDelta = panelSize;

        Place(barBgRt, new Vector2(0, 0), barOffset, barSize);
        barBg.color = barBgColor;
        progressFill.color = barColor;

        Place(comboRt, new Vector2(1, 0), comboOffset, new Vector2(640, 130));
        comboText.color = textColor; comboText.fontSize = comboFontSize;
        Place(speedText.rectTransform, new Vector2(1, 0), comboOffset + new Vector2(0, 120), new Vector2(640, 90));
        speedText.color = textColor; speedText.fontSize = speedFontSize;

        Place(itemRt, new Vector2(1, 1), itemOffset, new Vector2(700, 130));
        itemText.color = itemColor; itemText.fontSize = itemFontSize;
    }

    private void ApplyData()
    {
        // Edit 모드: 배치용 샘플 표시.
        if (!Application.isPlaying)
        {
            if (progressFill != null) progressFill.fillAmount = 0.3f;
            if (comboText != null) comboText.text = "COMBO 12";
            if (speedText != null) speedText.text = "14 m/s";
            if (itemText != null) itemText.text = "부스트  3s";
            return;
        }

        float p = 0f;
        if (raceManager != null && raceManager.FinishDistance > 0f && playerBoat != null)
            p = Mathf.Clamp01(playerBoat.DistanceTraveled / raceManager.FinishDistance);
        if (progressFill != null) progressFill.fillAmount = p;

        if (speedController != null)
        {
            comboText.text = $"COMBO {speedController.Combo}";
            float spd = playerBoat != null ? playerBoat.Speed : speedController.CurrentTargetSpeed;
            speedText.text = $"{spd:0} m/s";
        }

        if (itemActiveTimer > 0f) itemActiveTimer -= Time.deltaTime;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        if (itemActiveTimer > 0f) itemText.text = $"{KorName(activeItem)}  {Mathf.CeilToInt(itemActiveTimer)}s";
        else if (flashTimer > 0f) itemText.text = $"{KorName(flashItem)}!";
        else itemText.text = "";
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

    private TMP_Text NewText(string name, Transform parent, float fontSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : TMP_Settings.defaultFontAsset;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        return t;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = color;
        return img;
    }

    private static void Place(RectTransform rt, Vector2 corner, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = corner; rt.anchorMax = corner; rt.pivot = corner;
        rt.sizeDelta = size; rt.anchoredPosition = offset;
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
