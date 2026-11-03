using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class ArcGeometryTests
    {
        const double Mid = 3.0;
        const double T = 0.30;
        const double MaxStep = 7.5;
        const int Caps = 4;

        static double Radius(Point2 p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

        static double SignedArea(Point2[] poly, int count)
        {
            double sum = 0.0;
            for (int i = 0; i < count; i++)
            {
                Point2 a = poly[i], b = poly[(i + 1) % count];
                sum += a.X * b.Y - b.X * a.Y;
            }
            return sum * 0.5;
        }

        [TestCase(120.0, 7.5, 16)]
        [TestCase(100.0, 7.5, 14)]
        [TestCase(7.5, 7.5, 1)]
        [TestCase(1.0, 7.5, 1)]
        public void StepsFor_never_exceeds_the_requested_step(double sweep, double max, int expected)
        {
            int steps = ArcGeometry.StepsFor(sweep, max);
            Assert.AreEqual(expected, steps);
            Assert.LessOrEqual(sweep / steps, max + 1e-9, "a step came out coarser than asked");
        }

        [Test]
        public void StepsFor_rejects_a_nonsense_step()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ArcGeometry.StepsFor(90.0, 0.0));
        }

        [Test]
        public void Band_vertices_sit_exactly_on_the_two_radii()
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];
            ArcGeometry.FillBand(Mid, T, 0.0, 120.0, steps, Caps, verts, tris);

            for (int i = 0; i <= steps; i++)
            {
                Assert.AreEqual(Mid - T / 2, Radius(verts[2 * i]), 1e-5, "inner vertex off-radius");
                Assert.AreEqual(Mid + T / 2, Radius(verts[2 * i + 1]), 1e-5, "outer vertex off-radius");
            }
        }

        [Test]
        public void Band_triangles_all_face_the_camera()
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];
            ArcGeometry.FillBand(Mid, T, 0.0, 120.0, steps, Caps, verts, tris);

            for (int t = 0; t < tris.Length; t += 3)
            {
                Point2 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                Assert.Less(cross, 0.0, $"triangle at index {t} faces away from the camera");
            }
        }

        [Test]
        public void Band_refills_in_place_without_new_buffers()
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];

            ArcGeometry.FillBand(3.0, T, 0.0, 120.0, steps, Caps, verts, tris);
            ArcGeometry.FillBand(1.2, T, 45.0, 120.0, steps, Caps, verts, tris);

            Assert.AreEqual(1.2 - T / 2, Radius(verts[0]), 1e-5);
            Assert.AreEqual(1.2 + T / 2, Radius(verts[1]), 1e-5);
        }

        [Test]
        public void Band_refuses_a_thickness_that_swallows_the_radius()
        {
            int steps = 4;
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ArcGeometry.FillBand(0.1, T, 0.0, 90.0, steps, Caps, verts, tris));
        }

        [Test]
        public void Band_refuses_a_buffer_that_is_too_small()
        {
            var verts = new Point2[4];
            var tris = new int[6];
            Assert.Throws<ArgumentException>(
                () => ArcGeometry.FillBand(Mid, T, 0.0, 120.0, 8, Caps, verts, tris));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        public void Outline_is_closed_counter_clockwise_and_correctly_sized(int capPoints)
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            int count = ArcGeometry.OutlinePointCount(steps, capPoints);
            var outline = new Point2[count];
            ArcGeometry.FillOutline(Mid, T, 0.0, 120.0, steps, capPoints, outline);

            Assert.AreEqual(2 * (steps + 1) + 2 * capPoints, count);
            Assert.Greater(SignedArea(outline, count), 0.0, "collider path winds clockwise");
        }

        [Test]
        public void Outline_area_is_close_to_the_true_annular_sector()
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            int caps = 6;
            var outline = new Point2[ArcGeometry.OutlinePointCount(steps, caps)];
            ArcGeometry.FillOutline(Mid, T, 0.0, 120.0, steps, caps, outline);

            double sector = Math.PI * (Math.Pow(Mid + T / 2, 2) - Math.Pow(Mid - T / 2, 2)) * (120.0 / 360.0);
            double caps_area = Math.PI * Math.Pow(T / 2, 2);
            double measured = SignedArea(outline, outline.Length);

            Assert.AreEqual(sector + caps_area, measured, (sector + caps_area) * 0.01);
        }

        [Test]
        public void Rounded_caps_never_bulge_past_the_bands_own_radii()
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            int caps = 8;
            var outline = new Point2[ArcGeometry.OutlinePointCount(steps, caps)];
            ArcGeometry.FillOutline(Mid, T, 30.0, 120.0, steps, caps, outline);

            foreach (var p in outline)
            {
                double r = Radius(p);
                Assert.GreaterOrEqual(r, Mid - T / 2 - 1e-5, "a cap point dipped inside the inner surface");
                Assert.LessOrEqual(r, Mid + T / 2 + 1e-5, "a cap point poked outside the outer surface");
            }
        }

        [Test]
        public void Cap_endpoints_meet_the_edges_they_join()
        {
            int steps = 4;
            int caps = 3;
            var outline = new Point2[ArcGeometry.OutlinePointCount(steps, caps)];
            ArcGeometry.FillOutline(Mid, T, 0.0, 90.0, steps, caps, outline);

            Point2 lastOuter = outline[steps];
            Point2 firstCap = outline[steps + 1];
            double gap = Math.Sqrt(Math.Pow(lastOuter.X - firstCap.X, 2) + Math.Pow(lastOuter.Y - firstCap.Y, 2));
            Assert.Less(gap, T, "cap does not start where the outer edge ends");
        }

        [TestCase(0, 0)]
        [TestCase(1, 2)]
        [TestCase(4, 5)]
        public void Capped_band_is_sized_for_two_fans(int capPoints, int trianglesPerCap)
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            int verts = ArcGeometry.BandVertexCount(steps, capPoints);
            int indices = ArcGeometry.BandIndexCount(steps, capPoints);

            int capVerts = capPoints == 0 ? 0 : capPoints + 1;
            Assert.AreEqual(2 * (steps + 1) + 2 * capVerts, verts);
            Assert.AreEqual(6 * steps + 2 * 3 * trianglesPerCap, indices);
        }

        [Test]
        public void Drawn_end_is_the_physical_end()
        {
            int steps = ArcGeometry.StepsFor(120.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];
            var outline = new Point2[ArcGeometry.OutlinePointCount(steps, Caps)];

            ArcGeometry.FillBand(Mid, T, 30.0, 120.0, steps, Caps, verts, tris);
            ArcGeometry.FillOutline(Mid, T, 30.0, 120.0, steps, Caps, outline);

            foreach (var cap in CapPointsOf(outline, steps, Caps))
            {
                bool found = false;
                foreach (var v in verts)
                    if (Math.Abs(v.X - cap.X) < 1e-6 && Math.Abs(v.Y - cap.Y) < 1e-6) { found = true; break; }
                Assert.IsTrue(found, $"collider cap point {cap} is not drawn");
            }
        }

        [Test]
        public void Cap_fan_is_a_half_circle_of_half_the_thickness()
        {
            int steps = ArcGeometry.StepsFor(90.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];
            ArcGeometry.FillBand(Mid, T, 0.0, 90.0, steps, Caps, verts, tris);

            foreach (int fan in new[] { 2 * (steps + 1), 2 * (steps + 1) + Caps + 1 })
            {
                Point2 centre = verts[fan];
                Assert.AreEqual(Mid, Radius(centre), 1e-5, "cap centre is off the mid-line");

                for (int i = 1; i <= Caps; i++)
                {
                    Point2 p = verts[fan + i];
                    double d = Math.Sqrt(Math.Pow(p.X - centre.X, 2) + Math.Pow(p.Y - centre.Y, 2));
                    Assert.AreEqual(T / 2, d, 1e-5, "rim point is not at T/2 from the cap centre");
                    Assert.GreaterOrEqual(Radius(p), Mid - T / 2 - 1e-5, "rim dipped inside the inner surface");
                    Assert.LessOrEqual(Radius(p), Mid + T / 2 + 1e-5, "rim poked outside the outer surface");
                }
            }
        }

        [Test]
        public void Capped_band_still_faces_the_camera_at_both_ends()
        {
            int steps = ArcGeometry.StepsFor(37.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];

            foreach (double start in new[] { 0.0, 95.0, 214.0, -40.0 })
            {
                ArcGeometry.FillBand(Mid, T, start, 37.0, steps, Caps, verts, tris);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Point2 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                    double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                    Assert.Less(cross, 0.0, $"triangle {t / 3} at start {start} would be culled");
                }
            }
        }

        static Point2[] CapPointsOf(Point2[] outline, int steps, int capPoints)
        {
            var caps = new Point2[2 * capPoints];
            for (int i = 0; i < capPoints; i++)
            {
                caps[i] = outline[steps + 1 + i];
                caps[capPoints + i] = outline[2 * (steps + 1) + capPoints + i];
            }
            return caps;
        }

        [Test]
        public void Thickness_is_preserved_at_every_radius_the_tunnel_shrinks_through()
        {
            int steps = ArcGeometry.StepsFor(90.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps, Caps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps, Caps)];

            foreach (double radius in new[] { 7.65, 6.10, 4.55, 3.00, 1.45, 0.90, 0.70 })
            {
                ArcGeometry.FillBand(radius, T, 0.0, 90.0, steps, Caps, verts, tris);
                double measured = Radius(verts[1]) - Radius(verts[0]);
                Assert.AreEqual(T, measured, 1e-5, $"thickness drifted at R={radius}");
            }
        }
    }
}
