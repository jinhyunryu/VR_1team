using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 디제틱(월드 공간) HUD — 뱃머리 계기판처럼 보트에 부착해 콤보/순위/격차/속도를 표시.
///
/// VR 규칙(명세 2번): head-locked(Screen Space Overlay) 금지.
///   → 이 스크립트가 붙은 Canvas 는 반드시 'World Space' + PlayerBoat 자식(보트와 함께 이동).
///
/// 데이터만 읽어 표시한다(로직 X). 프로토타입 — 정식 UI(04 팀)로 교체/보강 가능.
///
/// 붙이는 법: World Space Canvas(보트 자식)에 추가 + 아래 참조/텍스트 연결.
///   필드는 전부 옵셔널 — 연결한 것만 갱신됨.
/// </summary>
public class ProtoHud : MonoBehaviour
{
    [Header("데이터 소스")]
    [SerializeField] private SpeedController speedController;
    [SerializeField] private RaceManager raceManager;

    [Header("텍스트 (옵셔널)")]
    [Tooltip("\"COMBO 12\"")]
    [SerializeField] private TMP_Text comboText;
    [Tooltip("\"2 / 4\" (내 등수 / 총)")]
    [SerializeField] private TMP_Text placeText;
    [Tooltip("앞/뒤 배와의 거리차. 뒤처지면 -, 1등이면 +리드")]
    [SerializeField] private TMP_Text gapText;
    [Tooltip("\"12 m/s\" (선택)")]
    [SerializeField] private TMP_Text speedText;

    [Header("진행 바 (옵셔널)")]
    [Tooltip("Image Type=Filled. 결승까지 진행도 0..1")]
    [SerializeField] private Image progressFill;

    private void Update()
    {
        if (speedController != null)
        {
            if (comboText != null) comboText.text = $"COMBO {speedController.Combo}";
            if (speedText != null) speedText.text = $"{speedController.CurrentTargetSpeed:F0} m/s";
        }

        if (raceManager == null) return;

        var standings = raceManager.BuildStandings();
        if (standings == null || standings.Count == 0) return;

        int meIdx = standings.FindIndex(s => s.isPlayer);
        if (meIdx < 0) return;
        var me = standings[meIdx];

        if (placeText != null) placeText.text = $"{me.place} / {standings.Count}";

        if (gapText != null)
        {
            if (meIdx > 0) // 앞에 배가 있음 → 그 배까지 얼마나 뒤처졌나
            {
                float behind = standings[meIdx - 1].distance - me.distance;
                gapText.text = $"-{behind:F0}m";
            }
            else // 1등 → 2등과의 리드
            {
                float lead = standings.Count > 1 ? me.distance - standings[1].distance : 0f;
                gapText.text = $"+{lead:F0}m";
            }
        }

        if (progressFill != null && raceManager.FinishDistance > 0f)
            progressFill.fillAmount = Mathf.Clamp01(me.distance / raceManager.FinishDistance);
    }
}
