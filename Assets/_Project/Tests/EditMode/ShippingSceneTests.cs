using System.Linq;
using HR.App;
using HR.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HR.Tests.EditMode
{
    public class ShippingSceneTests
    {
        [Test]
        public void The_first_scene_in_the_build_is_boot()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            Assert.IsNotEmpty(scenes, "the build has no enabled scenes at all");
            Assert.AreEqual(SceneBuilder.BootScene, scenes[0].path,
                "the first enabled scene is the one that loads on launch");
        }

        [Test]
        public void The_shipping_scene_carries_everything_the_bootstrap_needs()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                SceneBuilder.MainScene,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);

            try
            {
                HR.App.GameBootstrap bootstrap = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    bootstrap = root.GetComponentInChildren<HR.App.GameBootstrap>(true);
                    if (bootstrap != null) break;
                }

                Assert.IsNotNull(bootstrap, "the playing scene has no bootstrap");

                var serialized = new SerializedObject(bootstrap);
                Assert.IsNotNull(serialized.FindProperty("config").objectReferenceValue,
                    "the bootstrap ships without a GameConfig");

                Assert.IsNotNull(serialized.FindProperty("ads").objectReferenceValue,
                    "the bootstrap ships without an AdsConfig, so the ad layer is dead code");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void The_sandbox_never_ships()
        {
            Assert.IsFalse(EditorBuildSettings.scenes.Any(s => s.path == SceneBuilder.SandboxScene),
                "the sandbox scene is in the build settings");
        }

        [Test]
        public void Boot_hands_over_to_a_scene_that_is_in_the_build()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.BootScene, OpenSceneMode.Single);
            try
            {
                var loader = scene.GetRootGameObjects()
                    .Select(go => go.GetComponentInChildren<BootLoader>(true))
                    .FirstOrDefault(l => l != null);

                Assert.IsNotNull(loader, "the boot scene has nothing that loads the game");

                bool reachable = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Any(s => System.IO.Path.GetFileNameWithoutExtension(s.path) == loader.NextScene);

                Assert.IsTrue(reachable,
                    $"boot loads '{loader.NextScene}', which is not an enabled scene in the build");
            }
            finally
            {
                EditorSceneManager.OpenScene(SceneBuilder.MainScene, OpenSceneMode.Single);
            }
        }

        [Test]
        public void Main_carries_a_bootstrap_with_its_config_wired()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.MainScene, OpenSceneMode.Single);

            var bootstrap = scene.GetRootGameObjects()
                .Select(go => go.GetComponentInChildren<GameBootstrap>(true))
                .FirstOrDefault(b => b != null);

            Assert.IsNotNull(bootstrap, "the main scene has no GameBootstrap — it would load to nothing");

            var serialized = new SerializedObject(bootstrap);
            Assert.IsNotNull(serialized.FindProperty("config").objectReferenceValue,
                "GameBootstrap has no GameConfig assigned");

            var bank = serialized.FindProperty("audioBank").objectReferenceValue as HR.Audio.AudioBank;
            Assert.IsNotNull(bank, "GameBootstrap has no AudioBank assigned");
            Assert.IsTrue(bank.IsComplete, "the audio bank is missing clips");

            var camera = scene.GetRootGameObjects()
                .Select(go => go.GetComponentInChildren<Camera>(true))
                .FirstOrDefault(c => c != null);
            Assert.IsNotNull(camera, "the main scene has no camera");
            Assert.IsTrue(camera.orthographic, "the tunnel is drawn by an orthographic camera");
        }
    }
}
