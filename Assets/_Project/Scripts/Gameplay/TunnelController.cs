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

        readonly bool[] _struck = new bool[MaxBalls];

        public void Initialise(GameConfig config, BallController ball, RingPool pool = null)
        {
            _config = config;
            _settings = config.ToSettings();
            if (_balls[0] != null) _balls[0].Struck -= NoteStrike;
            _balls[0] = ball;
            _ballCount = ball != null ? 1 : 0;
            if (ball != null) ball.Struck += NoteStrike;
            _pool = pool;
            BaseCompressionSpeed = config.baseCompressionSpeed;
            _difficulty = DifficultySnapshot.Opening;
        }

        void NoteStrike(float speed) => _struck[0] = true;

        void OnDestroy()
        {
            if (_balls[0] != null) _balls[0].Struck -= NoteStrike;
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
            _lastPassedIndex = -1;
            _ballCount = _balls[0] != null ? 1 : 0;

            _ledgerClock = 0.0;
            for (int i = 0; i < MaxBalls; i++)
            {
                _ledgers[i].Reset();
                _struck[i] = false;
                _stepsOutside[i] = 0;
            }

            for (double radius = _settings.TargetRadius; radius <= OuterLimit; radius += StepNow)
            {
                if (radius <= _settings.Thickness) continue;
                SpawnAt(radius);
            }

            var band = BallLaunch.BandFor(_settings);
            _ball.Launch(BallLaunch.For(seed, band.Inner, band.Outer, _config.cruiseSpeed));
            ApplyShading(InnermostOccupiedRing());
        }

        RingController _solidRing;

        public RingController SolidRing => _solidRing;

        public double SolidRingRadius { get; private set; }

        public void BeginCountdown(double ratio, int seed)
        {
            if (_pool == null)
                throw new InvalidOperationException("BeginCountdown needs a pool");

            EndCountdown(dissolve: false);

            double radius = _settings.TargetRadius * ratio;
            SolidRingRadius = radius;

            _solidRing = _pool.Rent();
            _solidRing.SpawnIndex = -1;
            _solidRing.ConfigureSolid(radius, 0.0);
            ApplyHaloTo(_solidRing);
            _rings.Add(_solidRing);

            double outer = radius - _settings.Thickness * 0.5 - _settings.BallRadius;
            double inner = outer - _settings.BallRadius * 0.5;
            _ball.Launch(BallLaunch.For(seed, inner, outer, _config.cruiseSpeed));

            ApplyShading(InnermostOccupiedRing());
        }

        public void EndCountdown(bool dissolve)
        {
            if (_solidRing == null) return;

            if (dissolve) _solidRing.BeginDissolve(_config.dissolveSeconds);
            else
            {
                _rings.Remove(_solidRing);
                _pool.Return(_solidRing);
            }

            _solidRing = null;
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
            ApplySolidity(ballRing, dt);

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

        void ApplySolidity(RingController ballRing, double dt)
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

                    double reach = ReachOf(_balls[b], dt);
                    double inner = Math.Min(own.Radius - step, _balls[b].DistanceToCentre - reach);

                    solid = ring.Radius >= inner - 1e-6
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
                bool knocked = _struck[b];
                _struck[b] = false;

                if (ball == null || ring == null) { _ledgers[b].Reset(); continue; }

                _ledgers[b].Watch(ring.SpawnIndex);

                double distance = ball.DistanceToCentre;
                if (distance <= 1e-6) continue;

                Vector2 position = ball.Body.position;
                Vector2 outward = new Vector2((float)(position.x / distance), (float)(position.y / distance));
                double radial = Vector2.Dot(ball.Body.linearVelocity, outward);

                double innerSurface = ring.Radius - _settings.Thickness * 0.5;
                double reach = Math.Abs(radial) * dt;
                bool touching = knocked
                                || distance + _settings.BallRadius >= innerSurface - ContactSlack - reach;

                if (_ledgers[b].Step(_ledgerClock, radial, touching, knocked))
                    BallTouchedRing?.Invoke(_ledgers[b].Touches);
            }
        }

        public const double ContactSlack = 0.02;

        public const int ReachSteps = 4;

        public const double ReachMargin = 2.0;

        double ReachOf(BallController ball, double dt)
        {
            double skin = _settings.BallRadius + _settings.Thickness;
            if (ball == null) return skin;

            double thisStep = ball.Body.linearVelocity.magnitude * dt * ReachMargin;
            double floor = ball.MaxTravelPerStep * ReachSteps;
            return (thisStep > floor ? thisStep : floor) + skin;
        }

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

            if (ResolveRingFor(ball) == null) return;

            double distance = ball.DistanceToCentre;

            while (true)
            {
                var passed = LowestClearedRing(distance, slot);
                if (passed == null) return;

                PassedCount++;
                _lastPassedIndex = passed.SpawnIndex;

                passed.BeginDissolve(_config.dissolveSeconds);
                RingPassed?.Invoke(passed.SpawnIndex);
            }
        }

        RingController LowestClearedRing(double distance, int slot)
        {
            RingController lowest = null;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.IsDissolving) continue;
                if (ring.SpawnIndex <= _lastPassedIndex) continue;
                if (!TunnelMath.HasClearedRing(distance, ring.Radius,
                                               _settings.Thickness, _settings.PassMargin)) continue;

                if (lowest == null || ring.SpawnIndex < lowest.SpawnIndex) lowest = ring;
            }

            if (lowest == null || IsOwnedByAnother(lowest, slot)) return null;

            return lowest;
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

        public const int StepsOutsideBeforeLost = 2;

        readonly int[] _stepsOutside = new int[MaxBalls];

        public bool IsBallOutOfBounds()
        {
            for (int b = 0; b < _ballCount; b++)
            {
                var ball = _balls[b];
                if (ball == null) continue;

                var ring = ResolveRingFor(ball);
                if (ring == null)
                {
                    _stepsOutside[b]++;
                    if (_stepsOutside[b] >= StepsOutsideBeforeLost) return true;
                    continue;
                }

                _stepsOutside[b] = 0;
                if (TunnelMath.IsOutOfBounds(ball.DistanceToCentre, ring.Radius, StepNow)) return true;
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
