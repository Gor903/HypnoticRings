using System.Collections;
using System.IO;
using HR.Core;
using HR.Gameplay;
using HR.Themes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Captures the theme set by code, so the pictures regenerate with one command instead of
    /// being played by hand and going stale the first time anything changes.
    ///
    /// It lives in play mode rather than in an editor script for a reason that cost several
    /// attempts: under a scriptable pipeline, rendering a runtime-built scene from an editor
    /// method in batch mode produces the clear colour and nothing else. Geometry, camera,
    /// shader and render request all check out individually; the frame still comes back as a
    /// single flat colour. Letting the normal player loop render the camera into a target and
    /// reading it at end of frame simply works, and needs no coaxing.
    /// </summary>
    public class ThemeScreenshotTests
    {
        const string OutputDir = "docs/publishing/art/screenshots/themes";

        static readonly (string Name, int Width, int Height)[] Shapes =
        {
            ("9x16", 1080, 1920),
            ("9x20", 1080, 2400),
            ("3x4", 1536, 2048),
        };

        GameConfig _config;
        TunnelSettings _settings;
        GameObject _root;
        Camera _camera;
        TunnelController _tunnel;
        ThemeApplier _applier;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _settings = _config.ToSettings();
            Physics2D.gravity = new Vector2(0f, -14f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [UnityTest]
        [Category("Art")]
        [Timeout(600_000)]
        public IEnumerator Capture_every_theme_at_every_shape()
        {
            // Nothing to capture without a renderer, and this is not a failure of the game: the
            // suite runs headless (-nographics), where SubmitRenderRequest returns the colour the
            // target was cleared to. tools/screenshots.sh runs this same test with a display,
            // which is where the pictures actually come from. Said out loud, because for days
            // this pair was simply red and everyone had learned to read past it.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a renderer — run tools/screenshots.sh, which keeps one");

            Directory.CreateDirectory(OutputDir);

            _root = new GameObject("ShotRig");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);
            _camera = cameraGo.GetComponent<Camera>();
            if (cameraGo.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGo.AddComponent<UniversalAdditionalCameraData>();
            _camera.orthographic = true;
            _camera.orthographicSize = _config.orthoSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.transform.position = new Vector3(0f, _config.CameraOffsetY, -10f);

            var ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _tunnel.AutoStep = false;
            var pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, ball, pool);

            var applierGo = new GameObject("Themes");
            applierGo.transform.SetParent(_root.transform, false);
            _applier = applierGo.AddComponent<ThemeApplier>();
            _applier.Initialise(_camera, _tunnel, ball.GetComponent<Renderer>(),
                ball.transform.Find("BallOutline").GetComponent<Renderer>());

            // One frame before anything is rendered: objects built this frame are not yet known
            // to culling, and a render request issued before that draws nothing at all.
            yield return null;

            int written = 0;
            foreach (var theme in ThemePalette.All)
            {
                // Same seed and the same number of steps for every frame, so the pictures differ
                // by theme alone and can be read side by side.
                _tunnel.BeginRun(seed: 20261027);
                foreach (var ring in _tunnel.Rings)
                    for (int i = 0; i < ring.ActiveSegmentCount; i++)
                        ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;

                for (int step = 0; step < 90; step++) _tunnel.Step(1f / 90f);
                _applier.Apply(theme);
                yield return null;

                foreach (var shape in Shapes)
                {
                    yield return Capture(theme, shape);
                    written++;
                }
            }

            Assert.AreEqual(ThemePalette.All.Length * Shapes.Length, written);
            Debug.Log($"[HR] screenshots: {written} frames in {OutputDir}");
        }

        /// <summary>Render requests to spend before calling a flat frame a failure.</summary>
        const int RenderAttempts = 4;

        /// <summary>True once the frame holds more than the one colour it was cleared to.</summary>
        static bool ShowsMoreThanOneColour(Texture2D frame)
        {
            var pixels = frame.GetPixels32();
            var first = pixels[0];
            for (int i = 1; i < pixels.Length; i += 97)
                if (pixels[i].r != first.r || pixels[i].g != first.g || pixels[i].b != first.b)
                    return true;
            return false;
        }

        IEnumerator Capture(Theme theme, (string Name, int Width, int Height) shape)
        {
            var target = new RenderTexture(shape.Width, shape.Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(shape.Width, shape.Height, TextureFormat.RGB24, false);

            // Batch mode has no display driving the loop, so waiting for the camera to render
            // itself waits forever — WaitForEndOfFrame never fires at all, and a plain frame
            // leaves the target untouched. The render has to be asked for explicitly. In play
            // mode the pipeline is fully stood up and the request actually draws, which is the
            // difference from trying the same call out of an editor method.
            //
            // Asked for more than once, because the first request of a run comes back empty
            // while the pipeline is still warming. A frame of one flat colour means nothing
            // rendered — a silent failure the file size does not reveal.
            bool varied = false;
            for (int attempt = 0; attempt < RenderAttempts && !varied; attempt++)
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.IsTrue(RenderPipeline.SupportsRenderRequest(_camera, request),
                    "this pipeline cannot service a single-camera render request");
                RenderPipeline.SubmitRenderRequest(_camera, request);
                yield return null;

                var previousActive = RenderTexture.active;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, shape.Width, shape.Height), 0, 0);
                readback.Apply();
                RenderTexture.active = previousActive;

                varied = ShowsMoreThanOneColour(readback);
            }

            // Written only once it is a picture. The file is a tracked store asset, and writing
            // first meant every failed run replaced one with a grey rectangle.
            string path = Path.Combine(OutputDir, $"{theme.ToString().ToLowerInvariant()}-{shape.Name}.png");
            Assert.IsTrue(varied, $"{path} came out a single flat colour — nothing rendered");
            File.WriteAllBytes(path, readback.EncodeToPNG());

            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
