using System.Collections;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    public sealed class PauseStopsTheGameTests
    {
        const float Dt = 1f / 90f;

        GameObject _root;
        GameBootstrap _boot;
        Camera _camera;
        GameConfig _config;
        string _directory;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _directory = Path.Combine(Path.GetTempPath(), "hr-pause-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _config.orthoSize;
            _camera.transform.position = new Vector3(0f, _config.CameraOffsetY, -10f);

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, runSeed: 20261103,
                new SaveService(Path.Combine(_directory, SaveService.FileName)),
                view: _camera);
            _root.SetActive(true);
        }

        void PressPlay()
        {
            var screen = _boot.Ui.ScreenFor(GameState.Menu).gameObject;
            foreach (var button in screen.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.name != "menu.play") continue;
                button.onClick.Invoke();
                _boot.Game.StartPlaying();
                return;
            }

            Assert.Fail("no play button on the menu");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            if (_directory != null && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [UnityTest]
        public IEnumerator The_tunnel_does_not_step_itself_behind_the_game_loop()
        {
            yield return null;

            Assert.IsFalse(_boot.Tunnel.AutoStep,
                "the tunnel still steps itself, so it advances twice per physics frame");
        }

        [UnityTest]
        public IEnumerator A_paused_tunnel_stops_closing_in()
        {
            yield return null;
            PressPlay();
            yield return null;

            var ring = _boot.Tunnel.ResolveBallRing();
            Assert.IsNotNull(ring, "no ring to watch");

            _boot.Game.Pause();
            yield return null;

            double before = ring.Radius;
            for (int i = 0; i < 200; i++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(before, ring.Radius, 1e-6,
                $"the ring closed from {before:F4} to {ring.Radius:F4} while the game was paused");
        }

        [UnityTest]
        public IEnumerator A_paused_ball_stops_flying()
        {
            yield return null;
            PressPlay();
            yield return null;

            var ball = _boot.Tunnel.BallAt(0);
            Assert.IsNotNull(ball);

            _boot.Game.Pause();
            yield return null;

            Vector2 before = ball.transform.position;
            for (int i = 0; i < 200; i++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(before.x, ball.transform.position.x, 1e-4, "the ball drifted in x");
            Assert.AreEqual(before.y, ball.transform.position.y, 1e-4, "the ball drifted in y");
        }

        [UnityTest]
        public IEnumerator Resuming_gives_the_ball_its_flight_back()
        {
            yield return null;
            PressPlay();
            yield return null;

            var ball = _boot.Tunnel.BallAt(0);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Vector2 flying = ball.Body.linearVelocity;
            Assert.Greater(flying.magnitude, 0.1f, "the ball was not moving to begin with");
            _boot.Game.Pause();

            yield return null;
            Assert.IsTrue(ball.Frozen, "the ball is still simulated on pause");

            _boot.Game.Resume();

            Assert.IsFalse(ball.Frozen, "the ball stayed frozen after resuming");

            Assert.AreEqual(flying.x, ball.Body.linearVelocity.x, 1e-3, "lost its heading in x");
            Assert.AreEqual(flying.y, ball.Body.linearVelocity.y, 1e-3, "lost its heading in y");
        }

        [UnityTest]
        public IEnumerator Pause_takes_the_playfield_off_the_screen()
        {
            yield return null;
            PressPlay();
            yield return null;

            Assert.IsTrue(_boot.Tunnel.gameObject.activeInHierarchy, "the tunnel is not even up");

            _boot.Game.Pause();
            _boot.Ui.Show(_boot.Game.State);
            yield return null;

            Assert.IsFalse(_boot.Tunnel.gameObject.activeInHierarchy,
                "the tunnel is still on screen behind the pause card");
            Assert.IsFalse(_boot.Tunnel.BallAt(0).gameObject.activeInHierarchy,
                "the ball is left hanging on an otherwise empty screen");

            _boot.Game.Resume();
            _boot.Ui.Show(_boot.Game.State);
            yield return null;

            Assert.IsTrue(_boot.Tunnel.gameObject.activeInHierarchy, "the tunnel never came back");
            Assert.IsTrue(_boot.Tunnel.BallAt(0).gameObject.activeInHierarchy, "the ball never came back");
        }
    }
}
