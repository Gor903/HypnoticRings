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

        static GameObject Child(RectTransform screen, string name)
        {
            foreach (var found in screen.GetComponentsInChildren<Transform>(includeInactive: true))
                if (found.name == name) return found.gameObject;

            Assert.Fail($"no object named '{name}' under {screen.name}");
            return null;
        }

        [UnityTest]
        public IEnumerator Game_over_without_ads_keeps_the_score_inside_the_card()
        {
            _boot.Game.StartRun(7);
            yield return WaitOutCountdown();
            _boot.Game.States.Fire(GameTrigger.Crushed);
            yield return null;

            Assert.AreEqual(GameState.GameOver, _boot.Game.State);

            var screen = _boot.Ui.ScreenFor(GameState.GameOver);
            var card = Child(screen, "Card");

            Assert.IsFalse(Child(screen, "Score").activeInHierarchy,
                           "the top score band belongs to the ad layout");
            Assert.IsFalse(Child(screen, "Best").activeInHierarchy,
                           "the top record band belongs to the ad layout");
            Assert.IsTrue(Child(screen, "CardScore").activeInHierarchy,
                          "without a MREC the score lives inside the card");
            Assert.IsTrue(Child(screen, "CardBest").activeInHierarchy,
                          "without a MREC the record lives inside the card");

            var retry = Child(screen, "over.retry");
            Assert.AreEqual(GameUi.RetryTall,
                            ((RectTransform)retry.transform).sizeDelta.y, 0.5f,
                            "the old layout gives the accent button its shipped height");

            float cardTop = ((RectTransform)card.transform).anchoredPosition.y
                            + ((RectTransform)card.transform).rect.height;
            float safe = ((RectTransform)_boot.Ui.ScreenFor(GameState.GameOver)).rect.height;
            Assert.Greater(cardTop, safe * 0.45f,
                           "the card must not leave the screen empty above it");
        }

        [UnityTest]
        public IEnumerator Game_over_with_a_mrec_lifts_the_score_out_of_the_card()
        {
            _boot.Game.StartRun(7);
            yield return WaitOutCountdown();
            _boot.Game.States.Fire(GameTrigger.Crushed);
            yield return null;

            _boot.Ui.OverrideLayoutShape(Camera.main, 1080, 1920);
            yield return null;

            var plan = _boot.Ui.ApplyAdLayout(AdScreen.GameOver, new AdInsets(150.0, 750.0),
                                              rewardButton: false, themeUnlocked: false,
                                              adsAllowed: true);
            yield return null;

            Assert.IsTrue(plan.ShowsMrec,
                          $"a 750-unit MREC must fit 1920 units; §15.4 asks for "
                          + $"{AdLayout.MinimumHeight(AdScreen.GameOver, 150.0, 750.0, false, false)}");

            var screen = _boot.Ui.ScreenFor(GameState.GameOver);
            Assert.IsTrue(Child(screen, "Score").activeInHierarchy);
            Assert.IsTrue(Child(screen, "Best").activeInHierarchy);
            Assert.IsFalse(Child(screen, "CardScore").activeInHierarchy);
            Assert.IsFalse(Child(screen, "CardBest").activeInHierarchy);

            Assert.AreEqual(UiBuilder.TouchTarget,
                            ((RectTransform)Child(screen, "over.retry").transform).sizeDelta.y, 0.5f,
                            "the ad layout drops the accent button to 150");
        }

        static void Press(GameObject screen, string name)
        {
            foreach (var button in screen.GetComponentsInChildren<Button>(includeInactive: true))
                if (button.name == name) { button.onClick.Invoke(); return; }
            Assert.Fail($"no button named '{name}' on {screen.name}");
        }

        IEnumerator WaitOutCountdown()
        {
            double waited = 0.0;
            while (_boot.Game.State == GameState.Countdown
                   && waited < CountdownClock.Total * 2.0)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.AreNotEqual(GameState.Countdown, _boot.Game.State,
                "the countdown never ended on its own");
        }

        [UnityTest]
        public IEnumerator The_whole_journey_works_end_to_end()
        {
            var ui = _boot.Ui;
            Assert.AreEqual(GameState.Menu, _boot.Game.State);

            Press(ui.ScreenFor(GameState.Menu).gameObject, "menu.play");
            Assert.AreEqual(GameState.Countdown, _boot.Game.State,
                "every run opens with the countdown");
            yield return WaitOutCountdown();
            Assert.AreEqual(GameState.Playing, _boot.Game.State);

            _boot.Game.States.Fire(GameTrigger.Crushed);
            Assert.AreEqual(GameState.GameOver, _boot.Game.State);

            Press(ui.ScreenFor(GameState.GameOver).gameObject, "over.retry");
            Assert.AreEqual(GameState.Countdown, _boot.Game.State,
                "a retry opens with the countdown too");
            yield return WaitOutCountdown();
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
            _boot.Game.StartPlaying();

            Press(ui.ScreenFor(GameState.Playing).gameObject, "Pause");
            Assert.AreEqual(GameState.Paused, _boot.Game.State);

            Press(ui.ScreenFor(GameState.Paused).gameObject, "pause.resume");
            Assert.AreEqual(GameState.Playing, _boot.Game.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Every_tap_target_is_big_enough_to_hit()
        {
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

        double QuarterTurnOfAFinger()
        {
            const double radius = 3.0;
            const float dt = 1f / 30f;

            var input = _boot.Input;
            input.ExternalDriver = true;
            input.ResetAngle();

            input.FeedPointer(true, new Vector2((float)radius, 0f), dt);
            for (int i = 1; i <= 18; i++)
            {
                double radians = i * 5.0 * System.Math.PI / 180.0;
                input.FeedPointer(true, new Vector2((float)(System.Math.Cos(radians) * radius),
                                                    (float)(System.Math.Sin(radians) * radius)), dt);
            }

            double turned = input.UserAngleDeg;
            input.Cancel();
            return turned;
        }

        [UnityTest]
        public IEnumerator The_sensitivity_slider_reaches_the_finger()
        {
            var settings = _boot.Ui.ScreenFor(GameState.Settings);

            var row = FindByName(settings, "set.sensitivity");
            Assert.IsNotNull(row, "no sensitivity row on the settings screen");

            var sensitivity = row.GetComponentInChildren<Slider>(includeInactive: true);
            Assert.IsNotNull(sensitivity, "the sensitivity row has no slider in it");

            double atDefault = QuarterTurnOfAFinger();
            Assert.AreEqual(90.0 * _config.sensitivity, atDefault, 1e-3,
                            "the config's gain never reached the gesture");

            sensitivity.value = SaveData.MaxSensitivity;
            yield return null;

            Assert.AreEqual(atDefault * SaveData.MaxSensitivity, QuarterTurnOfAFinger(), 1e-3,
                            "moving the slider did not change how far a finger turns the rings");
        }

        [UnityTest]
        public IEnumerator Sound_and_music_are_switched_independently_and_persist()
        {
            var settings = _boot.Ui.ScreenFor(GameState.Settings);

            Toggle sound = null, music = null;
            foreach (var toggle in settings.GetComponentsInChildren<Toggle>(includeInactive: true))
            {
                if (toggle.name == "set.sound") sound = toggle;
                if (toggle.name == "set.music") music = toggle;
            }

            Assert.IsNotNull(sound, "no sound switch on the settings screen");
            Assert.IsNotNull(music, "no music switch on the settings screen");

            sound.isOn = false;
            yield return null;

            Assert.IsFalse(_boot.Settings.soundEnabled);
            Assert.IsTrue(_boot.Settings.musicEnabled, "turning sound off also turned music off");

            var reloaded = new SaveService(Path.Combine(_directory, SaveService.FileName)).Load();
            Assert.IsFalse(reloaded.soundEnabled, "the sound switch did not survive a reload");
            Assert.IsTrue(reloaded.musicEnabled);
        }

        static RectTransform FindByName(RectTransform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<RectTransform>(includeInactive: true))
                if (child.name == name) return child;
            return null;
        }
    }
}
