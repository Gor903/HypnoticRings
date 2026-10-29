using System.Collections;
using System.Collections.Generic;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// The run as it is played after D-058: the ball flies instead of resting, a ring is retired
    /// the moment the ball leaves it, and the only way to lose is a ring closing onto the red
    /// circle at the centre.
    /// </summary>
    public class FlyingBallTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;
        float _bounceThreshold;

        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        RingPool _pool;
        GameManager _game;
        PhysicsMaterial2D _ringPhysics;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _settings = _config.ToSettings();

            _simulationMode = Physics2D.simulationMode;
            _bounceThreshold = Physics2D.bounceThreshold;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -14f);

            // The player's build gets this from the project settings; a scripted fixture has to
            // say so itself, and without it a ball with a full bounce quietly stops bouncing.
            Physics2D.bounceThreshold = 0.01f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            Physics2D.simulationMode = _simulationMode;
            Physics2D.bounceThreshold = _bounceThreshold;
        }

        void Build()
        {
            _root = new GameObject("FlightRoot");

            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            _ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _tunnel.AutoStep = false;
            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);

            var gameGo = new GameObject("Game");
            gameGo.transform.SetParent(_root.transform, false);
            _game = gameGo.AddComponent<GameManager>();
            _game.AutoStep = false;
            _game.Initialise(_config, _tunnel, _ball, 0);
        }

        void StartRun(int seed)
        {
            _game.StartRun(seed);
            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = _ringPhysics;
        }

        /// <summary>One step of everything, the way the real loop orders it.</summary>
        void Step(double userAngleDeg = 0.0)
        {
            _game.Tick(Dt, userAngleDeg);
            _ball.HoldCruisingSpeed(Dt);
            _ball.ClampSpeed();
            Physics2D.Simulate(Dt);
        }

        float Speed => _ball.Body.linearVelocity.magnitude;

        bool IsLive(RingController ring)
        {
            for (int i = 0; i < _tunnel.Rings.Count; i++)
                if (ReferenceEquals(_tunnel.Rings[i], ring)) return true;
            return false;
        }

        [UnityTest]
        public IEnumerator The_ball_is_still_flying_a_minute_later()
        {
            Build();
            StartRun(seed: 11);
            yield return null;

            float slowest = float.MaxValue;
            for (int i = 0; i < 90 * 60; i++)
            {
                // Compression off: this is about the flight, and a run that ends at fifteen
                // seconds cannot say anything about a minute of it.
                _tunnel.CompressionEnabled = false;
                Step();

                if (Speed < slowest) slowest = Speed;
                if (i % 900 == 0) yield return null;
            }

            Assert.Greater(slowest, _config.cruiseSpeed * 0.5f,
                $"the ball dropped to {slowest:F2} against a cruise of {_config.cruiseSpeed}");
            Assert.AreEqual(_config.cruiseSpeed, Speed, _config.cruiseSpeed * 0.2f,
                "a minute in, the ball is not near its cruising speed");
        }

        [UnityTest]
        public IEnumerator Restarts_throw_the_ball_in_different_directions()
        {
            Build();
            yield return null;

            var octants = new HashSet<int>();
            for (int seed = 1; seed <= 40; seed++)
            {
                StartRun(seed);
                var v = _ball.Body.linearVelocity;
                float deg = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                octants.Add(Mathf.FloorToInt(((deg + 360f) % 360f) / 45f));
                yield return null;
            }

            Assert.Greater(octants.Count, 5, $"forty runs used only {octants.Count} of eight headings");
        }

        [UnityTest]
        public IEnumerator A_run_opens_with_no_ring_inside_the_ball()
        {
            Build();
            StartRun(seed: 3);
            yield return null;

            double ballRing = _tunnel.ResolveBallRing().Radius;
            foreach (var ring in _tunnel.Rings)
                Assert.GreaterOrEqual(ring.Radius, ballRing - 1e-6,
                    "the opening layout still puts a ring inside the ball");

            Assert.AreEqual(_settings.RingsAhead + 1, _tunnel.ActiveRingCount,
                "the opening layout is not the ball's ring plus the ones ahead");
        }

        [UnityTest]
        public IEnumerator A_ring_the_ball_has_left_stops_being_a_wall_at_once()
        {
            Build();
            StartRun(seed: 5);
            yield return null;

            var ring = _tunnel.ResolveBallRing();
            Assert.IsFalse(ring.IsDissolving, "the opening ring is already dissolving");

            ring.BeginDissolve(_config.dissolveSeconds);

            for (int i = 0; i < ring.ActiveSegmentCount; i++)
                Assert.IsFalse(ring.SegmentAt(i).Collider.enabled,
                    "a ring the ball has left can still bat it back");

            // And it is gone once the fade is over, back in the pool rather than destroyed.
            // Not a claim that nothing was built: the ball's ring going away does leave the
            // tunnel one short of its rings ahead, and filling that is the right thing to do.
            // That the pool settles instead of growing is the restart and endless gates' job.
            int steps = Mathf.CeilToInt(_config.dissolveSeconds / Dt) + 2;
            for (int i = 0; i < steps; i++) Step();

            Assert.IsFalse(IsLive(ring), "the dissolved ring is still in the tunnel");
            Assert.AreEqual(_pool.Created, _pool.InUse + _pool.Available,
                            "a retired ring went somewhere other than the pool");
        }

        [UnityTest]
        public IEnumerator Clearing_a_ring_scores_once_even_though_the_ring_goes_away()
        {
            Build();
            StartRun(seed: 9);
            yield return null;

            int scored = 0;
            _tunnel.RingPassed += _ => scored++;

            // Walk the ball out through the gap by hand: this is about the latch surviving the
            // ring's disappearance, not about whether a flight happens to find the opening.
            var ring = _tunnel.ResolveBallRing();
            double gap = ring.GapCentreWorldDeg * Mathf.Deg2Rad;
            double outside = ring.Radius + _settings.Thickness * 0.5 + _settings.PassMargin + 0.2;

            _ball.Place(new Vector2((float)(Mathf.Cos((float)gap) * outside),
                                    (float)(Mathf.Sin((float)gap) * outside)));
            _tunnel.CompressionEnabled = false;

            for (int i = 0; i < 20; i++) Step();

            Assert.AreEqual(1, scored, "clearing a ring scored something other than once");
            Assert.AreEqual(1, _tunnel.PassedCount);

            // And it stays once, through the whole dissolve and past it.
            int steps = Mathf.CeilToInt(_config.dissolveSeconds / Dt) + 60;
            for (int i = 0; i < steps; i++) Step();
            Assert.AreEqual(1, scored, "the same ring scored again after it dissolved");
        }

        [UnityTest]
        public IEnumerator A_player_who_does_nothing_loses_to_the_red_circle_on_time()
        {
            Build();
            StartRun(seed: 13);

            // The ball's ring is sealed for this one. A flying ball finds the opening by itself
            // often enough that "did nothing" does not mean "went nowhere", and a run that
            // scores has a different timer — this test is about the timer, not about luck.
            var sealedRing = _tunnel.ResolveBallRing();
            sealedRing.ConfigureSolid(sealedRing.Radius, autoSpinDeg: 0.0);
            for (int i = 0; i < sealedRing.ActiveSegmentCount; i++)
                sealedRing.SegmentAt(i).Collider.sharedMaterial = _ringPhysics;
            yield return null;

            double budget = _game.CurrentDifficulty.TimeBudget;
            int limit = Mathf.CeilToInt((float)(budget * 2.0) / Dt);

            int steps = 0;
            while (_game.State == GameState.Playing && steps < limit)
            {
                Step();
                steps++;
                if (steps % 900 == 0) yield return null;
            }

            Assert.AreEqual(GameState.GameOver, _game.State, "the run never ended");
            Assert.AreEqual(0, _game.Score, "the sealed ring was somehow cleared");

            double seconds = steps * Dt;
            Assert.AreEqual(budget, seconds, budget * 0.1,
                $"lost after {seconds:F1}s against a budget of {budget:F1}s");
            Debug.Log($"[HR] red circle: lost after {seconds:F2}s, budget {budget:F2}s");
        }

        [UnityTest]
        public IEnumerator The_red_circle_is_not_a_wall_and_not_a_loss()
        {
            Build();
            StartRun(seed: 17);
            _tunnel.CompressionEnabled = false;
            yield return null;

            // Straight across the centre, right over the circle.
            _ball.Place(new Vector2(-2f, 0f));
            _ball.Body.linearVelocity = new Vector2(_config.cruiseSpeed, 0f);

            bool crossed = false;
            bool emerged = false;

            // Stops at the far side of the circle rather than running a fixed two seconds. The
            // ball flies on and bounces off the ring beyond, and whether it happens to be heading
            // right again at step 180 is a matter of its speed dividing the time — which D-059
            // changed, and which says nothing at all about the circle.
            for (int i = 0; i < 180 && !emerged; i++)
            {
                Step();
                if (_ball.DistanceToCentre < _config.redRadius) crossed = true;
                if (crossed && _ball.Body.position.x > _config.redRadius) emerged = true;

                Assert.AreEqual(GameState.Playing, _game.State,
                    "flying over the red circle ended the run");
            }

            Assert.IsTrue(crossed, "the ball never actually reached the circle");
            Assert.IsTrue(emerged, "the ball never came out the far side of the circle");

            // Nothing deflected it, so it is still heading the way it was sent.
            Assert.Greater(_ball.Body.linearVelocity.x, 0f, "the circle pushed the ball back");
        }

        [UnityTest]
        public IEnumerator Losing_is_decided_by_the_ring_and_not_by_where_the_ball_is()
        {
            Build();
            StartRun(seed: 19);
            _tunnel.CompressionEnabled = false;
            yield return null;

            var ring = _tunnel.ResolveBallRing();

            // The ball parked far from the centre, the ring brought down onto the circle.
            _ball.Place(new Vector2(0f, -(float)(_settings.TargetRadius - _settings.Thickness * 0.5
                                                 - _settings.BallRadius)));
            _ball.Body.linearVelocity = Vector2.zero;

            Assert.IsFalse(_tunnel.HasRingClosedOnCentre(), "the run was lost before anything closed");

            ring.SetRadius(_settings.CrushRadius - 1e-3);
            Assert.IsTrue(_tunnel.HasRingClosedOnCentre(),
                "a ring on the circle did not end the run");
        }
    }
}
