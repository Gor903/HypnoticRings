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

        // Deliberately absent from the build list: the sandbox is a workbench, and a stray scene
        // in a shipping build is exactly what ProjectSetup.Verify is there to catch.
        public const string SandboxScene = ScenesDir + "/Sandbox.unity";

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
            BuildSandbox();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[HR] Scenes rebuilt: {BootScene}, {MainScene}, {SandboxScene}");
        }

        static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("Boot Camera");

            var go = new GameObject("Bootstrap");
            go.AddComponent<HR.App.BootLoader>();

            EditorSceneManager.SaveScene(scene, BootScene);
        }

        static void BuildMain()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("Main Camera");

            // One object. GameBootstrap raises the tunnel, the input, the themes and the
            // interface underneath itself at runtime, so empty roots waiting here would only be
            // a second place for the same hierarchy to be described.
            var go = new GameObject("Game");
            var bootstrap = go.AddComponent<HR.App.GameBootstrap>();

            var config = ConfigBuilder.EnsureGameConfig();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("audioBank").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<HR.Audio.AudioBank>(AudioBankBuilder.BankPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainScene);
        }

        static void BuildSandbox()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("Sandbox Camera");

            var go = new GameObject("Sandbox");
            var bootstrap = go.AddComponent<HR.Gameplay.SandboxBootstrap>();

            // Wired here rather than by hand, so regenerating the scene cannot lose the link.
            var config = ConfigBuilder.EnsureGameConfig();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SandboxScene);
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
