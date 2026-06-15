using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 공격형 아이템(폭탄/우주선)의 네트워크 중계. RPC 로 대상에게 "신호"만 보내고 효과는 대상 기기 로컬 적용.
///   폭탄: 다른 모든 사람 기기에 폭탄 함정 로컬 스폰. (MP 의 AI 는 노트 없으니 제외)
///   우주선: 현재 1등(사람=ClientRpc, AI=서버 직접) 감속.
/// 멀티 미접속(솔로)이면 RPC 대신 직접 로컬 적용 (AI 대상).
/// 붙이는 법: "Multiplayer" GameObject 에 NetworkObject 와 함께 + noteSpawner/hands/speedController/bombPrefab 연결.
/// </summary>
public class ItemNetworkRelay : NetworkBehaviour
{
    [Header("폭탄 함정")]
    [SerializeField] private ProtoNoteSpawner noteSpawner; // 폭탄 함정을 띄울 내 레인
    [SerializeField] private Striker[] hands;
    [SerializeField] private SpeedController speedController;
    [SerializeField] private BombHazard bombPrefab;
    [SerializeField] private NoteFeedback noteFeedback;
    [SerializeField] private float bombApproachSpeed = 2f;
    [SerializeField] private float bombHitRadius = 0.18f;
    [SerializeField] private float bombMissLocalZ = 0.1f;
    [SerializeField] private float bombSpawnDistance = 4f;
    [Range(0.1f, 1f)][SerializeField] private float bombSlowMult = 0.35f;
    [SerializeField] private float bombSlowDuration = 3f;

    [Header("우주선 (1등 감속)")]
    [Range(0.1f, 1f)][SerializeField] private float spaceshipSlowMult = 0.5f;
    [SerializeField] private float spaceshipSlowDuration = 3f;

    // 스폰 전이면 솔로 경로로 폴백 (RPC not-spawned 예외 방지).
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    // ── 폭탄 ──────────────────────────────────────────────
    public void SendBomb()
    {
        if (Online) RequestBombServerRpc(NetworkManager.Singleton.LocalClientId);
        else SoloBomb();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBombServerRpc(ulong senderId) => ApplyBombClientRpc(senderId);

    [ClientRpc]
    private void ApplyBombClientRpc(ulong senderId)
    {
        if (NetworkManager.Singleton.LocalClientId == senderId) return; // 발신자 제외
        SpawnLocalBomb();
    }

    private void SpawnLocalBomb()
    {
        if (bombPrefab == null || noteSpawner == null) return;
        var bomb = Instantiate(bombPrefab, noteSpawner.transform, false);
        bomb.transform.localPosition = new Vector3(0f, 0f, bombSpawnDistance);
        bomb.Init(hands, speedController, bombApproachSpeed, bombHitRadius, bombMissLocalZ,
                  bombSlowMult, bombSlowDuration, noteFeedback);
    }

    private void SoloBomb()
    {
        // 솔로: 내 앞 가장 가까운 AI 를 직접 감속 (노트 없으니 함정 대신).
        var target = NearestAiAhead();
        if (target != null) SlowBoat(target, bombSlowMult, bombSlowDuration);
    }

    // ── 우주선 ────────────────────────────────────────────
    public void SendSpaceship()
    {
        if (Online) RequestSpaceshipServerRpc();
        else SoloSpaceship();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpaceshipServerRpc()
    {
        var leader = LeaderRacer();
        if (leader == null) return;
        if (leader.IsAi.Value)
            SlowBoat(leader.gameObject, spaceshipSlowMult, spaceshipSlowDuration); // 서버가 AI 직접
        else
            ApplySlowClientRpc(spaceshipSlowMult, spaceshipSlowDuration, leader.OwnerClientId);
    }

    [ClientRpc]
    private void ApplySlowClientRpc(float mult, float duration, ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
            speedController?.ApplyExternalSlow(mult, duration);
    }

    private void SoloSpaceship()
    {
        // 솔로: 1등(보통 AI 고스트) 직접 감속.
        var leaderBoat = SoloLeaderBoat();
        if (leaderBoat != null) SlowBoat(leaderBoat, spaceshipSlowMult, spaceshipSlowDuration);
    }

    // ── 헬퍼 ──────────────────────────────────────────────
    // MP: NetDistance 최댓값 NetRacer.
    private NetRacer LeaderRacer()
    {
        NetRacer best = null;
        float max = float.NegativeInfinity;
        foreach (var r in FindObjectsByType<NetRacer>(FindObjectsSortMode.None))
            if (r.NetDistance.Value > max) { max = r.NetDistance.Value; best = r; }
        return best;
    }

    // 솔로: 모든 BoatMover 중 최장거리. 1등이 나면 무효(null).
    private GameObject SoloLeaderBoat()
    {
        BoatMover best = null;
        float max = float.NegativeInfinity;
        foreach (var m in FindObjectsByType<BoatMover>(FindObjectsSortMode.None))
            if (m.DistanceTraveled > max) { max = m.DistanceTraveled; best = m; }
        if (best == null) return null;
        return best.GetComponent<GhostRacer>() != null ? best.gameObject : null;
    }

    private GameObject NearestAiAhead()
    {
        var myBoat = speedController != null ? speedController.GetComponent<BoatMover>() : null;
        float myDist = myBoat != null ? myBoat.DistanceTraveled : 0f;
        GameObject best = null;
        float bestGap = float.PositiveInfinity;
        foreach (var g in FindObjectsByType<GhostRacer>(FindObjectsSortMode.None))
        {
            var m = g.GetComponent<BoatMover>();
            if (m == null) continue;
            float gap = m.DistanceTraveled - myDist;
            if (gap > 0f && gap < bestGap) { bestGap = gap; best = g.gameObject; }
        }
        return best;
    }

    private static void SlowBoat(GameObject go, float mult, float duration)
    {
        var sc = go.GetComponent<SpeedController>();
        if (sc != null) sc.ApplyExternalSlow(mult, duration);
        var gr = go.GetComponent<GhostRacer>();
        if (gr != null) gr.ApplyExternalSlow(mult, duration);
    }
}
