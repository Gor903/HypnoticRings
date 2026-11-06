using System.Collections;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    public sealed class HardcoreRuleTests
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
        PhysicsMaterial2D _ringPhysics;

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

        void Build()
        {
            _root = new GameObject("HardcoreRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            _ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
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
            _game.Initialise(_config, _tunnel, _ball, 0);
        }

        void Seal(RingController ring)
        {
            if (ring == null) return;
            ring.ConfigureSolid(ring.Radius, autoSpinDeg: 0.0);
            for (int i = 0; i < ring.ActiveSegmentCount; i++)
                ring.SegmentAt(i).Collider.sharedMaterial = _ringPhysics;
        }

        RingController SealFirstJudgedRing(int budget = 2000)
        {
            for (int i = 0; i < budget && _game.State == GameState.Playing; i++)
            {
                var ring = _tunnel.ResolveBallRing();
                if (ring != null && ring.SpawnIndex > GameManager.FreeRingIndex)
                {
                    Seal(ring);
                    return ring;
                }

                _game.Tick(Dt, 0.0);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);
            }
            return null;
        }

        RingController StartOnAJudgedRing(GameMode mode)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (_root != null) { Object.DestroyImmediate(_root); _root = null; }
                Build();
                _game.Mode = mode;
                _game.StartRun(12345 + attempt * 877);
                _game.StartPlaying();

                var ring = SealFirstJudgedRing();
                if (ring != null) return ring;
            }
            return null;
        }

        int StepUntilTouches(int wanted, int budget)
        {
            int touches = 0;
            _tunnel.BallTouchedRing += seen => touches = seen;

            for (int i = 0; i < budget && touches < wanted; i++)
            {
                _game.Tick(Dt, 0.0);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);
                if (_game.State != GameState.Playing) break;
            }

            return touches;
        }

        [UnityTest]
        public IEnumerator A_second_touch_of_the_same_ring_ends_a_hardcore_run()
        {
            Assert.NotNull(StartOnAJudgedRing(GameMode.Hardcore),
                           "no seed got the ball onto a ring the rule judges");

            int touches = StepUntilTouches(2, 4000);

            Assert.AreEqual(2, touches, "the ball never touched its sealed ring twice");
            Assert.AreEqual(GameState.GameOver, _game.State,
                            "a second touch of the same ring left the run alive");
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_first_touch_only_warns_and_does_not_kill()
        {
            Assert.NotNull(StartOnAJudgedRing(GameMode.Hardcore),
                           "no seed got the ball onto a ring the rule judges");

            int touches = StepUntilTouches(1, 4000);

            Assert.AreEqual(1, touches, "the ball never touched its sealed ring");
            Assert.AreEqual(GameState.Playing, _game.State, "the first touch ended the run");
            Assert.IsTrue(_tunnel.ResolveBallRing().IsHot, "the first touch gave no warning");
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_same_two_touches_are_harmless_in_the_normal_game()
        {
            Assert.NotNull(StartOnAJudgedRing(GameMode.Normal),
                           "no seed got the ball onto a ring the rule judges");

            int touches = StepUntilTouches(2, 4000);

            Assert.AreEqual(2, touches, "the ball never touched its sealed ring twice");
            Assert.AreEqual(GameState.Playing, _game.State,
                            "touching twice ended a normal run, which is a hardcore rule");
            yield return null;
        }

        public const int KnocksAllowedBeforeDeath = 2;

        [UnityTest]
        public IEnumerator A_ball_trapped_in_a_ring_cannot_knock_it_for_free()
        {
            int worst = 0;
            int worstSeed = 0;

            for (int run = 0; run < 8; run++)
            {
                int seed = 4000 + run * 131;
                Build();
                _game.Mode = GameMode.Hardcore;
                _game.StartRun(seed);
                _game.StartPlaying();
                if (SealFirstJudgedRing() == null) { Object.DestroyImmediate(_root); _root = null; yield return null; continue; }

                int contacts = 0;
                _ball.Struck += _ => contacts++;

                for (int i = 0; i < 1200 && _game.State == GameState.Playing; i++)
                {
                    _game.Tick(Dt, 0.0);
                    _ball.ClampSpeed();
                    Physics2D.Simulate(Dt);
                }

                Assert.AreEqual(GameState.GameOver, _game.State,
                                $"seed {seed}: the ball sat in a sealed ring and the run never ended");

                if (contacts > worst) { worst = contacts; worstSeed = seed; }

                Object.DestroyImmediate(_root);
                _root = null;
                yield return null;
            }

            Debug.Log($"[HR] trapped ball: worst run knocked its ring {worst} times before dying");

            Assert.LessOrEqual(worst, KnocksAllowedBeforeDeath,
                               $"seed {worstSeed}: the ball knocked one ring {worst} times before the "
                               + "rule caught it; a player can sit in a ring and hit it for free");
        }

        [UnityTest]
        public IEnumerator The_first_ring_of_a_run_is_a_run_up_and_does_not_kill()
        {
            Build();
            _game.Mode = GameMode.Hardcore;
            _game.StartRun(12345);
            _game.StartPlaying();

            var first = _tunnel.ResolveBallRing();
            Assert.AreEqual(GameManager.FreeRingIndex, first.SpawnIndex,
                            "the run did not start on the ring this test is about");
            Seal(first);

            int knocks = 0;
            int killedOn = int.MinValue;
            _ball.Struck += _ => knocks++;
            _game.KilledByTouch += ring => killedOn = ring;

            for (int i = 0; i < 900 && _game.State == GameState.Playing; i++)
            {
                var here = _tunnel.ResolveBallRing();
                if (here == null || here.SpawnIndex != GameManager.FreeRingIndex) break;

                _game.Tick(Dt, 0.0);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);
            }

            Assert.Greater(knocks, 2, "the ball never knocked the starting ring, so nothing was proven");
            Assert.AreEqual(int.MinValue, killedOn,
                            $"the touch rule killed the run on the starting ring after {knocks} knocks; "
                            + "that ring is meant to be a run-up where the player finds the ball");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_ball_never_crosses_a_ring_anywhere_but_a_gap(
            [Values(0f, 0.033f, 0.05f)] float stutter)
        {
            int throughGap = 0, throughWall = 0;

            for (int run = 0; run < 5; run++)
            {
                Build();
                _game.Mode = GameMode.Normal;

                _tunnel.RingPassed += index =>
                {
                    RingController ring = null;
                    for (int i = 0; i < _tunnel.Rings.Count; i++)
                        if (_tunnel.Rings[i].SpawnIndex == index) ring = _tunnel.Rings[i];
                    if (ring == null) return;

                    if (Mathf.Abs((float)ring.TurnToNearestGapDeg(_ball.AngleDeg)) <= 0.001f) throughGap++;
                    else throughWall++;
                };

                _game.StartRun(700 + run * 97);

                _game.StartPlaying();
                foreach (var r in _tunnel.Rings)
                    for (int i = 0; i < r.ActiveSegmentCount; i++)
                        r.SegmentAt(i).Collider.sharedMaterial = _ringPhysics;

                for (int i = 0; i < 2000 && _game.State == GameState.Playing; i++)
                {
                    float dt = stutter > 0f && i % 7 == 0 ? stutter : Dt;
                    _game.Tick(dt, 0.0);
                    _ball.ClampSpeed();
                    Physics2D.Simulate(dt);
                }

                Object.DestroyImmediate(_root);
                _root = null;
                yield return null;
            }

            Debug.Log($"[HR] stutter {stutter:F3}s: {throughGap} passes through gaps, {throughWall} through wall");

            Assert.Greater(throughGap, 10, "too few passes to judge anything");
            Assert.AreEqual(0, throughWall,
                            $"the ball crossed {throughWall} rings somewhere other than a gap; "
                            + "a ring the player must aim through is not solid");
        }

        [Test]
        public void Hardcore_is_played_on_the_very_same_curve_as_the_normal_game()
        {
            Build();

            _game.Mode = GameMode.Normal;
            _game.StartRun(7);
            _game.StartPlaying();
            var normal = _game.CurrentDifficulty;

            _game.Mode = GameMode.Hardcore;
            _game.StartRun(7);
            _game.StartPlaying();
            var hardcore = _game.CurrentDifficulty;

            Assert.AreEqual(normal.GapArcWidth, hardcore.GapArcWidth, 1e-9, "openings differ by mode");
            Assert.AreEqual(normal.BaseCompressionSpeed, hardcore.BaseCompressionSpeed, 1e-9,
                            "shrink speed differs by mode");
            Assert.AreEqual(normal.Clearance, hardcore.Clearance, 1e-9, "corridor differs by mode");
            Assert.AreEqual(normal.AutoSpinDeg, hardcore.AutoSpinDeg, 1e-9, "ring spin differs by mode");
            Assert.AreEqual(normal.MultiGapChance, hardcore.MultiGapChance, 1e-9,
                            "multi-gap chance differs by mode");
        }

        [UnityTest]
        public IEnumerator A_hardcore_ball_meets_rings_often_enough_for_the_rule_to_bite()
        {
            Build();
            _game.Mode = GameMode.Hardcore;

            int runs = 0;
            int touchEvents = 0;
            int killedByTouch = 0;

            _tunnel.BallTouchedRing += _ => touchEvents++;
            _game.KilledByTouch += _ => killedByTouch++;

            for (int run = 0; run < 12; run++)
            {
                _game.StartRun(1000 + run * 37);
                _game.StartPlaying();
                runs++;

                for (int i = 0; i < 3000 && _game.State == GameState.Playing; i++)
                {
                    _game.Tick(Dt, 0.0);
                    _ball.ClampSpeed();
                    Physics2D.Simulate(Dt);
                }
            }

            double perRun = touchEvents / (double)runs;

            Debug.Log($"[HR] hardcore bite: {touchEvents} touches over {runs} runs "
                      + $"({perRun:F2} per run), {killedByTouch} ended on the rule");

            Assert.Greater(perRun, 2.0,
                           $"the ball met a ring {perRun:F2} times per run; a one-touch rule cannot "
                           + "bite a ball that never touches anything");
            Assert.Greater(killedByTouch, 0,
                           "not one of these runs ended on the rule the mode is named after");
            yield return null;
        }
    }
}
