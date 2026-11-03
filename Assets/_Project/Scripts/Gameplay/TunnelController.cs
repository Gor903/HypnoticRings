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

        // One ball, in a fixed slot. The array and the count are what is left of the second
        // ball a bead used to wake; the shape costs nothing and keeps the crush rule, which
        // still asks how many balls a closing ring has inside it.
        const int MaxBalls = 1;
        readonly BallController[] _balls = new BallController[MaxBalls];
        int _ballCount;

        /// <summary>The first ball. Most of the game has exactly one, and the bot steers it.</summary>
        BallController _ball => _ballCount > 0 ? _balls[0] : null;
        RingPool _pool;
        RingGenerator _generator;
        DifficultySnapshot _difficulty;

        int _nextSpawnIndex;
        // One latch per ball: each clears rings on its own, and a shared latch would let the
        // ball that happens to be ahead swallow the other one's pass.
        readonly int[] _pendingIndex = { -1, -1 };
        readonly double[] _pendingRadius = new double[MaxBalls];
        readonly RingController[] _ballRings = new RingController[MaxBalls];

        // Scoring is shared: a ring is worth a point once, whichever ball cleared it.
        int _lastPassedIndex = -1;
        Color[] _ringColours = { Color.white };

        // Depth is carried by fading toward the background rather than by alpha: the unlit
        // shader is opaque and ignores alpha entirely, and making it transparent would buy
        // sorting artefacts between overlapping rings for no visible gain on a solid backdrop.
        Color _fadeToward = Color.black;

        /// <summary>Fires once per ring cleared, with that ring's spawn index.</summary>
        public event Action<int> RingPassed;

        /// <summary>
        /// Fires when a ball is turned back by its ring, with how many times that ring has
        /// turned it now. Hardcore ends a run on the second one; the ordinary game only listens
        /// so it can make a noise.
        /// </summary>
        public event Action<int> BallTouchedRing;

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
            _balls[0] = ball;
            _ballCount = ball != null ? 1 : 0;
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

        Color _hotColour = new Color(1f, 0.231f, 0.188f);

        /// <summary>How much of the danger colour a spent ring takes. Plan [HV-01].</summary>
        public const float HotMix = 0.7f;

        /// <summary>The danger colour, which is the red circle's — one idea, one colour.</summary>
        public void SetHotColour(Color colour) { _hotColour = colour; ForceReshade(); }

        /// <summary>
        /// Marks the ring a ball is in as spent, or not. Driven from outside because whether a
        /// ring is spent depends on the mode's allowance, and the tunnel does not own rules.
        /// </summary>
        public void SetHotRing(int ballSlot, bool hot)
        {
            if (ballSlot < 0 || ballSlot >= _ballCount) return;
            var ring = ResolveRingFor(_balls[ballSlot]);
            if (ring != null) ring.SetHot(hot);
        }
        double _depthFade = 1.0;
        float _haloWidth;
        float _haloStrength;

        /// <summary>
        /// How much of the tunnel a theme wants to keep visible, and how wide a halo it wants.
        ///
        /// Both arrive together because both are the same decision: a palette that glows wants
        /// its far rings to stay, and one that does not wants them gone.
        /// </summary>
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
            for (int i = 0; i < MaxBalls; i++)
            {
                _ballRings[i] = null;
                _pendingIndex[i] = -1;
            }
            _lastPassedIndex = -1;
            _ballCount = _balls[0] != null ? 1 : 0;

            // Touch tallies are per ring, and ring numbers start over with the run. Left alone,
            // the ledger still believes it is watching ring zero and keeps the count it ended
            // the last run with — which in hardcore means starting a run already one touch down.
            _ledgerClock = 0.0;
            for (int i = 0; i < MaxBalls; i++) _ledgers[i].Reset();

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
            for (int i = 0; i < _ballCount; i++) _ballRings[i] = ResolveRingFor(_balls[i]);
            ApplyShading(InnermostOccupiedRing());
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
            // The innermost occupied ring, not the first ball's: a ball that has run ahead must
            // not drag the tunnel in after it, and the danger is always whatever is closest to
            // the red circle with somebody inside it. Plan [G-33].
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
            double step = StepNow;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                bool solid = false;

                // A ring is a wall if SOME ball could reach it: its own ring, or one of the two
                // beyond it. Stated per ball rather than from a single reference, because with
                // two balls there is no single "here" — and a ring that is somebody's own ring
                // is covered by its own clause, which is what keeps an occupied inner ring solid
                // for the ball still in it. Plan [G-11].
                for (int b = 0; b < _ballCount && !solid; b++)
                {
                    var own = ResolveRingFor(_balls[b]);

                    // A ball with no ring is outside the whole tunnel, and the previous rule
                    // made every ring passable for it — nothing was anybody's own ring, so
                    // nothing was a wall, and a ball that got out once was gone for good. The
                    // outermost rings become its wall instead, which is what the tunnel would
                    // have been if it had never escaped.
                    if (own == null)
                    {
                        solid = ring.Radius >= OutermostRadius() - step * SolidRingsOutside;
                        continue;
                    }

                    // One step further in than the ball's own ring, not level with it. The ring
                    // a ball is in the middle of crossing is no longer "its" ring — the moment
                    // its centre passes the inner surface, the next ring out becomes that — and
                    // keying solidity on ownership alone switched off the walls of the very ring
                    // the ball was inside. It stays a wall until it dissolves, which now happens
                    // only once the ball has genuinely cleared it.
                    solid = ring.Radius >= own.Radius - step - 1e-6
                            && ring.Radius <= own.Radius + step * SolidRingsOutside;
                }

                ring.SetSolid(solid);
            }
        }

        /// <summary>
        /// The occupied ring closest to the centre — the one the compression law and the danger
        /// are both measured from. Null when no ball is inside anything.
        /// </summary>
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

        /// <summary>Balls in play right now. One, since the spare was taken out (D-088).</summary>
        public int BallCount => _ballCount;

        public BallController BallAt(int index) => _balls[index];

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

        readonly TouchLedger[] _ledgers = { new TouchLedger() };
        double _ledgerClock;

        /// <summary>How many times the ring a ball is in has turned it back.</summary>
        public int TouchesOn(int ballSlot) =>
            ballSlot >= 0 && ballSlot < MaxBalls ? _ledgers[ballSlot].Touches : 0;

        /// <summary>
        /// Watches each ball for the moment its ring turns it back.
        ///
        /// Contact is decided geometrically rather than by a collision callback, for the same
        /// reason the ledger counts reversals rather than events: a ring is a row of colliders,
        /// and the engine's idea of "a contact" is an artefact of where the seams happen to be.
        /// The ball is against the wall when its own edge reaches the ring's inner surface, and
        /// that is a fact about the tunnel rather than about Box2D.
        /// </summary>
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

        /// <summary>
        /// How close to the wall still counts as against it, in world units.
        ///
        /// Box2D keeps a small separation between touching bodies, so an exact test would miss
        /// every real contact by a hair. Small enough that open flight is never mistaken for a
        /// bounce.
        /// </summary>
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
                    // Radius captured here, not read later: the ring it left keeps shrinking,
                    // and measuring against a moving target would make the test drift easier.
                    // Capturing it is also what lets the ring itself go away — the latch holds a
                    // number and an index, never the object.
                    _pendingIndex[slot] = left.SpawnIndex;
                    _pendingRadius[slot] = left.Radius;
                }
                _ballRings[slot] = current;
            }

            if (_pendingIndex[slot] < 0) return;

            // Scoring only ever moves forward. A ball that is pushed back inside a ring and
            // comes out again would otherwise be counted twice for the same ring, which is
            // exactly what "fires once per ring" forbids. Shared across balls on purpose: the
            // second ball to leave a ring is following, not scoring.
            if (_pendingIndex[slot] <= _lastPassedIndex) { _pendingIndex[slot] = -1; return; }

            if (TunnelMath.HasClearedRing(ball.DistanceToCentre, _pendingRadius[slot],
                                          _settings.Thickness, _settings.PassMargin))
            {
                PassedCount++;
                _lastPassedIndex = _pendingIndex[slot];

                // The ring goes now, not when the ball first poked into its opening.
                //
                // It used to dissolve at the moment the ball's centre crossed its inner surface
                // — while the ball was still level with the ring, halfway through the gap. Two
                // things followed, and both were reported from the phone. The ring appeared to
                // vanish as the ball reached the NEXT one, because that is when the eye catches
                // up with it. And its colliders went off while the ball was still inside it, so
                // a ball lined up with the next opening could run the whole tunnel without ever
                // meeting a wall.
                DissolvePassed(_pendingIndex[slot], slot);

                RingPassed?.Invoke(_pendingIndex[slot]);
                _pendingIndex[slot] = -1;
            }
        }

        /// <summary>
        /// Retires the ring a ball has just cleared, by its number rather than by holding it.
        ///
        /// By number because the latch deliberately keeps no reference: between the ball
        /// leaving and the pass being confirmed the ring may have been culled, dissolved by a
        /// super ring's wave, or burst around the other ball.
        /// </summary>
        void DissolvePassed(int spawnIndex, int slot)
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.SpawnIndex != spawnIndex) continue;
                if (ring.IsDissolving) return;

                // Not while the other ball is still inside it: it would find itself in open
                // space with no wall to bounce off.
                if (IsOwnedByAnother(ring, slot)) return;

                ring.BeginDissolve(_config.dissolveSeconds);
                return;
            }
        }

        /// <summary>Whether any other ball still calls this ring its own.</summary>
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
                // How many rings are ahead follows from the step and the frame, so the fade-in
                // window stays one ring wide however dense the tunnel has become.
                double alpha = DepthShading.AlphaFor(ring.Radius, reference, _settings.VanishRadius,
                                                     StepNow, RingsAheadNow(reference), _depthFade);

                // A ring the ball has left is already not a wall; the fade is the only part of
                // it the player still sees, so it rides on top of the depth cue rather than
                // replacing it.
                if (ring.IsDissolving) alpha *= 1.0 - ring.DissolveProgress;
                int slot = ((ring.SpawnIndex % _ringColours.Length) + _ringColours.Length)
                           % _ringColours.Length;

                // A super ring keeps its own colour but not its own depth: it has to be
                // recognisable from far out, and it still has to sit in the tunnel.
                var ink = _ringColours[slot];

                // A ring that has used up its allowance burns. Mixed over its own ink rather
                // than replacing it, so the ring stays recognisable as the one it was — the
                // player has to see a change, not a different ring.
                if (ring.IsHot) ink = Color.Lerp(ink, _hotColour, HotMix);
                var colour = Color.Lerp(_fadeToward, ink, (float)alpha);
                colour.a = 1f;
                ring.ApplyShading(colour);
            }
        }

        /// <summary>
        /// The ring the ball is resting on: the innermost ring still outside it. "Outside" is
        /// the direction gravity pulls, because the tunnel centre is above the ball.
        /// </summary>
        /// <summary>The first ball's ring. Kept for the bot, the fixtures and the diagnostics.</summary>
        public RingController ResolveBallRing() => ResolveRingFor(_ball);

        /// <summary>
        /// The innermost ring that still encloses a given ball — the one it is flying inside.
        /// </summary>
        public RingController ResolveRingFor(BallController ball)
        {
            if (ball == null) return null;

            double ballDistance = ball.DistanceToCentre;
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
        public bool HasRingClosedOnCentre() => RingClosedOnCentre() != null;

        /// <summary>
        /// Bug guard from CONTRACT.md §1: no ball should ever get this far out.
        /// Judged per ball against its own ring, because two balls are rarely in the same place.
        /// </summary>
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

        /// <summary>
        /// The ring that has closed onto the red circle, or null.
        ///
        /// Returns WHICH ring rather than a yes-or-no because the crush rule asks how many
        /// balls it has inside it.
        /// </summary>
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

        /// <summary>How many balls are inside a given ring — what the crush rule asks for.</summary>
        public int BallsInside(RingController ring)
        {
            int n = 0;
            for (int b = 0; b < _ballCount; b++)
                if (ReferenceEquals(ResolveRingFor(_balls[b]), ring)) n++;
            return n;
        }
    }
}
