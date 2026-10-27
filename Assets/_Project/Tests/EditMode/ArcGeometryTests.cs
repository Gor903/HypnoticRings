using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class ArcGeometryTests
    {
        const double Mid = 3.0;
        const double T = 0.30;
        const double MaxStep = 7.5;   // CONTRACT.md §2.2: arc approximation step

        static double Radius(Point2 p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

        /// <summary>Positive means counter-clockwise, which is what a collider path wants.</summary>
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
            var verts = new Point2[ArcGeometry.BandVertexCount(steps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps)];
            ArcGeometry.FillBand(Mid, T, 0.0, 120.0, steps, verts, tris);

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
            var verts = new Point2[ArcGeometry.BandVertexCount(steps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps)];
            ArcGeometry.FillBand(Mid, T, 0.0, 120.0, steps, verts, tris);

            // Negative, not merely consistent. The camera looks along +Z, and a triangle wound
            // the other way is back-face culled: the mesh exists, its bounds are right, the
            // renderer reports itself visible, and nothing appears. This test used to demand
            // the opposite sign, so it passed happily while every ring was invisible — nothing
            // rendered anything until the screenshot gate, and that is what finally caught it.
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
            var verts = new Point2[ArcGeometry.BandVertexCount(steps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps)];

            // The shrinking tunnel refills these every time a ring's radius moves; allocating
            // per refill is what the no-garbage gate is about.
            ArcGeometry.FillBand(3.0, T, 0.0, 120.0, steps, verts, tris);
            ArcGeometry.FillBand(1.2, T, 45.0, 120.0, steps, verts, tris);

            Assert.AreEqual(1.2 - T / 2, Radius(verts[0]), 1e-5);
            Assert.AreEqual(1.2 + T / 2, Radius(verts[1]), 1e-5);
        }

        [Test]
        public void Band_refuses_a_thickness_that_swallows_the_radius()
        {
            int steps = 4;
            var verts = new Point2[ArcGeometry.BandVertexCount(steps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps)];
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ArcGeometry.FillBand(0.1, T, 0.0, 90.0, steps, verts, tris));
        }

        [Test]
        public void Band_refuses_a_buffer_that_is_too_small()
        {
            var verts = new Point2[4];
            var tris = new int[6];
            Assert.Throws<ArgumentException>(
                () => ArcGeometry.FillBand(Mid, T, 0.0, 120.0, 8, verts, tris));
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
            double caps_area = Math.PI * Math.Pow(T / 2, 2);   // two half-circles make one circle
            double measured = SignedArea(outline, outline.Length);

            // Polygonal approximation sits just inside the true arc, so a small deficit is correct.
            Assert.AreEqual(sector + caps_area, measured, (sector + caps_area) * 0.01);
        }

        [Test]
        public void Rounded_caps_never_bulge_past_the_bands_own_radii()
        {
            // This is why the caps are safe to put on every segment end, including the internal
            // joints: the ball rolls on the inner surface and must not meet a bump there.
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

            // First cap point must leave the outer edge's last vertex, not jump across the band.
            Point2 lastOuter = outline[steps];
            Point2 firstCap = outline[steps + 1];
            double gap = Math.Sqrt(Math.Pow(lastOuter.X - firstCap.X, 2) + Math.Pow(lastOuter.Y - firstCap.Y, 2));
            Assert.Less(gap, T, "cap does not start where the outer edge ends");
        }

        [Test]
        public void Thickness_is_preserved_at_every_radius_the_tunnel_shrinks_through()
        {
            // The one invariant the whole design rests on: themes and compression may not change T.
            int steps = ArcGeometry.StepsFor(90.0, MaxStep);
            var verts = new Point2[ArcGeometry.BandVertexCount(steps)];
            var tris = new int[ArcGeometry.BandIndexCount(steps)];

            foreach (double radius in new[] { 7.65, 6.10, 4.55, 3.00, 1.45, 0.90, 0.70 })
            {
                ArcGeometry.FillBand(radius, T, 0.0, 90.0, steps, verts, tris);
                double measured = Radius(verts[1]) - Radius(verts[0]);
                Assert.AreEqual(T, measured, 1e-5, $"thickness drifted at R={radius}");
            }
        }
    }
}
