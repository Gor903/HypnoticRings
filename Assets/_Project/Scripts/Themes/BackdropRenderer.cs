using System.Collections.Generic;
using HR.Core;
using UnityEngine;

namespace HR.Themes
{
    public sealed class BackdropRenderer : MonoBehaviour
    {
        public const float Depth = 1.0f;

        const int MinSegments = 8;
        const int MaxSegments = 64;

        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _base = new List<Vector3>();
        readonly List<Color32> _colours = new List<Color32>();
        readonly List<int> _triangles = new List<int>();
        readonly List<Span> _spans = new List<Span>();

        Mesh _mesh;
        MeshRenderer _renderer;
        Camera _camera;

        BackdropElement[] _parts = System.Array.Empty<BackdropElement>();
        double _clock;
        bool _wanted = true;

        public int PartCount => _parts.Length;

        public bool Showing => _renderer != null && _renderer.enabled;

        readonly struct Span
        {
            public readonly int From;
            public readonly int Count;
            public readonly float BaseY;

            public Span(int from, int count, float baseY)
            {
                From = from;
                Count = count;
                BaseY = baseY;
            }
        }

        public void Initialise(Camera camera)
        {
            _camera = camera;

            _mesh = new Mesh { name = "HR.Backdrop" };
            _mesh.MarkDynamic();

            var filter = gameObject.GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            _renderer = gameObject.GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();

            _renderer.sharedMaterial = CreateMaterial();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                throw new System.InvalidOperationException("no blended shader for the backdrop");

            return new Material(shader) { name = "HR Backdrop" };
        }

        public void Show(Theme theme, bool effectsWanted)
        {
            _wanted = effectsWanted;
            _parts = Backdrop.For(theme);
            _clock = 0.0;

            Rebuild();
            Place();

            if (_renderer != null) _renderer.enabled = _wanted && _parts.Length > 0;
        }

        public void SetEffectsWanted(bool wanted)
        {
            _wanted = wanted;
            if (_renderer != null) _renderer.enabled = _wanted && _parts.Length > 0;
        }

        void Place()
        {
            if (_camera == null) return;

            var eye = _camera.transform.position;
            transform.position = new Vector3(eye.x, eye.y, Depth);
            transform.localScale = Vector3.one * (_camera.orthographicSize * 2f);
        }

        void Rebuild()
        {
            _vertices.Clear();
            _base.Clear();
            _colours.Clear();
            _triangles.Clear();
            _spans.Clear();

            foreach (var part in _parts)
            {
                int from = _vertices.Count;
                Emit(part);
                _spans.Add(new Span(from, _vertices.Count - from, (float)part.Y));
            }

            _base.AddRange(_vertices);

            _mesh.Clear();
            if (_vertices.Count == 0) return;

            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colours);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
        }

        void Update()
        {
            if (_renderer == null || !_renderer.enabled || _parts.Length == 0) return;

            _clock += Time.unscaledDeltaTime;

            bool moved = false;
            bool faded = false;

            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                var span = _spans[i];

                if (part.Motion == BackdropMotion.Rise)
                {
                    float shift = Wrapped(span.BaseY + (float)(part.Speed * _clock)) - span.BaseY;
                    for (int v = span.From; v < span.From + span.Count; v++)
                    {
                        var point = _base[v];
                        point.y += shift;
                        _vertices[v] = point;
                    }

                    moved = true;
                }

                if (part.Motion == BackdropMotion.Twinkle)
                {
                    double phase = _clock / part.Period * System.Math.PI * 2.0;
                    double alpha = part.Alpha + part.Swing * System.Math.Sin(phase);
                    byte a = (byte)(Mathf.Clamp01((float)alpha) * 255f);

                    for (int v = span.From; v < span.From + span.Count; v++)
                    {
                        var colour = _colours[v];
                        colour.a = a;
                        _colours[v] = colour;
                    }

                    faded = true;
                }
            }

            if (moved) _mesh.SetVertices(_vertices);
            if (faded) _mesh.SetColors(_colours);
            if (moved) _mesh.RecalculateBounds();
        }

        static float Wrapped(float y)
        {
            const float span = 1.12f;
            const float low = -0.56f;

            float shifted = y - low;
            shifted -= Mathf.Floor(shifted / span) * span;
            return shifted + low;
        }

        void Emit(in BackdropElement part)
        {
            var tint = new Color32(part.Colour.R, part.Colour.G, part.Colour.B,
                                   (byte)(Mathf.Clamp01((float)part.Alpha) * 255f));

            switch (part.Shape)
            {
                case BackdropShape.Disc:
                    Fan(part.X, part.Y, part.Size, 0.0, 360.0, tint);
                    break;

                case BackdropShape.Wedge:
                    Fan(part.X, part.Y, part.Size, part.StartDegrees, part.SweepDegrees, tint);
                    break;

                case BackdropShape.Ring:
                    Band(part.X, part.Y, part.Size, part.Thickness, 0.0, 360.0, tint);
                    break;

                case BackdropShape.Arc:
                    Band(part.X, part.Y, part.Size, part.Thickness,
                         part.StartDegrees, part.SweepDegrees, tint);
                    break;

                case BackdropShape.Rect:
                    Quad(part.X, part.Y, part.Size, part.Thickness, tint);
                    break;

                case BackdropShape.Triangle:
                    Corner(part.X, part.Y, part.Size, tint);
                    break;
            }
        }

        int Segments(double radius, double sweep)
        {
            int whole = Mathf.Clamp(Mathf.CeilToInt((float)radius * 220f), MinSegments, MaxSegments);
            return Mathf.Max(3, Mathf.CeilToInt(whole * (float)(sweep / 360.0)));
        }

        void Fan(double cx, double cy, double radius, double start, double sweep, Color32 tint)
        {
            int steps = Segments(radius, sweep);
            int centre = _vertices.Count;

            _vertices.Add(new Vector3((float)cx, (float)cy, 0f));
            _colours.Add(tint);

            for (int i = 0; i <= steps; i++)
            {
                double angle = (start + sweep * i / steps) * Mathf.Deg2Rad;
                _vertices.Add(new Vector3((float)(cx + System.Math.Cos(angle) * radius),
                                          (float)(cy + System.Math.Sin(angle) * radius), 0f));
                _colours.Add(tint);
            }

            for (int i = 0; i < steps; i++)
            {
                _triangles.Add(centre);
                _triangles.Add(centre + 1 + i);
                _triangles.Add(centre + 2 + i);
            }
        }

        void Band(double cx, double cy, double radius, double thickness,
                  double start, double sweep, Color32 tint)
        {
            int steps = Segments(radius, sweep);
            double inner = System.Math.Max(0.0, radius - thickness * 0.5);
            double outer = radius + thickness * 0.5;
            int first = _vertices.Count;

            for (int i = 0; i <= steps; i++)
            {
                double angle = (start + sweep * i / steps) * Mathf.Deg2Rad;
                double cos = System.Math.Cos(angle);
                double sin = System.Math.Sin(angle);

                _vertices.Add(new Vector3((float)(cx + cos * inner), (float)(cy + sin * inner), 0f));
                _colours.Add(tint);
                _vertices.Add(new Vector3((float)(cx + cos * outer), (float)(cy + sin * outer), 0f));
                _colours.Add(tint);
            }

            for (int i = 0; i < steps; i++)
            {
                int a = first + i * 2;
                _triangles.Add(a);
                _triangles.Add(a + 1);
                _triangles.Add(a + 2);
                _triangles.Add(a + 1);
                _triangles.Add(a + 3);
                _triangles.Add(a + 2);
            }
        }

        void Quad(double cx, double cy, double halfWidth, double halfHeight, Color32 tint)
        {
            int first = _vertices.Count;
            float x0 = (float)(cx - halfWidth), x1 = (float)(cx + halfWidth);
            float y0 = (float)(cy - halfHeight), y1 = (float)(cy + halfHeight);

            _vertices.Add(new Vector3(x0, y0, 0f));
            _vertices.Add(new Vector3(x0, y1, 0f));
            _vertices.Add(new Vector3(x1, y1, 0f));
            _vertices.Add(new Vector3(x1, y0, 0f));
            for (int i = 0; i < 4; i++) _colours.Add(tint);

            _triangles.Add(first);
            _triangles.Add(first + 1);
            _triangles.Add(first + 2);
            _triangles.Add(first);
            _triangles.Add(first + 2);
            _triangles.Add(first + 3);
        }

        void Corner(double cx, double cy, double size, Color32 tint)
        {
            int first = _vertices.Count;

            _vertices.Add(new Vector3((float)cx, (float)cy, 0f));
            _vertices.Add(new Vector3((float)(cx + size), (float)cy, 0f));
            _vertices.Add(new Vector3((float)cx, (float)(cy + size), 0f));
            for (int i = 0; i < 3; i++) _colours.Add(tint);

            _triangles.Add(first);
            _triangles.Add(first + 2);
            _triangles.Add(first + 1);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_renderer != null && _renderer.sharedMaterial != null)
                Destroy(_renderer.sharedMaterial);
        }
    }
}
