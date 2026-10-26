using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class RingSegmentationTests
    {
        const double MaxSweep = 120.0;   // CONTRACT.md §2.2: no collider spans more than this

        static ArcSpan[] Buffer(int gapCount) =>
            new ArcSpan[RingSegmentation.UpperBoundSegments(gapCount, MaxSweep)];

        static bool InsideAnyGap(double angleDeg, params Gap[] gaps)
        {
            foreach (var g in gaps)
                if (Math.Abs(TunnelMath.ShortestDeltaDeg(g.CentreDeg, angleDeg)) < g.WidthDeg * 0.5 - 1e-9)
                    return true;
            return false;
        }

        [Test]
        public void No_gaps_gives_a_full_circle()
        {
            var output = Buffer(0);
            int n = RingSegmentation.BuildSolidSegments(null, 0, MaxSweep, output);
            Assert.AreEqual(360.0, RingSegmentation.SolidSweep(output, n), 1e-9);
        }

        [Test]
        public void One_gap_removes_exactly_its_own_width()
        {
            var gaps = new[] { new Gap(270.0, 30.0) };
            var output = Buffer(1);
            int n = RingSegmentation.BuildSolidSegments(gaps, 1, MaxSweep, output);
            Assert.AreEqual(330.0, RingSegmentation.SolidSweep(output, n), 1e-9);
        }

        [Test]
        public void Two_gaps_remove_both_widths()
        {
            var gaps = new[] { new Gap(270.0, 30.0), new Gap(90.0, 20.0) };
            var output = Buffer(2);
            int n = RingSegmentation.BuildSolidSegments(gaps, 2, MaxSweep, output);
            Assert.AreEqual(310.0, RingSegmentation.SolidSweep(output, n), 1e-9);
        }

        [Test]
        public void No_segment_exceeds_the_collider_sweep_limit()
        {
            var gaps = new[] { new Gap(270.0, 30.0) };
            var output = Buffer(1);
            int n = RingSegmentation.BuildSolidSegments(gaps, 1, MaxSweep, output);

            Assert.Greater(n, 0);
            for (int i = 0; i < n; i++)
                Assert.LessOrEqual(output[i].SweepDeg, MaxSweep + 1e-9, $"segment {i} is too wide");
        }

        [Test]
        public void No_segment_ever_covers_part_of_a_gap()
        {
            // The gap is the way out. A collider leaking into it makes the ring unpassable and
            // nothing else in the game would report why.
            var gaps = new[] { new Gap(270.0, 28.6), new Gap(42.0, 15.0) };
            var probe = new[] { new Gap(270.0, 28.6), new Gap(42.0, 15.0) };
            var output = Buffer(2);
            int n = RingSegmentation.BuildSolidSegments(gaps, 2, MaxSweep, output);

            for (int i = 0; i < n; i++)
                for (int s = 0; s <= 20; s++)
                {
                    double angle = output[i].StartDeg + output[i].SweepDeg * s / 20.0;
                    Assert.IsFalse(InsideAnyGap(angle, probe),
                        $"segment {i} covers {angle:F2} deg, which is inside a gap");
                }
        }

        [Test]
        public void Segments_are_contiguous_and_leave_only_the_gaps()
        {
            var gaps = new[] { new Gap(270.0, 30.0) };
            var output = Buffer(1);
            int n = RingSegmentation.BuildSolidSegments(gaps, 1, MaxSweep, output);

            for (int i = 0; i < n - 1; i++)
                Assert.AreEqual(output[i].EndDeg, output[i + 1].StartDeg, 1e-9,
                    $"a crack between segment {i} and {i + 1}");
        }

        [Test]
        public void Gap_order_does_not_change_the_result()
        {
            var ascending = new[] { new Gap(30.0, 20.0), new Gap(200.0, 40.0) };
            var descending = new[] { new Gap(200.0, 40.0), new Gap(30.0, 20.0) };
            var a = Buffer(2);
            var b = Buffer(2);

            int na = RingSegmentation.BuildSolidSegments(ascending, 2, MaxSweep, a);
            int nb = RingSegmentation.BuildSolidSegments(descending, 2, MaxSweep, b);

            Assert.AreEqual(na, nb);
            Assert.AreEqual(RingSegmentation.SolidSweep(a, na), RingSegmentation.SolidSweep(b, nb), 1e-9);
        }

        [Test]
        public void Gaps_that_have_merged_leave_no_sliver_behind()
        {
            // Late-game rings carry two gaps that widen as the radius falls; they can meet.
            var gaps = new[] { new Gap(0.0, 60.0), new Gap(20.0, 60.0) };
            var output = Buffer(2);
            int n = RingSegmentation.BuildSolidSegments(gaps, 2, MaxSweep, output);

            for (int i = 0; i < n; i++)
                Assert.Greater(output[i].SweepDeg, 1e-9, "a zero-width segment was emitted");
        }

        [Test]
        public void A_gap_spanning_the_wrap_is_handled()
        {
            var gaps = new[] { new Gap(0.0, 40.0) };
            var output = Buffer(1);
            int n = RingSegmentation.BuildSolidSegments(gaps, 1, MaxSweep, output);
            Assert.AreEqual(320.0, RingSegmentation.SolidSweep(output, n), 1e-9);
        }

        [Test]
        public void The_upper_bound_is_actually_big_enough()
        {
            // The bound is what lets callers allocate once and refill forever.
            foreach (double sweep in new[] { 30.0, 45.0, 90.0, 120.0, 180.0 })
                for (int gapCount = 0; gapCount <= 2; gapCount++)
                {
                    var gaps = new[] { new Gap(10.0, 12.0), new Gap(190.0, 12.0) };
                    var output = new ArcSpan[RingSegmentation.UpperBoundSegments(gapCount, sweep)];
                    Assert.DoesNotThrow(
                        () => RingSegmentation.BuildSolidSegments(gaps, gapCount, sweep, output),
                        $"bound too small for {gapCount} gap(s) at {sweep} deg");
                }
        }


        [Test]
        public void ForOpening_delivers_the_opening_that_was_asked_for()
        {
            // The caps eat half a thickness from each side. Cutting exactly the opening would
            // leave the ball a way out narrower by a full T, and nothing downstream would say so.
            const double T = 0.30;
            foreach (double opening in new[] { 1.2, 1.5, 2.2, 3.2 })
                foreach (double radius in new[] { 3.0, 1.5, 0.9 })
                {
                    var gap = Gap.ForOpening(270.0, opening, T, radius);
                    double delivered = RingSegmentation.OpeningFor(gap.WidthDeg, T, radius);
                    Assert.AreEqual(opening, delivered, 1e-9,
                        $"opening {opening} at R={radius} came out as {delivered}");
                }
        }

        [Test]
        public void A_naive_cut_really_would_lose_a_whole_thickness()
        {
            // Guards the reasoning, not just the result: if the caps ever stop bulging into the
            // gap, ForOpening is over-cutting and this test is the one that should fail.
            const double T = 0.30;
            var naive = Gap.FromArcLength(270.0, 1.5, 3.0);
            double delivered = RingSegmentation.OpeningFor(naive.WidthDeg, T, 3.0);
            Assert.AreEqual(1.5 - T, delivered, 1e-9);
        }

        [Test]
        public void The_minimum_gap_still_clears_the_ball_after_the_cap_allowance()
        {
            var s = TunnelSettings.Default;
            var gap = Gap.ForOpening(270.0, s.MinGapWidth, s.Thickness, s.TargetRadius);
            double delivered = RingSegmentation.OpeningFor(gap.WidthDeg, s.Thickness, s.TargetRadius);
            Assert.Greater(delivered, s.BallRadius * 2.0, "the narrowest legal gap does not fit the ball");
        }

        [Test]
        public void A_shrinking_ring_keeps_the_same_opening_in_world_units()
        {
            // Same arc length at three radii: the angular gap grows, the solid arc length that
            // remains shrinks, and the opening the ball must fit through does not change.
            const double arc = 1.5;
            foreach (double radius in new[] { 3.0, 1.5, 0.9 })
            {
                var gaps = new[] { Gap.FromArcLength(270.0, arc, radius) };
                var output = Buffer(1);
                int n = RingSegmentation.BuildSolidSegments(gaps, 1, MaxSweep, output);

                double solidDeg = RingSegmentation.SolidSweep(output, n);
                double gapDeg = 360.0 - solidDeg;
                double gapArc = gapDeg / TunnelMath.DegPerRad * radius;
                Assert.AreEqual(arc, gapArc, 1e-6, $"opening changed size at R={radius}");
            }
        }
    }

    public class TunnelSettingsTests
    {
        [Test]
        public void The_default_configuration_is_playable()
        {
            var problems = TunnelSettings.Default.Validate();
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }

        [Test]
        public void Derived_values_match_the_contract()
        {
            var s = TunnelSettings.Default;
            Assert.AreEqual(1.55, s.Step, 1e-9, "ring step");
            Assert.AreEqual(0.70, s.CrushRadius, 1e-9, "crush radius");
        }

        [Test]
        public void A_ring_can_always_reach_the_crush_radius_before_it_is_culled()
        {
            // If rings vanished before they could crush, the game would have no lose condition.
            var s = TunnelSettings.Default;
            Assert.Greater(s.VanishRadius, s.CrushRadius);
        }

        [Test]
        public void A_corridor_too_narrow_for_the_ball_is_rejected()
        {
            var bad = new TunnelSettings(0.30, 0.9, 3.0, 0.9, 0.5, 0.05, 0.3, 1.5,
                                         3.0, 4.0, 200.0, 12.0, 7.5, 120.0, 3, 4);
            Assert.IsNotEmpty(bad.Validate());
        }

        [Test]
        public void A_gap_narrower_than_the_ball_is_rejected()
        {
            var bad = new TunnelSettings(0.30, 1.25, 3.0, 0.9, 0.5, 0.05, 0.3, 0.8,
                                         3.0, 4.0, 200.0, 12.0, 7.5, 120.0, 3, 4);
            Assert.IsNotEmpty(bad.Validate());
        }
    }
}
