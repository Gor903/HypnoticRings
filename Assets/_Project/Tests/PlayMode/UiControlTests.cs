using System.Collections;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using HR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// The controls themselves, as opposed to the screens they sit on.
    ///
    /// Every one of these is a defect the captured frames showed first: a switch that vanished
    /// when it was off, a knob sliced in half by the margin it rode to, a button that gave no
    /// sign of having been touched, and a pause button painted the same colour as its own glyph.
    /// A picture finds those; a test is what keeps them found.
    /// </summary>
    public class UiControlTests
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
            _directory = Path.Combine(Path.GetTempPath(), "hr-ctl-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7.5f;

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, 11, new SaveService(Path.Combine(_directory, SaveService.FileName)),
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

        GameObject Settings()
        {
            _boot.Ui.Show(GameState.Settings);
            Canvas.ForceUpdateCanvases();
            return _boot.Ui.ScreenFor(GameState.Settings).gameObject;
        }

        [UnityTest]
        public IEnumerator An_off_switch_is_still_on_the_screen()
        {
            // uGUI's own answer is to hide the tick, which leaves a settings row that is a line
            // of text with nothing beside it — the player cannot tell it is a control at all.
            var settings = Settings();
            yield return null;

            int checkedSwitches = 0;
            foreach (var toggle in settings.GetComponentsInChildren<Toggle>(includeInactive: true))
            {
                var visual = toggle.GetComponent<ToggleVisual>();
                Assert.IsNotNull(visual, $"{toggle.name} has no visible switch");

                toggle.isOn = false;
                Canvas.ForceUpdateCanvases();

                Assert.IsTrue(visual.Track.gameObject.activeInHierarchy,
                    $"{toggle.name} loses its track when it is off");
                Assert.AreEqual(1f, visual.Track.color.a, 1e-3f,
                    $"{toggle.name} fades its track to nothing when it is off");
                Assert.IsTrue(visual.Knob.gameObject.activeInHierarchy,
                    $"{toggle.name} loses its knob when it is off");
                checkedSwitches++;
            }

            Assert.Greater(checkedSwitches, 0, "no switches on the settings screen");
        }

        [UnityTest]
        public IEnumerator A_switch_says_which_way_it_is_set()
        {
            // Visible in both states is not enough on its own: the two states have to differ,
            // or it is a decoration that happens to be lit.
            var settings = Settings();
            yield return null;

            foreach (var toggle in settings.GetComponentsInChildren<Toggle>(includeInactive: true))
            {
                var visual = toggle.GetComponent<ToggleVisual>();

                toggle.isOn = false;
                Canvas.ForceUpdateCanvases();
                float offX = ((RectTransform)visual.Knob.transform).anchorMin.x;
                var offColour = visual.Track.color;

                toggle.isOn = true;
                Canvas.ForceUpdateCanvases();
                float onX = ((RectTransform)visual.Knob.transform).anchorMin.x;

                Assert.Greater(onX, offX, $"{toggle.name} does not move its knob when it is set");
                Assert.AreNotEqual(offColour, visual.Track.color,
                    $"{toggle.name} is the same colour on and off");
            }
        }

        [UnityTest]
        public IEnumerator A_slider_keeps_its_knob_inside_itself()
        {
            // Both ends, because uGUI centres the knob on the value: an area that reaches the
            // track's ends hangs half a knob off the control at nought and at full.
            var settings = Settings();
            yield return null;

            int checkedSliders = 0;
            foreach (var slider in settings.GetComponentsInChildren<Slider>(includeInactive: true))
            {
                foreach (float value in new[] { slider.minValue, slider.maxValue })
                {
                    slider.value = value;
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    var whole = Corners((RectTransform)slider.transform);
                    var knob = Corners(slider.handleRect);

                    Assert.GreaterOrEqual(knob.xMin, whole.xMin - 1f,
                        $"{slider.name} at {value} hangs its knob off the left");
                    Assert.LessOrEqual(knob.xMax, whole.xMax + 1f,
                        $"{slider.name} at {value} hangs its knob off the right");
                }

                checkedSliders++;
            }

            Assert.Greater(checkedSliders, 0, "no sliders on the settings screen");
        }

        [UnityTest]
        public IEnumerator A_slider_shows_how_far_along_it_is()
        {
            // Without a filled part the player is judging a knob's position against a bare line.
            var settings = Settings();
            yield return null;

            foreach (var slider in settings.GetComponentsInChildren<Slider>(includeInactive: true))
            {
                Assert.IsNotNull(slider.fillRect, $"{slider.name} has nothing filled in");

                slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, 0.2f);
                Canvas.ForceUpdateCanvases();
                yield return null;
                float quiet = Corners(slider.fillRect).width;

                slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, 0.8f);
                Canvas.ForceUpdateCanvases();
                yield return null;

                Assert.Greater(Corners(slider.fillRect).width, quiet + 1f,
                    $"{slider.name} fills the same amount wherever it is set");
            }
        }

        [UnityTest]
        public IEnumerator Every_button_answers_a_press()
        {
            // On a phone a press with no response reads as a dropped tap, and the player presses
            // again — which on Retry is two runs started and one of them thrown away.
            foreach (var state in new[] { GameState.Menu, GameState.Playing, GameState.Paused,
                                          GameState.GameOver, GameState.ThemeSelect,
                                          GameState.Settings })
            {
                _boot.Ui.Show(state);
                Canvas.ForceUpdateCanvases();
                yield return null;

                foreach (var button in _boot.Ui.ScreenFor(state)
                             .GetComponentsInChildren<Button>(includeInactive: true))
                    Assert.IsNotNull(button.GetComponent<PressFeedback>(),
                        $"{state}: '{button.name}' gives no sign of having been touched");
            }
        }

        [UnityTest]
        public IEnumerator A_pressed_button_comes_back_to_where_it_was()
        {
            // The failure this catches is not a stuck button but a creeping one: every press
            // that does not return leaves the control a little lower than the row beside it.
            _boot.Ui.Show(GameState.Menu);

            // Settled before anything is read. A layout group places its children over the
            // frames after the screen appears, and a position taken before that is the one the
            // button had when it was nothing but a rect.
            for (int frame = 0; frame < 3; frame++)
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
            }

            var play = _boot.Ui.ScreenFor(GameState.Menu)
                .GetComponentInChildren<PressFeedback>(includeInactive: true);
            var rect = (RectTransform)play.transform;
            var home = rect.anchoredPosition;

            var pointer = new PointerEventData(EventSystem.current);
            play.OnPointerDown(pointer);
            Assert.AreNotEqual(home, rect.anchoredPosition, "pressing it did nothing at all");

            play.OnPointerUp(pointer);
            float waited = 0f;
            while (waited < PressFeedback.Recovery * 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(home.x, rect.anchoredPosition.x, 0.5f, "it came back sideways");
            Assert.AreEqual(home.y, rect.anchoredPosition.y, 0.5f, "it never came back up");
        }

        [UnityTest]
        public IEnumerator The_pause_button_is_never_painted_its_own_glyphs_colour()
        {
            // It was: tracked as ink, fill and glyph took the same colour and the HUD showed a
            // solid disc with nothing on it. Checked on every theme, because the roles are
            // derived and a palette nobody has looked at yet is exactly where this returns.
            _boot.Ui.Show(GameState.Playing);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Button pause = null;
            foreach (var button in _boot.Ui.ScreenFor(GameState.Playing)
                         .GetComponentsInChildren<Button>(includeInactive: true))
                if (button.name == "Pause") pause = button;

            Assert.IsNotNull(pause, "the HUD has no pause button");
            var glyph = pause.GetComponentInChildren<Text>(includeInactive: true);

            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                _boot.Ui.ApplyTheme(palette);
                Canvas.ForceUpdateCanvases();
                yield return null;

                double ratio = Contrast.Ratio(ToRgb(glyph.color), ToRgb(pause.targetGraphic.color));
                Assert.GreaterOrEqual(ratio, ThemePalette.LargeTextRequired,
                    $"{theme}: the pause glyph reads {ratio:F2} against its own button");
            }
        }

        [UnityTest]
        public IEnumerator The_language_row_shows_which_language_is_on()
        {
            // It showed the word "Language" and nothing else — the only control in the game
            // that stated its question and not its answer, on the one screen a player opens
            // precisely because the answer is currently wrong.
            var settings = Settings();
            yield return null;

            var row = FindByName(settings.transform, "set.language");
            Assert.IsNotNull(row, "no language row on the settings screen");

            var chip = row.GetComponentInChildren<Button>(includeInactive: true);
            Assert.IsNotNull(chip, "the language row has nothing to press");

            var value = chip.GetComponentInChildren<Text>(includeInactive: true);
            Assert.AreEqual(Strings.Get("set.lang.auto"), value.text, "a fresh save is on Auto");

            var seen = new System.Collections.Generic.HashSet<string> { value.text };
            for (int press = 0; press < 2; press++)
            {
                chip.onClick.Invoke();
                yield return null;
                Assert.IsTrue(seen.Add(value.text),
                    $"pressing it again showed '{value.text}', which it had already shown");
            }

            chip.onClick.Invoke();
            yield return null;
            Assert.AreEqual(Strings.Get("set.lang.auto"), value.text, "the cycle does not close");
        }

        static RectTransform FindByName(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<RectTransform>(includeInactive: true))
                if (child.name == name) return child;
            return null;
        }

        static Rgb ToRgb(Color colour) => new Rgb((byte)Mathf.RoundToInt(colour.r * 255f),
                                                  (byte)Mathf.RoundToInt(colour.g * 255f),
                                                  (byte)Mathf.RoundToInt(colour.b * 255f));

        /// <summary>A rect's extent in world space, which is the only space two of them share.</summary>
        static Rect Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
