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
        public void A_held_gap_delivers_the_opening_it_was_drawn_with()
        {
            // The caps eat half a thickness from each side. Cutting exactly the opening would
            // leave the ball a way out narrower by a full T, and nothing downstream would say so.
            var s = TunnelSettings.Default;
            foreach (double opening in new[] { 1.2, 1.5, 2.2, 3.2 })
            {
                double widthDeg = TunnelMath.HeldGapWidthDeg(opening, s.Thickness, s.TargetRadius,
                                                             s.TargetRadius, s.MinGapWidth);
                double delivered = RingSegmentation.OpeningFor(widthDeg, s.Thickness, s.TargetRadius);
                Assert.AreEqual(opening, delivered, 1e-9,
                    $"opening {opening} came out as {delivered}");
            }
        }

        [Test]
        public void A_naive_cut_really_would_lose_a_whole_thickness()
        {
            // Guards the reasoning, not just the result: if the caps ever stop bulging into the
            // gap, the cap allowance is over-cutting and this test is the one that should fail.
            const double T = 0.30;
            double naive = TunnelMath.GapWidthDeg(1.5, 3.0);
            Assert.AreEqual(1.5 - T, RingSegmentation.OpeningFor(naive, T, 3.0), 1e-9);
        }

        [Test]
        public void The_minimum_gap_still_clears_the_ball_after_the_cap_allowance()
        {
            var s = TunnelSettings.Default;
            double widthDeg = TunnelMath.HeldGapWidthDeg(s.MinGapWidth, s.Thickness, s.TargetRadius,
                                                         s.TargetRadius, s.MinGapWidth);
            double delivered = RingSegmentation.OpeningFor(widthDeg, s.Thickness, s.TargetRadius);
            Assert.Greater(delivered, s.BallRadius * 2.0, "the narrowest legal gap does not fit the ball");
        }

        [Test]
        public void A_shrinking_ring_keeps_the_same_opening_angle()
        {
            // D-059, and the whole point of it: a ring closing in on the centre keeps the notch
            // it was drawn with. The rule this replaced held the arc length instead, so the same
            // ring at a third of its radius showed three times the angle and read as falling apart.
            var s = TunnelSettings.Default;
            const double opening = 2.0;
            double atReference = TunnelMath.HeldGapWidthDeg(opening, s.Thickness, s.TargetRadius,
                                                            s.TargetRadius, s.MinGapWidth);

            foreach (double radius in new[] { 12.0, 6.0, 4.0, s.TargetRadius })
                Assert.AreEqual(atReference,
                    TunnelMath.HeldGapWidthDeg(opening, s.Thickness, s.TargetRadius, radius,
                                               s.MinGapWidth),
                    1e-9, $"the angle changed at R={radius}");
        }

        [Test]
        public void Close_to_the_centre_the_angle_grows_only_as_fast_as_the_ball_needs()
        {
            // The one exception to holding the angle, and it is physical: held all the way down,
            // the opening would pinch below the ball before the ring reached the red circle.
            var s = TunnelSettings.Default;
            const double opening = 2.0;
            double held = TunnelMath.HeldGapWidthDeg(opening, s.Thickness, s.TargetRadius,
                                                     s.TargetRadius, s.MinGapWidth);
            double atCrush = TunnelMath.HeldGapWidthDeg(opening, s.Thickness, s.TargetRadius,
                                                        s.CrushRadius, s.MinGapWidth);

            Assert.Greater(atCrush, held, "the opening pinched shut on its way in");
            Assert.AreEqual(s.MinGapWidth,
                RingSegmentation.OpeningFor(atCrush, s.Thickness, s.CrushRadius), 1e-9,
                "the floor gives away more than the ball needs");
        }

        [Test]
        public void A_ring_asked_for_less_than_the_floor_is_left_narrow()
        {
            // The floor rescues a ring that shrinks into its own opening. It must not widen one
            // that was deliberately built impassable, or the play-mode test proving the ball
            // cannot squeeze through a gap narrower than itself would be proving nothing.
            var s = TunnelSettings.Default;
            double narrow = s.BallRadius;   // half a ball wide
            double widthDeg = TunnelMath.HeldGapWidthDeg(narrow, s.Thickness, s.TargetRadius,
                                                         s.TargetRadius, s.MinGapWidth);

            Assert.AreEqual(narrow, RingSegmentation.OpeningFor(widthDeg, s.Thickness, s.TargetRadius),
                            1e-9, "the floor widened a ring that was asked to be a wall");
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
            Assert.AreEqual(1.35, s.Step, 1e-9, "ring step");
            Assert.AreEqual(0.95, s.CrushRadius, 1e-9, "crush radius");
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
