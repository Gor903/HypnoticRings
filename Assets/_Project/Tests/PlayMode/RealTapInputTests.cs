using System.Collections;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HR.Tests.PlayMode
{
    public class RealTapInputTests : InputTestFixture
    {
        GameConfig _config;
        GameObject _root;
        GameBootstrap _boot;
        Camera _camera;
        string _directory;

        public override void Setup()
        {
            base.Setup();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _directory = Path.Combine(Path.GetTempPath(), "hr-tap-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, runSeed: 7,
                new SaveService(Path.Combine(_directory, SaveService.FileName)), view: _camera);
            _root.SetActive(true);
        }

        public override void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
            base.TearDown();
        }

        Button Find(GameState screen, string name)
        {
            var host = _boot.Ui.ScreenFor(screen).gameObject;
            foreach (var button in host.GetComponentsInChildren<Button>(includeInactive: true))
                if (button.name == name) return button;
            Assert.Fail($"no button named '{name}' on {screen}");
            return null;
        }

        Vector2 ScreenPointOf(Button button)
        {
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(eventCamera, rect.position);
        }

        [UnityTest]
        public IEnumerator The_interface_brings_an_event_system_with_it()
        {
            var system = Object.FindAnyObjectByType<EventSystem>();
            Assert.IsNotNull(system, "the interface was built without an EventSystem");
            Assert.IsNotNull(system.GetComponent<BaseInputModule>(),
                             "the EventSystem carries no input module, so it reads nothing");

            yield return null;
            Assert.IsNotNull(system.currentInputModule, "the input module never came up");
        }

        [UnityTest]
        public IEnumerator A_tap_on_Play_starts_a_run()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null;

            Assert.AreEqual(GameState.Menu, _boot.Game.State, "the game did not start on the menu");

            Vector2 point = ScreenPointOf(Find(GameState.Menu, "menu.play"));

            BeginTouch(1, point, screen: touchscreen);
            yield return null;
            yield return null;
            EndTouch(1, point, screen: touchscreen);
            yield return null;

            Assert.AreEqual(GameState.Playing, _boot.Game.State,
                            "a real tap on Play did not reach the button");
        }

        [UnityTest]
        public IEnumerator A_tap_on_Themes_opens_the_theme_screen()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null;

            Vector2 point = ScreenPointOf(Find(GameState.Menu, "menu.themes"));

            BeginTouch(1, point, screen: touchscreen);
            yield return null;
            yield return null;
            EndTouch(1, point, screen: touchscreen);
            yield return null;

            Assert.AreEqual(GameState.ThemeSelect, _boot.Game.State,
                            "a real tap on Themes did not reach the button");
        }

        [UnityTest]
        public IEnumerator System_back_closes_the_screen_it_opened()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null;

            Vector2 point = ScreenPointOf(Find(GameState.Menu, "menu.settings"));
            BeginTouch(1, point, screen: touchscreen);
            yield return null;
            yield return null;
            EndTouch(1, point, screen: touchscreen);
            yield return null;
            Assert.AreEqual(GameState.Settings, _boot.Game.State, "settings never opened");

            _boot.Back.Raise();
            yield return null;

            Assert.AreEqual(GameState.Menu, _boot.Game.State,
                            "system back did not leave the settings screen");
        }

        [UnityTest]
        public IEnumerator System_back_twice_in_one_frame_counts_once()
        {
            yield return null;

            _boot.Game.StartRun(7);
            Assert.AreEqual(GameState.Playing, _boot.Game.State, "the run did not start");

            _boot.Back.Raise();
            _boot.Back.Raise();
            yield return null;

            Assert.AreEqual(GameState.Paused, _boot.Game.State,
                            "one press of back was handled twice and resumed the run again");
        }

        [UnityTest]
        public IEnumerator A_tap_on_Settings_opens_the_settings_screen()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null;

            Vector2 point = ScreenPointOf(Find(GameState.Menu, "menu.settings"));

            BeginTouch(1, point, screen: touchscreen);
            yield return null;
            yield return null;
            EndTouch(1, point, screen: touchscreen);
            yield return null;

            Assert.AreEqual(GameState.Settings, _boot.Game.State,
                            "a real tap on Settings did not reach the button");
        }
    }
}
