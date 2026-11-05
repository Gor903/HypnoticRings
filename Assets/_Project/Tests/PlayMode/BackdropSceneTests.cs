using System.Collections;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using HR.Themes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    public sealed class BackdropSceneTests
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
            _directory = Path.Combine(Path.GetTempPath(), "hr-back-" + Path.GetRandomFileName());
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
            _boot.Configure(_config, 11,
                new SaveService(Path.Combine(_directory, SaveService.FileName)), view: _camera);
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
        }

        BackdropRenderer Layer => _boot.Themes.Backdrop;

        [UnityTest]
        public IEnumerator Every_theme_builds_the_backdrop_its_palette_asks_for()
        {
            foreach (var theme in ThemePalette.Every)
            {
                _boot.Themes.Apply(theme);
                yield return null;

                Assert.AreEqual(Backdrop.For(theme).Length, Layer.PartCount,
                    $"{theme} built a different number of parts than it declares");
            }
        }

        [UnityTest]
        public IEnumerator Reducing_effects_takes_the_backdrop_away_entirely()
        {
            _boot.Settings.reduceEffects = false;
            _boot.Themes.Apply(Theme.Neon);
            yield return null;
            Assert.IsTrue(Layer.Showing, "the backdrop never appeared to begin with");

            _boot.Themes.SetEffectsWanted(false);
            yield return null;
            Assert.IsFalse(Layer.Showing, "reduce effects left the backdrop on screen");

            _boot.Themes.SetEffectsWanted(true);
            yield return null;
            Assert.IsTrue(Layer.Showing, "the backdrop did not come back");
        }

        [UnityTest]
        public IEnumerator The_plain_theme_shows_nothing_even_with_effects_on()
        {
            _boot.Themes.SetEffectsWanted(true);
            _boot.Themes.Apply(Theme.MinimalLight);
            yield return null;

            Assert.AreEqual(0, Layer.PartCount);
            Assert.IsFalse(Layer.Showing, "an empty backdrop is still drawing");
        }

        [UnityTest]
        public IEnumerator The_backdrop_is_one_batch_and_sits_behind_the_rings()
        {
            _boot.Themes.Apply(Theme.Crt);
            yield return null;

            var renderer = Layer.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer);
            Assert.AreEqual(1, renderer.sharedMaterials.Length,
                "the backdrop should cost one draw call, not one per shape");

            Assert.Greater(Layer.transform.position.z, CentreMarker.Depth,
                "the backdrop must sit further from the camera than the red circle");
            Assert.Greater(Layer.transform.position.z, RingSegment.HaloDepth,
                "the backdrop must sit further from the camera than a ring halo");
        }

        [UnityTest]
        public IEnumerator The_ball_flies_inside_the_solid_ring_while_the_digits_run()
        {
            _boot.Game.StartRun(20261027);
            yield return null;

            Assert.AreEqual(GameState.Countdown, _boot.Game.State);
            Assert.IsFalse(_boot.Ball.Frozen, "the ball stands still through the whole countdown");

            var ring = _boot.Tunnel.SolidRing;
            Assert.IsNotNull(ring, "there is no solid ring to be safe inside");
            Assert.AreEqual(0, ring.GapCount, "the countdown ring must have no way out");

            var start = _boot.Ball.transform.position;
            float travelled = 0f;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                if (_boot.Game.State != GameState.Countdown) break;
                travelled = Mathf.Max(travelled,
                    Vector2.Distance(start, _boot.Ball.transform.position));
            }

            Assert.Greater(travelled, _boot.Ball.Radius,
                "the ball never moved further than its own radius");

            Assert.LessOrEqual(_boot.Ball.DistanceToCentre,
                _boot.Tunnel.SolidRingRadius + _boot.Ball.Radius,
                "the ball got out of a ring that has no gaps");
        }

        [UnityTest]
        public IEnumerator The_ball_is_still_once_the_run_is_over()
        {
            _boot.Game.StartRun(20261027);
            _boot.Game.StartPlaying();
            yield return null;
            Assert.IsFalse(_boot.Ball.Frozen);

            _boot.Game.States.Fire(GameTrigger.Crushed);
            yield return null;

            Assert.IsTrue(_boot.Ball.Frozen, "the ball kept flying after the run ended");
        }

        [UnityTest]
        public IEnumerator Leaving_hardcore_puts_the_chosen_backdrop_back()
        {
            _boot.Themes.Apply(Theme.Ocean);
            yield return null;
            int chosen = Layer.PartCount;

            _boot.Themes.Apply(Theme.Ember);
            yield return null;
            Assert.AreEqual(Backdrop.For(Theme.Ember).Length, Layer.PartCount,
                "the hardcore skin kept the chosen theme's backdrop");

            _boot.Themes.Apply(Theme.Ocean);
            yield return null;
            Assert.AreEqual(chosen, Layer.PartCount,
                "leaving hardcore did not restore the chosen backdrop");
        }
    }
}
