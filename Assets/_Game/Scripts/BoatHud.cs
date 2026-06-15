using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보트 부착 대시보드 HUD(코드 생성 월드 캔버스). 머리고정 RuntimeHud/ItemHud 대체.
///   좌하단: 진행도 바   우하단: 콤보 + 속도   우상단: 활성 아이템(이름 + 남은 시간) + 발동 플래시
/// 보트의 한 지점(attachTo)에 parent → 보트와 함께 움직임. 표현 only(로직 비의존).
///
/// ★Play 중 인스펙터 라이브 튜닝★ — 매 프레임 위치/크기/기울기 재적용.
/// 붙이는 법: 빈 GameObject 에 추가 → speedController/playerBoat/raceManager/noteSpawner/itemSystem 연결.
///   attachTo = 보트 대시보드 지점(빈 GameObject). 비우면 playerBoat 기준 자동 배치.
/// </summary>
public class BoatHud : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private SpeedController speedController;
    [SerializeField] private BoatMover playerBoat;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private ProtoNoteSpawner noteSpawner;
    [SerializeField] private ProtoItemSystem itemSystem;

    [Header("배치 (보트 부착)")]
    [Tooltip("보트의 대시보드 지점. 비우면 playerBoat 기준.")]
    [SerializeField] private Transform attachTo;
    [SerializeField] private float distance = 0.9f;
    [SerializeField] private float heightOffset = -0.1f;
    [SerializeField] private float horizontalOffset = 0f;
    [Tooltip("플레이어 쪽으로 기울이는 각도(X). 양수=윗변이 플레이어로.")]
    [SerializeField] private float tiltX = 35f;
    [SerializeField] private Vector2 panelSize = new Vector2(1200, 700);
    [SerializeField] private float worldScale = 0.0011f;

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

    private RectTransform canvasRt, barBgRt, comboRt, itemRt;
    private Image barBg, progressFill;
    private TMP_Text comboText, speedText, itemText;
    private static Sprite sWhite;

    // 아이템 상태.
    private ItemType activeItem;
    private float itemActiveTimer;     // 지속형(부스트/무적/자석) 남은 시간
    private float flashTimer;          // 발동 플래시
    private ItemType flashItem;

    private void Start()
    {
        Build();
        ApplyLayout();
        if (itemSystem != null) itemSystem.OnItemActivated += OnItemActivated;
    }

    private void OnDestroy()
    {
        if (itemSystem != null) itemSystem.OnItemActivated -= OnItemActivated;
    }

    private void OnItemActivated(ItemType type, float duration)
    {
        flashItem = type;
        flashTimer = flashSeconds;
        if (duration > 0f) { activeItem = type; itemActiveTimer = duration; } // 지속형만 카운트다운
    }

    private void Update()
    {
        ApplyLayout();
        ApplyData();
    }

    // ── 1회 생성 ──
    private void Build()
    {
        Transform parent = attachTo != null ? attachTo
                         : (playerBoat != null ? playerBoat.transform
                         : (Camera.main != null ? Camera.main.transform : null));

        var go = new GameObject("BoatHud", typeof(RectTransform), typeof(Canvas));
        go.layer = 0;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        canvasRt = (RectTransform)go.transform;
        if (parent != null) canvasRt.SetParent(parent, false);

        // 진행도 바 (좌하단).
        barBg = NewImage("ProgressBg", canvasRt, barBgColor);
        barBgRt = barBg.rectTransform;
        progressFill = NewImage("ProgressFill", barBgRt, barColor);
        var frt = progressFill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;

        // 콤보 + 속도 (우하단).
        comboText = NewText("Combo", canvasRt, comboFontSize, TextAlignmentOptions.BottomRight, textColor);
        comboRt = comboText.rectTransform;
        speedText = NewText("Speed", canvasRt, speedFontSize, TextAlignmentOptions.BottomRight, textColor);

        // 아이템 (우상단).
        itemText = NewText("Item", canvasRt, itemFontSize, TextAlignmentOptions.TopRight, itemColor);
        itemRt = itemText.rectTransform;
    }

    // ── 매 프레임 레이아웃 (라이브 튜닝) ──
    private void ApplyLayout()
    {
        if (canvasRt == null) return;
        canvasRt.localScale = Vector3.one * worldScale;
        canvasRt.localPosition = new Vector3(horizontalOffset, heightOffset, distance);
        canvasRt.localRotation = Quaternion.Euler(tiltX, 0f, 0f);
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

    // ── 데이터 갱신 ──
    private void ApplyData()
    {
        // 진행도.
        float p = 0f;
        if (raceManager != null && raceManager.FinishDistance > 0f && playerBoat != null)
            p = Mathf.Clamp01(playerBoat.DistanceTraveled / raceManager.FinishDistance);
        if (progressFill != null) progressFill.fillAmount = p;

        // 콤보 + 속도.
        if (speedController != null)
        {
            comboText.text = $"COMBO {speedController.Combo}";
            float spd = playerBoat != null ? playerBoat.Speed : speedController.CurrentTargetSpeed;
            speedText.text = $"{spd:0} m/s";
        }

        // 아이템 (우상단): 지속형 카운트다운 우선, 없으면 발동 플래시.
        if (itemActiveTimer > 0f) itemActiveTimer -= Time.deltaTime;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        if (itemActiveTimer > 0f)
            itemText.text = $"{KorName(activeItem)}  {Mathf.CeilToInt(itemActiveTimer)}s";
        else if (flashTimer > 0f)
            itemText.text = $"{KorName(flashItem)}!";
        else
            itemText.text = "";
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
