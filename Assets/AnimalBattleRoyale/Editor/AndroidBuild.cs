#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnimalBattleRoyale.Editor
{
    public static class AndroidBuild
    {
        private const string PackageName = "com.centraldodev.animalbattleroyale";

        [MenuItem("AnimalBattleRoyale/Build Android Development APK")]
        public static void BuildDevelopmentApk()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string outputPath = ReadCommandLineValue("-androidBuildPath");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.Combine(projectRoot, "Builds/Android/AnimalBattleRoyale.apk");
            else if (!Path.IsPathRooted(outputPath))
                outputPath = Path.Combine(projectRoot, outputPath);

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes found in Build Settings.");

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android build failed: {summary.result} ({summary.totalErrors} errors).");

            Debug.Log($"ANDROID_BUILD_OUTPUT={outputPath}");
            Debug.Log($"Android APK built successfully: {summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime}.");
        }

        private static string ReadCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
#endif
