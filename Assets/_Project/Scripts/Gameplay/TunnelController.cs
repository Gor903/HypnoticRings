using System.Collections.Generic;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// Drives compression and rotation for every ring. Owns the shrinking, not the finger —
    /// the user angle is handed in (CONTRACT.md §3.5).
    ///
    /// Phase 1 scope: a fixed set of rings. Spawning, pooling and culling arrive in phase 3.
    /// </summary>
    public sealed class TunnelController : MonoBehaviour
    {
        readonly List<RingController> _rings = new List<RingController>();

        GameConfig _config;
        TunnelSettings _settings;
        BallController _ball;

        /// <summary>Equilibrium shrink speed. The difficulty curve moves this from phase 4.</summary>
        public double BaseCompressionSpeed { get; set; }

        /// <summary>Shared rotation from the finger, applied identically to every ring.</summary>
        public double UserAngleDeg { get; set; }

        public bool CompressionEnabled { get; set; } = true;
        public bool RotationEnabled { get; set; } = true;

        public IReadOnlyList<RingController> Rings => _rings;
        public double LastCompressionSpeed { get; private set; }

        public void Initialise(GameConfig config, BallController ball)
        {
            _config = config;
            _settings = config.ToSettings();
            _ball = ball;
            BaseCompressionSpeed = config.baseCompressionSpeed;
        }

        public void Register(RingController ring) => _rings.Add(ring);
        public void Clear() => _rings.Clear();

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>One physics step of the tunnel. Public so tests can drive it deterministically.</summary>
        public void Step(double dt)
        {
            if (_rings.Count == 0) return;

            if (CompressionEnabled)
            {
                var ballRing = ResolveBallRing();
                double ballRingRadius = ballRing != null ? ballRing.Radius : _settings.TargetRadius;

                // One speed for every ring, which is what keeps the corridor width constant.
                LastCompressionSpeed = TunnelMath.CompressionSpeed(
                    BaseCompressionSpeed, _settings.PullToCentre, _settings.MaxCompressionSpeed,
                    ballRingRadius, _settings.TargetRadius);

                double delta = LastCompressionSpeed * dt;
                for (int i = 0; i < _rings.Count; i++)
                {
                    var ring = _rings[i];
                    double next = ring.Radius - delta;
                    // A ring may shrink until it crushes the ball, but not through the centre.
                    if (next <= _settings.Thickness) continue;
                    ring.SetRadius(next);
                }
            }

            if (RotationEnabled)
                for (int i = 0; i < _rings.Count; i++)
                    _rings[i].StepRotation(UserAngleDeg, dt);
        }

        /// <summary>
        /// The ring the ball is resting on: the innermost ring still outside it. "Outside" is
        /// the direction gravity pulls, because the tunnel centre is above the ball.
        /// </summary>
        public RingController ResolveBallRing()
        {
            if (_ball == null) return null;

            double ballDistance = _ball.DistanceToCentre;
            RingController best = null;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                double innerSurface = ring.Radius - _settings.Thickness * 0.5;
                if (innerSurface < ballDistance - 1e-4) continue;
                if (best == null || ring.Radius < best.Radius) best = ring;
            }

            return best;
        }

        /// <summary>True when the ball's ring has closed to the ball's own size with nothing inside it.</summary>
        public bool IsBallCrushed()
        {
            var ring = ResolveBallRing();
            if (ring == null) return false;

            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].Radius < ring.Radius - 1e-6) return false;   // a ring still inside

            return TunnelMath.IsCrushed(ring.Radius, _settings.Thickness,
                                        _settings.BallRadius, _settings.CrushMargin);
        }
    }
}
