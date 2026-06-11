using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 레이스 종료 시 결과 패널을 코드로 생성해 표시(에디터 캔버스 세팅 불필요).
///   순위 목록 + 터치 버튼(컨트롤러로 닿으면 재시작/로비 이동).
///
/// 버튼은 XR UI 레이 대신 "손 거리 터치"(Striker)로 누른다 → 셋업 의존 적고 확실.
/// VR: World Space + layer=Default(카메라 렌더). 평소 숨김, RaceEnded 시 표시.
///
/// 붙이는 법: 빈 GameObject 에 추가 → raceManager / hands(좌우 Striker) 연결.
///   (선택) attachTo=Camera Offset, music(종료 시 정지).
///   ※ RaceManager 의 resultsSceneName 은 비워둘 것(이 패널이 인게임으로 처리하므로 씬 전환 X).
/// </summary>
public class RaceResultScreen : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private Striker[] hands;
    [Tooltip("종료 시 정지시킬 음악(선택).")]
    [SerializeField] private AudioSource music;

    [Header("배치")]
    [SerializeField] private Transform attachTo;
    [SerializeField] private float distance = 2f;
    [SerializeField] private float heightOffset = 0f;
    [SerializeField] private Vector2 panelSize = new Vector2(1400, 1000);
    [SerializeField] private float worldScale = 0.0012f;

    [Header("버튼")]
    [Tooltip("버튼 터치 인정 반경(m).")]
    [SerializeField] private float buttonTouchRadius = 0.18f;
    [Tooltip("이동할 씬. 비우면 현재 씬 재시작.")]
    [SerializeField] private string returnSceneName = "";
    [Tooltip("한글 쓰려면 font 에 한글 TMP 폰트 연결 필요(기본 폰트엔 한글 없음).")]
    [SerializeField] private string buttonText = "RESTART";
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.6f, 1f, 1f);
    [Tooltip("레이가 버튼을 가리킬 때 색(하이라이트).")]
    [SerializeField] private Color buttonHoverColor = new Color(0.5f, 0.85f, 1f, 1f);

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject root;
    private RectTransform crt;
    private TMP_Text titleText, standingsText, buttonLabel;
    private RectTransform buttonRt;
    private Image buttonImg;
    private bool shown;
    private static Sprite sWhite;

    private void Start()
    {
        Build();
        if (root != null) root.SetActive(false);
    }

    private void Update()
    {
        if (!shown)
        {
            if (raceManager != null && raceManager.RaceEnded) Show();
            return;
        }

        // 라이브 위치/크기 반영(인스펙터로 거리/높이/크기 조절 가능).
        if (crt != null)
        {
            crt.localPosition = new Vector3(0f, heightOffset, distance);
            crt.localScale = Vector3.one * worldScale;
            crt.sizeDelta = panelSize;
        }

        if (buttonRt == null || hands == null) return;

        bool hovering = false;
        foreach (var hand in hands)
        {
            if (hand == null) continue;

            // 가까우면 그냥 터치.
            if (Vector3.Distance(hand.WorldPosition, buttonRt.position) <= buttonTouchRadius)
            {
                Proceed();
                return;
            }
            // 멀면 레이로 가리키고 트리거 당기면 클릭.
            if (PointingAtButton(hand))
            {
                hovering = true;
                if (hand.TriggerHeld) { Proceed(); return; }
            }
        }

        if (buttonImg != null)
            buttonImg.color = hovering ? buttonHoverColor : buttonColor;
    }

    // 컨트롤러 정면 레이가 버튼을 가리키나(캔버스 평면 교차 + 버튼 rect 내).
    private bool PointingAtButton(Striker hand)
    {
        if (root == null) return false;
        Vector3 n = root.transform.forward; // 캔버스 법선
        Vector3 o = hand.WorldPosition;
        Vector3 d = hand.Forward;
        float denom = Vector3.Dot(d, n);
        if (Mathf.Abs(denom) < 1e-5f) return false;
        float t = Vector3.Dot(buttonRt.position - o, n) / denom;
        if (t < 0f) return false; // 뒤쪽
        Vector3 hit = o + d * t;
        Vector2 local = buttonRt.InverseTransformPoint(hit);
        return buttonRt.rect.Contains(local);
    }

    private void Show()
    {
        shown = true;
        if (root != null) root.SetActive(true);
        if (music != null) music.Stop();

        int place = RaceResult.PlayerPlace;
        titleText.text = place > 0 ? $"RANK {place}" : "RESULT";

        var sb = new StringBuilder();
        if (RaceResult.Standings != null)
            foreach (var s in RaceResult.Standings)
                sb.AppendLine($"{s.place}.  {s.name}{(s.isPlayer ? " (YOU)" : "")}   {s.distance:F0}m");
        standingsText.text = sb.ToString();
    }

    private void Proceed()
    {
        Time.timeScale = 1f;

        // 멀티: 세션 정리 후 재시작 — NGO 가 살아있는 채 씬을 리로드하면 잔존 상태로 꼬임.
        var connector = FindFirstObjectByType<SessionConnector>();
        if (connector != null) connector.Disconnect();
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();

        if (!string.IsNullOrEmpty(returnSceneName) && Application.CanStreamedLevelBeLoaded(returnSceneName))
            SceneManager.LoadScene(returnSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // 현재 씬 재시작
    }

    // ── 패널 생성 ──
    private void Build()
    {
        Transform parent = attachTo != null ? attachTo
                         : (Camera.main != null ? Camera.main.transform : null);

        var go = new GameObject("RaceResult", typeof(RectTransform), typeof(Canvas));
        go.layer = 0;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        root = go;

        crt = (RectTransform)go.transform;
        crt.sizeDelta = panelSize;
        if (parent != null) crt.SetParent(parent, false);
        crt.localScale = Vector3.one * worldScale;
        crt.localPosition = new Vector3(0f, heightOffset, distance);
        crt.localRotation = Quaternion.identity;

        var bg = NewImage("Bg", crt, new Color(0f, 0f, 0f, 0.7f));
        var brt = bg.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        titleText = NewText("Title", crt, 130f);
        Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1200, 200));

        standingsText = NewText("Standings", crt, 64f);
        Place(standingsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 520));

        buttonImg = NewImage("Button", crt, buttonColor);
        buttonRt = buttonImg.rectTransform;
        Place(buttonRt, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(620, 170));
        buttonLabel = NewText("ButtonLabel", buttonRt, 60f);
        var lrt = buttonLabel.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        buttonLabel.text = buttonText;
    }

    private TMP_Text NewText(string name, Transform parent, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 0;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : TMP_Settings.defaultFontAsset;
        t.fontSize = fontSize;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
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

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
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
