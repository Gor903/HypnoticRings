using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HR.Editor
{
    public static class SceneBuilder
    {
        public const string ScenesDir = "Assets/_Project/Scenes";
        public const string BootScene = ScenesDir + "/Boot.unity";
        public const string MainScene = ScenesDir + "/Main.unity";

        public const string SandboxScene = ScenesDir + "/Sandbox.unity";

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

            var config = ConfigBuilder.EnsureGameConfig();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SandboxScene);
        }

        static Camera CreateCamera(string name)
        {
            var config = ConfigBuilder.EnsureGameConfig();

            var go = new GameObject(name, typeof(Camera));
            go.tag = "MainCamera";

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = config.orthoSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            go.transform.position = new Vector3(0f, config.CameraOffsetY, -10f);
            return camera;
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScene, true),
                new EditorBuildSettingsScene(MainScene, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
