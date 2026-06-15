using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등수 가중 아이템 분배 (카트라이더식). 각 플레이어가 자기 아이템 노트 명중 시 로컬로 뽑음
/// (개인 보상이라 네트워크 동기화 불필요).
///   1등: 부스트/자석 위주(약함)  중위권: 무지개별/폭탄  꼴찌권: 우주선↑
///   우주선은 1등이면 드롭 안 됨(앞에 1등 없으면 무의미).
/// </summary>
public static class ItemDistributor
{
    /// place: 내 등수(1=선두). totalRacers: 전체 레이서 수. 반환: 발동할 아이템.
    public static ItemType Pick(int place, int totalRacers)
    {
        bool isLeader = place <= 1;
        bool nearBack = totalRacers > 1 && place > (totalRacers + 1) / 2;

        var pool = new List<(ItemType type, int weight)>
        {
            (ItemType.Boost, 3),
            (ItemType.Magnet, isLeader ? 4 : 2),
        };
        if (isLeader)
        {
            pool.Add((ItemType.RainbowStar, 1)); // 선두는 견제 풀 약하게
        }
        else
        {
            pool.Add((ItemType.RainbowStar, 2));
            pool.Add((ItemType.Bomb, 3));
            pool.Add((ItemType.Spaceship, nearBack ? 4 : 2)); // 1등 제외 + 뒤일수록↑
        }
        return WeightedPick(pool);
    }

    private static ItemType WeightedPick(List<(ItemType type, int weight)> pool)
    {
        int total = 0;
        foreach (var e in pool) total += e.weight;
        int roll = Random.Range(0, total);
        foreach (var e in pool)
        {
            roll -= e.weight;
            if (roll < 0) return e.type;
        }
        return pool[0].type;
    }
}
