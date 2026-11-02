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
    /// What a super ring does once it is in a scene rather than in the generator: the wave of
    /// rings it takes, the points they are worth, and the rest it buys.
    ///
    /// The generator's half of this is arithmetic and lives in EditMode. This half cannot be:
    /// the wave picks rings out of a live tunnel and the rest shows up as a compression speed.
    /// </summary>
    public sealed class SuperRingWaveTests
    {
        const float Dt = 1f / 90f;

        GameConfig _config;
        SimulationMode2D _simulationMode;
        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        RingPool _pool;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;

            _root = new GameObject("SuperRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material,
                                             _config.ToSettings());

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.AutoStep = false;
            _tunnel.BeginRun(seed: 4);
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Rides the tunnel the way a ball does: inside its ring until that ring reaches the
        /// working radius, then out through it, then inside the next one.
        ///
        /// Three simpler harnesses came before this and each failed for a reason worth keeping.
        /// Nudging the ball out by one thickness leaves it short of the pass margin, so nothing
        /// scores. Parking the ball and letting rings shrink through it scores nothing either —
        /// a pass is the BALL moving out past a radius captured as it left, so a ball that never
        /// moves clears nothing however far the tunnel travels. And a parked ball breaks the
        /// invariant the whole tunnel rests on: it stops being INSIDE its ring, the tunnel keeps
        /// retracting to bring a ring that can never contain it back to the working radius, and
        /// the ball walks out of the tunnel altogether.
        ///
        /// Keeping the ball inside its corridor is not decoration here. It is the one condition
        /// under which the compression law means what it says.
        /// </summary>
        IEnumerator RunUntilWave(Func<bool> fired)
        {
            const int budget = 60000;
            double inside = _config.thickness * 0.5 + _config.ballRadius + 0.05;
            double outside = _config.thickness * 0.5 + _config.passMargin + 0.05;

            for (int i = 0; i < budget; i++)
            {
                var ring = _tunnel.ResolveBallRing();
                if (ring == null)
                    Assert.Fail($"lost the tunnel after {_tunnel.PassedCount} passes");

                // Settled at the working radius: time to leave. Otherwise ride it in.
                bool settled = ring.Radius <= _config.targetRadius + 0.05;
                double at = settled ? ring.Radius + outside : ring.Radius - inside;
                _ball.Place(new Vector2((float)at, 0f));

                _tunnel.Step(Dt);
                if (fired()) yield break;
                if (i % 2000 == 0) yield return null;
            }

            Assert.Fail($"no super ring after {_tunnel.PassedCount} rings passed");
        }

        [UnityTest]
        public IEnumerator Clearing_a_super_ring_takes_the_rings_that_were_next()
        {
            // Reach the reward by walking the ball outwards rather than by playing: the wave is
            // what is under test, not whether a bot can get there.
            int dissolved = 0;
            _tunnel.RingsDissolved += count => dissolved += count;

            yield return RunUntilWave(() => dissolved > 0);

            Assert.AreEqual(_config.superDissolveCount, dissolved,
                "the wave did not take the number of rings the config asks for");
        }

        [UnityTest]
        public IEnumerator The_wave_arrives_in_order_rather_than_all_at_once()
        {
            bool waved = false;
            _tunnel.RingsDissolved += _ => waved = true;

            yield return RunUntilWave(() => waved);

            // One stagger in, the first ring of the wave should be going and the last not yet.
            // Three rings vanishing on one frame reads as a glitch rather than as a reward.
            int dissolvingNow = CountDissolving();
            _tunnel.Step(_config.superWaveStagger * 0.5);
            Assert.LessOrEqual(dissolvingNow, _config.superDissolveCount,
                "more rings are dissolving than the wave ever queued");

            _tunnel.Step(_config.superWaveStagger * _config.superDissolveCount + 0.05);
            Assert.GreaterOrEqual(CountDissolvingOrGone(), 1,
                "the wave never actually started any ring dissolving");
        }

        int CountDissolving()
        {
            int n = 0;
            foreach (var ring in _tunnel.Rings) if (ring.IsDissolving) n++;
            return n;
        }

        int CountDissolvingOrGone() => CountDissolving() + Mathf.Max(0, _pool.Available);

        [UnityTest]
        public IEnumerator The_rest_slows_the_tunnel_and_then_gives_it_back()
        {
            bool waved = false;
            _tunnel.RingsDissolved += _ => waved = true;

            yield return RunUntilWave(() => waved);

            _tunnel.Step(Dt);
            double resting = _tunnel.LastCompressionSpeed;

            // Past the rest and past the fade, the tunnel must be at full speed again — a rest
            // that never ends is a game that stopped.
            double total = _config.superSlowSeconds + _config.superSlowReturnSeconds + 0.2;
            for (double t = 0.0; t < total; t += Dt) _tunnel.Step(Dt);
            double after = _tunnel.LastCompressionSpeed;

            Assert.Less(resting, after,
                $"compression was {resting:F3} during the rest and {after:F3} after it");
        }
    }
}
