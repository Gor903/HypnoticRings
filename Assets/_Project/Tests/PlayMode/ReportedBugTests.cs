using System.Collections;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Three bugs found by playing the build on a phone, each turned into the test that would
    /// have caught it.
    ///
    /// Worth keeping together: none of them were visible to any gate the project had. Two came
    /// from the same cause and one from a reset that was never written, and all three needed a
    /// human to notice that the game was behaving oddly rather than wrongly.
    /// </summary>
    public sealed class ReportedBugTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        SimulationMode2D _simulationMode;
        GameObject _root;
        BallController _ball;
        BallController _spare;
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
            _spare = TunnelFactory.CreateBall(_config, _root.transform, physics, _material, settings);
            _spare.name = "SpareBall";

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, _material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.RegisterSpareBall(_spare);
            _tunnel.SetBeadMaterial(_material);
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

        /// <summary>
        /// Reported: "the ring disappears when the ball hits the ring above; it should go when
        /// the ball has left the smallest one".
        ///
        /// It used to dissolve the instant the ball's centre crossed its inner surface — while
        /// the ball was still level with it, halfway through the gap. On screen that reads as
        /// the ring vanishing as the ball reaches the next one.
        /// </summary>
        [UnityTest]
        public IEnumerator A_ring_does_not_dissolve_until_the_ball_has_actually_left_it()
        {
            var ring = _tunnel.ResolveBallRing();
            Assert.IsNotNull(ring);

            // Dead centre of the ring's own thickness: inside its opening, not yet through it.
            _ball.Place(new Vector2((float)ring.Radius, 0f));
            _tunnel.Step(Dt);
            yield return null;

            Assert.IsFalse(ring.IsDissolving,
                "the ring began dissolving while the ball was still level with it");

            // Past the outer edge and the margin: now it really has been cleared.
            double clear = ring.Radius + _config.thickness * 0.5 + _config.passMargin + 0.05;
            _ball.Place(new Vector2((float)clear, 0f));
            _tunnel.Step(Dt);

            Assert.IsTrue(ring.IsDissolving, "the ring survived a ball that had cleared it");
        }

        /// <summary>
        /// The same cause, seen from the side that cost a run: colliders went off with the
        /// dissolve, so a ball still inside the ring had nothing to bounce off.
        /// </summary>
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

        /// <summary>
        /// Reported: "I reached the bead ring, lost without taking it, and the next game had
        /// two balls".
        ///
        /// BeginRun reset the rings, the score, the latches and the wave — and never the ball
        /// count. A run that ended with two balls handed both to the next one.
        /// </summary>
        [UnityTest]
        public IEnumerator A_new_run_always_starts_with_exactly_one_ball()
        {
            var ring = _tunnel.ResolveBallRing();
            ring.SetBead(0, _material, (float)(_config.ballRadius * 0.6));
            _ball.Place(ring.BeadWorldPosition);
            _tunnel.Step(Dt);
            yield return null;

            Assert.AreEqual(2, _tunnel.BallCount, "the bead did not produce a second ball");

            _tunnel.BeginRun(seed: 8);

            Assert.AreEqual(1, _tunnel.BallCount, "the second ball survived into the next run");
            Assert.IsFalse(_tunnel.BallAt(1).gameObject.activeSelf,
                "the spare is still on screen at the start of a run");
        }

        /// <summary>
        /// The same reset, for the tally the hardcore mode kills on. Ring numbers start over
        /// with the run, so a ledger still watching "ring 0" keeps the count it ended with.
        /// </summary>
        [UnityTest]
        public IEnumerator A_new_run_starts_with_a_clean_touch_tally()
        {
            var ring = _tunnel.ResolveBallRing();
            Assert.IsNotNull(ring);

            // Against the inner wall, turned from outward to inward: one touch by definition.
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

        /// <summary>
        /// Reported: "the ball left a ring, flew straight through and vanished".
        ///
        /// A ball outside every ring had no ring of its own, and the solidity rule made rings
        /// walls only for balls that had one — so nothing was a wall, and a ball that got out
        /// once could never be caught again.
        /// </summary>
        [UnityTest]
        public IEnumerator A_ball_that_got_outside_the_tunnel_still_meets_a_wall()
        {
            double outermost = 0.0;
            foreach (var ring in _tunnel.Rings)
                if (ring.Radius > outermost) outermost = ring.Radius;

            // Beyond everything: no ring encloses it any more.
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
