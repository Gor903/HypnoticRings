using System.Collections;
using System.Globalization;
using System.Text;
using HR.Core;
using HR.Gameplay;
using HR.Themes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 5's gate: a theme changes how the game looks and nothing about how it plays.
    ///
    /// This is checked by hashing every physics-relevant number in the scene before and after
    /// each theme is applied. Asserting "the applier only sets colours" by reading it is not the
    /// same thing — the point is that a future edit cannot quietly reach further.
    /// </summary>
    public class ThemeIsolationTests
    {
        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;

        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        RingPool _pool;
        ThemeApplier _applier;
        Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _settings = _config.ToSettings();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -14f);

            _root = new GameObject("ThemeRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _tunnel.AutoStep = false;
            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.BeginRun(seed: 42);

            var applierGo = new GameObject("Themes");
            applierGo.transform.SetParent(_root.transform, false);
            _applier = applierGo.AddComponent<ThemeApplier>();
            _applier.Initialise(_camera, _tunnel, _ball.GetComponent<Renderer>());
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Every number physics reads, in one string. Collider paths included: a theme that
        /// moved a single point of a single segment would change what the ball can touch.
        /// </summary>
        string PhysicsFingerprint()
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;

            sb.Append("gravity=").Append(Physics2D.gravity.y.ToString("F6", ci));
            sb.Append(";step=").Append(Time.fixedDeltaTime.ToString("F8", ci));
            sb.Append(";ballR=").Append(_ball.Radius.ToString("F6", ci));
            sb.Append(";ballMass=").Append(_ball.Body.mass.ToString("F6", ci));
            sb.Append(";ballGrav=").Append(_ball.Body.gravityScale.ToString("F6", ci));

            foreach (var ring in _tunnel.Rings)
            {
                sb.Append("|R=").Append(ring.Radius.ToString("F6", ci));
                sb.Append(",T=").Append(ring.MeasureWorldThickness().ToString("F6", ci));
                sb.Append(",gap=").Append(ring.MeasureWorldGapArcLength().ToString("F6", ci));
                sb.Append(",rot=").Append(ring.TotalRotationDeg.ToString("F6", ci));
                sb.Append(",segs=").Append(ring.ActiveSegmentCount);

                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                {
                    var collider = ring.SegmentAt(i).Collider;
                    var path = collider.GetPath(0);
                    sb.Append(",p").Append(i).Append('=').Append(path.Length);
                    foreach (var point in path)
                        sb.Append(':').Append(point.x.ToString("F5", ci))
                          .Append('/').Append(point.y.ToString("F5", ci));
                    sb.Append(",m=").Append(collider.sharedMaterial != null
                        ? collider.sharedMaterial.friction.ToString("F4", ci) : "none");
                }
            }

            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator Switching_theme_changes_no_physical_quantity()
        {
            yield return null;
            string before = PhysicsFingerprint();

            foreach (var theme in ThemePalette.All)
            {
                _applier.Apply(theme);
                Assert.AreEqual(before, PhysicsFingerprint(),
                    $"applying {theme} changed something physics reads");
            }

            // And back again, in case applying a theme twice differs from applying it once.
            foreach (var theme in ThemePalette.All) _applier.Apply(theme);
            Assert.AreEqual(before, PhysicsFingerprint(), "a second pass through the themes drifted");
        }

        [UnityTest]
        public IEnumerator Switching_theme_does_change_what_is_on_screen()
        {
            // The companion to the test above: proving a theme changes nothing would be easy if
            // it also changed nothing visible.
            yield return null;

            _applier.Apply(Theme.Monochrome);
            Color monochrome = _camera.backgroundColor;

            _applier.Apply(Theme.Neon);
            Assert.AreNotEqual(monochrome, _camera.backgroundColor, "the background did not change");
            Assert.AreEqual(Theme.Neon, _applier.Current);
        }

        [UnityTest]
        public IEnumerator A_theme_switch_mid_run_keeps_the_run()
        {
            yield return null;

            for (int i = 0; i < 200; i++)
            {
                _tunnel.Step(1f / 90f);
                Physics2D.Simulate(1f / 90f);
            }

            int ringsBefore = _tunnel.ActiveRingCount;
            double radiusBefore = _tunnel.Rings[0].Radius;

            _applier.Apply(Theme.Earth);

            Assert.AreEqual(ringsBefore, _tunnel.ActiveRingCount, "a theme change disturbed the tunnel");
            Assert.AreEqual(radiusBefore, _tunnel.Rings[0].Radius, 1e-9);
        }

        [UnityTest]
        public IEnumerator Painting_a_theme_never_clones_a_material()
        {
            // renderer.material clones on access, so a careless applier leaks one material per
            // theme change — invisible until somebody sits on the theme carousel.
            yield return null;

            int before = Resources.FindObjectsOfTypeAll<Material>().Length;
            for (int i = 0; i < 40; i++)
                _applier.Apply(ThemePalette.All[i % ThemePalette.All.Length]);

            int after = Resources.FindObjectsOfTypeAll<Material>().Length;
            Assert.LessOrEqual(after, before, $"materials grew from {before} to {after} over 40 theme changes");
        }

        [UnityTest]
        public IEnumerator Rings_take_their_colour_from_the_cycle_in_spawn_order()
        {
            yield return null;
            _applier.Apply(Theme.Neon);

            var palette = ThemePalette.For(Theme.Neon);
            Assert.Greater(palette.RingColours.Length, 1, "Neon should cycle several colours");

            // Neighbouring rings must not land on the same slot, or the tunnel reads flat.
            var rings = _tunnel.Rings;
            for (int i = 1; i < rings.Count; i++)
                Assert.AreNotEqual(rings[i - 1].SpawnIndex % palette.RingColours.Length,
                                   rings[i].SpawnIndex % palette.RingColours.Length,
                                   "two neighbouring rings share a colour slot");
        }
    }
}
