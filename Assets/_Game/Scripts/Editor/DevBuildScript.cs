using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// CLI 자동 빌드 (Claude Code 자동 테스트용):
///   Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod DevBuildScript.BuildWindowsDev -logFile <로그>
/// 출력: Builds/auto/VR1Team.exe (Development Build, Windows 64).
/// </summary>
public static class DevBuildScript
{
    public static void BuildWindowsDev()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/WaterTemplate 1.unity" },
            locationPathName = "Builds/auto/VR1Team.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"[DevBuildScript] 결과: {report.summary.result}, 에러 {report.summary.totalErrors}개, " +
                  $"시간 {report.summary.totalTime.TotalMinutes:F1}분, 출력 {report.summary.outputPath}");
        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
