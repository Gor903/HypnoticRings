using System.Collections.Generic;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// One solid arc of a ring: its mesh and its collider. Knows geometry and nothing else —
    /// colour belongs to ThemeApplier, rotation to RingController (CONTRACT.md §6).
    ///
    /// Buffers are sized once for the worst case and refilled in place, so a tunnel that shrinks
    /// every physics step produces no garbage.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
    public sealed class RingSegment : MonoBehaviour
    {
        MeshFilter _filter;
        MeshRenderer _renderer;
        PolygonCollider2D _collider;
        Mesh _mesh;

        Point2[] _bandVertices;
        int[] _bandTriangles;
        Point2[] _outline;

        readonly List<Vector3> _meshVertices = new List<Vector3>();
        readonly List<int> _meshTriangles = new List<int>();
        readonly List<Vector2> _path = new List<Vector2>();

        // Fixed for the life of the segment: a constant tessellation keeps every buffer the same
        // size whatever the sweep, which is what makes refilling allocation-free. A narrow
        // segment simply gets a finer step than asked for, which is never worse.
        int _steps;
        int _capPoints;
        bool _ready;

        public ArcSpan Span { get; private set; }
        public double Radius { get; private set; }

        public void Initialise(in TunnelSettings settings)
        {
            if (_ready) return;

            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<PolygonCollider2D>();

            _steps = ArcGeometry.StepsFor(settings.MaxSegmentSweepDeg, settings.MaxArcStepDeg);
            _capPoints = settings.CapPoints;

            _bandVertices = new Point2[ArcGeometry.BandVertexCount(_steps)];
            _bandTriangles = new int[ArcGeometry.BandIndexCount(_steps)];
            _outline = new Point2[ArcGeometry.OutlinePointCount(_steps, _capPoints)];

            _meshVertices.Capacity = _bandVertices.Length;
            _meshTriangles.Capacity = _bandTriangles.Length;
            _path.Capacity = _outline.Length;

            _mesh = new Mesh { name = "RingSegment" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;

            _collider.pathCount = 1;
            _collider.useDelaunayMesh = false;
            _ready = true;
        }

        /// <summary>Rebuilds mesh and collider for a span at a radius. Allocation-free after the first call.</summary>
        public void Rebuild(ArcSpan span, double radius, in TunnelSettings settings)
        {
            if (!_ready) Initialise(settings);

            Span = span;
            Radius = radius;

            ArcGeometry.FillBand(radius, settings.Thickness, span.StartDeg, span.SweepDeg,
                                 _steps, _bandVertices, _bandTriangles);
            ArcGeometry.FillOutline(radius, settings.Thickness, span.StartDeg, span.SweepDeg,
                                    _steps, _capPoints, _outline);

            _meshVertices.Clear();
            for (int i = 0; i < _bandVertices.Length; i++)
                _meshVertices.Add(new Vector3(_bandVertices[i].X, _bandVertices[i].Y, 0f));

            if (_meshTriangles.Count != _bandTriangles.Length)
            {
                // Index order never changes once the step count is fixed, so this runs once.
                _meshTriangles.Clear();
                for (int i = 0; i < _bandTriangles.Length; i++) _meshTriangles.Add(_bandTriangles[i]);
                _mesh.SetVertices(_meshVertices);
                _mesh.SetTriangles(_meshTriangles, 0, true);
            }
            else
            {
                _mesh.SetVertices(_meshVertices);
                _mesh.RecalculateBounds();
            }

            _path.Clear();
            for (int i = 0; i < _outline.Length; i++)
                _path.Add(new Vector2(_outline[i].X, _outline[i].Y));
            _collider.SetPath(0, _path);
        }

        /// <summary>Used by the TransformScale benchmark arm, which never rebuilds geometry.</summary>
        public void SetUniformScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public Mesh Mesh => _mesh;
        public MeshRenderer Renderer => _renderer;
        public PolygonCollider2D Collider => _collider;

        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }
    }
}
