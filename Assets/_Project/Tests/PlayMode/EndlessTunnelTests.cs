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
            _tunnel.AutoStep = false;
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
                _fingerAngle += bot.Decide(TunnelMath.BottomAngleDeg, ring.GapCentreWorldDeg, Dt)
                                / _config.sensitivity;
                _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
            }

            _tunnel.Step(Dt);
            _ball.ClampSpeed();
            Physics2D.Simulate(Dt);
        }

        int SceneObjectCount() =>
            _root == null ? 0 : _root.GetComponentsInChildren<Transform>(includeInactive: true).Length;

        [UnityTest]
        [Timeout(2_700_000)]
        [Category("Endurance")]
        public IEnumerator Ten_thousand_passes_leave_the_scene_exactly_as_big_as_it_started()
        {
            const int target = 10000;
            const int warmup = 200;
            const int stepCap = 2_500_000;

            Build(seed: 20261027);
            var bot = BotBrain.Strong(1);
            yield return null;

            int steps = 0;
            while (_tunnel.PassedCount < warmup && steps < stepCap) { StepOnce(bot); steps++; }

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

                if (_tunnel.PassedCount >= nextReport)
                {
                    nextReport += 1000;
                    Debug.Log($"[HR] endless progress: {_tunnel.PassedCount}/{target} passes, "
                              + $"{steps} steps, {_pool.Created} rings created, "
                              + $"{_tunnel.ActiveRingCount} live");
                }

                if (steps - lastYield > 50000) { lastYield = steps; yield return null; }
            }

            Assert.AreEqual(target, _tunnel.PassedCount,
                $"only {_tunnel.PassedCount} passes in {steps} steps — the bot stalled");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memoryAtEnd = GC.GetTotalMemory(true);
            int objectsAtEnd = SceneObjectCount();

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
        }

        [UnityTest]
        [Category("Endurance")]
        public IEnumerator The_number_of_live_rings_stays_bounded_and_does_not_creep()
        {
            Build(seed: 7);
            var bot = BotBrain.Strong(2);
            yield return null;

            double furthestBallRing = _settings.TargetRadius + 2.0 * _settings.Step;
            int inside = (int)Math.Floor((furthestBallRing - _settings.VanishRadius) / _settings.Step);
            int allowed = inside + 1 + _settings.RingsAhead + 1;

            int peak = 0;
            const int steps = 60000;
            const int window = steps / 10;

            long earlySum = 0;
            long lateSum = 0;

            for (int i = 0; i < steps; i++)
            {
                StepOnce(bot);
                int live = _tunnel.ActiveRingCount;
                if (live > peak) peak = live;

                if (i < window) earlySum += live;
                else if (i >= steps - window) lateSum += live;

                Assert.LessOrEqual(live, allowed, $"{live} rings live at step {i}, allowed {allowed}");
                if (i % 40000 == 0) yield return null;
            }

            double early = earlySum / (double)window;
            double late = lateSum / (double)window;

            Assert.LessOrEqual(late, early + 0.5,
                $"live rings averaged {early:F2} early and {late:F2} late, which is a leak");

            Assert.Greater(_tunnel.PassedCount, 10, "the run did not actually progress");
            Debug.Log($"[HR] ring budget: peak {peak} live, average {early:F2} early "
                      + $"and {late:F2} late, allowed {allowed}, {_tunnel.PassedCount} passes");
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

            Assert.Greater(_tunnel.PassedCount, 5, "no rings were cycled");
            Assert.LessOrEqual(_pool.Created, _settings.RingsAhead + 5,
                $"{_pool.Created} rings created for {_tunnel.PassedCount} passes — they are not being reused");
            Assert.AreEqual(_pool.Created, _pool.InUse + _pool.Available, "a ring went missing");
            Debug.Log($"[HR] pool reuse: {_pool.Created} created for {_tunnel.PassedCount} passes");
        }
    }
}
