using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HR.Editor
{
    /// <summary>
    /// Command-line entry points. Each one exits with a real status code — a batch-mode editor
    /// will happily exit 0 after logging a failure, so every method here decides its own code.
    /// </summary>
    public static class CI
    {
        const string BuildDir = "Build";

        /// <summary>Applies project settings, regenerates the scenes, then reads it all back.</summary>
        public static void CIBootstrap()
        {
            Run(() =>
            {
                ProjectSetup.ApplyAll();
                SceneBuilder.BuildAll();
                AssetDatabase.SaveAssets();

                // Applying is not evidence. Unity 6 changed Fixed Timestep to a rational, and the
                // old float assignment failed with a log line and no exception — the only thing
                // that caught it was reading the value back.
                var problems = ProjectSetup.Verify();
                if (problems.Count > 0)
                    throw new Exception("settings did not apply:\n  " + string.Join("\n  ", problems));
            });
        }

        /// <summary>Development APK. Proves the Android toolchain works end to end.</summary>
        public static void CIBuildAndroidDev()
        {
            Run(() =>
            {
                Directory.CreateDirectory(BuildDir);
                string output = Path.Combine(BuildDir, "hypnoticrings-dev.apk");

                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { SceneBuilder.BootScene, SceneBuilder.MainScene },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.Development,
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;

                if (summary.result != BuildResult.Succeeded)
                    throw new Exception($"Android build {summary.result} with {summary.totalErrors} error(s)");

                // Size is read from the file, not from the report: the report's totalSize field has
                // been observed measuring the intermediate Gradle project instead of the artifact.
                long bytes = new FileInfo(output).Length;
                Debug.Log($"[HR] Built {output} ({bytes / 1024 / 1024} MB on disk)");
            });
        }

        static void Run(Action action)
        {
            try
            {
                action();
                Debug.Log("[HR] CI step OK");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HR] CI step FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
