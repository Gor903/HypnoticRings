using System.Collections;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using HR.Themes;
using HR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HR.Tests.PlayMode
{
    public sealed class ThemeSkinTests
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
            _directory = Path.Combine(Path.GetTempPath(), "hr-skin-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _config.orthoSize;

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, runSeed: 20261103,
                new SaveService(Path.Combine(_directory, SaveService.FileName)),
                view: _camera);
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
        }

        Button Play()
        {
            foreach (var button in _boot.Ui.ScreenFor(GameState.Menu)
                         .GetComponentsInChildren<Button>(includeInactive: true))
                if (button.name == "menu.play") return button;

            Assert.Fail("no play button on the menu");
            return null;
        }

        [UnityTest]
        public IEnumerator A_theme_with_an_edge_actually_draws_one()
        {
            var play = Play();

            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                _boot.Ui.ApplyTheme(palette);
                yield return null;

                var chrome = play.targetGraphic.GetComponent<UiChrome>();
                double wanted = UiSkin.For(theme).OutlineWidth;

                if (wanted <= 0.0)
                {
                    Assert.IsTrue(chrome == null || !Drawn(chrome),
                        $"{theme} asks for no edge and got one");
                    continue;
                }

                Assert.IsNotNull(chrome, $"{theme} asks for a {wanted} edge and has no chrome");
                Assert.IsTrue(Drawn(chrome), $"{theme} asks for an edge and draws none");
            }
        }

        [UnityTest]
        public IEnumerator Only_a_glowing_palette_gets_a_halo()
        {
            var play = Play();

            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                _boot.Ui.ApplyTheme(palette);
                yield return null;

                var chrome = play.targetGraphic.GetComponent<UiChrome>();
                bool wanted = palette.Glow > 0.0;

                if (!wanted)
                {
                    Assert.IsTrue(chrome == null || !chrome.Glowing,
                        $"{theme} does not glow in the tunnel but glows in the interface");
                    continue;
                }

                Assert.IsNotNull(chrome, $"{theme} glows and has no chrome at all");
                Assert.IsTrue(chrome.Glowing, $"{theme} glows in the tunnel and not in the interface");
            }
        }

        [UnityTest]
        public IEnumerator Reducing_effects_stops_the_halo_and_the_breath()
        {
            _boot.Settings.reduceEffects = false;
            _boot.Ui.ApplyTheme(ThemePalette.For(Theme.Neon));
            yield return null;

            var play = Play();
            var chrome = play.targetGraphic.GetComponent<UiChrome>();

            Assert.IsNotNull(chrome, "Neon draws no chrome at all");
            Assert.IsTrue(chrome.Glowing, "Neon is not glowing to begin with");
            Assert.IsTrue(_boot.Ui.Shimmer.Breathing, "Neon is not breathing to begin with");

            var before = play.targetGraphic.color;
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.AreNotEqual(before, play.targetGraphic.color,
                "the accent never moved, so there was nothing to switch off");

            _boot.Settings.reduceEffects = true;
            _boot.Ui.ApplyTheme(ThemePalette.For(Theme.Neon));
            yield return null;

            Assert.IsFalse(chrome.Glowing, "the halo survived reduce effects");
            Assert.IsFalse(_boot.Ui.Shimmer.Breathing, "the breath survived reduce effects");

            var settled = play.targetGraphic.color;
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.AreEqual(settled, play.targetGraphic.color, "something is still moving");
        }

        [UnityTest]
        public IEnumerator A_still_theme_never_breathes_even_with_effects_on()
        {
            _boot.Settings.reduceEffects = false;
            _boot.Ui.ApplyTheme(ThemePalette.For(Theme.Sumie));
            yield return null;

            Assert.IsFalse(_boot.Ui.Shimmer.Breathing, "Sumi-e is animating something");

            var play = Play();
            var before = play.targetGraphic.color;
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.AreEqual(before, play.targetGraphic.color, "Sumi-e moved a colour");
        }

        [UnityTest]
        public IEnumerator The_breath_puts_the_colour_back_when_the_theme_changes()
        {
            _boot.Settings.reduceEffects = false;
            _boot.Ui.ApplyTheme(ThemePalette.For(Theme.Neon));
            for (int frame = 0; frame < 8; frame++) yield return null;

            _boot.Ui.ApplyTheme(ThemePalette.For(Theme.Sumie));
            yield return null;

            var expected = ThemeApplier.ToColor(ThemePalette.For(Theme.Sumie).Accent);
            var actual = Play().targetGraphic.color;

            Assert.AreEqual(expected.r, actual.r, 1e-3f, "the accent kept the old theme's red");
            Assert.AreEqual(expected.g, actual.g, 1e-3f, "the accent kept the old theme's green");
            Assert.AreEqual(expected.b, actual.b, 1e-3f, "the accent kept the old theme's blue");
        }

        [UnityTest]
        public IEnumerator A_rippling_theme_answers_where_the_finger_landed()
        {
            _boot.Ui.ApplyTheme(ThemePalette.For(Theme.Sumie));
            yield return null;

            var press = Play().GetComponent<PressFeedback>();
            Assert.IsNull(press.Ripple, "a sinking theme built a ripple it will never show");

            _boot.Ui.ApplyTheme(ThemePalette.For(Theme.Ocean));
            yield return null;

            Assert.IsNotNull(press.Ripple, "Deep ocean ripples and has no ripple to show");
            Assert.AreEqual(0f, press.Ripple.color.a, 1e-3f, "the ripple is showing unprompted");

            press.OnPointerDown(new PointerEventData(EventSystem.current));
            Assert.Greater(press.Ripple.color.a, 0f, "pressing it drew nothing");

            float firstWidth = press.Ripple.rectTransform.sizeDelta.x;
            float firstAlpha = press.Ripple.color.a;

            press.OnPointerUp(new PointerEventData(EventSystem.current));
            yield return null;
            yield return null;

            Assert.Greater(press.Ripple.rectTransform.sizeDelta.x, firstWidth, "the ripple never spread");
            Assert.Less(press.Ripple.color.a, firstAlpha, "the ripple never faded");
        }

        [UnityTest]
        public IEnumerator Every_theme_paints_every_screen_without_leaving_a_hole()
        {
            var screens = new[]
            {
                GameState.Menu, GameState.Playing, GameState.Paused, GameState.GameOver,
                GameState.ThemeSelect, GameState.Settings,
            };

            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                _boot.Ui.ApplyTheme(palette);
                var paper = ThemeApplier.ToColor(palette.Background);

                foreach (var state in screens)
                {
                    _boot.Ui.Show(state);
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    foreach (var text in _boot.Ui.ScreenFor(state)
                                 .GetComponentsInChildren<Text>(includeInactive: false))
                    {
                        if (text.color.a < 0.05f || string.IsNullOrEmpty(text.text)) continue;

                        double ratio = Contrast.Ratio(ToRgb(text.color), ToRgb(Behind(text, paper)));
                        Assert.GreaterOrEqual(ratio, LegibleEnough,
                            $"{theme}/{state}: '{text.name}' reads {ratio:F2} against what is behind it");
                    }
                }
            }
        }

        const double LegibleEnough = 2.0;

        static Color Behind(Graphic graphic, Color paper)
        {
            var parent = graphic.transform.parent;
            while (parent != null)
            {
                var image = parent.GetComponent<Image>();
                if (image != null && image.enabled && image.color.a > 0.5f)
                    return Flatten(image.color, paper);

                parent = parent.parent;
            }

            return paper;
        }

        static Color Flatten(Color over, Color under) => Color.Lerp(under, over, over.a);

        static Rgb ToRgb(Color colour) => new Rgb((byte)Mathf.RoundToInt(Mathf.Clamp01(colour.r) * 255f),
                                                  (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.g) * 255f),
                                                  (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.b) * 255f));

        static bool Drawn(UiChrome chrome)
        {
            foreach (var outline in chrome.GetComponents<Outline>())
                if (outline.enabled && outline.effectDistance.x > 0f) return true;

            return false;
        }
    }
}
