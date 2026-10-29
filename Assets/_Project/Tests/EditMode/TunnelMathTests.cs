using System;
using System.Linq;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// The numbers asserted here come from CONTRACT.md §2 and the spec tables it quotes.
    /// When balance moves, these move with it — deliberately, by editing the contract first.
    /// </summary>
    public class TunnelMathTests
    {
        // Starting values from CONTRACT.md §2.4 and §2.3.
        const double T = 0.30;
        const double Step = 1.55;
        const double TargetRadius = 3.0;
        const double BallRadius = 0.5;
        const double RedRadius = 0.55;
        const double ComfortSpin = 200.0;

        [TestCase(0.0, 0.0)]
        [TestCase(180.0, 180.0)]
        [TestCase(-180.0, 180.0)]
        [TestCase(540.0, 180.0)]
        [TestCase(270.0, -90.0)]
        [TestCase(-270.0, 90.0)]
        [TestCase(361.0, 1.0)]
        public void NormalizeDeg_folds_into_half_open_range(double input, double expected)
        {
            Assert.AreEqual(expected, TunnelMath.NormalizeDeg(input), 1e-9);
        }

        [Test]
        public void ShortestDelta_crosses_the_wrap_the_short_way()
        {
            // The ±180 crossing is the gesture bug that looks like a sudden reversal.
            Assert.AreEqual(20.0, TunnelMath.ShortestDeltaDeg(350.0, 10.0), 1e-9);
            Assert.AreEqual(-20.0, TunnelMath.ShortestDeltaDeg(10.0, 350.0), 1e-9);
        }

        [Test]
        public void Gap_keeps_its_arc_length_as_the_ring_shrinks()
        {
            // The whole point of storing width as arc length: shrinking must not narrow the way out.
            const double arc = 1.5;
            foreach (double radius in new[] { 3.0, 2.0, 1.5, 0.9 })
            {
                double recovered = TunnelMath.GapWidthDeg(arc, radius) / TunnelMath.DegPerRad * radius;
                Assert.AreEqual(arc, recovered, 1e-9, $"arc length changed at R={radius}");
            }
        }

        [Test]
        public void Gap_angular_width_doubles_when_the_radius_halves()
        {
            Assert.AreEqual(2.0 * TunnelMath.GapWidthDeg(1.5, 3.0),
                            TunnelMath.GapWidthDeg(1.5, 1.5), 1e-9);
        }

        [Test]
        public void GapWidth_rejects_a_degenerate_radius()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TunnelMath.GapWidthDeg(1.5, 0.0));
        }

        [Test]
        public void Compression_sits_at_base_speed_in_equilibrium()
        {
            Assert.AreEqual(0.15, TunnelMath.CompressionSpeed(0.15, 3.0, 4.0, TargetRadius, TargetRadius), 1e-9);
        }

        [Test]
        public void Compression_spikes_after_a_pass_and_is_capped()
        {
            // Clearing a ring raises R_E by one step; that is what retracts the tunnel.
            double v = TunnelMath.CompressionSpeed(0.15, 3.0, 4.0, TargetRadius + Step, TargetRadius);
            Assert.AreEqual(4.0, v, 1e-9, "should saturate at v_max");
        }

        [Test]
        public void Compression_never_runs_backwards()
        {
            // Inside equilibrium the pull term goes negative; the tunnel must still close, not open.
            double v = TunnelMath.CompressionSpeed(0.15, 3.0, 4.0, TargetRadius - 1.0, TargetRadius);
            Assert.AreEqual(0.15, v, 1e-9);
        }

        [Test]
        public void The_run_ends_exactly_when_a_ring_reaches_the_red_circle()
        {
            // Free radius = R - T/2, and the circle is drawn at 0.55, so R = 0.70 is the line.
            Assert.IsTrue(TunnelMath.HasReachedRed(0.70, T, RedRadius));
            Assert.IsFalse(TunnelMath.HasReachedRed(0.7001, T, RedRadius));
        }

        [Test]
        public void A_ring_holding_the_ball_outlives_the_vanish_radius()
        {
            // Rings vanish at R < 0.9 but the circle is reached at 0.70, so the ball's own ring
            // must survive below the vanish threshold or the run could never be lost.
            Assert.IsFalse(TunnelMath.HasReachedRed(0.9, T, RedRadius));
        }

        [Test]
        public void OutOfBounds_only_triggers_beyond_two_steps()
        {
            Assert.IsFalse(TunnelMath.IsOutOfBounds(TargetRadius + 2.0 * Step, TargetRadius, Step));
            Assert.IsTrue(TunnelMath.IsOutOfBounds(TargetRadius + 2.0 * Step + 0.01, TargetRadius, Step));
        }

        [Test]
        public void HasClearedRing_needs_the_outer_edge_plus_the_margin()
        {
            const double margin = 0.3;
            double boundary = TargetRadius + T * 0.5 + margin;
            Assert.IsFalse(TunnelMath.HasClearedRing(boundary, TargetRadius, T, margin));
            Assert.IsTrue(TunnelMath.HasClearedRing(boundary + 0.01, TargetRadius, T, margin));
        }

        [TestCase(0.15, 15.33)]
        [TestCase(0.30, 7.67)]
        [TestCase(0.60, 3.83)]
        public void TimeBudget_matches_the_spec_table(double vBase, double expectedSeconds)
        {
            double budget = TunnelMath.TimeBudget(TargetRadius, T, RedRadius, vBase);
            Assert.AreEqual(expectedSeconds, budget, 0.01);
        }

        [Test]
        public void TimeBudget_rejects_a_stopped_tunnel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TunnelMath.TimeBudget(TargetRadius, T, RedRadius, 0.0));
        }

        [TestCase(70.0, 70.0)]
        [TestCase(-70.0, -70.0)]
        [TestCase(250.0, 100.0)]
        [TestCase(-250.0, -100.0)]
        [TestCase(200.0, 100.0)]
        public void AutoSpin_is_clamped_once_it_reaches_finger_speed(double input, double expected)
        {
            Assert.AreEqual(expected, TunnelMath.ClampAutoSpin(input, ComfortSpin), 1e-9);
        }

        [Test]
        public void A_clamped_ring_always_leaves_room_to_catch_it()
        {
            // After clamping, the player keeps at least half of comfortable speed, so the
            // reachability denominator can never collapse to zero.
            foreach (double raw in new[] { -400.0, -200.0, 0.0, 199.0, 200.0, 400.0 })
            {
                double clamped = TunnelMath.ClampAutoSpin(raw, ComfortSpin);
                Assert.Less(Math.Abs(clamped), ComfortSpin, $"spin {raw} left no headroom");
            }
        }

        [Test]
        public void Reachability_accepts_the_worst_case_on_a_still_ring()
        {
            double budget = TunnelMath.TimeBudget(TargetRadius, T, RedRadius, 0.60);
            Assert.IsTrue(TunnelMath.IsReachable(180.0, 0.0, ComfortSpin, budget));
        }

        [Test]
        public void Reachability_rejects_a_gap_that_outruns_the_finger()
        {
            double budget = TunnelMath.TimeBudget(TargetRadius, T, RedRadius, 0.60);
            Assert.IsFalse(TunnelMath.IsReachable(180.0, 190.0, ComfortSpin, budget));
        }

        [Test]
        public void Reachability_refuses_rather_than_dividing_by_zero()
        {
            Assert.IsFalse(TunnelMath.IsReachable(10.0, 200.0, ComfortSpin, 10.0));
            Assert.IsFalse(TunnelMath.IsReachable(10.0, 999.0, ComfortSpin, 10.0));
        }
    }

    public class CoreIsolationTests
    {
        [Test]
        public void HR_Core_references_no_engine_assembly()
        {
            // The architectural boundary, asserted rather than trusted. noEngineReferences in the
            // asmdef enforces it at compile time; this catches the day someone flips that flag.
            var offenders = typeof(TunnelMath).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("UnityEngine", StringComparison.Ordinal)
                            || n.StartsWith("UnityEditor", StringComparison.Ordinal))
                .ToArray();

            Assert.IsEmpty(offenders, "HR.Core must stay engine-free: " + string.Join(", ", offenders));
        }
    }
}
