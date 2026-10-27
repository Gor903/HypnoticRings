using System.Collections;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Asserts the tunnel actually puts pixels on the screen.
    ///
    /// This exists because for five phases it did not, and nothing noticed. Every ring was
    /// wound the wrong way round and so back-face culled: the meshes were built, the vertex
    /// and index counts were right, the bounds were right, the renderers reported themselves
    /// visible, the geometry tests passed — and the screen was empty. There is no error for
    /// this, no warning and no magenta. The first time anything was rendered at all was the
    /// phase 5 screenshot gate, and it fell out immediately.
    ///
    /// So: one test that draws the real tunnel and counts pixels. Cheap, and it fails loudly
    /// for a whole class of mistakes that are otherwise completely silent.
    /// </summary>
    public class GeometryRendersTests
    {
        GameConfig _config;
        GameObject _root;
        SimulationMode2D _simulationMode;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [UnityTest]
        public IEnumerator The_tunnel_and_the_ball_are_actually_drawn()
        {
            var settings = _config.ToSettings();
            _root = new GameObject("RenderCheck");

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);
            if (cameraGo.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGo.AddComponent<UniversalAdditionalCameraData>();

            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0f, 0.6f, -10f);

            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ball = TunnelFactory.CreateBall(_config, _root.transform,
                TunnelFactory.CreatePhysicsMaterial("B", 0.25f, 0.2f), material, settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            var tunnel = tunnelGo.AddComponent<TunnelController>();
            tunnel.AutoStep = false;
            tunnel.Initialise(_config, ball, new RingPool(_config, tunnelGo.transform, material));
            tunnel.SetFadeColour(Color.black);
            tunnel.SetRingColours(new[] { Color.white });
            tunnel.BeginRun(seed: 1);

            yield return null;

            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request),
                "the pipeline cannot service a render request; this test cannot judge anything");
            RenderPipeline.SubmitRenderRequest(camera, request);
            yield return null;

            var readback = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            int lit = 0;
            foreach (var pixel in readback.GetPixels32())
                if (pixel.r > 60 || pixel.g > 60 || pixel.b > 60) lit++;

            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);

            // Rings and a ball across a 256-square frame are thousands of pixels. A handful
            // would mean something drew; zero means the scene is invisible.
            Assert.Greater(lit, 500, $"only {lit} of 65536 pixels were drawn — the tunnel is invisible");
            Debug.Log($"[HR] render check: {lit}/65536 pixels drawn");
        }

        [UnityTest]
        public IEnumerator Ring_meshes_face_the_camera()
        {
            // The same fact stated where it is cheap to check, so a regression names its cause
            // instead of just reporting a blank frame.
            int steps = ArcGeometry.StepsFor(120.0, 7.5);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps)];
            ArcGeometry.FillBand(3.0, 0.3, 0.0, 120.0, steps, verts, tris);

            for (int t = 0; t < tris.Length; t += 3)
            {
                Point2 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                Assert.Less(cross, 0.0, $"triangle {t / 3} faces away from the camera and will be culled");
            }

            yield return null;
        }
    }
}
