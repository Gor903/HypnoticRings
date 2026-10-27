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
        TunnelSettings _settings;

        CompressionMode _mode;
        double _rebuildThreshold;

        int _activeSegments;
        int _gapCount;
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
        /// Radius the geometry was last built at. In threshold mode this trails Radius, and the
        /// difference is the mode's real error — the measurements that read built geometry
        /// cannot see it, so it is exposed rather than left implicit.
        /// </summary>
        public double BuiltRadius => _builtRadius;
        public double RadiusLag => System.Math.Abs(_radius - _builtRadius);
        public double GapCentreLocalDeg { get; private set; }
        public double GapArcWidth { get; private set; }
        public double AutoSpinDeg { get => _autoSpinDeg; set => _autoSpinDeg = value; }

        /// <summary>World-space angle of the first gap's centre, which is what the ball cares about.</summary>
        public double GapCentreWorldDeg => TunnelMath.NormalizeDeg(GapCentreLocalDeg + TotalRotationDeg);

        public double TotalRotationDeg { get; private set; }

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

            _gaps = new Gap[2];
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

        /// <summary>Places the ring. Gap width is an arc length, not an angle — see CONTRACT.md §2.2.</summary>
        public void Configure(double radius, double gapCentreLocalDeg, double gapArcWidth, double autoSpinDeg)
        {
            GapCentreLocalDeg = gapCentreLocalDeg;
            GapArcWidth = gapArcWidth;
            _autoSpinDeg = autoSpinDeg;
            _gapCount = 1;
            _radius = radius;
            _baseRadius = radius;
            _spinDeg = 0.0;
            _shaded = false;

            RebuildGeometry(radius);
            ApplyRotation(0.0);
        }

        /// <summary>
        /// A ring with no way out. Not a game state — it is what the tunnelling test needs, so
        /// that any escape at all is a failure rather than a judgement call.
        /// </summary>
        public void ConfigureSolid(double radius, double autoSpinDeg)
        {
            GapCentreLocalDeg = 0.0;
            GapArcWidth = 0.0;
            _autoSpinDeg = autoSpinDeg;
            _gapCount = 0;
            _radius = radius;
            _baseRadius = radius;
            _spinDeg = 0.0;
            _shaded = false;

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

            // The angular width follows from the arc length, so shrinking widens the angle and
            // the opening stays the same size in world units. ForOpening also pays for the
            // rounded caps, which bulge into the gap from both sides.
            _gaps[0] = Gap.ForOpening(GapCentreLocalDeg, GapArcWidth, _settings.Thickness, radius);

            int count = RingSegmentation.BuildSolidSegments(
                _gaps, _gapCount, _settings.MaxSegmentSweepDeg, _spans);

            for (int i = 0; i < count; i++)
            {
                var segment = _segments[i];
                if (!segment.gameObject.activeSelf) segment.gameObject.SetActive(true);
                segment.Rebuild(_spans[i], radius, _settings);
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
                var renderer = _segments[i].Renderer;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, colour);
                _block.SetColor(ColorId, colour);
                renderer.SetPropertyBlock(_block);
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
