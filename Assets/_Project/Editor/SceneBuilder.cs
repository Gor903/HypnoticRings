using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HR.Editor
{
    /// <summary>
    /// Scenes are built by this script, never by hand-editing YAML. A scene that cannot be
    /// regenerated with one command drifts the moment two sessions touch it.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenesDir = "Assets/_Project/Scenes";
        public const string BootScene = ScenesDir + "/Boot.unity";
        public const string MainScene = ScenesDir + "/Main.unity";

        // CONTRACT.md G-10: the camera is static, and the tunnel centre O sits at 46% of screen
        // height from the bottom. Screen centre is 50%, so O is 4% of the full height below it.
        public const float OrthoSize = 7.5f;
        const float CentreHeightFraction = 0.46f;

        [MenuItem("Hypnotic Rings/Setup/Rebuild scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(ScenesDir);
            BuildBoot();
            BuildMain();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[HR] Scenes rebuilt: {BootScene}, {MainScene}");
        }

        static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("Boot Camera");
            new GameObject("Bootstrap");
            EditorSceneManager.SaveScene(scene, BootScene);
        }

        static void BuildMain()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("Main Camera");

            // Roots only. Phase 1 fills Tunnel, phase 6 fills UI; naming them now keeps every
            // later generator appending to a known place instead of inventing its own.
            new GameObject("Tunnel");
            new GameObject("UI");
            new GameObject("Systems");

            EditorSceneManager.SaveScene(scene, MainScene);
        }

        static Camera CreateCamera(string name)
        {
            var go = new GameObject(name, typeof(Camera));
            go.tag = "MainCamera";

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthoSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            float offset = (0.5f - CentreHeightFraction) * 2f * OrthoSize;
            go.transform.position = new Vector3(0f, offset, -10f);
            return camera;
        }

        static void RegisterInBuildSettings()
        {
            // Order matters, not just membership: the first enabled scene is the one that loads.
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScene, true),
                new EditorBuildSettingsScene(MainScene, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
