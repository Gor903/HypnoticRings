using System.Collections;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using HR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 6's interface gates: the whole journey works, every tap target is big enough to
    /// hit, and nothing interactive sits where the finger has to swipe.
    /// </summary>
    public class UserInterfaceTests
    {
        GameConfig _config;
        GameObject _root;
        GameBootstrap _boot;
        string _directory;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _directory = Path.Combine(Path.GetTempPath(), "hr-ui-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            cameraGo.GetComponent<Camera>().orthographic = true;

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            // The save has to be handed over before activation: Awake builds immediately, and a
            // Build call afterwards is too late to redirect it away from the real save file.
            _boot.Configure(_config, runSeed: 7,
                new SaveService(Path.Combine(_directory, SaveService.FileName)),
                view: cameraGo.GetComponent<Camera>());
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
        }

        static void Press(GameObject screen, string name)
        {
            foreach (var button in screen.GetComponentsInChildren<Button>(includeInactive: true))
                if (button.name == name) { button.onClick.Invoke(); return; }
            Assert.Fail($"no button named '{name}' on {screen.name}");
        }

        [UnityTest]
        public IEnumerator The_whole_journey_works_end_to_end()
        {
            // The gate: Menu to Play to GameOver to Retry to Home, through the real buttons.
            var ui = _boot.Ui;
            Assert.AreEqual(GameState.Menu, _boot.Game.State);

            Press(ui.ScreenFor(GameState.Menu).gameObject, "menu.play");
            Assert.AreEqual(GameState.Playing, _boot.Game.State);
            yield return null;

            _boot.Game.States.Fire(GameTrigger.Crushed);
            Assert.AreEqual(GameState.GameOver, _boot.Game.State);

            Press(ui.ScreenFor(GameState.GameOver).gameObject, "over.retry");
            Assert.AreEqual(GameState.Playing, _boot.Game.State);
            Assert.AreEqual(0, _boot.Game.Score, "retry should start from nothing");

            _boot.Game.States.Fire(GameTrigger.Crushed);
            Press(ui.ScreenFor(GameState.GameOver).gameObject, "over.home");
            Assert.AreEqual(GameState.Menu, _boot.Game.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pausing_and_resuming_works_through_the_buttons()
        {
            var ui = _boot.Ui;
            Press(ui.ScreenFor(GameState.Menu).gameObject, "menu.play");

            Press(ui.ScreenFor(GameState.Playing).gameObject, "Pause");
            Assert.AreEqual(GameState.Paused, _boot.Game.State);

            Press(ui.ScreenFor(GameState.Paused).gameObject, "pause.resume");
            Assert.AreEqual(GameState.Playing, _boot.Game.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Every_tap_target_is_big_enough_to_hit()
        {
            // CONTRACT.md §3. Measured on the built rects, not trusted to the builder.
            foreach (var rect in _boot.Ui.InteractiveElements())
            {
                var size = rect.rect.size;
                float height = size.y > 0f ? size.y : rect.sizeDelta.y;
                Assert.GreaterOrEqual(height, UiBuilderHeightFloor,
                    $"{rect.name} is only {height:F0} units tall");
            }
            yield return null;
        }

        const float UiBuilderHeightFloor = 150f;

        [UnityTest]
        public IEnumerator Nothing_interactive_sits_in_the_band_the_finger_uses()
        {
            // Spec §10.1: the score is at the top, the controls at the bottom, and the middle
            // where the tunnel is and the gesture happens stays clear.
            Press(_boot.Ui.ScreenFor(GameState.Menu).gameObject, "menu.play");
            yield return null;

            var hud = _boot.Ui.ScreenFor(GameState.Playing);
            float screenHeight = UiBuilder.ReferenceHeight;

            foreach (var selectable in hud.GetComponentsInChildren<Selectable>(includeInactive: false))
            {
                var rect = (RectTransform)selectable.transform;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);

                float low = Mathf.Min(corners[0].y, corners[1].y);
                float high = Mathf.Max(corners[2].y, corners[3].y);

                // The playfield band is the middle third of the screen.
                bool intrudes = high > Screen.height * 0.3f && low < Screen.height * 0.7f;
                Assert.IsFalse(intrudes,
                    $"{selectable.name} overlaps the gesture band (y {low:F0}..{high:F0} of {Screen.height})");
            }
        }

        [UnityTest]
        public IEnumerator Changing_language_rewrites_the_whole_interface()
        {
            var menu = _boot.Ui.ScreenFor(GameState.Menu);

            Strings.Use(Language.English);
            _boot.Ui.RefreshStrings();
            string english = FindLabel(menu, "menu.play");

            Strings.Use(Language.Russian);
            _boot.Ui.RefreshStrings();
            string russian = FindLabel(menu, "menu.play");

            Assert.AreNotEqual(english, russian, "the play button did not change language");
            Assert.AreEqual(Strings.Get("menu.play"), russian);
            yield return null;
        }

        static string FindLabel(RectTransform screen, string buttonName)
        {
            foreach (var button in screen.GetComponentsInChildren<Button>(includeInactive: true))
                if (button.name == buttonName) return button.GetComponentInChildren<Text>().text;
            Assert.Fail($"no button '{buttonName}'");
            return null;
        }

        [UnityTest]
        public IEnumerator A_fresh_install_shows_no_empty_record()
        {
            // CONTRACT.md G-04: "Best: 0" before the first run is noise.
            var menu = _boot.Ui.ScreenFor(GameState.Menu);
            var best = FindByName(menu, "Best");

            Assert.IsNotNull(best);
            Assert.IsFalse(best.gameObject.activeSelf, "an unearned record should not be shown");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_locked_theme_cannot_be_chosen()
        {
            var themes = _boot.Ui.ScreenFor(GameState.ThemeSelect);
            foreach (var button in themes.GetComponentsInChildren<Button>(includeInactive: true))
            {
                if (!button.name.StartsWith("Theme")) continue;

                string name = button.name.Substring("Theme".Length);
                var theme = (Theme)System.Enum.Parse(typeof(Theme), name);
                bool unlocked = _boot.Game.ScoreKeeper.IsUnlocked(theme);

                Assert.AreEqual(unlocked, button.interactable,
                    $"{theme} interactable={button.interactable} but unlocked={unlocked}");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Changing_a_setting_writes_it_to_disk()
        {
            // Through the real control, not through reflection: what matters is that the path a
            // player actually takes ends in a file on disk.
            var settings = _boot.Ui.ScreenFor(GameState.Settings);
            Toggle vibration = null;
            foreach (var toggle in settings.GetComponentsInChildren<Toggle>(includeInactive: true))
                if (toggle.name == "set.vibration") vibration = toggle;

            Assert.IsNotNull(vibration, "the settings screen has no vibration toggle");
            Assert.IsTrue(_boot.Settings.vibrationEnabled, "vibration should start on");

            vibration.isOn = false;
            yield return null;

            Assert.IsFalse(_boot.Settings.vibrationEnabled, "the toggle did not reach the settings");

            string path = Path.Combine(_directory, SaveService.FileName);
            Assert.IsTrue(File.Exists(path), "changing a setting wrote nothing");
            Assert.IsFalse(new SaveService(path).Load().vibrationEnabled,
                "the change did not survive a reload");
        }

        [UnityTest]
        public IEnumerator About_opens_shows_the_real_text_and_closes()
        {
            // The handler for this button was an empty lambda for a whole phase: the screen was
            // on the checklist, the strings were written, and pressing it did nothing at all.
            var settings = _boot.Ui.ScreenFor(GameState.Settings);
            var about = FindByName(settings, "About");
            Assert.IsNotNull(about, "the settings screen has no About panel");
            Assert.IsFalse(about.gameObject.activeSelf, "About should start closed");

            Press(settings.gameObject, "set.about");
            yield return null;
            Assert.IsTrue(about.gameObject.activeSelf, "pressing About opened nothing");

            bool hasBody = false;
            bool hasVersion = false;
            foreach (var text in about.GetComponentsInChildren<Text>(includeInactive: true))
            {
                hasBody |= text.text == Strings.Get("about.body");
                hasVersion |= text.text == Application.version;
            }

            Assert.IsTrue(hasBody, "About does not show the text the contract specifies");
            Assert.IsTrue(hasVersion, $"About does not show the version '{Application.version}'");

            Press(about.gameObject, "common.back");
            yield return null;
            Assert.IsFalse(about.gameObject.activeSelf, "Back did not close About");
        }

        [UnityTest]
        public IEnumerator Leaving_settings_closes_whatever_was_open_over_it()
        {
            var settings = _boot.Ui.ScreenFor(GameState.Settings);
            Press(settings.gameObject, "set.about");
            yield return null;

            _boot.Ui.Show(GameState.Menu);
            yield return null;

            var about = FindByName(settings, "About");
            Assert.IsFalse(about.gameObject.activeSelf,
                "About survived a screen change and would reappear over Settings");
        }

        [UnityTest]
        public IEnumerator Sound_and_music_are_switched_and_levelled_independently_and_persist()
        {
            // Phase 7's gate, taken literally: independent, and still independent after a reload.
            var settings = _boot.Ui.ScreenFor(GameState.Settings);

            Toggle sound = null;
            foreach (var toggle in settings.GetComponentsInChildren<Toggle>(includeInactive: true))
                if (toggle.name == "set.sound") sound = toggle;
            Assert.IsNotNull(sound, "no sound switch on the settings screen");

            Slider musicLevel = null;
            foreach (var slider in settings.GetComponentsInChildren<Slider>(includeInactive: true))
                if (slider.name == "set.music.level") musicLevel = slider;
            Assert.IsNotNull(musicLevel, "no music level on the settings screen");

            sound.isOn = false;
            musicLevel.value = 0.25f;
            yield return null;

            Assert.IsFalse(_boot.Settings.soundEnabled);
            Assert.IsTrue(_boot.Settings.musicEnabled, "turning sound off also turned music off");
            Assert.AreEqual(0.25f, _boot.Settings.musicVolume, 0.001f);

            var reloaded = new SaveService(Path.Combine(_directory, SaveService.FileName)).Load();
            Assert.IsFalse(reloaded.soundEnabled, "the sound switch did not survive a reload");
            Assert.IsTrue(reloaded.musicEnabled);
            Assert.AreEqual(0.25f, reloaded.musicVolume, 0.001f,
                "the music level did not survive a reload");
        }

        static RectTransform FindByName(RectTransform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<RectTransform>(includeInactive: true))
                if (child.name == name) return child;
            return null;
        }
    }
}
