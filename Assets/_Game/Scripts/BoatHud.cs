using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보트 부착 대시보드 HUD (디자인 프리팹 구동판 — 2026-06-16 PlaySceneUI 아트로 교체).
///   좌하단: 진행도 바(채움 + 자기 보트 마커 + 다른 레이서 막대)
///   우하단: 콤보 + 속도   우상단: 활성 아이템(이름 + 남은시간)
///
/// 이전(코드 생성 흰 박스) → 사용자가 PlaySceneUI 스프라이트로 조립한 프리팹의 시각 요소를 참조해 값만 주입.
/// 표현 only(로직 비의존) — 데이터는 SpeedController/BoatMover/RaceManager/ProtoItemSystem 에서 읽기만.
/// VR 가독성: 항상-위 셰이더(배/지형 위에 그림), Play 모드 한정(Edit 모드 공유 폰트 머티리얼 오염 방지).
///
/// 붙이는 법: 조립한 HUD 프리팹 루트(월드공간 Canvas, 보트 자식)에 이 컴포넌트
///   → 데이터 4종 + 시각 요소(comboText/speedText/distanceFill/distanceTrack/selfMarker/otherMarkers/itemRoot/itemText) 연결.
/// </summary>
[DisallowMultipleComponent]
public class BoatHud : MonoBehaviour
{
    [Header("데이터 (로직 소스 — 읽기만)")]
    [SerializeField] private SpeedController speedController;
    [SerializeField] private BoatMover playerBoat;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private ProtoItemSystem itemSystem;

    [Header("표시 타이밍 (레이스 시작 ~ 완주 전까지만)")]
    [Tooltip("HUD 전체 루트(HudCanvas). 레이스 시작 시 켜고, 로컬 플레이어 완주 시 끔. 비우면 항상 표시.")]
    [SerializeField] private GameObject hudRoot;
    [Tooltip("레이스 시작 신호(RaceStarted). 비우면 시작 게이트 없이 항상 시작된 것으로 간주.")]
    [SerializeField] private NetRaceCoordinator raceCoordinator;

    [Header("콤보 / 속도 (우하단)")]
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private string comboFormat = "{0}";
    [SerializeField] private string speedFormat = "{0:0} m/s";

    [Header("진행도 바 (좌하단)")]
    [Tooltip("자기 진행 채움. Image Type=Filled, Fill Method=Horizontal.")]
    [SerializeField] private Image distanceFill;
    [Tooltip("마커 x 위치 계산 기준 트랙(폭 사용). 보통 BG/채움의 RectTransform.")]
    [SerializeField] private RectTransform distanceTrack;
    [Tooltip("자기 보트 마커 — 채움 끝(진행률 위치)으로 이동. 트랙의 왼쪽 기준 앵커 권장.")]
    [SerializeField] private RectTransform selfMarker;
    [Tooltip("다른 레이서 막대(최대 3). 진행률 위치로 이동, 인원 적으면 자동 비활성.")]
    [SerializeField] private RectTransform[] otherMarkers = new RectTransform[3];
    [Tooltip("마커가 0%(시작)일 때 x — Track 로컬, 왼쪽 끝 기준. 시작 아이콘 안쪽 여백.")]
    [SerializeField] private float markerStartX = 0f;
    [Tooltip("마커가 100%(결승)일 때 x. 0이면 트랙 폭(rect.width) 사용.")]
    [SerializeField] private float markerEndX = 0f;

    [Header("아이템 (우상단)")]
    [Tooltip("아이템 발동 시에만 표시할 슬롯 루트.")]
    [SerializeField] private GameObject itemRoot;
    [SerializeField] private TMP_Text itemText;
    [SerializeField] private float flashSeconds = 1.5f;

    [Header("항상 위에 (VR 가독성, Play 모드 한정)")]
    [Tooltip("켜면 ZTest Always — HUD 가 배/지형 위에 그려짐.")]
    [SerializeField] private bool alwaysOnTop = true;
    [Tooltip("렌더 큐(클수록 위에). Overlay(4000)보다 크게.")]
    [SerializeField] private int renderQueue = 5000;

    private ItemType activeItem;
    private float itemActiveTimer;
    private float flashTimer;
    private ItemType flashItem;
    private bool subscribed;
    private bool lastHudActive = true;

    private void OnEnable()
    {
        if (itemSystem != null && !subscribed)
        {
            itemSystem.OnItemActivated += OnItemActivated;
            subscribed = true;
        }
        if (itemRoot != null) itemRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (itemSystem != null && subscribed)
        {
            itemSystem.OnItemActivated -= OnItemActivated;
            subscribed = false;
        }
    }

    private void Start()
    {
        if (alwaysOnTop) ApplyImagesAlwaysOnTop();
        // 안전장치: hudRoot 가 BoatHud 자신이면 끄는 순간 Update 가 멈춰 영영 못 켬 → 게이팅 무효.
        if (hudRoot == gameObject)
        {
            Debug.LogWarning("[BoatHud] Hud Root 가 BoatHud 자신으로 연결됨 — 자식 HudCanvas 로 바꾸세요. 게이팅 끕니다.");
            hudRoot = null;
        }
        // 레이스 시작 전엔 숨김 (게이팅). hudRoot 미연결 시 항상 표시.
        if (hudRoot != null) { hudRoot.SetActive(false); lastHudActive = false; }
    }

    private void OnItemActivated(ItemType type, float duration)
    {
        flashItem = type;
        flashTimer = flashSeconds;
        if (duration > 0f) { activeItem = type; itemActiveTimer = duration; }
    }

    private void Update()
    {
        // 표시 게이팅: 레이스 시작 후 ~ 로컬 플레이어 완주 전까지만.
        bool active = HudShouldBeActive();
        if (hudRoot != null && active != lastHudActive)
        {
            hudRoot.SetActive(active);
            lastHudActive = active;
        }
        if (!active) return;

        ApplyComboSpeed();
        ApplyDistance();
        ApplyItem();
        if (alwaysOnTop) ApplyTextAlwaysOnTop(); // TMP 는 머티리얼 재생성하므로 매 프레임
    }

    // 레이스 시작됨(또는 코디네이터 미연결) && 로컬 플레이어 아직 미완주.
    private bool HudShouldBeActive()
    {
        bool started = raceCoordinator == null || raceCoordinator.RaceStarted;
        return started && !LocalPlayerFinished();
    }

    private bool LocalPlayerFinished()
    {
        if (raceManager == null) return false;
        var standings = raceManager.BuildStandings();
        if (standings == null) return false;
        foreach (var s in standings)
            if (s != null && s.isPlayer && s.finished) return true;
        return false;
    }

    private void ApplyComboSpeed()
    {
        if (comboText != null && speedController != null)
            comboText.text = string.Format(comboFormat, speedController.Combo);

        if (speedText != null)
        {
            float spd = playerBoat != null ? playerBoat.Speed
                      : (speedController != null ? speedController.CurrentTargetSpeed : 0f);
            speedText.text = string.Format(speedFormat, spd);
        }
    }

    private void ApplyDistance()
    {
        float finish = raceManager != null ? raceManager.FinishDistance : 0f;
        float trackW = distanceTrack != null ? distanceTrack.rect.width : 0f;

        float endX = markerEndX > 0f ? markerEndX : trackW; // 0이면 트랙 폭

        // 자기 진행 = 채움 + 마커.
        float p = 0f;
        if (finish > 0f && playerBoat != null)
            p = Mathf.Clamp01(playerBoat.DistanceTraveled / finish);
        if (distanceFill != null) distanceFill.fillAmount = p;
        if (selfMarker != null)
            selfMarker.anchoredPosition = new Vector2(Mathf.Lerp(markerStartX, endX, p), selfMarker.anchoredPosition.y);

        // 다른 레이서 = 막대(최대 3). RaceManager 순위(솔로 고스트 / 멀티 NetRacer 통합).
        if (otherMarkers == null || otherMarkers.Length == 0) return;
        int idx = 0;
        if (raceManager != null && finish > 0f)
        {
            foreach (var s in raceManager.BuildStandings())
            {
                if (s.isPlayer) continue; // 나는 채움 막대로 표시
                if (idx >= otherMarkers.Length) break;
                var m = otherMarkers[idx];
                if (m != null)
                {
                    m.gameObject.SetActive(true);
                    m.anchoredPosition = new Vector2(Mathf.Lerp(markerStartX, endX, Mathf.Clamp01(s.distance / finish)),
                                                     m.anchoredPosition.y);
                }
                idx++;
            }
        }
        for (int j = idx; j < otherMarkers.Length; j++)
            if (otherMarkers[j] != null) otherMarkers[j].gameObject.SetActive(false);
    }

    private void ApplyItem()
    {
        if (itemActiveTimer > 0f) itemActiveTimer -= Time.deltaTime;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        string txt = null;
        if (itemActiveTimer > 0f) txt = $"{ItemName(activeItem)}  {Mathf.CeilToInt(itemActiveTimer)}s";
        else if (flashTimer > 0f) txt = $"{ItemName(flashItem)}!";

        bool show = txt != null;
        if (itemRoot != null) itemRoot.SetActive(show);
        if (show && itemText != null) itemText.text = txt;
    }

    private static string ItemName(ItemType t) => t switch
    {
        ItemType.Boost => "Boost",
        ItemType.RainbowStar => "Star",
        ItemType.Magnet => "Magnet",
        ItemType.Bomb => "Bomb",
        ItemType.Spaceship => "Spaceship",
        _ => t.ToString(),
    };

    // ── 항상-위 (검증된 셰이더 교체, Play 모드 한정) ──
    private static Shader uiOnTopShader;
    private static Shader tmpOnTopShader;

    // Image 는 안정적이라 1회. (Start)
    private void ApplyImagesAlwaysOnTop()
    {
        if (!Application.isPlaying) return;
        if (uiOnTopShader == null) uiOnTopShader = Shader.Find("UI/AlwaysOnTop");
        if (uiOnTopShader == null) return;
        foreach (var g in GetComponentsInChildren<Graphic>(true))
        {
            if (g is TMP_Text) continue;
            if (g is Image || g is RawImage)
                g.material = new Material(uiOnTopShader) { renderQueue = renderQueue, hideFlags = HideFlags.DontSave };
        }
    }

    // TMP 는 텍스트 변경/한글 폴백 시 머티리얼 재생성 → 매 프레임 재적용. (Update)
    private void ApplyTextAlwaysOnTop()
    {
        if (!Application.isPlaying) return;
        if (tmpOnTopShader == null) tmpOnTopShader = Shader.Find("TextMeshPro/Distance Field AlwaysOnTop");
        if (tmpOnTopShader == null) return;
        SetZTest(comboText);
        SetZTest(speedText);
        SetZTest(itemText);
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
}
