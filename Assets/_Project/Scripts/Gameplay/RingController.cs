using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// One ring: its radius, its gaps, its rotation, its colliders. Does not know about colour,
    /// sound, or the finger — CONTRACT.md §6 keeps those elsewhere.
    ///
    /// Rotation is carried by the transform, so spinning never rebuilds geometry. Only a change
    /// of radius does, and how often that happens is the compression mode's business.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RingController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        const float ColourEpsilon = 1f / 255f;

        Rigidbody2D _body;
        MaterialPropertyBlock _block;
        Color _lastColour;
        bool _shaded;
        RingSegment[] _segments;
        ArcSpan[] _spans;
        Gap[] _gaps;
        RingGaps _openings;
        TunnelSettings _settings;

        CompressionMode _mode;
        double _rebuildThreshold;

        int _activeSegments;
        int _gapCount;
        bool _solid;
        double _dissolveLeft;
        double _dissolveSeconds;
        double _radius;
        double _builtRadius;
        double _baseRadius;
        double _autoSpinDeg;
        double _spinDeg;

        public int Id { get; private set; }

        /// <summary>
        /// Which ring this is in the run. Reassigned every time the pool hands it out, so it
        /// identifies the ring the ball just cleared rather than the reused GameObject.
        /// </summary>
        public int SpawnIndex { get; set; }

        public double Radius => _radius;

        /// <summary>
        /// How far the built geometry trails the real radius. In threshold mode that lag is the
        /// mode's actual error, and the measurements that read built geometry cannot see it, so
        /// it is exposed rather than left implicit. The benchmark reports the worst of it.
        /// </summary>
        public double RadiusLag => System.Math.Abs(_radius - _builtRadius);
        public double GapCentreLocalDeg { get; private set; }
        public double GapArcWidth { get; private set; }
        public double AutoSpinDeg { get => _autoSpinDeg; set => _autoSpinDeg = value; }

        /// <summary>How many ways out this ring has.</summary>
        public int GapCount => _gapCount;

        /// <summary>What the generator made this ring for. Colour and the wave both read it.</summary>
        public RingKind Kind { get; private set; }

        /// <summary>Which opening carries the bead, or -1. Set by the tunnel, not the generator.</summary>
        public int BeadGap { get; private set; } = -1;

        /// <summary>True while a bead is sitting in this ring waiting to be taken.</summary>
        public bool HasBead => BeadGap >= 0 && _bead != null && _bead.activeSelf;

        GameObject _bead;
        Transform _beadTransform;

        /// <summary>
        /// Puts a bead in one of this ring's openings, or takes it away.
        ///
        /// The object is built once with the ring and then only shown and hidden: a bead is
        /// created every few rings for a whole run, and creating one each time is the kind of
        /// per-ring allocation an endless game turns into a leak.
        /// </summary>
        public void SetBead(int gapIndex, Material material, float radius)
        {
            BeadGap = gapIndex;

            if (gapIndex < 0)
            {
                if (_bead != null) _bead.SetActive(false);
                return;
            }

            if (_bead == null)
            {
                _bead = new GameObject("Bead", typeof(MeshFilter), typeof(MeshRenderer));
                _bead.transform.SetParent(transform, false);
                _bead.GetComponent<MeshFilter>().sharedMesh = TunnelFactory.BuildDiscMesh(radius, 12);
                _bead.GetComponent<MeshRenderer>().sharedMaterial = material;
                _beadTransform = _bead.transform;
            }

            _bead.SetActive(true);
            PlaceBead();
        }

        /// <summary>Taken. The ring keeps the slot so nothing tries to hand out a second one.</summary>
        public void TakeBead()
        {
            if (_bead != null) _bead.SetActive(false);
            BeadGap = -1;
        }

        /// <summary>
        /// Keeps the bead in its opening as the ring shrinks.
        ///
        /// A child of the ring, so rotation is free — the bead turns with the finger like the
        /// opening it sits in. Only the radius has to be followed.
        /// </summary>
        void PlaceBead()
        {
            if (_beadTransform == null || BeadGap < 0 || BeadGap >= _gapCount) return;

            double rad = _openings[BeadGap].CentreDeg / TunnelMath.DegPerRad;
            _beadTransform.localPosition = new Vector3(
                (float)(System.Math.Cos(rad) * _radius), (float)(System.Math.Sin(rad) * _radius), 0f);
        }

        /// <summary>Where the bead is in the world, for the pick-up test.</summary>
        public Vector3 BeadWorldPosition =>
            _beadTransform != null ? _beadTransform.position : transform.position;

        /// <summary>World-space angle of the first gap's centre, which is what the ball cares about.</summary>
        public double GapCentreWorldDeg => TunnelMath.NormalizeDeg(GapCentreLocalDeg + TotalRotationDeg);

        /// <summary>World-space centre of a particular opening.</summary>
        public double GapCentreWorldDegAt(int index) =>
            TunnelMath.NormalizeDeg(_openings[index].CentreDeg + TotalRotationDeg);

        /// <summary>
        /// The opening nearest a given heading, which is the one a player would actually aim for.
        ///
        /// Without this the bot keeps steering at slot zero and a ring with three ways out plays
        /// harder than a ring with one — the measurement would say multi-gap rings are a penalty
        /// when for a human they are a gift.
        /// </summary>
        public double NearestGapWorldDeg(double towardsDeg)
        {
            if (_gapCount <= 0) return GapCentreWorldDeg;

            double best = GapCentreWorldDegAt(0);
            double bestDistance = System.Math.Abs(TunnelMath.ShortestDeltaDeg(best, towardsDeg));

            for (int i = 1; i < _gapCount; i++)
            {
                double candidate = GapCentreWorldDegAt(i);
                double distance = System.Math.Abs(TunnelMath.ShortestDeltaDeg(candidate, towardsDeg));
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }

            return best;
        }

        public double TotalRotationDeg { get; private set; }

        /// <summary>True from the moment the ball leaves this ring until it goes back to the pool.</summary>
        public bool IsDissolving => _dissolveLeft > 0.0;

        /// <summary>0 at the start of the dissolve, 1 when there is nothing left to draw.</summary>
        public double DissolveProgress =>
            _dissolveSeconds <= 0.0 ? 1.0 : 1.0 - _dissolveLeft / _dissolveSeconds;

        /// <summary>
        /// Whether this ring is a wall right now. Rings the ball cannot reach this step do not
        /// need to be: D-059 put ten of them in the frame and halved the corridor, and Box2D was
        /// then testing the ball against thirty polygon colliders it could not possibly touch.
        ///
        /// Cached rather than written through every step — flipping a collider that is already in
        /// the right state is a native call per segment per ring per step, which is the cost this
        /// is here to avoid. A dissolving ring never comes back: it has already been left.
        /// </summary>
        public void SetSolid(bool solid)
        {
            if (IsDissolving) solid = false;
            if (_solid == solid) return;
            _solid = solid;
            SetCollidersEnabled(solid);
        }

        /// <summary>
        /// The ball is out: stop being a wall at once, then fade.
        ///
        /// The colliders go first and the picture follows, because the edge of the opening the
        /// ball has just cleared is still right next to it — left solid for even the length of
        /// the fade, it would bat the ball back inside a ring it has already scored.
        /// </summary>
        public void BeginDissolve(double seconds)
        {
            if (IsDissolving) return;

            _dissolveSeconds = seconds > 0.0 ? seconds : 0.0;
            _dissolveLeft = _dissolveSeconds;
            _solid = false;
            SetCollidersEnabled(false);
        }

        /// <summary>Advances the fade. True once there is nothing left and the ring can be retired.</summary>
        public bool AdvanceDissolve(double dt)
        {
            if (!IsDissolving) return false;
            _dissolveLeft -= dt;
            return _dissolveLeft <= 0.0;
        }

        void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < _activeSegments; i++)
                _segments[i].Collider.enabled = enabled;
        }

        /// <summary>Builds the segment children. Call once; Configure after it, as often as you like.</summary>
        public void Initialise(GameConfig config, Material material, int id)
        {
            Id = id;
            _settings = config.ToSettings();
            _mode = config.compressionMode;
            _rebuildThreshold = config.rebuildThreshold;

            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.useFullKinematicContacts = true;

            // Up to three, and every ring carries the segments for three whether it uses them
            // or not: the spare ones sit deactivated, which costs a GameObject and no frame time,
            // whereas growing the array later would mean building geometry mid-run.
            _gaps = new Gap[RingGaps.MaxGaps];
            _openings = new RingGaps();
            int maxSegments = RingSegmentation.UpperBoundSegments(_gaps.Length, _settings.MaxSegmentSweepDeg);
            _spans = new ArcSpan[maxSegments];
            _segments = new RingSegment[maxSegments];

            for (int i = 0; i < maxSegments; i++)
            {
                var go = new GameObject($"Segment{i}", typeof(MeshFilter), typeof(MeshRenderer),
                                        typeof(PolygonCollider2D), typeof(RingSegment));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

                var segment = go.GetComponent<RingSegment>();
                segment.Initialise(_settings);
                _segments[i] = segment;
                go.SetActive(false);
            }
        }

        /// <summary>Places the ring with one opening — the shape most rings have.</summary>
        public void Configure(double radius, double gapCentreLocalDeg, double gapArcWidth, double autoSpinDeg)
        {
            Kind = RingKind.Plain;
            Place(radius, RingGaps.One(gapCentreLocalDeg, gapArcWidth), autoSpinDeg);
        }

        /// <summary>Places the ring with however many openings the generator decided on.</summary>
        public void Configure(double radius, in RingGaps gaps, double autoSpinDeg,
                              RingKind kind = RingKind.Plain)
        {
            Kind = kind;
            Place(radius, gaps, autoSpinDeg);
        }

        /// <summary>
        /// A ring with no way out. Not a game state — it is what the tunnelling test needs, so
        /// that any escape at all is a failure rather than a judgement call.
        /// </summary>
        public void ConfigureSolid(double radius, double autoSpinDeg)
        {
            Kind = RingKind.Plain;
            Place(radius, new RingGaps { Count = 0 }, autoSpinDeg);
        }

        void Place(double radius, in RingGaps gaps, double autoSpinDeg)
        {
            _openings = gaps;
            if (_bead != null) _bead.SetActive(false);
            BeadGap = -1;
            GapCentreLocalDeg = gaps.Count > 0 ? gaps.A.CentreDeg : 0.0;
            GapArcWidth = gaps.Count > 0 ? gaps.A.Opening : 0.0;
            _autoSpinDeg = autoSpinDeg;
            _gapCount = gaps.Count;
            _radius = radius;
            _baseRadius = radius;
            _spinDeg = 0.0;
            _shaded = false;
            _dissolveLeft = 0.0;
            _solid = true;

            // The rebuild is what switches the colliders on, because it is what decides which
            // segments exist at this radius in the first place.
            RebuildGeometry(radius);
            ApplyRotation(0.0);
        }

        /// <summary>
        /// Moves the ring to a new radius using the configured strategy. This is the hot path:
        /// every ring does this every physics step for the whole game.
        /// </summary>
        public void SetRadius(double radius)
        {
            _radius = radius;

            switch (_mode)
            {
                case CompressionMode.TransformScale:
                    // Cheapest possible, and geometrically wrong: scaling the transform scales
                    // thickness and gap arc length with it. Measured, not assumed — see the
                    // benchmark and DECISIONS D-011.
                    float scale = (float)(radius / _baseRadius);
                    transform.localScale = new Vector3(scale, scale, 1f);
                    break;

                case CompressionMode.RebuildEveryStep:
                    RebuildGeometry(radius);
                    break;

                case CompressionMode.RebuildOnThreshold:
                    if (System.Math.Abs(radius - _builtRadius) >= _rebuildThreshold)
                        RebuildGeometry(radius);
                    break;
            }

            // Every step, not only on a rebuild: the bead has to stay in its opening while the
            // ring closes in, and the rebuild threshold is coarse enough to be visible.
            if (BeadGap >= 0) PlaceBead();
        }

        /// <summary>Advances autonomous spin and applies the shared user angle.</summary>
        public void StepRotation(double userAngleDeg, double dt)
        {
            _spinDeg += _autoSpinDeg * dt;
            ApplyRotation(userAngleDeg);
        }

        void ApplyRotation(double userAngleDeg)
        {
            TotalRotationDeg = _spinDeg + userAngleDeg;
            // MoveRotation rather than setting the transform: a kinematic body moved through the
            // physics API sweeps against colliders instead of teleporting past them.
            _body.MoveRotation((float)TotalRotationDeg);
        }

        void RebuildGeometry(double radius)
        {
            _builtRadius = radius;

            // The angle is held, not the arc: a shrinking ring keeps the opening it was drawn
            // with instead of tearing wider as it closes in. The width also pays for the rounded
            // caps, which bulge into the gap from both sides.
            for (int i = 0; i < _gapCount; i++)
                _gaps[i] = new Gap(_openings[i].CentreDeg, TunnelMath.HeldGapWidthDeg(
                    _openings[i].Opening, _settings.Thickness, _settings.TargetRadius,
                    radius, _settings.MinGapWidth));

            int count = RingSegmentation.BuildSolidSegments(
                _gaps, _gapCount, _settings.MaxSegmentSweepDeg, _spans);

            for (int i = 0; i < count; i++)
            {
                var segment = _segments[i];
                if (!segment.gameObject.activeSelf) segment.gameObject.SetActive(true);
                segment.Rebuild(_spans[i], radius, _settings);

                // A segment switched on by a rebuild arrives with its collider enabled, which
                // would hand a ring back its walls behind SetSolid's back.
                if (segment.Collider.enabled != _solid) segment.Collider.enabled = _solid;
            }

            for (int i = count; i < _activeSegments; i++)
                _segments[i].gameObject.SetActive(false);

            _activeSegments = count;
        }

        public int ActiveSegmentCount => _activeSegments;
        public RingSegment SegmentAt(int index) => _segments[index];

        /// <summary>
        /// Depth cue. Written through a MaterialPropertyBlock rather than by touching the
        /// material: every ring shares one material, and assigning to renderer.material would
        /// clone it per ring and leak a material for every ring the pool ever hands out.
        /// </summary>
        /// <summary>Forgets the cached colour, so the next shading pass actually writes.</summary>
        public void InvalidateShading() => _shaded = false;

        /// <summary>
        /// Sets the halo width on every segment and rebuilds, which is the part that is easy to
        /// forget: the width is read while geometry is being filled, so setting it afterwards
        /// leaves a ring that has a halo in its fields and none on the screen.
        /// </summary>
        public void SetHaloWidth(float width)
        {
            bool changed = false;
            for (int i = 0; i < _activeSegments; i++)
            {
                if (Mathf.Approximately(_segments[i].HaloWidth, width)) continue;
                _segments[i].HaloWidth = width;
                changed = true;
            }

            if (changed) RebuildGeometry(_radius);
        }

        /// <summary>What the halo is mixed toward — the theme's background.</summary>
        public Color HaloBackground { get; set; } = Color.black;

        /// <summary>How much of the ring's own ink the halo keeps, 0 to 1.</summary>
        public float HaloStrength { get; set; } = 0.35f;

        public void ApplyShading(Color colour)
        {
            // Rewriting a property block every step for a colour that has not visibly changed is
            // pure cost: alpha moves by a fraction of a 255th between steps, and the block write
            // was the single most expensive thing in the tunnel loop.
            if (_shaded && ColourIsIndistinguishable(colour, _lastColour)) return;
            _lastColour = colour;
            _shaded = true;

            _block ??= new MaterialPropertyBlock();
            for (int i = 0; i < _activeSegments; i++)
            {
                var segment = _segments[i];
                var renderer = segment.Renderer;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, colour);
                _block.SetColor(ColorId, colour);
                renderer.SetPropertyBlock(_block);

                // The halo takes the same ink, mixed back toward the background. Mixed rather
                // than faded: the shader is opaque and ignores alpha, so a "transparent" halo
                // would draw at full strength and swallow the ring it is supposed to surround.
                var halo = segment.HaloRenderer;
                if (halo == null || !halo.enabled) continue;

                halo.GetPropertyBlock(_block);
                var glow = Color.Lerp(HaloBackground, colour, HaloStrength);
                glow.a = 1f;
                _block.SetColor(BaseColorId, glow);
                _block.SetColor(ColorId, glow);
                halo.SetPropertyBlock(_block);
            }
        }

        static bool ColourIsIndistinguishable(in Color a, in Color b) =>
            Mathf.Abs(a.r - b.r) < ColourEpsilon && Mathf.Abs(a.g - b.g) < ColourEpsilon
            && Mathf.Abs(a.b - b.b) < ColourEpsilon && Mathf.Abs(a.a - b.a) < ColourEpsilon;

        /// <summary>
        /// Thickness measured from the collider that physics actually uses, in world space.
        /// The benchmark reads this rather than trusting what was asked for.
        /// </summary>
        public double MeasureWorldThickness()
        {
            if (_activeSegments == 0) return 0.0;

            var segment = _segments[0];
            var points = segment.Collider.GetPath(0);
            var matrix = segment.transform.localToWorldMatrix;

            double min = double.MaxValue, max = 0.0;
            foreach (var p in points)
            {
                Vector3 world = matrix.MultiplyPoint3x4(new Vector3(p.x, p.y, 0f));
                double r = Mathf.Sqrt(world.x * world.x + world.y * world.y);
                if (r < min) min = r;
                if (r > max) max = r;
            }
            return max - min;
        }

        /// <summary>
        /// The clear opening in world units — the number that decides whether the ball fits.
        /// Measured from the geometry that was actually built, including the cap allowance, so
        /// a mode that silently narrows the way out shows up here.
        /// </summary>
        public double MeasureWorldGapArcLength()
        {
            double solid = 0.0;
            for (int i = 0; i < _activeSegments; i++) solid += _segments[i].Span.SweepDeg;

            double scale = transform.localScale.x;
            double worldRadius = _builtRadius * scale;
            return RingSegmentation.OpeningFor(360.0 - solid, _settings.Thickness * scale, worldRadius);
        }
    }
}
