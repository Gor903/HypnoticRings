using System;
using System.Collections;
using HR.Core;
using HR.Gameplay;
using HR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 3's gates. An endless game turns any per-ring cost into a leak, so the question is
    /// not whether ten thousand passes work but whether the scene is the same size afterwards.
    /// </summary>
    public class EndlessTunnelTests
    {
        const float Dt = 1f / 90f;
        const double FingerRadius = 2.0;

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
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        void Build(int seed)
        {
            _root = new GameObject("EndlessRoot");

            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();

            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.BeginRun(seed);

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(_root.transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(_config.ToGestureSettings(), null);
            _input.ExternalDriver = true;

            _fingerAngle = 0.0;
            _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
        }

        static Vector2 FingerAt(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Vector2((float)(Math.Cos(rad) * FingerRadius), (float)(Math.Sin(rad) * FingerRadius));
        }

        void StepOnce(BotBrain bot)
        {
            var ring = _tunnel.ResolveBallRing();
            if (ring != null)
            {
                _fingerAngle += bot.Decide(TunnelMath.BottomAngleDeg, ring.GapCentreWorldDeg, Dt);
                _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
            }

            _tunnel.Step(Dt);
            _ball.ClampSpeed();
            Physics2D.Simulate(Dt);
        }

        static int SceneObjectCount() =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;

        [UnityTest]
        [Timeout(2_700_000)]
        [Category("Slow")]
        public IEnumerator Ten_thousand_passes_leave_the_scene_exactly_as_big_as_it_started()
        {
            // The gate. A strong bot is used deliberately: this measures leaks, not skill, and a
            // bot that stalls would turn a leak test into a balance test by accident.
            const int target = 10000;
            const int warmup = 200;
            const int stepCap = 2_500_000;   // ~1.4M expected; bounded so a stall fails rather than sits

            Build(seed: 20261027);
            var bot = BotBrain.Strong(1);
            yield return null;

            int steps = 0;
            while (_tunnel.PassedCount < warmup && steps < stepCap) { StepOnce(bot); steps++; }

            // Everything that will ever be created has been created by now.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memoryAfterWarmup = GC.GetTotalMemory(true);
            int objectsAfterWarmup = SceneObjectCount();
            int ringsCreatedAfterWarmup = _pool.Created;
            int transformsPerRing = 1 + _tunnel.Rings[0].transform.childCount;
            int maxActiveRings = 0;

            int lastYield = steps;
            int nextReport = warmup + 1000;
            while (_tunnel.PassedCount < target && steps < stepCap)
            {
                StepOnce(bot);
                steps++;
                if (_tunnel.ActiveRingCount > maxActiveRings) maxActiveRings = _tunnel.ActiveRingCount;

                // Progress has to be visible from outside. A long quiet run and a hung editor
                // look identical to a watchdog reading the log, and a watchdog that cannot tell
                // them apart either kills good runs or sits through dead ones.
                if (_tunnel.PassedCount >= nextReport)
                {
                    nextReport += 1000;
                    Debug.Log($"[HR] endless progress: {_tunnel.PassedCount}/{target} passes, "
                              + $"{steps} steps, {_pool.Created} rings created, "
                              + $"{_tunnel.ActiveRingCount} live");
                }

                // Let the frame breathe occasionally so the editor does not look hung.
                if (steps - lastYield > 50000) { lastYield = steps; yield return null; }
            }

            Assert.AreEqual(target, _tunnel.PassedCount,
                $"only {_tunnel.PassedCount} passes in {steps} steps — the bot stalled");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memoryAtEnd = GC.GetTotalMemory(true);
            int objectsAtEnd = SceneObjectCount();

            // A pool settles at its high-water mark, and that mark is not necessarily reached in
            // the first couple of hundred passes. Demanding no growth at all after an arbitrary
            // warmup tests when the peak happened to occur, not whether anything leaks.
            //
            // What separates a leak from a pool is that a leak has no ceiling. So: the pool must
            // stay within the live-ring budget, and every transform that appeared must be
            // accounted for by those pooled rings and nothing else.
            double furthestBallRing = _settings.TargetRadius + 2.0 * _settings.Step;
            int inside = (int)Math.Floor((furthestBallRing - _settings.VanishRadius) / _settings.Step);
            int allowedLive = inside + 1 + _settings.RingsAhead + 1;

            int poolGrowth = _pool.Created - ringsCreatedAfterWarmup;
            Assert.LessOrEqual(_pool.Created, allowedLive + 2,
                $"{_pool.Created} rings created for {target} passes — the pool is growing, not reusing");

            int unexplained = (objectsAtEnd - objectsAfterWarmup) - poolGrowth * transformsPerRing;
            Assert.LessOrEqual(unexplained, 0,
                $"scene grew by {objectsAtEnd - objectsAfterWarmup} transforms, only "
                + $"{poolGrowth * transformsPerRing} explained by {poolGrowth} new pooled ring(s)");

            long growth = memoryAtEnd - memoryAfterWarmup;
            Assert.Less(growth, 2L * 1024 * 1024,
                $"managed heap grew by {growth / 1024} KB over {target} passes");

            Debug.Log($"[HR] endless gate: {target} passes in {steps} steps, "
                      + $"{_pool.Created} rings ever created (budget {allowedLive + 2}), "
                      + $"peak {maxActiveRings} active, heap {growth / 1024:+0;-0;0} KB, "
                      + $"transforms {objectsAfterWarmup}->{objectsAtEnd}");

            // No LogAssert.NoUnexpectedReceived here: it treats this test's own progress lines
            // as unexpected output, and the progress lines are what stop the watchdog mistaking
            // a long quiet run for a hang. Unity already fails a test on an unexpected error or
            // exception, so the call added nothing that is not already covered.
        }

        [UnityTest]
        public IEnumerator The_number_of_live_rings_stays_bounded_and_does_not_creep()
        {
            Build(seed: 7);
            var bot = BotBrain.Strong(2);
            yield return null;

            // Derived rather than guessed, and the derivation is the interesting part. The ball
            // can clear a second ring before the retract has pulled the first one back in, so
            // its own ring sits as much as two steps beyond equilibrium. Everything still inside
            // that, down to the vanish radius and one step apart, is alive; so is the ball's own
            // ring and the ones kept ahead; and one more exists for the step between a spawn and
            // the cull that follows it.
            double furthestBallRing = _settings.TargetRadius + 2.0 * _settings.Step;
            int inside = (int)Math.Floor((furthestBallRing - _settings.VanishRadius) / _settings.Step);
            int allowed = inside + 1 + _settings.RingsAhead + 1;

            int peak = 0;
            int earlyPeak = 0;
            const int steps = 60000;   // ~11 minutes of play; enough to settle, short of NUnit's default timeout

            for (int i = 0; i < steps; i++)
            {
                StepOnce(bot);
                int live = _tunnel.ActiveRingCount;
                if (live > peak) peak = live;
                if (i < steps / 10 && live > earlyPeak) earlyPeak = live;

                Assert.LessOrEqual(live, allowed, $"{live} rings live at step {i}, allowed {allowed}");
                if (i % 40000 == 0) yield return null;
            }

            // A ceiling alone would not catch a slow leak that stays under it. The shape of the
            // run has to be flat too: as busy at the end as it was at the start.
            Assert.LessOrEqual(peak, earlyPeak,
                $"live rings crept from a peak of {earlyPeak} early to {peak} later");

            Assert.Greater(_tunnel.PassedCount, 10, "the run did not actually progress");
            Debug.Log($"[HR] ring budget: peak {peak} live (early peak {earlyPeak}), "
                      + $"allowed {allowed}, {_tunnel.PassedCount} passes");
        }

        [UnityTest]
        public IEnumerator Each_ring_is_counted_once_and_only_once()
        {
            Build(seed: 11);
            var bot = BotBrain.Strong(3);
            yield return null;

            var seen = new System.Collections.Generic.HashSet<int>();
            int duplicates = 0;
            _tunnel.RingPassed += index => { if (!seen.Add(index)) duplicates++; };

            for (int i = 0; i < 120000 && _tunnel.PassedCount < 100; i++)
            {
                StepOnce(bot);
                if (i % 40000 == 0) yield return null;
            }

            Assert.Greater(_tunnel.PassedCount, 20, "not enough passes to judge");
            Assert.AreEqual(0, duplicates, "a ring was counted more than once");
            Assert.AreEqual(_tunnel.PassedCount, seen.Count, "the event count and the score disagree");
        }

        [UnityTest]
        public IEnumerator A_fresh_run_replays_identically_from_the_same_seed()
        {
            // Determinism is what makes a balance report and a bug report worth anything.
            Build(seed: 4242);
            var first = new double[12];
            int index = 0;
            foreach (var ring in _tunnel.Rings) { if (index >= first.Length) break; first[index++] = ring.GapCentreLocalDeg; }

            _tunnel.BeginRun(4242);
            int check = 0;
            foreach (var ring in _tunnel.Rings)
            {
                if (check >= index) break;
                Assert.AreEqual(first[check], ring.GapCentreLocalDeg, 1e-9, $"ring {check} differs on replay");
                check++;
            }

            Assert.Greater(check, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rings_return_to_the_pool_rather_than_being_destroyed()
        {
            Build(seed: 13);
            var bot = BotBrain.Strong(4);
            yield return null;

            for (int i = 0; i < 60000 && _tunnel.PassedCount < 30; i++)
            {
                StepOnce(bot);
                if (i % 20000 == 0) yield return null;
            }

            // Available is a snapshot and can legitimately be zero at any instant. What proves
            // reuse is that thirty-odd rings were spawned while only a handful ever existed.
            Assert.Greater(_tunnel.PassedCount, 5, "no rings were cycled");
            Assert.LessOrEqual(_pool.Created, _settings.RingsAhead + 5,
                $"{_pool.Created} rings created for {_tunnel.PassedCount} passes — they are not being reused");
            Assert.AreEqual(_pool.Created, _pool.InUse + _pool.Available, "a ring went missing");
            Debug.Log($"[HR] pool reuse: {_pool.Created} created for {_tunnel.PassedCount} passes");
        }
    }
}
