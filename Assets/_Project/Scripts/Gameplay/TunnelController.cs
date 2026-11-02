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
        bool _pendingWasSuper;
        Color[] _ringColours = { Color.white };

        // Depth is carried by fading toward the background rather than by alpha: the unlit
        // shader is opaque and ignores alpha entirely, and making it transparent would buy
        // sorting artefacts between overlapping rings for no visible gain on a solid backdrop.
        Color _fadeToward = Color.black;

        /// <summary>Fires once per ring cleared, with that ring's spawn index.</summary>
        public event Action<int> RingPassed;

        /// <summary>
        /// Fires when a super ring's wave has taken rings the player will now never meet, with
        /// how many. They are worth a point each: the score stays equal to the number of the
        /// ring the ball is in, so difficulty and unlock thresholds keep meaning what they say.
        /// </summary>
        public event Action<int> RingsDissolved;

        public int PassedCount { get; private set; }
        public double BaseCompressionSpeed { get; set; }
        public double UserAngleDeg { get; set; }
        public bool CompressionEnabled { get; set; } = true;
        public bool RotationEnabled { get; set; } = true;
        public bool ShadingEnabled { get; set; } = true;

        /// <summary>
        /// Distance between neighbouring rings right now.
        ///
        /// It used to be TunnelSettings.Step — one number for a whole run. The corridor now
        /// narrows with difficulty, so the live value belongs to the snapshot and every place
        /// that spaces, spawns or fades rings has to read the same one. The fallback is for
        /// fixtures that drive the tunnel before any difficulty has been applied.
        /// </summary>
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
            _ball = ball;
            _pool = pool;
            BaseCompressionSpeed = config.baseCompressionSpeed;
            _difficulty = DifficultySnapshot.Opening;
        }

        /// <summary>
        /// The colour cycle rings are drawn in. Indexed by spawn order, so a ring keeps its
        /// colour for its whole life and neighbours differ — the depth read in spec §11.2.
        /// </summary>
        public void SetRingColours(IReadOnlyList<Color> colours)
        {
            if (colours == null || colours.Count == 0) return;
            if (_ringColours.Length != colours.Count) _ringColours = new Color[colours.Count];
            for (int i = 0; i < colours.Count; i++) _ringColours[i] = colours[i];
            ForceReshade();
        }

        /// <summary>The reward ring's ink, which the theme owns like every other colour.</summary>
        public void SetSuperColour(Color colour)
        {
            _superColour = colour;
            ForceReshade();
        }

        Color _superColour = new Color(0.91f, 0.64f, 0.09f);

        /// <summary>The colour distant rings fade into — the theme's background.</summary>
        public void SetFadeColour(Color colour)
        {
            _fadeToward = colour;
            ForceReshade();
        }

        /// <summary>Makes every ring re-read its colour, after a theme change.</summary>
        public void ForceReshade()
        {
            for (int i = 0; i < _rings.Count; i++) _rings[i].InvalidateShading();
            ApplyShading(ResolveBallRing());
        }
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
            _pendingWasSuper = false;
            _restLeft = 0.0;
            for (int i = 0; i < _waveCount; i++) _wave[i] = null;
            _waveCount = 0;

            // The ball's own ring, then outwards until the tunnel reaches the edge of the frame.
            // Nothing inside it: a ring the ball has left is retired the moment it leaves, so an
            // opening ring inside the ball would be the only one of its kind in the whole run and
            // the first level would play unlike every other.
            for (double radius = _settings.TargetRadius; radius <= OuterLimit; radius += StepNow)
            {
                if (radius <= _settings.Thickness) continue;
                SpawnAt(radius);
            }

            var band = BallLaunch.BandFor(_settings);
            _ball.Launch(BallLaunch.For(seed, band.Inner, band.Outer, _config.cruiseSpeed));
            _ballRing = ResolveBallRing();
            ApplyShading(_ballRing);
        }

        /// <summary>
        /// Whether the component advances itself from FixedUpdate. On in the game; off in tests,
        /// which step explicitly — leaving both on means the tunnel advances twice per frame
        /// boundary and nothing says so.
        /// </summary>
        public bool AutoStep { get; set; } = true;

        void FixedUpdate()
        {
            if (AutoStep) Step(Time.fixedDeltaTime);
        }

        /// <summary>One step of the tunnel. Public so tests can drive it deterministically.</summary>
        /// <summary>CONTRACT.md G-14: the longest frame the game will believe.</summary>
        public const double MaxStepSeconds = 0.1;

        /// <summary>
        /// How many rings outside the ball's own stay solid. Two, and the margin is not taste:
        /// the ball is inside its ring and can only ever touch that one, so the ring beyond it
        /// matters only after a pass — and at the speed ceiling the ball covers a fifth of a unit
        /// in a step against two steps of tunnel, which is this many times over.
        /// </summary>
        public const int SolidRingsOutside = 2;

        public void Step(double dt)
        {
            if (_rings.Count == 0) return;

            // Both ends of this arrive in practice. A frame of zero comes on the step after a
            // resume; a frame of a second comes when the OS stalls the app, and taken at face
            // value it compresses the tunnel by a second's worth in one go and kills a player
            // who did nothing. A negative one should be impossible and is refused rather than
            // run backwards, because running the compression in reverse is not a recovery.
            if (double.IsNaN(dt) || dt <= 0.0) return;
            if (dt > MaxStepSeconds) dt = MaxStepSeconds;

            // Resolved once and threaded through. It used to be recomputed by each stage, which
            // is five scans of the ring list per step for an answer that cannot change mid-step.
            var ballRing = ResolveBallRing();

            StepRest(dt);
            StepWave(dt);
            if (CompressionEnabled) Compress(dt, ballRing);
            if (RotationEnabled) Rotate(dt);
            ApplySolidity(ballRing);

            if (_pool != null)
            {
                ballRing = Retire(dt, ballRing);
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
            // The ceiling comes from the difficulty row, not from the config: how hard the
            // tunnel snaps back after a pass is a dial that opens up over a run, while the
            // config's value is only the floor a table may never go under.
            double ceiling = Math.Max(_difficulty.MaxCompressionSpeed, BaseCompressionSpeed);

            // The pull is derived from the live step, not stored: see TunnelMath.PullFor.
            LastCompressionSpeed = TunnelMath.CompressionSpeed(
                BaseCompressionSpeed, TunnelMath.PullFor(_settings.PullPerStep, StepNow), ceiling,
                ballRingRadius, _settings.TargetRadius);

            LastCompressionSpeed *= RestFactor;
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

        /// <summary>
        /// Leaves colliders on only where the ball could reach. Measured, not assumed: ten rings
        /// in the frame cost 5365 us a step against 1836 before D-059, and almost all of it was
        /// Box2D working through polygons the ball was nowhere near.
        /// </summary>
        void ApplySolidity(RingController ballRing)
        {
            double reference = ballRing != null ? ballRing.Radius : OutermostRadius();
            double limit = reference + StepNow * SolidRingsOutside;

            for (int i = 0; i < _rings.Count; i++)
                _rings[i].SetSolid(_rings[i].Radius <= limit);
        }

        /// <summary>Seconds left of the rest a super ring bought, counting the fade back.</summary>
        double _restLeft;

        // The wave of dissolving rings, as a fixed queue. Sized once at the largest the config
        // allows, because a list that grows is an allocation and this fires mid-run.
        const int MaxWave = 5;
        readonly RingController[] _wave = new RingController[MaxWave];
        readonly double[] _waveDelay = new double[MaxWave];
        int _waveCount;

        /// <summary>
        /// Starts the reward for clearing a super ring: the next few rings let the ball through
        /// without being cleared, one after another rather than all at once.
        ///
        /// Staggered because three rings vanishing on the same frame reads as a glitch, and
        /// because the wave travelling outwards is the thing that says what just happened.
        /// </summary>
        void BeginWave(RingController ballRing)
        {
            if (_config == null) return;

            int wanted = Mathf.Clamp(_config.superDissolveCount, 0, MaxWave);
            if (wanted == 0) return;

            _waveCount = 0;
            double reference = ballRing != null ? ballRing.Radius : OutermostRadius();

            // Outwards from the ball, nearest first: these are the rings it would have met next.
            for (int picked = 0; picked < wanted; picked++)
            {
                RingController best = null;
                for (int i = 0; i < _rings.Count; i++)
                {
                    var ring = _rings[i];
                    if (ring == ballRing || ring.IsDissolving) continue;
                    if (ring.Radius <= reference + 1e-6) continue;
                    if (AlreadyInWave(ring)) continue;
                    if (best == null || ring.Radius < best.Radius) best = ring;
                }

                if (best == null) break;
                _wave[_waveCount] = best;
                _waveDelay[_waveCount] = _config.superWaveStagger * _waveCount;
                _waveCount++;
            }

            _restLeft = _config.superSlowSeconds + _config.superSlowReturnSeconds;
            if (_waveCount > 0) RingsDissolved?.Invoke(_waveCount);
        }

        bool AlreadyInWave(RingController ring)
        {
            for (int i = 0; i < _waveCount; i++)
                if (ReferenceEquals(_wave[i], ring)) return true;
            return false;
        }

        void StepWave(double dt)
        {
            for (int i = 0; i < _waveCount; i++)
            {
                if (_wave[i] == null) continue;

                _waveDelay[i] -= dt;
                if (_waveDelay[i] > 0.0) continue;

                _wave[i].BeginDissolve(_config.dissolveSeconds);
                _wave[i] = null;
            }
        }

        /// <summary>
        /// How much of normal compression is running right now.
        ///
        /// A flat multiplier that snapped back to one would read as the tunnel being shoved; the
        /// last second eases it back instead, so the rest ends rather than stops.
        /// </summary>
        double RestFactor
        {
            get
            {
                if (_restLeft <= 0.0 || _config == null) return 1.0;

                double fade = _config.superSlowReturnSeconds;
                if (fade <= 0.0 || _restLeft > fade) return _config.superSlowFactor;

                double t = _restLeft / fade;
                return _config.superSlowFactor + (1.0 - _config.superSlowFactor) * (1.0 - t);
            }
        }

        void StepRest(double dt)
        {
            if (_restLeft > 0.0) _restLeft -= dt;
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
                if (ring.IsDissolving) continue;   // Retire owns these
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

        /// <summary>
        /// Advances the fade on rings the ball has left and returns the finished ones to the pool.
        ///
        /// This is how rings mostly die now; Cull is left as the backstop for a ring that
        /// collapses without the ball ever having been in it, which the opening layout no longer
        /// produces but a future one might.
        /// </summary>
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

        /// <summary>
        /// Grows the tunnel outwards until it reaches the edge of the frame.
        ///
        /// Counted by radius rather than by number of rings, because the corridor narrows over a
        /// run: a fixed count would reach less and less far out, and the tunnel would visibly
        /// stop short of the frame edge exactly when it got densest. How many rings that is
        /// follows from the step, which is the right way round.
        /// </summary>
        void Fill(RingController ballRing)
        {
            double step = StepNow;
            if (step <= 0.0) return;

            // Bounded so a nonsense step cannot spin here forever. The bound is generous: it is
            // reached only if something has already gone wrong with the geometry.
            int guard = MaxRingsInFrame;
            while (OutermostRadius() + step <= OuterLimit && guard-- > 0)
                SpawnAt(OutermostRadius() + step);
        }

        /// <summary>
        /// Furthest a ring is spawned to. Beyond it the tunnel is off-screen, so a ring there is
        /// geometry nobody sees.
        /// </summary>
        double OuterLimit => _config != null && _config.outerRadiusLimit > _settings.TargetRadius
            ? _config.outerRadiusLimit
            : _settings.TargetRadius + _settings.Step * _settings.RingsAhead;

        /// <summary>Hard ceiling on rings drawn at once, as a loop guard rather than a design number.</summary>
        public const int MaxRingsInFrame = 32;

        /// <summary>
        /// How many rings stand between the ball's ring and the edge of the frame right now.
        ///
        /// The depth fade needs this to know how far out "just arrived" is. It used to be a
        /// configured count; it is now a consequence of the step, so a denser tunnel gets more
        /// rings rather than a shorter one.
        /// </summary>
        int RingsAheadNow(double reference)
        {
            double step = StepNow;
            if (step <= 0.0) return _settings.RingsAhead;
            int ahead = (int)((OuterLimit - reference) / step);
            return ahead > 0 ? ahead : 1;
        }

        void SpawnAt(double radius)
        {
            var ring = _pool.Rent();
            var spec = _generator.Next(_difficulty, PreviousSpin());
            ring.SpawnIndex = _nextSpawnIndex++;
            ring.Configure(radius, spec.Gaps, spec.AutoSpinDeg, spec.Kind);
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
                    // Capturing it is also what lets the ring itself go away — the latch holds a
                    // number and an index, never the object.
                    _pendingIndex = _ballRing.SpawnIndex;
                    _pendingRadius = _ballRing.Radius;
                    _pendingWasSuper = _ballRing.Kind == RingKind.Super;
                    _ballRing.BeginDissolve(_config.dissolveSeconds);
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
                if (_pendingWasSuper) BeginWave(current);
                _pendingWasSuper = false;
                _pendingIndex = -1;
            }
        }

        void ApplyShading(RingController ballRing)
        {
            double reference = ballRing != null ? ballRing.Radius : _settings.TargetRadius;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                // How many rings are ahead follows from the step and the frame, so the fade-in
                // window stays one ring wide however dense the tunnel has become.
                double alpha = DepthShading.AlphaFor(ring.Radius, reference, _settings.VanishRadius,
                                                     StepNow, RingsAheadNow(reference));

                // A ring the ball has left is already not a wall; the fade is the only part of
                // it the player still sees, so it rides on top of the depth cue rather than
                // replacing it.
                if (ring.IsDissolving) alpha *= 1.0 - ring.DissolveProgress;
                int slot = ((ring.SpawnIndex % _ringColours.Length) + _ringColours.Length)
                           % _ringColours.Length;

                // A super ring keeps its own colour but not its own depth: it has to be
                // recognisable from far out, and it still has to sit in the tunnel.
                var ink = ring.Kind == RingKind.Super ? _superColour : _ringColours[slot];
                var colour = Color.Lerp(_fadeToward, ink, (float)alpha);
                colour.a = 1f;
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
                if (ring.IsDissolving) continue;   // no collider, so no longer a wall
                double innerSurface = ring.Radius - _settings.Thickness * 0.5;
                if (innerSurface < ballDistance - 1e-4) continue;
                if (best == null || ring.Radius < best.Radius) best = ring;
            }

            return best;
        }

        /// <summary>
        /// True when the innermost ring still standing has closed onto the red circle.
        ///
        /// The ball's position is deliberately not part of this. The player is told where the
        /// boundary is by a circle drawn at exactly that radius, and a rule they can watch is
        /// worth more than one that also depends on where the ball happened to be.
        /// </summary>
        public bool HasRingClosedOnCentre()
        {
            double innermost = double.MaxValue;
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.IsDissolving) continue;
                if (ring.Radius < innermost) innermost = ring.Radius;
            }

            if (innermost == double.MaxValue) return false;
            return TunnelMath.HasReachedRed(innermost, _settings.Thickness, _settings.RedRadius);
        }

        /// <summary>Bug guard from CONTRACT.md §1: the ball should never get this far out.</summary>
        public bool IsBallOutOfBounds()
        {
            var ring = ResolveBallRing();
            double reference = ring != null ? ring.Radius : OutermostRadius();
            return TunnelMath.IsOutOfBounds(_ball.DistanceToCentre, reference, StepNow);
        }
    }
}
