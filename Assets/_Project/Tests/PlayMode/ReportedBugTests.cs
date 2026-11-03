using System.Collections;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    public sealed class ReportedBugTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        SimulationMode2D _simulationMode;
        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        RingPool _pool;
        Material _material;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;

            _root = new GameObject("BugRoot");
            _material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var physics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);
            var settings = _config.ToSettings();

            _ball = TunnelFactory.CreateBall(_config, _root.transform, physics, _material, settings);
            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, _material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.AutoStep = false;
            _tunnel.BeginRun(seed: 7);
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [UnityTest]
        public IEnumerator A_ring_does_not_dissolve_until_the_ball_has_actually_left_it()
        {
            var ring = _tunnel.ResolveBallRing();
            Assert.IsNotNull(ring);

            _ball.Place(new Vector2((float)ring.Radius, 0f));
            _tunnel.Step(Dt);
            yield return null;

            Assert.IsFalse(ring.IsDissolving,
                "the ring began dissolving while the ball was still level with it");

            double clear = ring.Radius + _config.thickness * 0.5 + _config.passMargin + 0.05;
            _ball.Place(new Vector2((float)clear, 0f));
            _tunnel.Step(Dt);

            Assert.IsTrue(ring.IsDissolving, "the ring survived a ball that had cleared it");
        }

        [UnityTest]
        public IEnumerator A_ring_the_ball_is_still_inside_is_still_a_wall()
        {
            var ring = _tunnel.ResolveBallRing();
            Assert.IsNotNull(ring);

            _ball.Place(new Vector2((float)ring.Radius, 0f));
            _tunnel.Step(Dt);
            yield return null;

            int live = 0;
            for (int i = 0; i < ring.ActiveSegmentCount; i++)
                if (ring.SegmentAt(i).Collider.enabled) live++;

            Assert.Greater(live, 0,
                "the ring switched its colliders off while the ball was still inside it");
        }

        [UnityTest]
        public IEnumerator A_new_run_starts_with_a_clean_touch_tally()
        {
            var ring = _tunnel.ResolveBallRing();
            Assert.IsNotNull(ring);

            double atWall = ring.Radius - _config.thickness * 0.5 - _config.ballRadius;
            _ball.Place(new Vector2((float)atWall, 0f));
            _ball.Body.linearVelocity = new Vector2(5f, 0f);
            _tunnel.Step(Dt);
            _ball.Body.linearVelocity = new Vector2(-5f, 0f);
            _tunnel.Step(Dt);
            yield return null;

            Assert.AreEqual(1, _tunnel.TouchesOn(0), "the touch was not counted at all");

            _tunnel.BeginRun(seed: 9);
            Assert.AreEqual(0, _tunnel.TouchesOn(0),
                "the new run started with the last run's touches against it");
        }

        [UnityTest]
        public IEnumerator A_ball_that_got_outside_the_tunnel_still_meets_a_wall()
        {
            double outermost = 0.0;
            foreach (var ring in _tunnel.Rings)
                if (ring.Radius > outermost) outermost = ring.Radius;

            _ball.Place(new Vector2((float)(outermost + 0.5), 0f));
            _tunnel.Step(Dt);
            yield return null;

            Assert.IsNull(_tunnel.ResolveBallRing(), "the ball is still inside something");

            int solid = 0;
            foreach (var ring in _tunnel.Rings)
            {
                if (ring.IsDissolving) continue;
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    if (ring.SegmentAt(i).Collider.enabled) { solid++; break; }
            }

            Assert.Greater(solid, 0,
                "every ring went soft for a ball that had left the tunnel, so it could never come back");
        }
    }
}
