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

    [Header("멀티 순위 작대기 (진행바 위 다른 플레이어)")]
    [Tooltip("진행바에 다른 레이서들을 P# 작대기로 표시.")]
    [SerializeField] private bool showStandingTicks = true;
    [SerializeField] private Color tickColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float tickWidth = 8f;
    [SerializeField] private float tickLabelFontSize = 32f;

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

    private readonly System.Collections.Generic.List<RectTransform> tickBars = new();
    private readonly System.Collections.Generic.List<TMP_Text> tickLabels = new();
    private static Shader uiOnTopShader;

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
        ApplyTextOnTop();
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

    // Image 만 1회 처리 (전용 셰이더라 안정적). TMP 는 ApplyTextOnTop 으로 매 프레임 재적용.
    private void MakeAlwaysOnTop()
    {
        var shader = Shader.Find("UI/AlwaysOnTop");
        if (shader == null) return;
        foreach (var g in canvasRt.GetComponentsInChildren<Graphic>(true))
        {
            if (g is TMP_Text) continue;
            if (g is Image || g is RawImage)
                g.material = new Material(shader) { renderQueue = renderQueue, hideFlags = HideFlags.DontSave };
        }
    }

    // TMP 항상-위: 폰트 머티리얼의 셰이더를 "ZTest Always 변형"으로 교체 (TMP_SDF 는 _ZTestMode 없음).
    // 매 프레임 + 한글 폴백 서브메시까지 (TMP 가 머티리얼 재생성해도 유지).
    private static Shader tmpOnTopShader;
    private void ApplyTextOnTop()
    {
        if (!alwaysOnTop) return;
        if (!Application.isPlaying) return; // ⚠️ Edit 모드에선 공유 폰트 머티리얼 오염 위험 → Play 에서만
        if (tmpOnTopShader == null) tmpOnTopShader = Shader.Find("TextMeshPro/Distance Field AlwaysOnTop");
        if (tmpOnTopShader == null) return;
        SetZTest(comboText); SetZTest(speedText); SetZTest(itemText);
        foreach (var lbl in tickLabels) SetZTest(lbl);
    }

    private void SetZTest(TMP_Text t)
    {
        if (t == null) return;
        SetZTestMat(t.fontMaterial);
        foreach (var sm in t.GetComponentsInChildren<TMP_SubMeshUI>(true))
            SetZTestMat(sm.material);
    }

    private void SetZTestMat(Material m)
    {
        if (m == null) return;
        if (m.shader != tmpOnTopShader) m.shader = tmpOnTopShader; // ZTest Always 변형으로 교체(속성 유지)
        m.renderQueue = renderQueue;
        m.hideFlags = HideFlags.DontSave;
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
            if (showStandingTicks) { PlaceTick(0, 0.55f, "P2"); PlaceTick(1, 0.72f, "P3"); HideTicksFrom(2); }
            else HideTicksFrom(0);
            return;
        }

        float p = 0f;
        if (raceManager != null && raceManager.FinishDistance > 0f && playerBoat != null)
            p = Mathf.Clamp01(playerBoat.DistanceTraveled / raceManager.FinishDistance);
        if (progressFill != null) progressFill.fillAmount = p;

        UpdateStandingTicks();

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

    // 다른 레이서들을 진행바 위 P# 작대기로 (RaceManager 순위 = 솔로 고스트/멀티 NetRacer 통합).
    private void UpdateStandingTicks()
    {
        if (!showStandingTicks || raceManager == null) { HideTicksFrom(0); return; }
        float finish = raceManager.FinishDistance;
        if (finish <= 0f) { HideTicksFrom(0); return; }

        int idx = 0;
        foreach (var s in raceManager.BuildStandings())
        {
            if (s.isPlayer) continue; // 나는 채움 막대로 표시
            PlaceTick(idx, Mathf.Clamp01(s.distance / finish), $"P{s.racerNumber}");
            idx++;
        }
        HideTicksFrom(idx);
    }

    private void PlaceTick(int i, float progress, string label)
    {
        EnsureTick(i);
        var bar = tickBars[i];
        var lbl = tickLabels[i];
        bar.gameObject.SetActive(true);
        lbl.gameObject.SetActive(true);

        float x = progress * barSize.x;
        bar.anchoredPosition = new Vector2(x, 0f);
        bar.sizeDelta = new Vector2(tickWidth, barSize.y);
        ((Image)bar.GetComponent<Graphic>()).color = tickColor;

        // 라벨은 작대기 번호 순으로 수직 스택 → X 가 겹쳐도 안 겹침.
        float rowH = tickLabelFontSize + 6f;
        lbl.rectTransform.anchoredPosition = new Vector2(x, barSize.y + 4f + i * rowH);
        lbl.fontSize = tickLabelFontSize;
        lbl.color = tickColor;
        lbl.text = label;
    }

    private void EnsureTick(int i)
    {
        while (tickBars.Count <= i)
        {
            var bar = NewImage($"Tick{tickBars.Count}", barBgRt, tickColor);
            var brt = bar.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.zero; brt.pivot = new Vector2(0.5f, 0f);
            if (uiOnTopShader == null) uiOnTopShader = Shader.Find("UI/AlwaysOnTop");
            if (uiOnTopShader != null) bar.material = new Material(uiOnTopShader) { renderQueue = renderQueue + 1, hideFlags = HideFlags.DontSave };
            tickBars.Add(brt);

            var lbl = NewText($"TickLabel{tickLabels.Count}", barBgRt, tickLabelFontSize, TextAlignmentOptions.Bottom, tickColor);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.zero; lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(120, 60);
            tickLabels.Add(lbl);
        }
    }

    private void HideTicksFrom(int idx)
    {
        for (int j = idx; j < tickBars.Count; j++)
        {
            if (tickBars[j] != null) tickBars[j].gameObject.SetActive(false);
            if (tickLabels[j] != null) tickLabels[j].gameObject.SetActive(false);
        }
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
