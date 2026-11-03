using System.Collections;
using HR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    public class SceneSmokeTests
    {
        [UnityTest]
        public IEnumerator Boot_scene_loads_with_a_camera()
        {
            yield return LoadScene("Boot");
            Assert.IsNotNull(Camera.main, "Boot has no MainCamera-tagged camera");
        }

        [UnityTest]
        public IEnumerator Main_scene_carries_the_bootstrap_that_raises_the_game()
        {
            yield return LoadScene("Main");

            var bootstrap = Object.FindAnyObjectByType<HR.App.GameBootstrap>();
            Assert.IsNotNull(bootstrap, "Main has no GameBootstrap, so it would load to nothing");

            yield return null;
            Assert.IsNotNull(bootstrap.Game, "the bootstrap did not raise the game");
            Assert.IsNotNull(bootstrap.Ui, "the bootstrap did not raise the interface");
            Assert.AreEqual(GameState.Menu, bootstrap.Game.State, "a fresh scene opens in the menu");
        }

        [UnityTest]
        public IEnumerator Main_camera_frames_the_tunnel_centre_where_the_contract_puts_it()
        {
            yield return LoadScene("Main");

            yield return null;

            var bootstrap = Object.FindAnyObjectByType<HR.App.GameBootstrap>();
            Assert.IsNotNull(bootstrap, "Main has no GameBootstrap");
            var config = bootstrap.Config;
            Assert.IsNotNull(config, "the bootstrap carries no config");

            var camera = Camera.main;
            Assert.IsNotNull(camera, "Main has no MainCamera-tagged camera");
            Assert.IsTrue(camera.orthographic, "camera must be orthographic");
            Assert.AreEqual(config.orthoSize, camera.orthographicSize, 1e-4f);

            Vector3 viewport = camera.WorldToViewportPoint(Vector3.zero);
            Assert.AreEqual(0.5f, viewport.x, 1e-4f, "tunnel centre is off the horizontal middle");
            Assert.AreEqual(config.centreHeightFraction, viewport.y, 1e-4f,
                            "tunnel centre is at the wrong height");
            Assert.Greater(viewport.y, 0.5f, "the tunnel centre belongs above the middle of the screen");
        }

        [UnityTest]
        public IEnumerator Physics_runs_at_the_rate_the_contract_specifies()
        {
            yield return LoadScene("Main");

            Assert.AreEqual(1f / 90f, Time.fixedDeltaTime, 1e-6f);
            Assert.AreEqual(-14f, Physics2D.gravity.y, 1e-4f);
        }

        static IEnumerator LoadScene(string name)
        {
            var load = SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;
        }
    }
}
