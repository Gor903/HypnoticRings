using System.Collections.Generic;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
    public sealed class RingSegment : MonoBehaviour
    {
        MeshFilter _filter;
        MeshRenderer _renderer;
        PolygonCollider2D _collider;
        Mesh _mesh;

        MeshFilter _haloFilter;
        MeshRenderer _haloRenderer;
        Mesh _haloMesh;
        Point2[] _haloVertices;
        int[] _haloTriangles;
        readonly List<Vector3> _haloMeshVertices = new List<Vector3>();
        readonly List<int> _haloMeshTriangles = new List<int>();

        public const float HaloDepth = 0.1f;

        Point2[] _bandVertices;
        int[] _bandTriangles;
        Point2[] _outline;

        readonly List<Vector3> _meshVertices = new List<Vector3>();
        readonly List<int> _meshTriangles = new List<int>();
        readonly List<Vector2> _path = new List<Vector2>();

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

            _bandVertices = new Point2[ArcGeometry.BandVertexCount(_steps, _capPoints)];
            _bandTriangles = new int[ArcGeometry.BandIndexCount(_steps, _capPoints)];
            _outline = new Point2[ArcGeometry.OutlinePointCount(_steps, _capPoints)];

            _meshVertices.Capacity = _bandVertices.Length;
            _meshTriangles.Capacity = _bandTriangles.Length;
            _path.Capacity = _outline.Length;

            _mesh = new Mesh { name = "RingSegment" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;

            _haloVertices = new Point2[_bandVertices.Length];
            _haloTriangles = new int[_bandTriangles.Length];
            _haloMeshVertices.Capacity = _haloVertices.Length;
            _haloMeshTriangles.Capacity = _haloTriangles.Length;

            _collider.pathCount = 1;
            _collider.useDelaunayMesh = false;
            _ready = true;
        }

        public float HaloWidth { get; set; }

        void EnsureHalo(in TunnelSettings settings)
        {
            if (_haloFilter != null) return;

            var go = new GameObject("Halo", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, HaloDepth);

            _haloFilter = go.GetComponent<MeshFilter>();
            _haloRenderer = go.GetComponent<MeshRenderer>();
            _haloRenderer.sharedMaterial = _renderer.sharedMaterial;
            _haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _haloRenderer.receiveShadows = false;

            _haloMesh = new Mesh { name = "RingHalo" };
            _haloMesh.MarkDynamic();
            _haloFilter.sharedMesh = _haloMesh;
        }

        public void Rebuild(ArcSpan span, double radius, in TunnelSettings settings)
        {
            if (!_ready) Initialise(settings);

            Span = span;
            Radius = radius;

            ArcGeometry.FillBand(radius, settings.Thickness, span.StartDeg, span.SweepDeg,
                                 _steps, _capPoints, _bandVertices, _bandTriangles);
            ArcGeometry.FillOutline(radius, settings.Thickness, span.StartDeg, span.SweepDeg,
                                    _steps, _capPoints, _outline);

            _meshVertices.Clear();
            for (int i = 0; i < _bandVertices.Length; i++)
                _meshVertices.Add(new Vector3(_bandVertices[i].X, _bandVertices[i].Y, 0f));

            if (_meshTriangles.Count != _bandTriangles.Length)
            {
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

            RebuildHalo(span, radius, settings);
        }

        void RebuildHalo(ArcSpan span, double radius, in TunnelSettings settings)
        {
            if (HaloWidth <= 0f)
            {
                if (_haloRenderer != null) _haloRenderer.enabled = false;
                return;
            }

            EnsureHalo(settings);
            _haloRenderer.enabled = true;

            ArcGeometry.FillBand(radius, settings.Thickness + HaloWidth * 2f,
                                 span.StartDeg, span.SweepDeg,
                                 _steps, _capPoints, _haloVertices, _haloTriangles);

            _haloMeshVertices.Clear();
            for (int i = 0; i < _haloVertices.Length; i++)
                _haloMeshVertices.Add(new Vector3(_haloVertices[i].X, _haloVertices[i].Y, 0f));

            if (_haloMeshTriangles.Count != _haloTriangles.Length)
            {
                _haloMeshTriangles.Clear();
                for (int i = 0; i < _haloTriangles.Length; i++) _haloMeshTriangles.Add(_haloTriangles[i]);
                _haloMesh.SetVertices(_haloMeshVertices);
                _haloMesh.SetTriangles(_haloMeshTriangles, 0, true);
            }
            else
            {
                _haloMesh.SetVertices(_haloMeshVertices);
                _haloMesh.RecalculateBounds();
            }
        }

        public MeshRenderer HaloRenderer => _haloRenderer;

        public void SetUniformScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public Mesh Mesh => _mesh;
        public MeshRenderer Renderer => _renderer;
        public PolygonCollider2D Collider => _collider;

        void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            }
            if (_haloMesh != null)
            {
                if (Application.isPlaying) Destroy(_haloMesh); else DestroyImmediate(_haloMesh);
            }
        }
    }
}
