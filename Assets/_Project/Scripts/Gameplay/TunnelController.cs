using System;
using System.Collections.Generic;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    public sealed class TunnelController : MonoBehaviour
    {
        readonly List<RingController> _rings = new List<RingController>();
        readonly List<RingController> _culled = new List<RingController>();

        GameConfig _config;
        TunnelSettings _settings;

        const int MaxBalls = 1;
        readonly BallController[] _balls = new BallController[MaxBalls];
        int _ballCount;

        BallController _ball => _ballCount > 0 ? _balls[0] : null;
        RingPool _pool;
        RingGenerator _generator;
        DifficultySnapshot _difficulty;

        int _nextSpawnIndex;

        readonly int[] _pendingIndex = { -1, -1 };
        readonly double[] _pendingRadius = new double[MaxBalls];
        readonly RingController[] _ballRings = new RingController[MaxBalls];

        int _lastPassedIndex = -1;
        Color[] _ringColours = { Color.white };

        Color _fadeToward = Color.black;

        public event Action<int> RingPassed;

        public event Action<int> BallTouchedRing;

        public int PassedCount { get; private set; }
        public double BaseCompressionSpeed { get; set; }
        public double UserAngleDeg { get; set; }
        public bool CompressionEnabled { get; set; } = true;
        public bool RotationEnabled { get; set; } = true;
        public bool ShadingEnabled { get; set; } = true;

        public double StepNow => _difficulty.Step > 0.0 ? _difficulty.Step : _settings.Step;

        public IReadOnlyList<RingController> Rings => _rings;
        public double LastCompressionSpeed { get; private set; }
        public RingPool Pool => _pool;
        public RingGenerator Generator => _generator;
        public int ActiveRingCount => _rings.Count;

        public void Initialise(GameConfig config, BallController ball, RingPool pool = null)
        {
            _config = config;
            _settings = config.ToSettings();
            _balls[0] = ball;
            _ballCount = ball != null ? 1 : 0;
            _pool = pool;
            BaseCompressionSpeed = config.baseCompressionSpeed;
            _difficulty = DifficultySnapshot.Opening;
        }

        public void SetRingColours(IReadOnlyList<Color> colours)
        {
            if (colours == null || colours.Count == 0) return;
            if (_ringColours.Length != colours.Count) _ringColours = new Color[colours.Count];
            for (int i = 0; i < colours.Count; i++) _ringColours[i] = colours[i];
            ForceReshade();
        }

        Color _hotColour = new Color(1f, 0.231f, 0.188f);

        public const float HotMix = 0.7f;

        public void SetHotColour(Color colour) { _hotColour = colour; ForceReshade(); }

        public void SetHotRing(int ballSlot, bool hot)
        {
            if (ballSlot < 0 || ballSlot >= _ballCount) return;
            var ring = ResolveRingFor(_balls[ballSlot]);
            if (ring != null) ring.SetHot(hot);
        }
        double _depthFade = 1.0;
        float _haloWidth;
        float _haloStrength;

        public void SetDepthAndGlow(double depthFade, double glow)
        {
            _depthFade = depthFade;
            _haloStrength = (float)glow;
            _haloWidth = (float)(_settings.Thickness * glow);

            for (int i = 0; i < _rings.Count; i++) ApplyHaloTo(_rings[i]);
            ForceReshade();
        }

        void ApplyHaloTo(RingController ring)
        {
            ring.HaloBackground = _fadeToward;
            ring.HaloStrength = Mathf.Lerp(0.2f, 0.5f, _haloStrength);
            ring.SetHaloWidth(_haloWidth);
            ring.InvalidateShading();
        }

        public void SetFadeColour(Color colour)
        {
            _fadeToward = colour;
            ForceReshade();
        }

        public void ForceReshade()
        {
            for (int i = 0; i < _rings.Count; i++) _rings[i].InvalidateShading();
            ApplyShading(ResolveBallRing());
        }
        public void SetDifficulty(in DifficultySnapshot difficulty) => _difficulty = difficulty;

        public void Register(RingController ring) => _rings.Add(ring);
        public void Clear() => _rings.Clear();

        public void BeginRun(int seed)
        {
            if (_pool == null)
                throw new InvalidOperationException("BeginRun needs a pool; pass one to Initialise");

            for (int i = 0; i < _rings.Count; i++) _pool.Return(_rings[i]);
            _rings.Clear();

            _generator = new RingGenerator(seed);
            _nextSpawnIndex = 0;
            PassedCount = 0;
            for (int i = 0; i < MaxBalls; i++)
            {
                _ballRings[i] = null;
                _pendingIndex[i] = -1;
            }
            _lastPassedIndex = -1;
            _ballCount = _balls[0] != null ? 1 : 0;

            _ledgerClock = 0.0;
            for (int i = 0; i < MaxBalls; i++) _ledgers[i].Reset();

            for (double radius = _settings.TargetRadius; radius <= OuterLimit; radius += StepNow)
            {
                if (radius <= _settings.Thickness) continue;
                SpawnAt(radius);
            }

            var band = BallLaunch.BandFor(_settings);
            _ball.Launch(BallLaunch.For(seed, band.Inner, band.Outer, _config.cruiseSpeed));
            for (int i = 0; i < _ballCount; i++) _ballRings[i] = ResolveRingFor(_balls[i]);
            ApplyShading(InnermostOccupiedRing());
        }

        public bool AutoStep { get; set; } = true;

        void FixedUpdate()
        {
            if (AutoStep) Step(Time.fixedDeltaTime);
        }

        public const double MaxStepSeconds = 0.1;

        public const int SolidRingsOutside = 2;

        public void Step(double dt)
        {
            if (_rings.Count == 0) return;

            if (double.IsNaN(dt) || dt <= 0.0) return;
            if (dt > MaxStepSeconds) dt = MaxStepSeconds;

            var ballRing = ResolveBallRing();

            StepLedgers(dt);
            if (CompressionEnabled) Compress(dt, ballRing);
            if (RotationEnabled) Rotate(dt);
            ApplySolidity(ballRing);

            if (_pool != null)
            {
                ballRing = Retire(dt, ballRing);
                ballRing = Cull(ballRing);
                Fill(ballRing);
            }

            DetectPass();
            if (ShadingEnabled) ApplyShading(ballRing);
        }

        void Compress(double dt, RingController ballRing)
        {
            double ballRingRadius = ballRing != null ? ballRing.Radius : _settings.TargetRadius;

            double ceiling = Math.Max(_difficulty.MaxCompressionSpeed, BaseCompressionSpeed);

            LastCompressionSpeed = TunnelMath.CompressionSpeed(
                BaseCompressionSpeed, TunnelMath.PullFor(_settings.PullPerStep, StepNow), ceiling,
                ballRingRadius, _settings.TargetRadius);

            double delta = LastCompressionSpeed * dt;
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                double next = ring.Radius - delta;

                if (next <= _settings.Thickness) continue;
                ring.SetRadius(next);
            }
        }

        void ApplySolidity(RingController ballRing)
        {
            double step = StepNow;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                bool solid = false;

                for (int b = 0; b < _ballCount && !solid; b++)
                {
                    var own = ResolveRingFor(_balls[b]);

                    if (own == null)
                    {
                        solid = ring.Radius >= OutermostRadius() - step * SolidRingsOutside;
                        continue;
                    }

                    solid = ring.Radius >= own.Radius - step - 1e-6
                            && ring.Radius <= own.Radius + step * SolidRingsOutside;
                }

                ring.SetSolid(solid);
            }
        }

        public RingController InnermostOccupiedRing()
        {
            RingController best = null;
            for (int b = 0; b < _ballCount; b++)
            {
                var ring = ResolveRingFor(_balls[b]);
                if (ring == null) continue;
                if (best == null || ring.Radius < best.Radius) best = ring;
            }
            return best;
        }

        public int BallCount => _ballCount;

        public BallController BallAt(int index) => _balls[index];

        void Rotate(double dt)
        {
            for (int i = 0; i < _rings.Count; i++)
                _rings[i].StepRotation(UserAngleDeg, dt);
        }

        RingController Cull(RingController ballRing)
        {
            _culled.Clear();
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring == ballRing) continue;
                if (ring.IsDissolving) continue;
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

        RingController Retire(double dt, RingController ballRing)
        {
            _culled.Clear();
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (!ring.IsDissolving) continue;
                if (ring.AdvanceDissolve(dt)) _culled.Add(ring);
            }

            for (int i = 0; i < _culled.Count; i++)
            {
                _rings.Remove(_culled[i]);
                _pool.Return(_culled[i]);
            }

            return ballRing;
        }

        void Fill(RingController ballRing)
        {
            double step = StepNow;
            if (step <= 0.0) return;

            int guard = MaxRingsInFrame;
            while (OutermostRadius() + step <= OuterLimit && guard-- > 0)
                SpawnAt(OutermostRadius() + step);
        }

        double OuterLimit => _config != null && _config.outerRadiusLimit > _settings.TargetRadius
            ? _config.outerRadiusLimit
            : _settings.TargetRadius + _settings.Step * _settings.RingsAhead;

        public const int MaxRingsInFrame = 32;

        int RingsAheadNow(double reference)
        {
            double step = StepNow;
            if (step <= 0.0) return _settings.RingsAhead;
            int ahead = (int)((OuterLimit - reference) / step);
            return ahead > 0 ? ahead : 1;
        }

        readonly TouchLedger[] _ledgers = { new TouchLedger() };
        double _ledgerClock;

        public int TouchesOn(int ballSlot) =>
            ballSlot >= 0 && ballSlot < MaxBalls ? _ledgers[ballSlot].Touches : 0;

        void StepLedgers(double dt)
        {
            _ledgerClock += dt;

            for (int b = 0; b < _ballCount; b++)
            {
                var ball = _balls[b];
                var ring = ResolveRingFor(ball);
                if (ball == null || ring == null) { _ledgers[b].Reset(); continue; }

                _ledgers[b].Watch(ring.SpawnIndex);

                double distance = ball.DistanceToCentre;
                if (distance <= 1e-6) continue;

                Vector2 position = ball.Body.position;
                Vector2 outward = new Vector2((float)(position.x / distance), (float)(position.y / distance));
                double radial = Vector2.Dot(ball.Body.linearVelocity, outward);

                double innerSurface = ring.Radius - _settings.Thickness * 0.5;
                bool touching = distance + _settings.BallRadius >= innerSurface - ContactSlack;

                if (_ledgers[b].Step(_ledgerClock, radial, touching))
                    BallTouchedRing?.Invoke(_ledgers[b].Touches);
            }
        }

        public const double ContactSlack = 0.02;

        void SpawnAt(double radius)
        {
            var ring = _pool.Rent();
            var spec = _generator.Next(_difficulty, PreviousSpin());
            ring.SpawnIndex = _nextSpawnIndex++;
            ring.Configure(radius, spec.Gaps, spec.AutoSpinDeg, spec.Kind);

            ApplyHaloTo(ring);

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

        void DetectPass()
        {
            for (int slot = 0; slot < _ballCount; slot++) DetectPassFor(slot);
        }

        void DetectPassFor(int slot)
        {
            var ball = _balls[slot];
            if (ball == null) return;

            var current = ResolveRingFor(ball);
            if (current == null) return;

            if (_ballRings[slot] == null)
            {
                _ballRings[slot] = current;
                return;
            }

            if (!ReferenceEquals(current, _ballRings[slot]))
            {
                var left = _ballRings[slot];
                if (current.Radius > left.Radius + 1e-6)
                {
                    _pendingIndex[slot] = left.SpawnIndex;
                    _pendingRadius[slot] = left.Radius;
                }
                _ballRings[slot] = current;
            }

            if (_pendingIndex[slot] < 0) return;

            if (_pendingIndex[slot] <= _lastPassedIndex) { _pendingIndex[slot] = -1; return; }

            if (TunnelMath.HasClearedRing(ball.DistanceToCentre, _pendingRadius[slot],
                                          _settings.Thickness, _settings.PassMargin))
            {
                PassedCount++;
                _lastPassedIndex = _pendingIndex[slot];

                DissolvePassed(_pendingIndex[slot], slot);

                RingPassed?.Invoke(_pendingIndex[slot]);
                _pendingIndex[slot] = -1;
            }
        }

        void DissolvePassed(int spawnIndex, int slot)
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.SpawnIndex != spawnIndex) continue;
                if (ring.IsDissolving) return;

                if (IsOwnedByAnother(ring, slot)) return;

                ring.BeginDissolve(_config.dissolveSeconds);
                return;
            }
        }

        bool IsOwnedByAnother(RingController ring, int exceptSlot)
        {
            for (int i = 0; i < _ballCount; i++)
            {
                if (i == exceptSlot || _balls[i] == null) continue;
                if (ReferenceEquals(ResolveRingFor(_balls[i]), ring)) return true;
            }
            return false;
        }

        void ApplyShading(RingController ballRing)
        {
            double reference = ballRing != null ? ballRing.Radius : _settings.TargetRadius;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];

                double alpha = DepthShading.AlphaFor(ring.Radius, reference, _settings.VanishRadius,
                                                     StepNow, RingsAheadNow(reference), _depthFade);

                if (ring.IsDissolving) alpha *= 1.0 - ring.DissolveProgress;
                int slot = ((ring.SpawnIndex % _ringColours.Length) + _ringColours.Length)
                           % _ringColours.Length;

                var ink = _ringColours[slot];

                if (ring.IsHot) ink = Color.Lerp(ink, _hotColour, HotMix);
                var colour = Color.Lerp(_fadeToward, ink, (float)alpha);
                colour.a = 1f;
                ring.ApplyShading(colour);
            }
        }

        public RingController ResolveBallRing() => ResolveRingFor(_ball);

        public RingController ResolveRingFor(BallController ball)
        {
            if (ball == null) return null;

            double ballDistance = ball.DistanceToCentre;
            RingController best = null;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.IsDissolving) continue;
                double innerSurface = ring.Radius - _settings.Thickness * 0.5;
                if (innerSurface < ballDistance - 1e-4) continue;
                if (best == null || ring.Radius < best.Radius) best = ring;
            }

            return best;
        }

        public bool HasRingClosedOnCentre() => RingClosedOnCentre() != null;

        public bool IsBallOutOfBounds()
        {
            for (int b = 0; b < _ballCount; b++)
            {
                var ball = _balls[b];
                if (ball == null) continue;

                var ring = ResolveRingFor(ball);
                double reference = ring != null ? ring.Radius : OutermostRadius();
                if (TunnelMath.IsOutOfBounds(ball.DistanceToCentre, reference, StepNow)) return true;
            }
            return false;
        }

        public RingController RingClosedOnCentre()
        {
            RingController innermost = null;
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.IsDissolving) continue;
                if (innermost == null || ring.Radius < innermost.Radius) innermost = ring;
            }

            if (innermost == null) return null;
            return TunnelMath.HasReachedRed(innermost.Radius, _settings.Thickness, _settings.RedRadius)
                ? innermost : null;
        }

        public int BallsInside(RingController ring)
        {
            int n = 0;
            for (int b = 0; b < _ballCount; b++)
                if (ReferenceEquals(ResolveRingFor(_balls[b]), ring)) n++;
            return n;
        }
    }
}
