using System.Collections;
using HR.Core;
using HR.Gameplay;
using HR.Input;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 9: what the game does when it is treated badly.
    ///
    /// Everything here is a case a player reaches by accident — a frame that took a second
    /// because the phone woke up, a pause button hit four times, a restart before the previous
    /// run finished dying. None of them appear in normal play, all of them appear in a crash
    /// report, and none of them are reachable from a test that only plays the game properly.
    /// </summary>
    public class RobustnessTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;

        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        InputController _input;
        RingPool _pool;
        double _fingerAngle;

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

        void Build(int seed)
        {
            _root = new GameObject("RobustRoot");

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
            _tunnel.BeginRun(seed);

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(_root.transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(_config.ToGestureSettings(), null);
            _input.ExternalDriver = true;

            _fingerAngle = 0.0;
        }

        Vector2 FingerAt(double degrees)
        {
            float radians = (float)(degrees * Mathf.Deg2Rad);
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 3f;
        }

        [UnityTest]
        public IEnumerator A_frame_that_took_no_time_at_all_changes_nothing()
        {
            // CONTRACT.md G-14. dt = 0 arrives on the first frame after a resume, and a divide
            // by it turns the whole tunnel into NaN — which then never recovers, because NaN
            // propagates through every later step.
            Build(seed: 3);
            for (int i = 0; i < 50; i++) _tunnel.Step(Dt);

            double radiusBefore = _tunnel.Rings[0].Radius;
            double rotationBefore = _tunnel.Rings[0].TotalRotationDeg;

            for (int i = 0; i < 20; i++) _tunnel.Step(0.0);

            Assert.AreEqual(radiusBefore, _tunnel.Rings[0].Radius, 1e-12, "a zero step moved a ring");
            Assert.AreEqual(rotationBefore, _tunnel.Rings[0].TotalRotationDeg, 1e-12);
            AssertNothingIsNaN();
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_frame_that_took_a_whole_second_does_not_teleport_the_tunnel()
        {
            // CONTRACT.md G-14: game logic clamps dt at 0.1 s. Without the clamp, one stalled
            // frame compresses the tunnel by a second's worth in a single step and kills a
            // player who did nothing wrong.
            Build(seed: 4);
            for (int i = 0; i < 50; i++) _tunnel.Step(Dt);

            double before = _tunnel.Rings[0].Radius;
            _tunnel.Step(1.0);
            double jump = before - _tunnel.Rings[0].Radius;

            double clamped = before;
            Build(seed: 4);
            for (int i = 0; i < 50; i++) _tunnel.Step(Dt);
            clamped = _tunnel.Rings[0].Radius;
            _tunnel.Step(0.1);
            double clampedJump = clamped - _tunnel.Rings[0].Radius;

            Assert.LessOrEqual(jump, clampedJump + 1e-9,
                $"a one-second frame moved the tunnel {jump:F4}u against {clampedJump:F4}u for " +
                "the clamped maximum — the clamp is not being applied");
            AssertNothingIsNaN();
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_negative_frame_is_refused_rather_than_run_backwards()
        {
            Build(seed: 5);
            for (int i = 0; i < 50; i++) _tunnel.Step(Dt);

            double before = _tunnel.Rings[0].Radius;
            _tunnel.Step(-0.5);

            Assert.LessOrEqual(_tunnel.Rings[0].Radius, before + 1e-9,
                "a negative step expanded the tunnel");
            AssertNothingIsNaN();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fifty_restarts_in_a_row_leave_the_pool_where_it_started()
        {
            // Spec 14, case 9. The number that matters is the high-water mark, not the live
            // count: a pool that grows by one ring per restart looks perfectly stable at any
            // single moment.
            Build(seed: 6);
            for (int i = 0; i < 200; i++) _tunnel.Step(Dt);

            int created = _pool.Created;

            for (int run = 0; run < 50; run++)
            {
                _tunnel.BeginRun(100 + run);
                for (int i = 0; i < 60; i++) _tunnel.Step(Dt);
            }

            Assert.AreEqual(created, _pool.Created,
                $"fifty restarts built {_pool.Created - created} more rings than the first run");
            AssertNothingIsNaN();
            yield return null;
        }

        [UnityTest]
        // What this covers is rings, not minutes, and D-059 quadrupled how fast they arrive: the
        // same nonsense now delivers about 240 passes where it used to deliver 60. So the clock
        // was halved rather than the coverage — at ten simulated minutes the run walked past its
        // own twenty-minute timeout, because a tighter corridor doubled the cost of a step too.
        [Timeout(1_200_000)]
        public IEnumerator Five_minutes_of_nonsense_input_never_breaks_the_tunnel()
        {
            // Fuzzing, deterministically: the same nonsense every run, so a failure can be
            // reproduced rather than waited for. Jumps across the ±180 seam, taps with no
            // movement, and fingers that leave and return mid-swipe are all here because each
            // one is reachable with a real thumb.
            Build(seed: 7);
            var random = new System.Random(20261027);

            const int steps = 27000;   // five minutes at 90 Hz
            for (int i = 0; i < steps; i++)
            {
                switch (random.Next(5))
                {
                    case 0: _input.FeedPointer(false, Vector2.zero, Dt); break;
                    case 1: _input.FeedPointer(true, Vector2.zero, Dt); break;
                    case 2:
                        _fingerAngle += random.NextDouble() * 720.0 - 360.0;
                        _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                        break;
                    case 3:
                        _input.FeedPointer(true, FingerAt(_fingerAngle) * 1000f, Dt);
                        break;
                    default:
                        _fingerAngle += random.NextDouble() * 8.0 - 4.0;
                        _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                        break;
                }

                _tunnel.UserAngleDeg = _input.UserAngleDeg;
                _tunnel.Step(Dt);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);

                if (i % 9000 == 0)
                {
                    AssertNothingIsNaN();
                    Debug.Log($"[HR] fuzz: {i}/{steps} steps, {_tunnel.PassedCount} passes");
                    yield return null;
                }
            }

            AssertNothingIsNaN();
            Debug.Log($"[HR] fuzz gate: {steps} steps of nonsense, {_tunnel.PassedCount} passes, " +
                      $"{_pool.Created} rings ever built");
        }

        [UnityTest]
        public IEnumerator The_game_loop_allocates_nothing_once_it_is_running()
        {
            // CONTRACT.md: no allocations in the game loop. Measured with the profiler recorder
            // rather than GC.GetTotalMemory, which reports the heap's size and not whether this
            // frame added to it.
            Build(seed: 8);

            // Warmed by playing, not by stepping a still tunnel. The pool only reaches its
            // working size once rings have actually been passed and retired, and a warmup that
            // never scores leaves the first pass of the measured window building a ring — which
            // is the pool filling up, not the loop leaking.
            for (int i = 0; i < 1800; i++)
            {
                _fingerAngle += 3.0;
                _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
                _tunnel.Step(Dt);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);
            }

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            Assert.IsTrue(recorder.Valid, "the allocation recorder is unavailable");

            yield return null;   // let the recorder settle on a real frame

            long worst = 0;
            int createdBefore = _pool.Created;
            for (int i = 0; i < 240; i++)
            {
                _fingerAngle += 3.0;
                _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
                _tunnel.Step(Dt);
                _ball.ClampSpeed();
                Physics2D.Simulate(Dt);

                yield return null;
                worst = System.Math.Max(worst, recorder.LastValue);
            }

            recorder.Dispose();

            // Not zero: the test runner's own coroutine machinery allocates on every frame it
            // drives, and that is not the game's doing. The bar is that the game loop cannot be
            // what is producing kilobytes per frame.
            Debug.Log($"[HR] allocation gate: worst frame allocated {worst} bytes, "
                      + $"{_pool.Created - createdBefore} rings built during the window");

            // Named separately because it is the one cause worth telling apart: a ring built
            // mid-run is the pool failing, and the byte count alone does not say which it was.
            Assert.AreEqual(createdBefore, _pool.Created,
                $"the pool built {_pool.Created - createdBefore} ring(s) during a running game");
            Assert.Less(worst, 4096,
                $"the game loop allocated {worst} bytes in a frame");
        }

        void AssertNothingIsNaN()
        {
            var p = _ball.transform.position;
            Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y), "the ball went to NaN");

            var v = _ball.Body.linearVelocity;
            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y), "the ball's velocity went to NaN");

            foreach (var ring in _tunnel.Rings)
            {
                Assert.IsFalse(double.IsNaN(ring.Radius), "a ring radius went to NaN");
                Assert.IsFalse(double.IsNaN(ring.TotalRotationDeg), "a ring rotation went to NaN");
                Assert.Greater(ring.Radius, 0.0, "a ring radius went negative");
            }
        }
    }
}
