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

        void Build(int best = 0)
        {
            _root = new GameObject("FlowRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
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
        /// Steps while actively holding the opening away from the bottom, so the run is lost on
        /// purpose rather than by luck.
        ///
        /// Doing nothing does not reliably lose: rings turn by themselves from the fourth
        /// onward, so a gap sooner or later passes under the ball and it falls through unaided.
        /// A test that waits for a loss therefore passes or fails on the seed. This drives the
        /// opening to the top instead, at a rate a hand could actually manage.
        /// </summary>
        void StepLosing(int steps)
        {
            double userAngle = 0.0;
            double maxStep = _config.maxUserAngularSpeedDeg * Dt;

            for (int i = 0; i < steps; i++)
            {
                var ring = _tunnel.ResolveBallRing();
                if (ring != null)
                {
                    double wanted = TunnelMath.ShortestDeltaDeg(ring.GapCentreWorldDeg, 90.0);
                    userAngle += Math.Max(-maxStep, Math.Min(maxStep, wanted));
                }

                _game.Tick(Dt, userAngle);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);
                if (_game.State != GameState.Playing) return;
            }
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
        public IEnumerator Keeping_the_opening_away_loses_the_run_and_only_once()
        {
            // The tunnel closes and the ball is crushed. The budget at the opening pace is about
            // fifteen seconds; forty is generous even allowing for a pass or two slipping through.
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
            double radius = _tunnel.Rings[0].Radius;
            for (int i = 0; i < 300; i++) _game.Tick(Dt, 0.0);
            Assert.AreEqual(radius, _tunnel.Rings[0].Radius, 1e-9, "the tunnel moved while paused");

            Assert.IsTrue(_game.Resume());
            for (int i = 0; i < 90; i++) { _game.Tick(Dt, 0.0); Physics2D.Simulate(Dt); }
            Assert.Less(_tunnel.Rings[0].Radius, radius, "the tunnel did not resume closing");
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_crush_fires_on_geometry_and_not_a_moment_early()
        {
            // The lose condition is measured, not inferred from a collision: the ball is crushed
            // when the cavity inside its ring has closed to the ball's own size.
            Build();
            _game.StartRun(seed: 5);

            double crushRadius = _settings.CrushRadius;
            int steps = Mathf.RoundToInt(40f / Dt);
            double userAngle = 0.0;
            double maxStep = _config.maxUserAngularSpeedDeg * Dt;

            for (int i = 0; i < steps && _game.State == GameState.Playing; i++)
            {
                var ring = _tunnel.ResolveBallRing();
                if (ring != null)
                {
                    if (ring.Radius > crushRadius + 0.05)
                        Assert.AreNotEqual(GameState.GameOver, _game.State,
                            $"crushed at radius {ring.Radius:F3} with the crush radius at {crushRadius:F3}");

                    double wanted = TunnelMath.ShortestDeltaDeg(ring.GapCentreWorldDeg, 90.0);
                    userAngle += Math.Max(-maxStep, Math.Min(maxStep, wanted));
                }

                _game.Tick(Dt, userAngle);
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
