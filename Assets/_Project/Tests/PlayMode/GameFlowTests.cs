using System;
using System.Collections;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 4's play-mode gates: losing properly, restarting cleanly, and fifty restarts in a
    /// row leaving nothing behind.
    /// </summary>
    public class GameFlowTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;

        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        GameManager _game;
        RingPool _pool;

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
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        [UnityTest]
        public IEnumerator Resuming_holds_the_tunnel_still_for_the_grace_period()
        {
            // CONTRACT.md G-02 and G-08. The contract rules out a countdown, so the only thing
            // standing between a returning player and a tunnel caught mid-squeeze is this.
            Build();
            _game.StartRun(99);
            for (int i = 0; i < 10; i++) _game.Tick(Dt, 0.0);

            _game.Pause();
            Assert.IsTrue(_game.Resume(), "resume was refused");
            Assert.IsTrue(_game.IsWarmingUp, "resume gave no grace at all");

            double radiusAtResume = _tunnel.Rings[0].Radius;

            // Just short of the grace: the clock has moved, the tunnel has not.
            int steps = Mathf.FloorToInt((float)(GameManager.ResumeGraceSeconds / Dt)) - 1;
            for (int i = 0; i < steps; i++) _game.Tick(Dt, 0.0);

            Assert.AreEqual(radiusAtResume, _tunnel.Rings[0].Radius, 1e-9,
                "the tunnel advanced during the grace period");
            Assert.AreEqual(GameState.Playing, _game.State);

            // Past it, the game moves again.
            for (int i = 0; i < 4; i++) _game.Tick(Dt, 0.0);
            Assert.IsFalse(_game.IsWarmingUp, "the grace never ended");

            for (int i = 0; i < 20; i++) _game.Tick(Dt, 0.0);
            Assert.AreNotEqual(radiusAtResume, _tunnel.Rings[0].Radius,
                "the tunnel never resumed moving");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_retry_gets_no_leftover_grace()
        {
            // The grace belongs to a resume. Carried into a fresh run it would be a free moment
            // at the start of every retry, which is a different game.
            Build();
            _game.StartRun(1);
            for (int i = 0; i < 5; i++) _game.Tick(Dt, 0.0);
            _game.Pause();
            _game.Resume();
            Assert.IsTrue(_game.IsWarmingUp);

            _game.StartRun(2);
            Assert.IsFalse(_game.IsWarmingUp, "a retry inherited the resume grace");
            yield return null;
        }

        PhysicsMaterial2D _ringPhysics;

        void Build(int best = 0)
        {
            _root = new GameObject("FlowRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            _ringPhysics = ringPhysics;
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.AutoStep = false;

            var gameGo = new GameObject("Game");
            gameGo.transform.SetParent(_root.transform, false);
            _game = gameGo.AddComponent<GameManager>();
            _game.AutoStep = false;
            _game.Initialise(_config, _tunnel, _ball, best);
        }

        /// <summary>
        /// Steps a run to its end on purpose rather than by luck.
        ///
        /// Doing nothing does not reliably lose: the ball flies, and a gap sooner or later ends
        /// up on its path whatever the player does. A test that waits for a loss would therefore
        /// pass or fail on the seed.
        /// </summary>
        void StepLosing(int steps)
        {
            // Sealed, not steered. Holding the opening away from the bottom used to keep a
            // resting ball in; a flying one leaves through whichever gap it reaches, so the only
            // honest way to say "the player did not get out" is to give them no way out.
            Seal(_tunnel.ResolveBallRing());

            for (int i = 0; i < steps; i++)
            {
                _game.Tick(Dt, 0.0);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);
                if (_game.State != GameState.Playing) return;
            }
        }

        /// <summary>Closes a ring's opening and gives its new colliders the ring material back.</summary>
        void Seal(RingController ring)
        {
            if (ring == null) return;
            ring.ConfigureSolid(ring.Radius, autoSpinDeg: 0.0);
            for (int i = 0; i < ring.ActiveSegmentCount; i++)
                ring.SegmentAt(i).Collider.sharedMaterial = _ringPhysics;
        }

        /// <summary>The ring furthest out, which is the one no cull can take.</summary>
        RingController Outermost()
        {
            RingController best = null;
            for (int i = 0; i < _tunnel.Rings.Count; i++)
                if (best == null || _tunnel.Rings[i].Radius > best.Radius) best = _tunnel.Rings[i];
            return best;
        }

        static int SceneObjectCount() =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator A_run_starts_in_the_menu_and_enters_play()
        {
            Build();
            Assert.AreEqual(GameState.Menu, _game.State, "Initialise should leave the game at the menu");

            _game.StartRun(seed: 1);
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(0, _game.Score);
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_ball_with_no_way_out_loses_the_run_and_only_once()
        {
            // The ring closes onto the red circle. The budget at the opening pace is about four
            // and a half seconds since D-059, so forty is generous several times over.
            Build();
            _game.StartRun(seed: 2);

            int transitions = 0;
            _game.StateChanged += (_, to) => { if (to == GameState.GameOver) transitions++; };

            StepLosing(Mathf.RoundToInt(40f / Dt));

            Assert.AreEqual(GameState.GameOver, _game.State, "the run should have ended");
            Assert.AreEqual(1, transitions, "game over was entered more than once");
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_tunnel_stops_once_the_run_is_over()
        {
            Build();
            _game.StartRun(seed: 3);
            StepLosing(Mathf.RoundToInt(40f / Dt));
            Assert.AreEqual(GameState.GameOver, _game.State);

            double radius = _tunnel.Rings.Count > 0 ? _tunnel.Rings[0].Radius : 0.0;
            for (int i = 0; i < 200; i++) _game.Tick(Dt, 0.0);

            double after = _tunnel.Rings.Count > 0 ? _tunnel.Rings[0].Radius : 0.0;
            Assert.AreEqual(radius, after, 1e-9, "the tunnel kept closing after the run ended");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pausing_freezes_the_tunnel_and_resuming_releases_it()
        {
            Build();
            _game.StartRun(seed: 4);
            StepLosing(60);

            Assert.IsTrue(_game.Pause());

            // One ring, held by reference. Watching Rings[0] instead watches a slot, not a ring:
            // a cull removes an entry and everything behind it shifts forward, so the slot can
            // come back holding a ring further out — which reads exactly like a tunnel that
            // stopped closing. The outermost ring is the one the cull can never take.
            var ring = Outermost();
            double radius = ring.Radius;

            for (int i = 0; i < 300; i++) _game.Tick(Dt, 0.0);
            Assert.AreEqual(radius, ring.Radius, 1e-9, "the tunnel moved while paused");

            Assert.IsTrue(_game.Resume());
            for (int i = 0; i < 90; i++) { _game.Tick(Dt, 0.0); Physics2D.Simulate(Dt); }
            Assert.Less(ring.Radius, radius, "the tunnel did not resume closing");
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_crush_fires_on_geometry_and_not_a_moment_early()
        {
            // The lose condition is measured, not inferred from a collision: the run ends when
            // a ring's inner edge reaches the red circle, and not a moment before.
            Build();
            _game.StartRun(seed: 5);
            Seal(_tunnel.ResolveBallRing());

            double losingRadius = _settings.CrushRadius;
            int steps = Mathf.RoundToInt(40f / Dt);

            for (int i = 0; i < steps && _game.State == GameState.Playing; i++)
            {
                var ring = _tunnel.ResolveBallRing();
                if (ring != null && ring.Radius > losingRadius + 0.05)
                    Assert.AreNotEqual(GameState.GameOver, _game.State,
                        $"lost at radius {ring.Radius:F3} with the circle reached at {losingRadius:F3}");

                _game.Tick(Dt, 0.0);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);
            }

            Assert.AreEqual(GameState.GameOver, _game.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Retrying_resets_the_score_and_the_tunnel()
        {
            Build();
            _game.StartRun(seed: 6);
            StepLosing(Mathf.RoundToInt(40f / Dt));
            Assert.AreEqual(GameState.GameOver, _game.State);

            _game.StartRun(seed: 6);
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(0, _game.Score);
            Assert.AreEqual(_settings.TargetRadius + _settings.Step * _settings.RingsAhead,
                            MaxRadius(), 1e-6, "the tunnel did not go back to its opening shape");
            yield return null;
        }

        double MaxRadius()
        {
            double max = 0.0;
            foreach (var ring in _tunnel.Rings) if (ring.Radius > max) max = ring.Radius;
            return max;
        }

        [UnityTest]
        [Timeout(600_000)]
        public IEnumerator Fifty_restarts_leave_nothing_behind()
        {
            // The phase 4 gate. Each run is played to its end and restarted; what matters is
            // that the fiftieth leaves the scene the same size as the second.
            Build();
            _game.StartRun(seed: 0);
            StepLosing(Mathf.RoundToInt(40f / Dt));

            _game.StartRun(seed: 1);
            StepLosing(Mathf.RoundToInt(40f / Dt));

            int objectsAfterTwo = SceneObjectCount();
            int createdAfterTwo = _pool.Created;
            int transformsPerRing = 1 + _tunnel.Rings[0].transform.childCount;

            for (int run = 2; run < 50; run++)
            {
                _game.StartRun(seed: run);
                Assert.AreEqual(GameState.Playing, _game.State, $"run {run} did not start");
                Assert.AreEqual(0, _game.Score, $"run {run} started with a score");
                StepLosing(Mathf.RoundToInt(40f / Dt));
                Assert.AreEqual(GameState.GameOver, _game.State, $"run {run} did not end");
                if (run % 10 == 0) yield return null;
            }

            // The same distinction D-018 drew for the endless gate, which I should have applied
            // here first time: a pool settles at its high-water mark, and that mark need not be
            // reached by the second run. A leak is unbounded; a pool is not. So the pool must
            // stay inside the live-ring budget, and every transform that appeared has to be
            // accounted for by those pooled rings and nothing else.
            double furthestBallRing = _settings.TargetRadius + 2.0 * _settings.Step;
            int inside = (int)Math.Floor((furthestBallRing - _settings.VanishRadius) / _settings.Step);
            int allowedLive = inside + 1 + _settings.RingsAhead + 1;

            int poolGrowth = _pool.Created - createdAfterTwo;
            int unexplained = (SceneObjectCount() - objectsAfterTwo) - poolGrowth * transformsPerRing;

            Assert.AreEqual(50, _game.RunCount);
            Assert.LessOrEqual(_pool.Created, allowedLive + 2,
                $"{_pool.Created} rings created over 50 runs — the pool is growing, not reusing");
            Assert.LessOrEqual(unexplained, 0,
                $"the scene grew by {SceneObjectCount() - objectsAfterTwo} transforms, only "
                + $"{poolGrowth * transformsPerRing} explained by {poolGrowth} new pooled ring(s)");
            Assert.AreEqual(_pool.Created, _pool.InUse + _pool.Available, "a ring went missing");

            Debug.Log($"[HR] restart gate: 50 runs, {_pool.Created} rings created "
                      + $"(budget {allowedLive + 2}), {SceneObjectCount()} transforms, "
                      + $"{unexplained} unexplained");
        }

        [UnityTest]
        public IEnumerator The_best_survives_a_restart_and_the_shown_best_updates_between_runs()
        {
            Build(best: 5);
            Assert.AreEqual(5, _game.Best);

            _game.StartRun(seed: 7);
            Assert.AreEqual(5, _game.DisplayedBest, "the run should show the best it started with");

            StepLosing(Mathf.RoundToInt(40f / Dt));
            _game.StartRun(seed: 8);
            Assert.AreEqual(Math.Max(5, 0), _game.DisplayedBest);
            Assert.AreEqual(0, _game.Score);
            yield return null;
        }
    }
}
