using System;

namespace HR.Core
{
    /// <summary>A point in the tunnel plane. Float because meshes and colliders are float.</summary>
    public readonly struct Point2
    {
        public readonly float X;
        public readonly float Y;
        public Point2(float x, float y) { X = x; Y = y; }
        public override string ToString() => $"({X:F4}, {Y:F4})";
    }

    /// <summary>
    /// Tessellation of a ring segment — the band between two radii over an angular sweep, with
    /// rounded caps. Engine-free so the geometry can be checked without standing up a scene,
    /// and buffer-filling rather than allocating so a shrinking tunnel can refill in place.
    ///
    /// Caller sizes its arrays with the Count helpers and reuses them.
    /// </summary>
    public static class ArcGeometry
    {
        const double DegToRad = Math.PI / 180.0;

        /// <summary>Tessellation steps for a sweep, honouring a maximum angular step.</summary>
        public static int StepsFor(double sweepDeg, double maxStepDeg)
        {
            if (maxStepDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxStepDeg));
            double sweep = Math.Abs(sweepDeg);
            if (sweep <= 0.0) return 0;
            return Math.Max(1, (int)Math.Ceiling(sweep / maxStepDeg));
        }

        public static int BandVertexCount(int steps) => 2 * (steps + 1);
        public static int BandIndexCount(int steps) => 6 * steps;
        public static int OutlinePointCount(int steps, int capPoints) => 2 * (steps + 1) + 2 * capPoints;

        /// <summary>
        /// Fills the triangle band. Vertices alternate inner, outer per tessellation step, which
        /// keeps the index pattern fixed and lets the mesh be refilled without reallocating.
        /// </summary>
        public static void FillBand(double midRadius, double thickness, double startDeg, double sweepDeg,
                                    int steps, Point2[] vertices, int[] triangles)
        {
            if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps));
            Require(vertices, BandVertexCount(steps), nameof(vertices));
            Require(triangles, BandIndexCount(steps), nameof(triangles));

            double inner = midRadius - thickness * 0.5;
            double outer = midRadius + thickness * 0.5;
            if (inner <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(thickness), "thickness swallows the radius");

            for (int i = 0; i <= steps; i++)
            {
                double angle = (startDeg + sweepDeg * i / steps) * DegToRad;
                double cos = Math.Cos(angle), sin = Math.Sin(angle);
                vertices[2 * i] = new Point2((float)(inner * cos), (float)(inner * sin));
                vertices[2 * i + 1] = new Point2((float)(outer * cos), (float)(outer * sin));
            }

            for (int i = 0, t = 0; i < steps; i++, t += 6)
            {
                int b = 2 * i;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 3;
                triangles[t + 3] = b; triangles[t + 4] = b + 3; triangles[t + 5] = b + 2;
            }
        }

        /// <summary>
        /// Fills the closed outline used as a collider path, counter-clockwise: outer edge
        /// forward, end cap, inner edge back, start cap.
        ///
        /// The caps are not decoration. A square end is a corner the ball can catch on, and the
        /// spec requires the physical ends to be rounded at T/2 whatever the theme draws.
        /// </summary>
        public static void FillOutline(double midRadius, double thickness, double startDeg, double sweepDeg,
                                       int steps, int capPoints, Point2[] outline)
        {
            if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps));
            if (capPoints < 0) throw new ArgumentOutOfRangeException(nameof(capPoints));
            Require(outline, OutlinePointCount(steps, capPoints), nameof(outline));

            double half = thickness * 0.5;
            double inner = midRadius - half;
            if (inner <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(thickness), "thickness swallows the radius");

            double outerR = midRadius + half;
            int w = 0;

            for (int i = 0; i <= steps; i++)
            {
                double a = (startDeg + sweepDeg * i / steps) * DegToRad;
                outline[w++] = new Point2((float)(outerR * Math.Cos(a)), (float)(outerR * Math.Sin(a)));
            }

            WriteCap(outline, ref w, midRadius, half, startDeg + sweepDeg, capPoints, outward: true);

            for (int i = steps; i >= 0; i--)
            {
                double a = (startDeg + sweepDeg * i / steps) * DegToRad;
                outline[w++] = new Point2((float)(inner * Math.Cos(a)), (float)(inner * Math.Sin(a)));
            }

            WriteCap(outline, ref w, midRadius, half, startDeg, capPoints, outward: false);
        }

        /// <summary>
        /// Half-circle of radius <paramref name="half"/> centred on the mid-line at
        /// <paramref name="atDeg"/>. Only the interior points are written — the two endpoints
        /// already sit on the edges either side of it.
        /// </summary>
        static void WriteCap(Point2[] outline, ref int w, double midRadius, double half,
                             double atDeg, int capPoints, bool outward)
        {
            if (capPoints == 0) return;

            double a = atDeg * DegToRad;
            double cos = Math.Cos(a), sin = Math.Sin(a);
            double cx = midRadius * cos, cy = midRadius * sin;
            double tx = -sin, ty = cos;
            double sign = outward ? 1.0 : -1.0;

            for (int i = 1; i <= capPoints; i++)
            {
                double phi = Math.PI * i / (capPoints + 1);
                double r = Math.Cos(phi) * sign * half;
                double t = Math.Sin(phi) * sign * half;
                outline[w++] = new Point2((float)(cx + r * cos + t * tx), (float)(cy + r * sin + t * ty));
            }
        }

        static void Require<T>(T[] buffer, int needed, string name)
        {
            if (buffer == null) throw new ArgumentNullException(name);
            if (buffer.Length < needed)
                throw new ArgumentException($"{name} holds {buffer.Length}, needs {needed}", name);
        }
    }
}
