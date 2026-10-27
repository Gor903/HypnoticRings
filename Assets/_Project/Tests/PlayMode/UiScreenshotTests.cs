using System.Collections;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Captures every screen at every shape the game ships on, so the layout can be looked at
    /// rather than reasoned about — and so it is regenerated with one command when it changes.
    /// </summary>
    public class UiScreenshotTests
    {
        const string OutputDir = "docs/publishing/art/screenshots/screens";

        /// <summary>Portrait shapes from a tall phone to a tablet (CONTRACT.md phase 6 gate).</summary>
        static readonly (string Name, int Width, int Height)[] Shapes =
        {
            ("9x16", 1080, 1920),
            ("9x18", 1080, 2160),
            ("9x20", 1080, 2400),
            ("9x21", 1080, 2520),
            ("3x4", 1536, 2048),
        };

        static readonly (string Name, GameState State)[] Screens =
        {
            ("menu", GameState.Menu),
            ("hud", GameState.Playing),
            ("pause", GameState.Paused),
            ("gameover", GameState.GameOver),
            ("themes", GameState.ThemeSelect),
            ("settings", GameState.Settings),
        };

        GameConfig _config;
        GameObject _root;
        GameBootstrap _boot;
        Camera _camera;
        string _directory;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _directory = Path.Combine(Path.GetTempPath(), "hr-shot-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7.5f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.transform.position = new Vector3(0f, (0.5f - 0.46f) * 2f * 7.5f, -10f);
            if (cameraGo.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGo.AddComponent<UniversalAdditionalCameraData>();

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, runSeed: 20261027,
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

        [UnityTest]
        [Category("Art")]
        [Timeout(600_000)]
        public IEnumerator Capture_every_screen_at_every_shape()
        {
            Directory.CreateDirectory(OutputDir);

            // A record and a couple of unlocked themes, so the screens show something real
            // rather than a blank first-run state.
            _boot.Game.ScoreKeeper.RestoreBest(37);
            _boot.Ui.RefreshStrings();

            // The camera that takes the picture must be the camera the theme reached. When it was
            // not, every frame came out on Unity's default slate blue while the rings were
            // correctly themed — a picture that looks like a broken theme and is really a second
            // camera. Cheap to assert, and it fails here rather than forty frames later.
            var palette = ThemePalette.For(_boot.Settings.Theme);
            var expected = new Color(palette.Background.R / 255f, palette.Background.G / 255f,
                                     palette.Background.B / 255f, 1f);
            Assert.AreEqual(expected.r, _camera.backgroundColor.r, 0.01f, "camera is not themed");
            Assert.AreEqual(expected.g, _camera.backgroundColor.g, 0.01f, "camera is not themed");
            Assert.AreEqual(expected.b, _camera.backgroundColor.b, 0.01f, "camera is not themed");

            int written = 0;
            foreach (var screen in Screens)
            {
                PrepareFor(screen.State);
                yield return null;

                foreach (var shape in Shapes)
                {
                    yield return Capture(screen.Name, shape);
                    written++;
                }
            }

            // The two modals never appear in the state loop, because they are not states. Left
            // out they would be the only surfaces in the game nobody ever looks at.
            foreach (var modal in new[] { "set.about", "set.reset" })
            {
                _boot.Ui.Show(GameState.Settings);
                Press(GameState.Settings, modal);
                yield return null;

                string name = modal == "set.about" ? "about" : "confirm-reset";
                foreach (var shape in Shapes)
                {
                    yield return Capture(name, shape);
                    written++;
                }
            }

            Assert.AreEqual((Screens.Length + 2) * Shapes.Length, written);
            Debug.Log($"[HR] ui screenshots: {written} frames in {OutputDir}");
        }

        void Press(GameState screen, string button)
        {
            foreach (var candidate in _boot.Ui.ScreenFor(screen)
                         .GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true))
                if (candidate.name == button) { candidate.onClick.Invoke(); return; }

            Assert.Fail($"no button '{button}' on {screen}");
        }

        void PrepareFor(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                case GameState.Paused:
                    _boot.Game.StartRun(20261027);
                    _boot.Ui.SetScore(12);
                    _boot.Ui.RefreshHud();
                    if (state == GameState.Paused) _boot.Game.Pause();
                    break;

                case GameState.GameOver:
                    _boot.Game.StartRun(20261027);
                    _boot.Game.States.Fire(GameTrigger.Crushed);
                    break;

                default:
                    _boot.Ui.Show(state);
                    break;
            }

            _boot.Ui.Show(_boot.Game.State == state ? state : state);
        }

        IEnumerator Capture(string screen, (string Name, int Width, int Height) shape)
        {
            // The layout is told its size outright. Sizing only the texture would capture the
            // batch window's shape five times over and label the files as five devices.
            _camera.aspect = (float)shape.Width / shape.Height;
            _boot.Ui.OverrideLayoutShape(_camera, shape.Width, shape.Height);
            yield return null;

            var target = new RenderTexture(shape.Width, shape.Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(shape.Width, shape.Height, TextureFormat.RGB24, false);

            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(_camera, request));
            RenderPipeline.SubmitRenderRequest(_camera, request);
            yield return null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, shape.Width, shape.Height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            string path = Path.Combine(OutputDir, $"{screen}-{shape.Name}.png");
            File.WriteAllBytes(path, readback.EncodeToPNG());

            // A flat frame means nothing rendered — the silent failure that cost phase 5 an
            // afternoon. Caught here rather than by looking at fifteen files afterwards.
            var pixels = readback.GetPixels32();
            var first = pixels[0];
            bool varied = false;
            for (int i = 1; i < pixels.Length && !varied; i += 53)
                varied = pixels[i].r != first.r || pixels[i].g != first.g || pixels[i].b != first.b;
            Assert.IsTrue(varied, $"{path} is a single flat colour — nothing rendered");

            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
