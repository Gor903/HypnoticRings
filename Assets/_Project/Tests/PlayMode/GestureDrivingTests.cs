using System;
using System.Collections;
using System.Collections.Generic;
using HR.Core;
using HR.Gameplay;
using HR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 2's gate: a simulated player, steering through the same InputController a finger
    /// drives, gets the ball through a ring. The bot never touches a ring directly — it moves a
    /// virtual fingertip and lives with whatever the gesture layer makes of it.
    /// </summary>
    public class GestureDrivingTests
    {
        const float Dt = 1f / 90f;
        const double FingerRadius = 2.0;   // outside the dead zone, inside the screen

        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;
        readonly List<GameObject> _spawned = new List<GameObject>();

        BallController _ball;
        TunnelController _tunnel;
        RingController _ring;
        InputController _input;

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
            Cleanup();
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        void Build(double gapCentreDeg, double gapWidth, double autoSpinDeg, bool compress)
        {
            var root = new GameObject("GestureRoot");
            _spawned.Add(root);

            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _tunnel.Initialise(_config, _ball);
            _tunnel.CompressionEnabled = compress;

            _ring = TunnelFactory.CreateRing(_config, tunnelGo.transform, material, 0);
            _ring.Configure(_settings.TargetRadius, gapCentreDeg, gapWidth, autoSpinDeg);
            for (int i = 0; i < _ring.ActiveSegmentCount; i++)
                _ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;
            _tunnel.Register(_ring);

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(root.transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(_config.ToGestureSettings(), null);
            _input.ExternalDriver = true;   // the bot supplies the pointer, not the hardware
        }

        double OuterEdge => _settings.TargetRadius + _settings.Thickness * 0.5;
        bool BallHasPassed => _ball.DistanceToCentre > OuterEdge + _settings.PassMargin;

        static Vector2 FingerAt(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Vector2((float)(Math.Cos(rad) * FingerRadius), (float)(Math.Sin(rad) * FingerRadius));
        }

        /// <summary>Runs one attempt. Returns true if the ball got out before the time ran out.</summary>
        bool RunAttempt(BotBrain bot, float seconds)
        {
            double fingerAngle = 0.0;
            _input.FeedPointer(true, FingerAt(fingerAngle), Dt);

            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                double step = bot.Decide(_ball.AngleDeg, _ring.GapCentreWorldDeg, Dt);
                fingerAngle += step;

                // Everything the bot does goes through the gesture layer, speed cap and all.
                _input.FeedPointer(true, FingerAt(fingerAngle), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;

                _tunnel.Step(Dt);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);

                if (BallHasPassed) return true;
            }
            return false;
        }

        [UnityTest]
        public IEnumerator A_bot_steering_through_the_gesture_layer_clears_a_ring_almost_always()
        {
            // The phase 2 gate. Compression is on at the opening pace, so the bot has the real
            // time budget rather than forever.
            const int attempts = 300;
            int cleared = 0;
            var random = new System.Random(20261027);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                double gapAngle = random.NextDouble() * 360.0;
                Build(gapAngle, _settings.MinGapWidth * 1.5, autoSpinDeg: 0.0, compress: true);

                var bot = BotBrain.Medium(attempt);
                if (RunAttempt(bot, seconds: 10f)) cleared++;

                Cleanup();
                if (attempt % 50 == 0) yield return null;
            }

            double rate = cleared * 100.0 / attempts;
            // Logged, not just asserted: phase 4 tunes against this margin, and "it passed"
            // does not say whether it passed by three attempts or by none.
            Debug.Log($"[HR] bot gate: medium cleared {cleared}/{attempts} ({rate:F2}%)");
            Assert.GreaterOrEqual(rate, 99.0,
                $"medium bot cleared only {cleared}/{attempts} ({rate:F1}%)");
        }

        [UnityTest]
        public IEnumerator A_weak_bot_still_manages_a_wide_opening_most_of_the_time()
        {
            // Not a gate, a floor: if the slowest, shakiest hand cannot clear the tutorial ring,
            // the opening values are wrong rather than the player being bad.
            const int attempts = 100;
            int cleared = 0;
            var random = new System.Random(99);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Build(random.NextDouble() * 360.0, _settings.MinGapWidth * 2.0, 0.0, compress: true);
                if (RunAttempt(BotBrain.Weak(attempt), seconds: 12f)) cleared++;
                Cleanup();
                if (attempt % 25 == 0) yield return null;
            }

            Debug.Log($"[HR] bot floor: weak cleared {cleared}/{attempts} ({cleared * 100.0 / attempts:F2}%)");
            Assert.GreaterOrEqual(cleared, 90, $"weak bot cleared only {cleared}/{attempts}");
        }

        [UnityTest]
        public IEnumerator The_gesture_layer_turns_the_rings_it_is_given()
        {
            Build(90.0, _settings.MinGapWidth, 0.0, compress: false);
            yield return null;

            double before = _ring.GapCentreWorldDeg;

            _input.FeedPointer(true, FingerAt(0.0), Dt);
            for (int i = 1; i <= 20; i++)
            {
                _input.FeedPointer(true, FingerAt(i * 2.0), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
                _tunnel.Step(Dt);
                Physics2D.Simulate(Dt);
            }

            double moved = TunnelMath.ShortestDeltaDeg(before, _ring.GapCentreWorldDeg);
            Assert.AreEqual(40.0, moved, 1e-3, "the ring did not follow the gesture");
        }

        [UnityTest]
        public IEnumerator Losing_focus_mid_gesture_stops_the_tunnel_dead()
        {
            Build(90.0, _settings.MinGapWidth, 0.0, compress: false);
            yield return null;

            _input.FeedPointer(true, FingerAt(0.0), Dt);
            for (int i = 1; i <= 10; i++) _input.FeedPointer(true, FingerAt(i * 4.0), Dt);

            _input.Cancel();
            double atCancel = _input.UserAngleDeg;
            for (int i = 0; i < 30; i++) _input.FeedPointer(false, Vector2.zero, Dt);

            Assert.AreEqual(atCancel, _input.UserAngleDeg, 1e-6, "the tunnel coasted after a cancel");
            Assert.AreEqual(0.0, _input.VelocityDeg);
        }

        [UnityTest]
        public IEnumerator Releasing_mid_sweep_lets_the_tunnel_coast_to_a_stop()
        {
            Build(90.0, _settings.MinGapWidth, 0.0, compress: false);
            yield return null;

            _input.FeedPointer(true, FingerAt(0.0), Dt);
            for (int i = 1; i <= 10; i++) _input.FeedPointer(true, FingerAt(i * 4.0), Dt);

            double atRelease = _input.UserAngleDeg;
            _input.FeedPointer(false, Vector2.zero, Dt);
            for (int i = 0; i < 300; i++) _input.FeedPointer(false, Vector2.zero, Dt);

            Assert.Greater(_input.UserAngleDeg, atRelease, "no inertia after release");
            Assert.AreEqual(0.0, _input.VelocityDeg, "inertia never settled");
        }
    }
}
