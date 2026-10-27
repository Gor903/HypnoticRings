using System.Collections;
using System.Globalization;
using System.Text;
using HR.Core;
using HR.Gameplay;
using HR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 8's gates. The one that matters most is that "Reduce effects" is a visual setting
    /// and nothing else: a player who turns it off to save battery must not be playing a
    /// different game from one who leaves it on, and a leaderboard would make that a fairness
    /// problem rather than a cosmetic one.
    /// </summary>
    public class EffectsTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;

        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        InputController _input;
        Effects _effects;
        Camera _camera;
        double _fingerAngle;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _settings = _config.ToSettings();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -14f);
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        void Build(int seed, bool effectsEnabled)
        {
            _root = new GameObject("EffectsRoot");

            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _tunnel.Initialise(_config, _ball, new RingPool(_config, tunnelGo.transform, material));
            _tunnel.AutoStep = false;
            _tunnel.BeginRun(seed);

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(_root.transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(_config.ToGestureSettings(), null);
            _input.ExternalDriver = true;

            var effectsGo = new GameObject("Effects");
            effectsGo.transform.SetParent(_root.transform, false);
            _effects = effectsGo.AddComponent<Effects>();
            _effects.Initialise(_config, _camera, _ball.transform, material);
            _effects.Enabled = effectsEnabled;

            _fingerAngle = 0.0;
        }

        Vector2 FingerAt(double degrees)
        {
            float radians = (float)(degrees * Mathf.Deg2Rad);
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 3f;
        }

        void StepOnce(BotBrain bot)
        {
            var ring = _tunnel.ResolveBallRing();
            if (ring != null)
            {
                _fingerAngle += bot.Decide(TunnelMath.BottomAngleDeg, ring.GapCentreWorldDeg, Dt);
                _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
            }

            _tunnel.Step(Dt);
            _ball.ClampSpeed();
            Physics2D.Simulate(Dt);

            // Exactly where the game drives them, so anything they touched would show up below.
            _effects.Tick(Dt);
        }

        /// <summary>
        /// Everything the simulation reads, as one string.
        ///
        /// Modelled on ThemeIsolationTests, which proves the same kind of property the same way
        /// and was right to. The obvious alternative — run the game twice and compare the two
        /// runs — does not work here and is worth saying out loud: Unity's 2D physics is not
        /// reproducible across sequential runs inside one session, and the same settings twice
        /// gave 221374.69 and 226998.30. A test built on that compares the solver's mood, not
        /// the thing under test.
        /// </summary>
        string PhysicsFingerprint()
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;

            sb.Append("ballPos=").Append(_ball.transform.position.x.ToString("F7", ci))
              .Append('/').Append(_ball.transform.position.y.ToString("F7", ci));
            sb.Append(";vel=").Append(_ball.Body.linearVelocity.x.ToString("F7", ci))
              .Append('/').Append(_ball.Body.linearVelocity.y.ToString("F7", ci));
            sb.Append(";mass=").Append(_ball.Body.mass.ToString("F6", ci));
            sb.Append(";grav=").Append(Physics2D.gravity.y.ToString("F6", ci));
            sb.Append(";passes=").Append(_tunnel.PassedCount);

            foreach (var ring in _tunnel.Rings)
            {
                sb.Append("|R=").Append(ring.Radius.ToString("F7", ci));
                sb.Append(",rot=").Append(ring.TotalRotationDeg.ToString("F7", ci));
                sb.Append(",segs=").Append(ring.ActiveSegmentCount);
            }

            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator Effects_never_touch_anything_the_simulation_reads()
        {
            // The gate CONTRACT.md asks for, asked of the live scene rather than of two runs:
            // whatever the effects do during a tick, the game state on either side of it is
            // identical, step after step, including while a flash and a shake are running.
            Build(seed: 404, effectsEnabled: true);
            var bot = BotBrain.Strong(11);
            int checkedSteps = 0;

            for (int i = 0; i < 3000; i++)
            {
                var ring = _tunnel.ResolveBallRing();
                if (ring != null)
                {
                    _fingerAngle += bot.Decide(TunnelMath.BottomAngleDeg, ring.GapCentreWorldDeg, Dt);
                    _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                    _tunnel.UserAngleDeg = _input.UserAngleDeg;
                }

                _tunnel.Step(Dt);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);

                // Effects fire often, and while they are mid-flight, not only at rest.
                if (i % 300 == 0) _effects.Flash(_ball.transform.position, Color.cyan);
                if (i == 1500) _effects.Shake();

                string before = PhysicsFingerprint();
                _effects.Tick(Dt);
                string after = PhysicsFingerprint();

                Assert.AreEqual(before, after, $"the effects changed the game at step {i}");
                checkedSteps++;

                if (i % 1000 == 0) yield return null;
            }

            Assert.AreEqual(3000, checkedSteps);
            Debug.Log($"[HR] effects isolation: {checkedSteps} steps, nothing the simulation " +
                      "reads was touched");
        }

        [UnityTest]
        public IEnumerator Effects_build_nothing_once_the_run_has_started()
        {
            // CONTRACT.md: no Instantiate and no Destroy in the game loop. Everything the effects
            // can ever draw exists before the first step.
            Build(seed: 5, effectsEnabled: true);
            int built = _effects.transform.childCount;
            Assert.Greater(built, 0, "the effects built nothing at all");

            var bot = BotBrain.Strong(3);
            for (int i = 0; i < 2000; i++)
            {
                StepOnce(bot);
                if (i % 500 == 0) _effects.Flash(_ball.transform.position, Color.white);
            }

            Assert.AreEqual(built, _effects.transform.childCount,
                "the effects grew while the game was running");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_shake_puts_the_camera_back_exactly_where_it_was()
        {
            // Not approximately: a shake that leaves a residue leaves a bigger one every death.
            Build(seed: 9, effectsEnabled: true);
            var home = _camera.transform.localPosition;

            _effects.Shake();

            bool moved = false;
            for (float t = 0f; t < _config.crushSeconds * 1.5f; t += Dt)
            {
                _effects.Tick(Dt);
                if ((_camera.transform.localPosition - home).sqrMagnitude > 1e-8f) moved = true;
            }

            Assert.IsTrue(moved, "the camera never shook at all");
            Assert.AreEqual(home.x, _camera.transform.localPosition.x, 1e-6f);
            Assert.AreEqual(home.y, _camera.transform.localPosition.y, 1e-6f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_shake_stays_inside_the_amplitude_the_spec_gives()
        {
            // Spec 11.4: 0.08 world units. A shake that exceeds it reads as a bug, not a thump.
            Build(seed: 9, effectsEnabled: true);
            var home = _camera.transform.localPosition;
            _effects.Shake();

            float furthest = 0f;
            for (float t = 0f; t < _config.crushSeconds; t += Dt)
            {
                _effects.Tick(Dt);
                furthest = Mathf.Max(furthest,
                    (_camera.transform.localPosition - home).magnitude);
            }

            Assert.LessOrEqual(furthest, _config.crushShakeAmplitude * 1.45f,
                $"the camera moved {furthest:F4} units against an amplitude of " +
                $"{_config.crushShakeAmplitude}");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Switched_off_the_effects_draw_nothing_and_hold_nothing_open()
        {
            Build(seed: 12, effectsEnabled: true);

            var bot = BotBrain.Strong(4);
            for (int i = 0; i < 300; i++) StepOnce(bot);
            _effects.Flash(_ball.transform.position, Color.red);
            _effects.Shake();

            _effects.Enabled = false;
            _effects.Clear();

            for (int i = 0; i < _effects.transform.childCount; i++)
                Assert.IsFalse(_effects.transform.GetChild(i).gameObject.activeSelf,
                    $"{_effects.transform.GetChild(i).name} was left on screen");

            // And it stays that way: ticking a disabled director must not revive anything.
            for (int i = 0; i < 100; i++) StepOnce(bot);

            for (int i = 0; i < _effects.transform.childCount; i++)
                Assert.IsFalse(_effects.transform.GetChild(i).gameObject.activeSelf,
                    "a disabled effect came back");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_crush_draws_the_rings_in_and_clearing_puts_them_back()
        {
            // Spec 11.4: the rings close around the ball over 0.4 s. The part that matters for
            // correctness is the second half — a collapse that is not undone means the next run
            // starts in a tunnel that is permanently 18% smaller than the one before it.
            Build(seed: 77, effectsEnabled: true);

            var home = _tunnel.transform.position;
            var ball = _ball.transform.position;

            _effects.Collapse(_tunnel.transform, ball);

            for (float t = 0f; t < _config.crushSeconds; t += Dt) _effects.Tick(Dt);

            Assert.Less(_tunnel.transform.localScale.x, 0.95f, "the rings never drew in");

            _effects.Clear();
            Assert.AreEqual(1f, _tunnel.transform.localScale.x, 1e-5f,
                "the tunnel stayed shrunk after the effect finished");
            Assert.AreEqual(home.x, _tunnel.transform.position.x, 1e-5f);
            Assert.AreEqual(home.y, _tunnel.transform.position.y, 1e-5f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_collapse_never_starts_when_effects_are_off()
        {
            Build(seed: 77, effectsEnabled: false);
            var home = _tunnel.transform.position;

            _effects.Collapse(_tunnel.transform, _ball.transform.position);
            for (float t = 0f; t < _config.crushSeconds; t += Dt) _effects.Tick(Dt);

            Assert.AreEqual(1f, _tunnel.transform.localScale.x, 1e-6f);
            Assert.AreEqual(home.x, _tunnel.transform.position.x, 1e-6f);
            yield return null;
        }

        [Test]
        public void Vibration_obeys_the_switch_and_the_two_durations()
        {
            // Spec 11.4: 20 ms on a pass, 60 ms on a loss, and nothing at all when switched off.
            var driver = new Haptics.Silent();
            var haptics = new Haptics(_config.vibratePassMs, _config.vibrateLoseMs, driver);

            haptics.Pass();
            Assert.AreEqual(_config.vibratePassMs, driver.LastMilliseconds);

            haptics.Lose();
            Assert.AreEqual(_config.vibrateLoseMs, driver.LastMilliseconds);
            Assert.AreEqual(2, driver.Calls);

            haptics.Enabled = false;
            haptics.Pass();
            haptics.Lose();
            Assert.AreEqual(2, driver.Calls, "the device buzzed after vibration was switched off");
        }
    }
}
