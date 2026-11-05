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
