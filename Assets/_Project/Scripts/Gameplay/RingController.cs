using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
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

        public int SpawnIndex { get; set; }

        public double Radius => _radius;

        public double RadiusLag => System.Math.Abs(_radius - _builtRadius);
        public double GapCentreLocalDeg { get; private set; }
        public double GapArcWidth { get; private set; }
        public double AutoSpinDeg { get => _autoSpinDeg; set => _autoSpinDeg = value; }

        public int GapCount => _gapCount;

        public RingKind Kind { get; private set; }

        public double GapCentreWorldDeg => TunnelMath.NormalizeDeg(GapCentreLocalDeg + TotalRotationDeg);

        public double GapCentreWorldDegAt(int index) =>
            TunnelMath.NormalizeDeg(_openings[index].CentreDeg + TotalRotationDeg);

        public double TurnToNearestGapDeg(double towardsDeg)
        {
            if (_gapCount <= 0) return 180.0;

            double best = double.MaxValue;
            for (int i = 0; i < _gapCount; i++)
            {
                double toCentre = System.Math.Abs(TunnelMath.ShortestDeltaDeg(
                    GapCentreWorldDegAt(i), towardsDeg));

                double half = _gaps[i].WidthDeg * 0.5;
                double turn = System.Math.Max(0.0, toCentre - half);
                if (turn < best) best = turn;
            }

            return best;
        }

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

        public bool IsDissolving => _dissolveLeft > 0.0;

        public double DissolveProgress =>
            _dissolveSeconds <= 0.0 ? 1.0 : 1.0 - _dissolveLeft / _dissolveSeconds;

        public void SetSolid(bool solid)
        {
            if (IsDissolving) solid = false;
            if (_solid == solid) return;
            _solid = solid;
            SetCollidersEnabled(solid);
        }

        public void BeginDissolve(double seconds)
        {
            if (IsDissolving) return;

            _dissolveSeconds = seconds > 0.0 ? seconds : 0.0;
            _dissolveLeft = _dissolveSeconds;
            _solid = false;
            SetCollidersEnabled(false);
        }

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

        public void Initialise(GameConfig config, Material material, int id)
        {
            Id = id;
            _settings = config.ToSettings();
            _mode = config.compressionMode;
            _rebuildThreshold = config.rebuildThreshold;

            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.useFullKinematicContacts = true;

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

        public void Configure(double radius, double gapCentreLocalDeg, double gapArcWidth, double autoSpinDeg)
        {
            Kind = RingKind.Plain;
            Place(radius, RingGaps.One(gapCentreLocalDeg, gapArcWidth), autoSpinDeg);
        }

        public void Configure(double radius, in RingGaps gaps, double autoSpinDeg,
                              RingKind kind = RingKind.Plain)
        {
            Kind = kind;
            Place(radius, gaps, autoSpinDeg);
        }

        public void ConfigureSolid(double radius, double autoSpinDeg)
        {
            Kind = RingKind.Plain;
            Place(radius, new RingGaps { Count = 0 }, autoSpinDeg);
        }

        void Place(double radius, in RingGaps gaps, double autoSpinDeg)
        {
            _openings = gaps;
            IsHot = false;
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

            RebuildGeometry(radius);
            ApplyRotation(0.0);
        }

        public void SetRadius(double radius)
        {
            _radius = radius;

            switch (_mode)
            {
                case CompressionMode.TransformScale:

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

        public void StepRotation(double userAngleDeg, double dt)
        {
            _spinDeg += _autoSpinDeg * dt;
            ApplyRotation(userAngleDeg);
        }

        void ApplyRotation(double userAngleDeg)
        {
            TotalRotationDeg = _spinDeg + userAngleDeg;

            _body.MoveRotation((float)TotalRotationDeg);
        }

        void RebuildGeometry(double radius)
        {
            _builtRadius = radius;

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

                if (segment.Collider.enabled != _solid) segment.Collider.enabled = _solid;
            }

            for (int i = count; i < _activeSegments; i++)
                _segments[i].gameObject.SetActive(false);

            _activeSegments = count;
        }

        public int ActiveSegmentCount => _activeSegments;
        public RingSegment SegmentAt(int index) => _segments[index];

        public void InvalidateShading() => _shaded = false;

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

        public bool IsHot { get; private set; }

        public void SetHot(bool hot)
        {
            if (IsHot == hot) return;
            IsHot = hot;
            _shaded = false;
        }

        public Color HaloBackground { get; set; } = Color.black;

        public float HaloStrength { get; set; } = 0.35f;

        public void ApplyShading(Color colour)
        {
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
