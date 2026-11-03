using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HR.Editor
{
    public static class CI
    {
        const string BuildDir = "Build";

        public static void CIBootstrap()
        {
            Run(() =>
            {
                ProjectSetup.ApplyAll();
                ConfigBuilder.EnsureAll();
                SceneBuilder.BuildAll();
                AssetDatabase.SaveAssets();

                var problems = ProjectSetup.Verify();
                if (problems.Count > 0)
                    throw new Exception("settings did not apply:\n  " + string.Join("\n  ", problems));
            });
        }

        public static void CIBuildAndroidDev()
        {
            Run(() =>
            {
                Directory.CreateDirectory(BuildDir);
                string output = Path.Combine(BuildDir, "hypnoticrings-dev.apk");

                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

                bool customKeystore = PlayerSettings.Android.useCustomKeystore;
                PlayerSettings.Android.useCustomKeystore = false;

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

                PlayerSettings.Android.useCustomKeystore = customKeystore;

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
