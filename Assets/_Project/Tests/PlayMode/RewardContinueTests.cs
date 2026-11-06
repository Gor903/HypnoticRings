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
    public sealed class RewardContinueTests
    {
        GameConfig _config;
        GameObject _root;
        GameBootstrap _boot;
        Camera _camera;
        string _directory;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _directory = Path.Combine(Path.GetTempPath(), "hr-reward-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _config.orthoSize;

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, 20261027,
                new SaveService(Path.Combine(_directory, SaveService.FileName)), view: _camera);
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
        }

        IEnumerator DieWith(int score)
        {
            _boot.Game.StartRun(20261027);
            _boot.Game.StartPlaying();
            _boot.Game.ScoreKeeper.AddRings(score);
            yield return null;

            _boot.Game.States.Fire(GameTrigger.Crushed);
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_continue_keeps_the_score_the_run_died_with()
        {
            yield return DieWith(47);
            Assert.AreEqual(47, _boot.Game.Score);

            var snapshot = _boot.Game.Snapshot(0);
            Assert.IsTrue(_boot.Game.Continue(snapshot), "the continue was refused");
            yield return null;

            Assert.AreEqual(47, _boot.Game.Score, "the score was reset instead of carried over");
            Assert.AreEqual(GameState.Countdown, _boot.Game.State,
                "a continue comes back through the countdown");
        }

        [UnityTest]
        public IEnumerator A_continue_keeps_the_tempo_the_run_died_with()
        {
            yield return DieWith(60);

            var before = _boot.Game.CurrentDifficulty;
            Assert.IsTrue(_boot.Game.Continue(_boot.Game.Snapshot(0)));
            yield return null;

            var after = _boot.Game.CurrentDifficulty;
            Assert.AreEqual(before.Step, after.Step, 1e-9, "the rings got easier after a continue");
            Assert.AreEqual(before.MaxCompressionSpeed, after.MaxCompressionSpeed, 1e-9,
                "the tunnel slowed down after a continue");
        }

        [UnityTest]
        public IEnumerator A_continue_keeps_the_mode_so_hardcore_stays_hardcore()
        {
            _boot.Game.Mode = GameMode.Hardcore;
            yield return DieWith(12);

            Assert.IsTrue(_boot.Game.Continue(_boot.Game.Snapshot(0)));
            yield return null;

            Assert.AreEqual(GameMode.Hardcore, _boot.Game.Mode,
                "a hardcore continue dropped back to the normal game");
        }

        [UnityTest]
        public IEnumerator A_continue_is_refused_anywhere_but_game_over()
        {
            _boot.Game.StartRun(20261027);
            _boot.Game.StartPlaying();
            yield return null;

            Assert.IsFalse(_boot.Game.Continue(_boot.Game.Snapshot(0)),
                "a continue in the middle of a live run should be refused");
        }

        [UnityTest]
        public IEnumerator Without_an_ad_service_the_reward_button_never_shows()
        {
            yield return DieWith(5);

            Assert.IsFalse(_boot.Ads.RewardedReady, "the null service claims it has an ad");
            Assert.IsFalse(_boot.Ui.RewardButton.gameObject.activeSelf,
                "the reward button is offered with no ad behind it");
        }

        [UnityTest]
        public IEnumerator The_back_button_does_nothing_while_an_ad_is_open()
        {
            yield return DieWith(5);
            Assert.IsFalse(_boot.AdOpen);

            _boot.SystemBack();
            yield return null;
            Assert.AreEqual(GameState.Menu, _boot.Game.State,
                "back from game over should reach the menu when no ad is open");
        }
    }
}
