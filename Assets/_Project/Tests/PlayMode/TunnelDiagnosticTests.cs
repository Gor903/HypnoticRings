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
    /// Reports what the tunnel is actually doing, step by step. Not a gate — a way to see the
    /// state rather than guess at it when a run misbehaves.
    /// </summary>
    public class TunnelDiagnosticTests
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
        public IEnumerator Report_what_the_tunnel_does_over_the_first_seconds()
        {
            _root = new GameObject("DiagRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.AutoStep = false;   // this fixture steps explicitly
            _tunnel.BeginRun(99);

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(_root.transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(_config.ToGestureSettings(), null);
            _input.ExternalDriver = true;

            Debug.Log($"[DIAG] after BeginRun: rings={_tunnel.ActiveRingCount} "
                      + $"created={_pool.Created} ballDist={_ball.DistanceToCentre:F3}");
            foreach (var ring in _tunnel.Rings)
                Debug.Log($"[DIAG]   ring idx={ring.SpawnIndex} R={ring.Radius:F3} "
                          + $"gapLocal={ring.GapCentreLocalDeg:F1} w={ring.GapArcWidth:F2} "
                          + $"segs={ring.ActiveSegmentCount}");

            var ballRing = _tunnel.ResolveBallRing();
            Debug.Log($"[DIAG] ballRing={(ballRing == null ? "NULL" : ballRing.SpawnIndex.ToString())}");

            yield return null;

            var bot = BotBrain.Strong(1);
            double fingerAngle = 0.0;
            _input.FeedPointer(true, Finger(fingerAngle), Dt);

            var swBot = new System.Diagnostics.Stopwatch();
            var swTunnel = new System.Diagnostics.Stopwatch();
            var swPhysics = new System.Diagnostics.Stopwatch();

            for (int step = 0; step < 3000; step++)
            {
                swBot.Start();
                var ring = _tunnel.ResolveBallRing();
                if (ring != null)
                {
                    fingerAngle += bot.Decide(TunnelMath.BottomAngleDeg, ring.GapCentreWorldDeg, Dt);
                    _input.FeedPointer(true, Finger(fingerAngle), Dt);
                    _tunnel.UserAngleDeg = _input.UserAngleDeg;
                }
                swBot.Stop();

                swTunnel.Start();
                _tunnel.Step(Dt);
                _ball.ClampSpeed();
                swTunnel.Stop();

                swPhysics.Start();
                Physics2D.Simulate(Dt);
                swPhysics.Stop();

                if (step % 250 == 0)
                {
                    var r = _tunnel.ResolveBallRing();
                    double err = r != null
                        ? TunnelMath.ShortestDeltaDeg(r.GapCentreWorldDeg, _ball.AngleDeg)
                        : double.NaN;
                    Debug.Log($"[DIAG] step={step} t={step * Dt:F2}s rings={_tunnel.ActiveRingCount} "
                              + $"created={_pool.Created} passed={_tunnel.PassedCount} "
                              + $"ballDist={_ball.DistanceToCentre:F3} ballAng={_ball.AngleDeg:F1} "
                              + $"ringR={(r == null ? -1 : r.Radius):F3} gapErr={err:F1} "
                              + $"user={_input.UserAngleDeg:F1} crushed={_tunnel.IsBallCrushed()}");
                }
            }

            Debug.Log($"[DIAG] final: passed={_tunnel.PassedCount} created={_pool.Created} "
                      + $"rings={_tunnel.ActiveRingCount}");
            Debug.Log($"[DIAG] cost per step: bot={swBot.Elapsed.TotalMilliseconds / 3000:F3}ms "
                      + $"tunnel={swTunnel.Elapsed.TotalMilliseconds / 3000:F3}ms "
                      + $"physics={swPhysics.Elapsed.TotalMilliseconds / 3000:F3}ms");
            Assert.Pass("diagnostic only");
        }

        static Vector2 Finger(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Vector2((float)(Math.Cos(rad) * 2.0), (float)(Math.Sin(rad) * 2.0));
        }
    }
}
