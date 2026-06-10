using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

/// <summary>
/// 인위적 이동(스틱 이동 / 스틱·스냅 회전 / 텔레포트 / 그랩 무브)을 전부 끈다.
///
/// 이 게임은 배(BoatMover)만 플레이어를 움직인다 — 플레이어 스스로 위치 이동 금지.
/// 모든 이동 기능은 LocomotionProvider 를 베이스로 하므로, 그걸 전부 비활성화한다.
///
/// ⚠️ 머리/손 추적(6DoF)은 건드리지 않는다. 실제 몸을 기울이거나 둘러보는 건 그대로 유지.
///    (TrackedPoseDriver / 카메라는 LocomotionProvider 가 아니라서 영향 없음)
///
/// 붙이는 법: 씬의 아무 GameObject(예: PlayerBoat 또는 빈 "GameSystems")에 1개만 추가.
///           Play 시 자동으로 모든 LocomotionProvider 를 끈다.
///
/// 참고: 런타임(Play/빌드)에서 동작한다. 에디트 모드 인스펙터에는 여전히 체크돼 보이지만
///       플레이하면 꺼진다. 빌드에 들어가는 동작이 핵심.
/// </summary>
public class LockLocomotion : MonoBehaviour
{
    [Tooltip("끈 항목을 Console 에 로그로 남긴다(검증용).")]
    [SerializeField] private bool logDisabled = true;

    private void Awake()
    {
        var providers = FindObjectsByType<LocomotionProvider>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int disabledCount = 0;
        foreach (var provider in providers)
        {
            if (!provider.enabled) continue;
            provider.enabled = false;
            disabledCount++;
            if (logDisabled)
                Debug.Log($"[LockLocomotion] 비활성화: {provider.GetType().Name} (on '{provider.name}')");
        }

        if (logDisabled)
            Debug.Log($"[LockLocomotion] 인위적 이동 {disabledCount}개 차단 완료. (머리/손 추적은 유지)");
    }
}
