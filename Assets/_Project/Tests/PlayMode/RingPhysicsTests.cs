using System.Collections;
using System.Collections.Generic;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
// The namespace brings in the ConstraintExpression extension that makes Is.Not.AllocatingGCMemory
// resolve; the alias settles which Is wins against NUnit's. Both are needed, neither alone.
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// The phase 1 gates. Physics is stepped from the test rather than waited on: a thousand
    /// trials at real frame rate would take twenty minutes and would not be deterministic.
    /// </summary>
    public class RingPhysicsTests
    {
        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;
        readonly List<GameObject> _spawned = new List<GameObject>();

        BallController _ball;
        TunnelController _tunnel;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _settings = _config.ToSettings();

            // Scripted stepping makes the trials deterministic and fast. Nothing else in the
            // game runs this way, so every test drives Step and ClampSpeed itself.
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -14f);
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_config != null) Object.DestroyImmediate(_config);
        }

        GameObject Track(GameObject go) { _spawned.Add(go); return go; }

        /// <summary>One ring at the equilibrium radius with the ball resting on it.</summary>
        void BuildSingleRing(double gapCentreDeg, double gapWidth, double autoSpinDeg, bool solid = false)
        {
            var root = Track(new GameObject("TestRoot"));
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _tunnel.Initialise(_config, _ball);
            _tunnel.AutoStep = false;   // this fixture steps explicitly
            _tunnel.CompressionEnabled = false;   // phase 1 isolates contact from compression

            var ring = TunnelFactory.CreateRing(_config, tunnelGo.transform, material, 0);
            if (solid) ring.ConfigureSolid(_settings.TargetRadius, autoSpinDeg);
            else ring.Configure(_settings.TargetRadius, gapCentreDeg, gapWidth, autoSpinDeg);

            // Every segment carries this material; give the ring's colliders their own.
            for (int i = 0; i < ring.ActiveSegmentCount; i++)
                ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;

            _tunnel.Register(ring);
        }

        void StepOnce(float dt)
        {
            _tunnel.Step(dt);
            _ball.ClampSpeed();
            Physics2D.Simulate(dt);
        }

        /// <summary>Steps for a while, returning the furthest the ball ever got from the centre.</summary>
        double Simulate(float seconds, float dt)
        {
            double furthest = 0.0;
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                StepOnce(dt);
                double d = _ball.DistanceToCentre;
                if (d > furthest) furthest = d;
            }
            return furthest;
        }

        double OuterEdge => _settings.TargetRadius + _settings.Thickness * 0.5;

        [UnityTest]
        public IEnumerator A_ball_resting_on_a_solid_arc_stays_in_its_corridor()
        {
            BuildSingleRing(gapCentreDeg: 90.0, gapWidth: _settings.MinGapWidth, autoSpinDeg: 0.0);
            yield return null;

            double furthest = Simulate(2.0f, 1f / 90f);
            Assert.Less(furthest, OuterEdge,
                "the ball left through a solid arc with the gap on the far side");
        }

        [UnityTest]
        public IEnumerator A_ball_falls_through_a_gap_lined_up_underneath_it()
        {
            BuildSingleRing(gapCentreDeg: 270.0, gapWidth: _settings.MinGapWidth, autoSpinDeg: 0.0);
            yield return null;

            double furthest = Simulate(2.0f, 1f / 90f);
            Assert.Greater(furthest, OuterEdge + _settings.PassMargin,
                "the ball did not fall through an aligned gap");
        }

        [UnityTest]
        public IEnumerator A_gap_of_one_point_two_ball_diameters_is_passable()
        {
            // The phase 1 gate, stated in ball diameters as the plan states it.
            double width = _settings.BallRadius * 2.0 * 1.2;
            BuildSingleRing(gapCentreDeg: 270.0, gapWidth: width, autoSpinDeg: 0.0);
            yield return null;

            double furthest = Simulate(3.0f, 1f / 90f);
            Assert.Greater(furthest, OuterEdge + _settings.PassMargin,
                $"a {width:F2}u opening did not pass a {_settings.BallRadius * 2:F2}u ball");
        }

        [UnityTest]
        public IEnumerator A_gap_narrower_than_the_ball_does_not_pass_it()
        {
            // The opposite guard: physics must not stretch an opening to let the ball squeeze out.
            BuildSingleRing(gapCentreDeg: 270.0, gapWidth: 0.9, autoSpinDeg: 0.0);
            yield return null;

            double furthest = Simulate(3.0f, 1f / 90f);
            Assert.Less(furthest, OuterEdge,
                "the ball squeezed through an opening narrower than itself");
        }

        [UnityTest]
        public IEnumerator The_built_opening_is_the_opening_that_was_configured()
        {
            BuildSingleRing(gapCentreDeg: 270.0, gapWidth: 1.5, autoSpinDeg: 0.0);
            yield return null;

            var ring = _tunnel.Rings[0];
            Assert.AreEqual(1.5, ring.MeasureWorldGapArcLength(), 1e-3,
                "the geometry that was built does not deliver the configured opening");
            Assert.AreEqual(_settings.Thickness, ring.MeasureWorldThickness(), 1e-3,
                "collider thickness does not match the configured thickness");
        }

        [UnityTest]
        public IEnumerator A_thousand_spinning_trials_never_tunnel_through_a_solid_ring()
        {
            // A ring with no gap at all: any escape is unambiguous. Random ball velocity and
            // random ring spin, both at their ceilings, which is where tunnelling would show.
            const int trials = 1000;
            var random = new System.Random(20261027);
            int escapes = 0;
            double worst = 0.0;

            for (int trial = 0; trial < trials; trial++)
            {
                double spin = (random.NextDouble() * 2.0 - 1.0) * _settings.ComfortSpinDeg * 2.7;
                BuildSingleRing(0, 0, spin, solid: true);

                float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                float speed = (float)(random.NextDouble() * _settings.MaxBallSpeed);
                _ball.Body.linearVelocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

                double furthest = Simulate(0.5f, 1f / 90f);
                if (furthest > worst) worst = furthest;
                if (furthest >= OuterEdge) escapes++;

                foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
                _spawned.Clear();

                if (trial % 100 == 0) yield return null;
            }

            Assert.AreEqual(0, escapes,
                $"{escapes}/{trials} trials tunnelled out; furthest reach {worst:F3} against edge {OuterEdge:F3}");
        }

        [UnityTest]
        public IEnumerator The_speed_ceiling_keeps_a_step_shorter_than_a_ring_is_thick()
        {
            // Why tunnelling cannot happen, rather than only that it did not: at the ceiling the
            // ball moves less than one thickness per step, so discrete collision cannot miss.
            BuildSingleRing(90.0, _settings.MinGapWidth, 0.0);
            yield return null;

            foreach (float rate in new[] { 60f, 90f, 120f })
            {
                float travel = (float)_settings.MaxBallSpeed / rate;
                Assert.Less(travel, _settings.Thickness,
                    $"at {rate} Hz the ball moves {travel:F3}u per step through {_settings.Thickness:F3}u of ring");
            }
        }

        [UnityTest]
        public IEnumerator A_steady_tunnel_step_allocates_nothing()
        {
            BuildSingleRing(90.0, _settings.MinGapWidth, 30.0);
            _tunnel.CompressionEnabled = true;
            yield return null;

            // Warm up first: the first rebuild fills buffers that are reused forever after.
            for (int i = 0; i < 20; i++) StepOnce(1f / 90f);

            Assert.That(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _tunnel.Step(1f / 90f);
                    _ball.ClampSpeed();
                }
            }, Is.Not.AllocatingGCMemory(), "the steady-state tunnel step allocates");
        }
    }
}
