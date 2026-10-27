using System;
using System.Collections.Generic;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// The endless tunnel: shrinking, spinning, culling what has collapsed and growing what is
    /// needed outside. Owns none of the finger (the user angle is handed in) and none of the
    /// colour beyond the depth cue (CONTRACT.md §3.5).
    ///
    /// Rings come from a pool and go back to it. Nothing here allocates once a run is going —
    /// an endless game turns a per-ring allocation into a leak by definition.
    /// </summary>
    public sealed class TunnelController : MonoBehaviour
    {
        readonly List<RingController> _rings = new List<RingController>();
        readonly List<RingController> _culled = new List<RingController>();

        GameConfig _config;
        TunnelSettings _settings;
        BallController _ball;
        RingPool _pool;
        RingGenerator _generator;
        DifficultySnapshot _difficulty;

        int _nextSpawnIndex;
        RingController _ballRing;
        int _pendingIndex = -1;
        double _pendingRadius;
        int _lastPassedIndex = -1;
        Color _ringColour = Color.white;

        /// <summary>Fires once per ring cleared, with that ring's spawn index.</summary>
        public event Action<int> RingPassed;

        public int PassedCount { get; private set; }
        public double BaseCompressionSpeed { get; set; }
        public double UserAngleDeg { get; set; }
        public bool CompressionEnabled { get; set; } = true;
        public bool RotationEnabled { get; set; } = true;
        public bool ShadingEnabled { get; set; } = true;

        public IReadOnlyList<RingController> Rings => _rings;
        public double LastCompressionSpeed { get; private set; }
        public RingPool Pool => _pool;
        public RingGenerator Generator => _generator;
        public int ActiveRingCount => _rings.Count;

        public void Initialise(GameConfig config, BallController ball, RingPool pool = null)
        {
            _config = config;
            _settings = config.ToSettings();
            _ball = ball;
            _pool = pool;
            BaseCompressionSpeed = config.baseCompressionSpeed;
            _difficulty = DifficultySnapshot.Opening;
        }

        public void SetRingColour(Color colour) => _ringColour = colour;
        public void SetDifficulty(in DifficultySnapshot difficulty) => _difficulty = difficulty;

        /// <summary>Used by the phase 1 and 2 fixtures, which arrange rings by hand.</summary>
        public void Register(RingController ring) => _rings.Add(ring);
        public void Clear() => _rings.Clear();

        /// <summary>
        /// Starts a run: every ring back to the pool, the generator rewound to its seed, and the
        /// opening arrangement from CONTRACT.md §2.4 laid out again.
        /// </summary>
        public void BeginRun(int seed)
        {
            if (_pool == null)
                throw new InvalidOperationException("BeginRun needs a pool; pass one to Initialise");

            for (int i = 0; i < _rings.Count; i++) _pool.Return(_rings[i]);
            _rings.Clear();

            _generator = new RingGenerator(seed);
            _nextSpawnIndex = 0;
            PassedCount = 0;
            _ballRing = null;
            _pendingIndex = -1;
            _lastPassedIndex = -1;

            // One ring inside the ball, the ball's own ring, then the ones ahead.
            for (int i = -1; i <= _settings.RingsAhead; i++)
            {
                double radius = _settings.TargetRadius + _settings.Step * i;
                if (radius <= _settings.Thickness) continue;
                SpawnAt(radius);
            }

            _ball.Place(TunnelFactory.RestingPosition(_settings));
            _ballRing = ResolveBallRing();
            ApplyShading(_ballRing);
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>One step of the tunnel. Public so tests can drive it deterministically.</summary>
        public void Step(double dt)
        {
            if (_rings.Count == 0) return;

            // Resolved once and threaded through. It used to be recomputed by each stage, which
            // is five scans of the ring list per step for an answer that cannot change mid-step.
            var ballRing = ResolveBallRing();

            if (CompressionEnabled) Compress(dt, ballRing);
            if (RotationEnabled) Rotate(dt);

            if (_pool != null)
            {
                ballRing = Cull(ballRing);
                Fill(ballRing);
            }

            DetectPass(ResolveBallRing());
            if (ShadingEnabled) ApplyShading(ballRing);
        }

        void Compress(double dt, RingController ballRing)
        {
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

        void Rotate(double dt)
        {
            for (int i = 0; i < _rings.Count; i++)
                _rings[i].StepRotation(UserAngleDeg, dt);
        }

        /// <summary>
        /// Returns rings that have collapsed into the centre. The ball's own ring is never
        /// culled — it is allowed to shrink all the way down and crush, which is the lose
        /// condition, so culling it would remove the way the game ends.
        /// </summary>
        RingController Cull(RingController ballRing)
        {
            _culled.Clear();
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring == ballRing) continue;
                if (ring.Radius >= _settings.VanishRadius) continue;
                _culled.Add(ring);
            }

            for (int i = 0; i < _culled.Count; i++)
            {
                _rings.Remove(_culled[i]);
                _pool.Return(_culled[i]);
            }

            return ballRing;
        }

        /// <summary>Keeps the required number of rings outside the ball's ring.</summary>
        void Fill(RingController ballRing)
        {
            double reference = ballRing != null ? ballRing.Radius : OutermostRadius();

            int ahead = 0;
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].Radius > reference + 1e-6) ahead++;

            while (ahead < _settings.RingsAhead)
            {
                SpawnAt(OutermostRadius() + _settings.Step);
                ahead++;
            }
        }

        void SpawnAt(double radius)
        {
            var ring = _pool.Rent();
            var spec = _generator.Next(_difficulty, PreviousSpin());
            ring.SpawnIndex = _nextSpawnIndex++;
            ring.Configure(radius, spec.GapCentreDeg, spec.GapArcWidth, spec.AutoSpinDeg);
            _rings.Add(ring);
        }

        double PreviousSpin()
        {
            double outermost = double.MinValue;
            double spin = 0.0;
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].Radius > outermost) { outermost = _rings[i].Radius; spin = _rings[i].AutoSpinDeg; }
            return spin;
        }

        double OutermostRadius()
        {
            double outermost = _settings.TargetRadius;
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].Radius > outermost) outermost = _rings[i].Radius;
            return outermost;
        }

        /// <summary>
        /// Fires RingPassed exactly once per ring. The ball moving to a ring further out is the
        /// signal — it can only get there through a gap — and the contract's distance test is
        /// checked as well so a cull or a reorder cannot be mistaken for a pass.
        /// </summary>
        /// <summary>
        /// Fires RingPassed exactly once per ring.
        ///
        /// CONTRACT.md §2.6 asks for two conditions, and the trap is that they are never true in
        /// the same step: the ball counts as being in the next corridor the moment it enters the
        /// gap, which is while it is still level with the ring it is leaving — far short of that
        /// ring's outer edge plus the margin. Testing both at once therefore never fires at all.
        /// So the departure is latched, and the clearance is checked on later steps.
        /// </summary>
        void DetectPass(RingController current)
        {
            if (current == null) return;

            if (_ballRing == null)
            {
                _ballRing = current;
                return;
            }

            if (!ReferenceEquals(current, _ballRing))
            {
                if (current.Radius > _ballRing.Radius + 1e-6)
                {
                    // Radius captured here, not read later: the ring it left keeps shrinking,
                    // and measuring against a moving target would make the test drift easier.
                    _pendingIndex = _ballRing.SpawnIndex;
                    _pendingRadius = _ballRing.Radius;
                }
                _ballRing = current;
            }

            if (_pendingIndex < 0) return;

            // Scoring only ever moves forward. A ball that is pushed back inside a ring and
            // comes out again would otherwise be counted twice for the same ring, which is
            // exactly what "fires once per ring" forbids.
            if (_pendingIndex <= _lastPassedIndex) { _pendingIndex = -1; return; }

            if (TunnelMath.HasClearedRing(_ball.DistanceToCentre, _pendingRadius,
                                          _settings.Thickness, _settings.PassMargin))
            {
                PassedCount++;
                _lastPassedIndex = _pendingIndex;
                RingPassed?.Invoke(_pendingIndex);
                _pendingIndex = -1;
            }
        }

        void ApplyShading(RingController ballRing)
        {
            double reference = ballRing != null ? ballRing.Radius : _settings.TargetRadius;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                double alpha = DepthShading.AlphaFor(ring.Radius, reference, _settings.VanishRadius,
                                                     _settings.Step, _settings.RingsAhead);
                var colour = _ringColour;
                colour.a = (float)alpha;
                ring.ApplyShading(colour);
            }
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

        /// <summary>Bug guard from CONTRACT.md §1: the ball should never get this far out.</summary>
        public bool IsBallOutOfBounds()
        {
            var ring = ResolveBallRing();
            double reference = ring != null ? ring.Radius : OutermostRadius();
            return TunnelMath.IsOutOfBounds(_ball.DistanceToCentre, reference, _settings.Step);
        }
    }
}
