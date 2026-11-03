using System.Collections;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// The second ball, once it is in a scene: where it comes from, that it keeps out of the
    /// first one's way, and what happens when a ring bursts around one of them.
    ///
    /// The rule for whether a crush ends the run is arithmetic and lives in SpareLifeTests.
    /// What cannot live there is whether the tunnel actually counts who is inside what.
    /// </summary>
    public sealed class SecondBallTests
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

            _root = new GameObject("TwoBallRoot");
            _material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var physics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);
            var settings = _config.ToSettings();

            _ball = TunnelFactory.CreateBall(_config, _root.transform, physics, _material, settings);
            _spare = TunnelFactory.CreateBall(_config, _root.transform, physics, _material, settings);
            _spare.name = "SpareBall";
            Physics2D.IgnoreCollision(_ball.GetComponent<Collider2D>(),
                                      _spare.GetComponent<Collider2D>(), true);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, _material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.RegisterSpareBall(_spare);
            _tunnel.SetBeadMaterial(_material);
            _tunnel.AutoStep = false;
            _tunnel.BeginRun(seed: 12);
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        /// <summary>Hangs a bead on the ball's own ring and puts the ball on top of it.</summary>
        RingController PlantBeadUnderTheBall()
        {
            var ring = _tunnel.ResolveBallRing();
            Assert.IsNotNull(ring, "no ring to hang a bead on");

            ring.SetBead(0, _material, (float)(_config.ballRadius * 0.6));
            _ball.Place(ring.BeadWorldPosition);
            return ring;
        }

        [UnityTest]
        public IEnumerator Taking_a_bead_puts_a_second_ball_in_play()
        {
            Assert.AreEqual(1, _tunnel.BallCount, "the run did not start with one ball");

            var ring = PlantBeadUnderTheBall();
            _tunnel.Step(Dt);
            yield return null;

            Assert.AreEqual(2, _tunnel.BallCount, "the bead was not picked up");
            Assert.IsFalse(ring.HasBead, "the bead is still there after being taken");
            Assert.IsTrue(_tunnel.BallAt(1).gameObject.activeSelf, "the spare is still asleep");
        }

        [UnityTest]
        public IEnumerator A_second_bead_is_not_offered_while_two_balls_are_already_out()
        {
            PlantBeadUnderTheBall();
            _tunnel.Step(Dt);
            Assert.AreEqual(2, _tunnel.BallCount);
            yield return null;

            // A bead promises a ball the game has no room for. Any ring carrying one now would
            // be lying to the player about what taking it is worth.
            foreach (var ring in _tunnel.Rings)
                Assert.IsFalse(ring.HasBead, "a bead is on offer with both balls already in play");
        }

        [UnityTest]
        public IEnumerator The_spare_starts_somewhere_other_than_straight_ahead()
        {
            PlantBeadUnderTheBall();
            _ball.Body.linearVelocity = new Vector2((float)_config.cruiseSpeed, 0f);
            _tunnel.Step(Dt);
            yield return null;

            Vector2 first = _ball.Body.linearVelocity.normalized;
            Vector2 second = _tunnel.BallAt(1).Body.linearVelocity.normalized;

            // Born on the same heading the spare would shadow the first ball for the rest of
            // the run and be worth nothing at all.
            Assert.Less(Vector2.Dot(first, second), 0.5f,
                $"the spare set off along {second} against the first ball's {first}");
        }

        [UnityTest]
        public IEnumerator The_two_balls_pass_through_each_other()
        {
            PlantBeadUnderTheBall();
            _tunnel.Step(Dt);
            yield return null;

            var spare = _tunnel.BallAt(1);
            _ball.Place(new Vector2((float)_config.targetRadius - 0.5f, 0f));
            spare.Place(new Vector2((float)_config.targetRadius - 0.5f, 0f));
            _ball.Body.linearVelocity = Vector2.zero;
            spare.Body.linearVelocity = Vector2.zero;

            for (int i = 0; i < 30; i++) Physics2D.Simulate(Dt);

            // Two chances, not two objects in a pinball machine: a bounce between them would be
            // unreadable, and the player cannot steer either one directly to avoid it.
            Assert.Less(Vector2.Distance(_ball.transform.position, spare.transform.position), 0.05f,
                "the balls pushed each other apart");
        }

        [UnityTest]
        public IEnumerator Losing_a_ball_leaves_the_survivor_where_the_game_looks_for_it()
        {
            PlantBeadUnderTheBall();
            _tunnel.Step(Dt);
            yield return null;
            Assert.AreEqual(2, _tunnel.BallCount);

            // Apart first. Straight after a pick-up both balls share a ring, and the rule never
            // calls for a sacrifice then — two balls in one ring is one mistake, and
            // SpareLife.Resolve ends the run instead. Setting that state up here would be
            // testing a call the game never makes.
            var doomed = _tunnel.ResolveRingFor(_tunnel.BallAt(0));
            Assert.IsNotNull(doomed);

            var survivor = _tunnel.BallAt(1);
            survivor.Place(new Vector2((float)(doomed.Radius + _tunnel.StepNow), 0f));
            Assert.AreNotSame(doomed, _tunnel.ResolveRingFor(survivor),
                "the survivor is still in the ring that is about to burst");

            _tunnel.SacrificeBallIn(doomed);

            Assert.AreEqual(1, _tunnel.BallCount, "the run kept both balls");
            Assert.AreSame(survivor, _tunnel.BallAt(0),
                "the survivor is not in slot zero, where the bot and the camera look");
            Assert.IsTrue(_tunnel.BallAt(0).gameObject.activeSelf, "the survivor was switched off");
        }

        [UnityTest]
        public IEnumerator A_ring_is_worth_a_point_once_however_many_balls_leave_it()
        {
            PlantBeadUnderTheBall();
            _tunnel.Step(Dt);
            yield return null;

            int scored = 0;
            _tunnel.RingPassed += _ => scored++;

            var ring = _tunnel.ResolveRingFor(_tunnel.BallAt(0));
            Assert.IsNotNull(ring);

            // Both balls step out of the same ring. The second one is following, not earning.
            double clear = ring.Radius + _config.thickness * 0.5 + _config.passMargin + 0.05;
            _ball.Place(new Vector2((float)clear, 0f));
            _tunnel.Step(Dt);
            _tunnel.BallAt(1).Place(new Vector2(0f, (float)clear));
            _tunnel.Step(Dt);

            Assert.AreEqual(1, scored, $"the same ring scored {scored} times");
        }
    }
}
