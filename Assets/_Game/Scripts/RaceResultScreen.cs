using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 레이스 종료 시 결과 화면(Ranking 디자인 프리팹) 표시 — 2026-06-16 PlaySceneUI 아트로 교체.
///   순위 4행(메달/아바타/이름/점수) + 기능용 재시작 버튼(컨트롤러 손 터치/레이로 누름).
///
/// 이전(코드 생성 흰 박스) → 사용자가 Ranking 스프라이트로 조립한 프리팹의 요소를 참조해 채움.
/// 표현 only — 데이터는 RaceResult.Standings / RaceManager.RaceEnded 에서 읽기만.
/// VR: World Space, 버튼은 XR UI 레이 대신 "손 거리 터치/정면 레이"(Striker)로 누름(셋업 의존 적고 확실).
///
/// 붙이는 법: 조립한 결과 프리팹 루트(World Space Canvas)에 이 컴포넌트
///   → raceManager / hands(좌우 Striker) / panelRoot / rows(4) / restartButton(+Image) 연결.
///   (선택) music(종료 시 정지). RaceManager.resultsSceneName 은 비워둘 것(인게임 처리, 씬 전환 X).
/// </summary>
public class RaceResultScreen : MonoBehaviour
{
    /// 한 순위 행 — 사용자가 프리팹에서 행별 요소 연결. (메달/아바타 스프라이트는 행마다 고정이라 코드 비관여)
    [System.Serializable]
    public class ResultRow
    {
        [Tooltip("이 행 전체 루트. 레이서 수보다 많은 행은 숨김.")]
        public GameObject root;
        public TMP_Text nameText;
        public TMP_Text scoreText;
        [Tooltip("내(플레이어) 행일 때만 켜는 강조 표시(선택).")]
        public GameObject youMarker;
    }

    [Header("데이터")]
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private Striker[] hands;
    [Tooltip("종료 시 정지시킬 음악(선택).")]
    [SerializeField] private AudioSource music;

    [Header("결과 패널 (조립한 프리팹)")]
    [Tooltip("평소 숨김, RaceEnded 시 표시할 루트.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("순위 행 — 등수 순서대로(0=1등). 보통 4개.")]
    [SerializeField] private ResultRow[] rows = new ResultRow[4];

    [Header("재시작 버튼 (기능용 — Ranking 아트엔 없음)")]
    [SerializeField] private RectTransform restartButton;
    [Tooltip("hover/터치 색 피드백용(선택).")]
    [SerializeField] private Image restartButtonImage;
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color buttonHoverColor = new Color(0.5f, 0.85f, 1f, 1f);
    [Tooltip("버튼 터치 인정 반경(m).")]
    [SerializeField] private float buttonTouchRadius = 0.18f;
    [Tooltip("이동할 씬. 비우면 현재 씬 재시작.")]
    [SerializeField] private string returnSceneName = "";

    [Header("항상 위에 (VR 가독성, Play 모드 한정)")]
    [SerializeField] private bool alwaysOnTop = true;
    [SerializeField] private int renderQueue = 5000;

    private bool shown;

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (!shown)
        {
            if (raceManager != null && raceManager.RaceEnded) Show();
            return;
        }

        if (alwaysOnTop) ApplyTextAlwaysOnTop(); // TMP 는 매 프레임 재적용
        HandleButton();
    }

    private void Show()
    {
        shown = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (music != null) music.Stop();
        if (alwaysOnTop) ApplyImagesAlwaysOnTop();

        FillRows();
    }

    private void FillRows()
    {
        if (rows == null) return;
        var standings = RaceResult.Standings;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null) continue;

            // 이 행(i) = 등수 i+1 인 레이서.
            RaceStanding s = null;
            if (standings != null)
                foreach (var x in standings)
                    if (x.place == i + 1) { s = x; break; }

            bool has = s != null;
            if (row.root != null) row.root.SetActive(has);
            if (!has) continue;

            if (row.nameText != null)
                row.nameText.text = $"P{s.racerNumber}" + (s.isPlayer ? " (YOU)" : "");
            if (row.scoreText != null)
                row.scoreText.text = $"{s.distance:F0}m";
            if (row.youMarker != null)
                row.youMarker.SetActive(s.isPlayer);
        }
    }

    // ── 버튼: 손 거리 터치 or 정면 레이+트리거 ──
    private void HandleButton()
    {
        if (restartButton == null || hands == null) return;

        bool hovering = false;
        foreach (var hand in hands)
        {
            if (hand == null) continue;

            // 가까우면 그냥 터치.
            if (Vector3.Distance(hand.WorldPosition, restartButton.position) <= buttonTouchRadius)
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

        if (restartButtonImage != null)
            restartButtonImage.color = hovering ? buttonHoverColor : buttonColor;
    }

    // 컨트롤러 정면 레이가 버튼을 가리키나(버튼 평면 교차 + rect 내).
    private bool PointingAtButton(Striker hand)
    {
        Vector3 n = restartButton.forward; // 버튼(캔버스) 법선
        Vector3 o = hand.WorldPosition;
        Vector3 d = hand.Forward;
        float denom = Vector3.Dot(d, n);
        if (Mathf.Abs(denom) < 1e-5f) return false;
        float t = Vector3.Dot(restartButton.position - o, n) / denom;
        if (t < 0f) return false; // 뒤쪽
        Vector3 hit = o + d * t;
        Vector2 local = restartButton.InverseTransformPoint(hit);
        return restartButton.rect.Contains(local);
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

    // ── 항상-위 (검증된 셰이더 교체, Play 모드 한정) ──
    private static Shader uiOnTopShader;
    private static Shader tmpOnTopShader;

    private void ApplyImagesAlwaysOnTop()
    {
        if (!Application.isPlaying || panelRoot == null) return;
        if (uiOnTopShader == null) uiOnTopShader = Shader.Find("UI/AlwaysOnTop");
        if (uiOnTopShader == null) return;
        foreach (var g in panelRoot.GetComponentsInChildren<Graphic>(true))
        {
            if (g is TMP_Text) continue;
            if (g is Image || g is RawImage)
                g.material = new Material(uiOnTopShader) { renderQueue = renderQueue, hideFlags = HideFlags.DontSave };
        }
    }

    private void ApplyTextAlwaysOnTop()
    {
        if (!Application.isPlaying || panelRoot == null) return;
        if (tmpOnTopShader == null) tmpOnTopShader = Shader.Find("TextMeshPro/Distance Field AlwaysOnTop");
        if (tmpOnTopShader == null) return;
        foreach (var t in panelRoot.GetComponentsInChildren<TMP_Text>(true))
            SetZTest(t);
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
        if (m.shader != tmpOnTopShader) m.shader = tmpOnTopShader;
        m.renderQueue = renderQueue;
        m.hideFlags = HideFlags.DontSave;
    }
}
