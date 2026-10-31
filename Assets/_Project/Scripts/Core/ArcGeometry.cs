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

        /// <summary>Vertices a cap contributes to the mesh: its centre plus its rim.</summary>
        static int CapRun(int capPoints) => capPoints > 0 ? capPoints + 1 : 0;

        public static int BandVertexCount(int steps, int capPoints) => 2 * (steps + 1) + 2 * CapRun(capPoints);
        public static int BandIndexCount(int steps, int capPoints) => 6 * steps + 6 * CapRun(capPoints);
        public static int OutlinePointCount(int steps, int capPoints) => 2 * (steps + 1) + 2 * capPoints;

        /// <summary>
        /// Fills the triangle band and the two cap fans that round its ends. Band vertices
        /// alternate inner, outer per tessellation step, which keeps the index pattern fixed and
        /// lets the mesh be refilled without reallocating; the caps are appended after them.
        ///
        /// The caps use the same points as <see cref="FillOutline"/>, so what is drawn is the
        /// shape the ball actually hits — the spec's T/2 rounding, not a square end over a round
        /// collider.
        /// </summary>
        public static void FillBand(double midRadius, double thickness, double startDeg, double sweepDeg,
                                    int steps, int capPoints, Point2[] vertices, int[] triangles)
        {
            if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps));
            if (capPoints < 0) throw new ArgumentOutOfRangeException(nameof(capPoints));
            Require(vertices, BandVertexCount(steps, capPoints), nameof(vertices));
            Require(triangles, BandIndexCount(steps, capPoints), nameof(triangles));

            double half = thickness * 0.5;
            double inner = midRadius - half;
            double outer = midRadius + half;
            if (inner <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(thickness), "thickness swallows the radius");

            for (int i = 0; i <= steps; i++)
            {
                double angle = (startDeg + sweepDeg * i / steps) * DegToRad;
                double cos = Math.Cos(angle), sin = Math.Sin(angle);
                vertices[2 * i] = new Point2((float)(inner * cos), (float)(inner * sin));
                vertices[2 * i + 1] = new Point2((float)(outer * cos), (float)(outer * sin));
            }

            // Wound so the face points at a camera looking along +Z, which is where the camera
            // is. The opposite order is back-face culled: the mesh is built, has the right
            // bounds, reports itself visible, and draws absolutely nothing — with no error to
            // say so. Signed area of each triangle is negative; see the winding test.
            for (int i = 0, t = 0; i < steps; i++, t += 6)
            {
                int b = 2 * i;
                triangles[t] = b; triangles[t + 1] = b + 3; triangles[t + 2] = b + 1;
                triangles[t + 3] = b; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
            }

            if (capPoints == 0) return;

            int vertex = 2 * (steps + 1), index = 6 * steps;
            // Each fan hangs off the two band vertices already sitting on that end, so the cap
            // joins the band seamlessly instead of being a separate sliver with a hairline in it.
            FillCapFan(vertices, triangles, ref vertex, ref index, midRadius, half,
                       startDeg + sweepDeg, capPoints, outward: true,
                       fromEdge: 2 * steps + 1, toEdge: 2 * steps);
            FillCapFan(vertices, triangles, ref vertex, ref index, midRadius, half,
                       startDeg, capPoints, outward: false, fromEdge: 0, toEdge: 1);
        }

        /// <summary>
        /// Writes one cap as a triangle fan around the mid-line point at <paramref name="atDeg"/>,
        /// running from the band vertex <paramref name="fromEdge"/> round to <paramref name="toEdge"/>.
        /// </summary>
        static void FillCapFan(Point2[] vertices, int[] triangles, ref int vertex, ref int index,
                               double midRadius, double half, double atDeg, int capPoints,
                               bool outward, int fromEdge, int toEdge)
        {
            double a = atDeg * DegToRad;
            int centre = vertex;
            vertices[vertex++] = new Point2((float)(midRadius * Math.Cos(a)), (float)(midRadius * Math.Sin(a)));

            int rim = vertex;
            for (int i = 1; i <= capPoints; i++)
                vertices[vertex++] = CapPoint(midRadius, half, atDeg, capPoints, i, outward);

            // The rim runs counter-clockwise around the centre, so each triangle is wound the
            // other way — the same negative signed area the band uses to face the camera.
            for (int i = 0; i <= capPoints; i++)
            {
                int from = i == 0 ? fromEdge : rim + i - 1;
                int to = i == capPoints ? toEdge : rim + i;
                triangles[index++] = centre;
                triangles[index++] = to;
                triangles[index++] = from;
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
            for (int i = 1; i <= capPoints; i++)
                outline[w++] = CapPoint(midRadius, half, atDeg, capPoints, i, outward);
        }

        /// <summary>
        /// The i-th interior point of a cap, 1..capPoints, walking the half-circle from the edge
        /// the cap leaves to the edge it rejoins. Mesh and collider both read their caps from
        /// here, which is what keeps the drawn end and the physical end the same shape.
        /// </summary>
        static Point2 CapPoint(double midRadius, double half, double atDeg, int capPoints,
                               int i, bool outward)
        {
            double a = atDeg * DegToRad;
            double cos = Math.Cos(a), sin = Math.Sin(a);
            double sign = outward ? 1.0 : -1.0;

            double phi = Math.PI * i / (capPoints + 1);
            double r = Math.Cos(phi) * sign * half;
            double t = Math.Sin(phi) * sign * half;

            // Radial basis (cos, sin), tangential basis (-sin, cos).
            return new Point2((float)(midRadius * cos + r * cos - t * sin),
                              (float)(midRadius * sin + r * sin + t * cos));
        }

        static void Require<T>(T[] buffer, int needed, string name)
        {
            if (buffer == null) throw new ArgumentNullException(name);
            if (buffer.Length < needed)
                throw new ArgumentException($"{name} holds {buffer.Length}, needs {needed}", name);
        }
    }
}
