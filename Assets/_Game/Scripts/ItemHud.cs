using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 아이템 체감 HUD(코드 생성 월드 UI). 활성 효과(부스트/무적/자석) 상태 + 발동 배너 + 폭탄 경고.
/// 표현 only — 상태는 SpeedController/ProtoNoteSpawner 폴링 + 발동 이벤트 구독.
/// 붙이는 법: 빈 GameObject → speedController/noteSpawner/itemSystem/attachTo(Camera Offset) 연결.
/// </summary>
public class ItemHud : MonoBehaviour
{
    [SerializeField] private SpeedController speedController;
    [SerializeField] private ProtoNoteSpawner noteSpawner;
    [SerializeField] private ProtoItemSystem itemSystem;

    [Header("배치")]
    [SerializeField] private Transform attachTo;
    [SerializeField] private float distance = 1.6f;
    [SerializeField] private float heightOffset = 0.35f;
    [SerializeField] private Vector2 panelSize = new Vector2(900, 300);
    [SerializeField] private float worldScale = 0.0012f;
    [SerializeField] private float bannerSeconds = 1.5f;
    [SerializeField] private TMP_FontAsset font;

    private GameObject root;
    private RectTransform crt;
    private TMP_Text statusText, bannerText, warnText;
    private float bannerTimer;

    private void Start()
    {
        Build();
        if (itemSystem != null) itemSystem.OnItemActivated += OnActivated;
    }

    private void OnDestroy()
    {
        if (itemSystem != null) itemSystem.OnItemActivated -= OnActivated;
    }

    private void OnActivated(ItemType type, float duration)
    {
        if (bannerText != null) bannerText.text = $"{KorName(type)} 발동!";
        bannerTimer = bannerSeconds;
    }

    private void Update()
    {
        if (crt == null) return;
        crt.localPosition = new Vector3(0f, heightOffset, distance);
        crt.localScale = Vector3.one * worldScale;

        // 활성 효과.
        var active = new List<string>();
        if (speedController != null && speedController.Invincible) active.Add("무적");
        if (speedController != null && speedController.BoostActive) active.Add("부스트");
        if (noteSpawner != null && noteSpawner.MagnetActive) active.Add("자석");
        statusText.text = active.Count > 0 ? string.Join("  ", active) : "";

        // 발동 배너.
        if (bannerTimer > 0f) bannerTimer -= Time.deltaTime;
        bannerText.gameObject.SetActive(bannerTimer > 0f);

        // 폭탄 경고 — 레인에 BombHazard 존재 시.
        bool bombIncoming = FindObjectsByType<BombHazard>(FindObjectsSortMode.None).Length > 0;
        warnText.gameObject.SetActive(bombIncoming);
        warnText.text = bombIncoming ? "⚠ 폭탄! 피해!" : "";
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

    private void Build()
    {
        Transform parent = attachTo != null ? attachTo
                         : (Camera.main != null ? Camera.main.transform : null);
        var go = new GameObject("ItemHud", typeof(RectTransform), typeof(Canvas));
        go.layer = 0;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        root = go;
        crt = (RectTransform)go.transform;
        crt.sizeDelta = panelSize;
        if (parent != null) crt.SetParent(parent, false);
        crt.localRotation = Quaternion.identity;

        statusText = NewText("Status", 56f, new Vector2(0.5f, 0f), new Vector2(0, 20), Color.cyan);
        bannerText = NewText("Banner", 72f, new Vector2(0.5f, 0.5f), new Vector2(0, 60), Color.yellow);
        warnText = NewText("Warn", 64f, new Vector2(0.5f, 1f), new Vector2(0, -20), new Color(1f, 0.3f, 0.2f));
        bannerText.gameObject.SetActive(false);
        warnText.gameObject.SetActive(false);
    }

    private TMP_Text NewText(string name, float size, Vector2 anchor, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.transform.SetParent(crt, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : TMP_Settings.defaultFontAsset;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        var rt = t.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.sizeDelta = new Vector2(880, 120); rt.anchoredPosition = pos;
        return t;
    }
}
