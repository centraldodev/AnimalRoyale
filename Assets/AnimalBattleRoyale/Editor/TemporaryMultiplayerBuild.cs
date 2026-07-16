using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace AnimalBattleRoyale.Editor
{
    public static class TemporaryMultiplayerBuild
    {
        public static void BuildMacSmokeTest()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "/tmp/AnimalRoyaleMultiplayer.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 2);
        }
    }
}
