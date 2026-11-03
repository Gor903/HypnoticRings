using System.Collections;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    public sealed class PassRateDiagnosticTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        SimulationMode2D _simulationMode;
        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        RingPool _pool;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -14f);

            _root = new GameObject("DiagRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);
            var settings = _config.ToSettings();

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.AutoStep = false;
            _tunnel.BeginRun(seed: 3);

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [UnityTest]
        public IEnumerator A_perfect_hand_still_tells_us_how_long_a_ring_takes()
        {
            const int steps = 27000;
            int passes = 0;
            _tunnel.RingPassed += _ => passes++;

            int stuckInSameRing = 0;
            int lastRing = -1;
            int worstStall = 0;

            for (int step = 0; step < steps; step++)
            {
                var ring = _tunnel.ResolveBallRing();
                if (ring == null) break;

                if (ring.SpawnIndex == lastRing)
                {
                    stuckInSameRing++;
                    if (stuckInSameRing > worstStall) worstStall = stuckInSameRing;
                }
                else
                {
                    lastRing = ring.SpawnIndex;
                    stuckInSameRing = 0;
                }

                double want = TunnelMath.ShortestDeltaDeg(
                    ring.NearestGapWorldDeg(_ball.AngleDeg), _ball.AngleDeg);
                double most = _config.comfortSpinDeg * Dt;
                _tunnel.UserAngleDeg += Mathf.Clamp((float)want, (float)-most, (float)most);

                _tunnel.Step(Dt);
                _ball.HoldCruisingSpeed(Dt);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);

                if (step % 3000 == 0) yield return null;
            }

            double perPass = passes > 0 ? steps / (double)passes : 0.0;
            Debug.Log($"[HR] diag: {passes} passes in {steps} steps — {perPass:F0} steps/pass, "
                      + $"worst stall {worstStall} steps, {_tunnel.ActiveRingCount} rings live");

            Assert.Greater(passes, 0, "a hand that cannot miss cleared nothing at all");
        }
    }
}
