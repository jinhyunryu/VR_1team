using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 레이스 결과 화면(Ranking 디자인 프리팹) — 점진적 등장(2026-06-16).
///   1) 레이싱 중엔 숨김
///   2) 로컬 플레이어 완주 순간 → 패널 등장
///   3) 각 레이서 완주할 때마다 → 해당 Row 가 아래에서 제자리로 슬라이드업(완주=등수 순서, 위 슬롯부터)
///   4) 전원 완주(RaceEnded) + 마지막 Row 안착 → Restart 버튼 등장
///
/// 표현 only — 데이터는 RaceManager.BuildStandings(이미 완주 순서 정렬, finished 플래그) 에서 읽기만.
/// VR: 버튼은 손 거리 터치/정면 레이(Striker)로 누름. 애니메이션은 코루틴(DOTween 의존 없음).
///
/// 붙이는 법: 항상-켜진 오브젝트에 추가(패널이 꺼져도 완주 감지해야 함)
///   → raceManager / hands / panelRoot(평소 숨김) / rows(완주순=위부터) / restartButton(+Image) 연결.
/// </summary>
public class RaceResultScreen : MonoBehaviour
{
    /// 한 순위 행 — 사용자가 프리팹에서 행별 요소 연결. (메달/아바타 스프라이트는 행마다 고정이라 코드 비관여)
    [System.Serializable]
    public class ResultRow
    {
        [Tooltip("이 행 전체 루트. 평소 숨김, 해당 등수 완주 시 슬라이드업으로 등장.")]
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
    [Tooltip("평소 숨김, 로컬 플레이어 완주 시 표시할 루트.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("순위 행 — 위(0)=1등. 완주 순서대로 위 슬롯부터 채워짐.")]
    [SerializeField] private ResultRow[] rows = new ResultRow[4];

    [Header("등장 애니메이션")]
    [Tooltip("행이 아래에서 제자리로 올라오는 시간(초).")]
    [SerializeField] private float slideDuration = 0.4f;
    [Tooltip("행이 시작하는 아래쪽 거리(패널 단위). 클수록 더 아래에서 올라옴.")]
    [SerializeField] private float slideOffsetY = 400f;
    [Tooltip("행과 행 사이 등장 텀(초).")]
    [SerializeField] private float slideStagger = 0.15f;

    [Header("재시작 버튼 (전원 완주 후 등장)")]
    [SerializeField] private RectTransform restartButton;
    [Tooltip("hover/터치 색 피드백용(선택).")]
    [SerializeField] private Image restartButtonImage;
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color buttonHoverColor = new Color(0.5f, 0.85f, 1f, 1f);
    [Tooltip("버튼 터치 인정 반경(m).")]
    [SerializeField] private float buttonTouchRadius = 0.18f;
    [Tooltip("리스타트 시 돌아갈 로비(타이틀) 씬. 멀티는 세션 유지한 채 전원 이동. 비우면 현재 씬 리로드.")]
    [SerializeField] private string returnSceneName = "VR_Title_Lobby";

    [Header("항상 위에 (VR 가독성, Play 모드 한정)")]
    [SerializeField] private bool alwaysOnTop = true;
    [SerializeField] private int renderQueue = 5000;

    private bool shown;
    private bool restartReady;
    private readonly List<int> finishOrder = new();      // 완주 순서(racerNumber)
    private readonly HashSet<int> finishedSet = new();
    private int revealedCount;
    private Vector2[] rowTargets;

    private void Start()
    {
        CaptureAndHideRows();
        if (panelRoot != null) panelRoot.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(false);
    }

    // 각 행의 제자리(앵커 위치) 기록 후 숨김. (패널이 꺼져 있어도 RectTransform 값은 읽힘)
    private void CaptureAndHideRows()
    {
        if (rows == null) return;
        rowTargets = new Vector2[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null || row.root == null) continue;
            var rt = row.root.transform as RectTransform;
            rowTargets[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
            row.root.SetActive(false);
            if (row.youMarker != null) row.youMarker.SetActive(false);
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        var dbgKb = UnityEngine.InputSystem.Keyboard.current;
        if (dbgKb != null && dbgKb.oKey.wasPressedThisFrame) DebugForceShow();
#endif
        if (raceManager == null) return;

        TrackFinishers();

        if (!shown)
        {
            if (LocalPlayerFinished()) ShowPanel();
            return;
        }

        if (alwaysOnTop) ApplyTextAlwaysOnTop();
        if (restartReady) HandleButton();
    }

    // BuildStandings 는 이미 완주 순서 정렬 → 새로 finished 된 레이서를 순서대로 누적.
    private void TrackFinishers()
    {
        var standings = raceManager.BuildStandings();
        if (standings == null) return;
        foreach (var s in standings)
        {
            if (s == null || !s.finished) continue;
            if (finishedSet.Add(s.racerNumber)) finishOrder.Add(s.racerNumber);
        }
    }

    private bool LocalPlayerFinished()
    {
        var standings = raceManager.BuildStandings();
        if (standings == null) return false;
        foreach (var s in standings)
            if (s != null && s.isPlayer && s.finished) return true;
        return false;
    }

    private void ShowPanel()
    {
        shown = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (music != null) music.Stop();
        if (alwaysOnTop) ApplyImagesAlwaysOnTop();
        StartCoroutine(RevealLoop());
    }

    // 완주 순서대로 행을 하나씩 슬라이드인. 전원 완주 + 모두 등장하면 Restart.
    private IEnumerator RevealLoop()
    {
        while (true)
        {
            bool morePending = rows != null && revealedCount < finishOrder.Count && revealedCount < rows.Length;
            if (morePending)
            {
                int racerNum = finishOrder[revealedCount];
                yield return AnimateRowIn(revealedCount, racerNum);
                revealedCount++;
                if (slideStagger > 0f) yield return new WaitForSeconds(slideStagger);
                continue;
            }

            // 더 등장할 행이 없는 시점. 전원 완주 + 모든 레이서 행이 다 안착해야만 Restart.
            // (RaceEnded 가 마지막 완주자 finishOrder 누적보다 먼저 켜질 수 있어, 행 수로 게이트)
            int totalToReveal = Mathf.Min(raceManager.RacerCount(), rows != null ? rows.Length : 0);
            if (raceManager.RaceEnded && revealedCount >= totalToReveal && revealedCount > 0)
            {
                if (!restartReady) ShowRestart();
                yield break;
            }
            yield return null; // 다음 완주자 또는 종료 대기
        }
    }

    private IEnumerator AnimateRowIn(int rowIdx, int racerNum)
    {
        var row = rows[rowIdx];
        if (row == null || row.root == null) yield break;
        FillRow(row, racerNum);

        var rt = row.root.transform as RectTransform;
        Vector2 target = (rowTargets != null && rowIdx < rowTargets.Length)
            ? rowTargets[rowIdx]
            : (rt != null ? rt.anchoredPosition : Vector2.zero);

        row.root.SetActive(true);
        if (rt == null) yield break;

        Vector2 start = target + new Vector2(0f, -Mathf.Abs(slideOffsetY));
        float dur = Mathf.Max(0.01f, slideDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            rt.anchoredPosition = Vector2.Lerp(start, target, k);
            yield return null;
        }
        rt.anchoredPosition = target;
    }

    private void FillRow(ResultRow row, int racerNum)
    {
        var s = FindStanding(racerNum);
        if (row.nameText != null)
            row.nameText.text = $"P{racerNum}" + (s != null && s.isPlayer ? " (YOU)" : "");
        if (row.scoreText != null)
            row.scoreText.text = (s != null && s.finished) ? FormatFinishTime(s.finishTime) : "—";
        if (row.youMarker != null) row.youMarker.SetActive(s != null && s.isPlayer);
    }

    // 완주 시간 → "분:초.센티초" (예: 1:23.45 / 0:47.30). 결과창 Row 우측 표시.
    private static string FormatFinishTime(float seconds)
    {
        if (seconds <= 0f) return "—";
        int m = (int)(seconds / 60f);
        float s = seconds - m * 60f;
        return string.Format("{0}:{1:00.00}", m, s);
    }

    private RaceStanding FindStanding(int racerNum)
    {
        var standings = raceManager != null ? raceManager.BuildStandings() : null;
        if (standings == null) return null;
        foreach (var s in standings)
            if (s != null && s.racerNumber == racerNum) return s;
        return null;
    }

    private void ShowRestart()
    {
        restartReady = true;
        if (restartButton != null) restartButton.gameObject.SetActive(true);
    }

    // ── 버튼: 손 거리 터치 or 정면 레이+트리거 (Restart 등장 후에만 처리) ──
    private void HandleButton()
    {
        if (restartButton == null || hands == null) return;

        bool hovering = false;
        foreach (var hand in hands)
        {
            if (hand == null) continue;

            if (Vector3.Distance(hand.WorldPosition, restartButton.position) <= buttonTouchRadius)
            {
                Proceed();
                return;
            }
            if (PointingAtButton(hand))
            {
                hovering = true;
                if (hand.TriggerHeld) { Proceed(); return; }
            }
        }

        if (restartButtonImage != null)
            restartButtonImage.color = hovering ? buttonHoverColor : buttonColor;
    }

    private bool PointingAtButton(Striker hand)
    {
        Vector3 n = restartButton.forward;
        Vector3 o = hand.WorldPosition;
        Vector3 d = hand.Forward;
        float denom = Vector3.Dot(d, n);
        if (Mathf.Abs(denom) < 1e-5f) return false;
        float t = Vector3.Dot(restartButton.position - o, n) / denom;
        if (t < 0f) return false;
        Vector3 hit = o + d * t;
        Vector2 local = restartButton.InverseTransformPoint(hit);
        return restartButton.rect.Contains(local);
    }

    private void Proceed()
    {
        Time.timeScale = 1f;

        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            // 멀티: 세션 유지(끊지 않음) → 서버가 전원 로비로 이동. 도착하면 bridge 가 로비 화면 표시.
            //   (클라는 서버 씬 전환을 자동으로 따라오므로 여기선 아무것도 안 함.)
            if (nm.IsServer)
            {
                // 옛 AI 디스폰 + 상태 리셋 (안 하면 다음 라운드에 이월돼 앞서 출발한 채 보임).
                var coord = FindFirstObjectByType<NetRaceCoordinator>();
                if (coord != null) coord.ResetForRestart();

                if (nm.SceneManager != null && !string.IsNullOrEmpty(returnSceneName))
                    nm.SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
            }
            return;
        }

        // 세션 없음(순수 싱글) — 로비 씬으로 일반 로드, 없으면 현재 씬 리로드.
        if (!string.IsNullOrEmpty(returnSceneName) && Application.CanStreamedLevelBeLoaded(returnSceneName))
            SceneManager.LoadScene(returnSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

#if UNITY_EDITOR
    // 디버그(에디터 전용): O 키로 완주 없이 결과창 강제 표시 — 레이아웃/연결/애니메이션 격리 테스트.
    private void DebugForceShow()
    {
        if (shown) return;
        shown = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (alwaysOnTop) ApplyImagesAlwaysOnTop();
        finishOrder.Clear(); finishedSet.Clear();
        int n = rows != null ? rows.Length : 0;
        for (int i = 0; i < n; i++) { finishOrder.Add(i + 1); finishedSet.Add(i + 1); }
        StartCoroutine(DebugRevealAll());
        Debug.Log("[RaceResultScreen] 디버그 강제 표시 (O)");
    }

    private IEnumerator DebugRevealAll()
    {
        int n = rows != null ? rows.Length : 0;
        for (int i = 0; i < n; i++)
        {
            yield return AnimateRowIn(i, i + 1);
            revealedCount++;
            if (slideStagger > 0f) yield return new WaitForSeconds(slideStagger);
        }
        ShowRestart();
    }
#endif

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
