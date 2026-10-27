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
    /// <summary>
    /// What a player gets when the icon is tapped.
    ///
    /// The editor never walks the build list — it opens whichever scene was saved last — so a
    /// first scene that does nothing looks perfectly healthy right up until the .aab is on a
    /// phone. This is the only check that follows the path the player takes.
    /// </summary>
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
        public void The_sandbox_never_ships()
        {
            // A workbench scene inside a shipping build is dead weight and a leak of the tools.
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

            // Awake logs an error and disables itself on a null config, which in a player is a
            // game that starts and then simply does not respond.
            var serialized = new SerializedObject(bootstrap);
            Assert.IsNotNull(serialized.FindProperty("config").objectReferenceValue,
                "GameBootstrap has no GameConfig assigned");

            var camera = scene.GetRootGameObjects()
                .Select(go => go.GetComponentInChildren<Camera>(true))
                .FirstOrDefault(c => c != null);
            Assert.IsNotNull(camera, "the main scene has no camera");
            Assert.IsTrue(camera.orthographic, "the tunnel is drawn by an orthographic camera");
        }
    }
}
