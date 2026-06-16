/// <summary>
/// 타이틀에서 고른 곡을 레이스 씬으로 전달하는 정적 보관소 (씬 전환 간 유지).
///   타이틀: SongSelection.SelectedIndex = 고른 인덱스
///   레이스: ProtoBeatmapSpawner 가 SelectedIndex 로 곡/채보 선택
/// RaceResult 패턴 — MonoBehaviour 아님, 인스펙터 작업 0.
/// </summary>
public static class SongSelection
{
    /// 선택한 곡 인덱스 (ProtoBeatmapSpawner.songs 배열 기준). 기본 0 = Song1.
    public static int SelectedIndex;
}
