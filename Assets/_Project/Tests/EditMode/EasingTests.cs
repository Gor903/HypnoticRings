using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// The effect curves, checked where they matter: at the ends and at the peak. A pulse that
    /// does not come back to zero leaves the score permanently larger, and a shake that does not
    /// leaves the camera permanently crooked — both are cumulative, and both look like nothing
    /// at all for the first few repetitions.
    /// </summary>
    public class EasingTests
    {
        [Test]
        public void A_pulse_starts_and_ends_at_nothing()
        {
            Assert.AreEqual(0.0, Easing.Pulse(0.0), 1e-12);
            Assert.AreEqual(0.0, Easing.Pulse(1.0), 1e-12);
        }

        [Test]
        public void A_pulse_peaks_once_in_the_middle()
        {
            Assert.AreEqual(1.0, Easing.Pulse(0.5), 1e-12);

            // Monotone up to the peak and down after it: one swell, not a flicker.
            for (int i = 1; i < 50; i++)
            {
                Assert.Greater(Easing.Pulse(i / 100.0), Easing.Pulse((i - 1) / 100.0));
                Assert.Greater(Easing.Pulse(1.0 - i / 100.0), Easing.Pulse(1.0 - (i - 1) / 100.0));
            }
        }

        [Test]
        public void Repeating_a_pulse_cannot_accumulate()
        {
            // The property that actually matters: a hundred pulses leave no residue.
            double scale = 1.0;
            for (int pulse = 0; pulse < 100; pulse++)
                for (int step = 0; step <= 20; step++)
                    scale = 1.0 + 0.15 * Easing.Pulse(step / 20.0);

            Assert.AreEqual(1.0, scale, 1e-12, "the score stayed enlarged after a hundred passes");
        }

        [Test]
        public void A_fade_runs_from_full_to_nothing_and_never_climbs()
        {
            Assert.AreEqual(1.0, Easing.Fade(0.0), 1e-12);
            Assert.AreEqual(0.0, Easing.Fade(1.0), 1e-12);

            for (int i = 1; i <= 100; i++)
                Assert.LessOrEqual(Easing.Fade(i / 100.0), Easing.Fade((i - 1) / 100.0));
        }

        [Test]
        public void A_shake_returns_the_camera_to_exactly_where_it_started()
        {
            Assert.AreEqual(0.0, Easing.Shake(0.0), 1e-12);
            Assert.AreEqual(0.0, Easing.Shake(1.0), 1e-12);
        }

        [Test]
        public void A_shake_stays_inside_one_unit_and_dies_down()
        {
            double earlyMax = 0.0;
            double lateMax = 0.0;

            for (int i = 0; i <= 1000; i++)
            {
                double t = i / 1000.0;
                double value = System.Math.Abs(Easing.Shake(t));
                Assert.LessOrEqual(value, 1.0, $"shake left the unit range at {t}");

                if (t < 0.25) earlyMax = System.Math.Max(earlyMax, value);
                if (t > 0.75) lateMax = System.Math.Max(lateMax, value);
            }

            Assert.Greater(earlyMax, lateMax, "the shake did not decay");
        }

        [Test]
        public void A_shake_is_the_same_every_time_it_is_asked()
        {
            // Deterministic by design: a shake built on Random cannot be tested at all.
            for (int i = 0; i <= 100; i++)
                Assert.AreEqual(Easing.Shake(i / 100.0), Easing.Shake(i / 100.0), 1e-15);
        }

        [Test]
        public void Smooth_step_spans_zero_to_one_without_overshooting()
        {
            Assert.AreEqual(0.0, Easing.SmoothStep(0.0), 1e-12);
            Assert.AreEqual(1.0, Easing.SmoothStep(1.0), 1e-12);
            Assert.AreEqual(0.5, Easing.SmoothStep(0.5), 1e-12);

            for (int i = 0; i <= 100; i++)
            {
                double v = Easing.SmoothStep(i / 100.0);
                Assert.GreaterOrEqual(v, 0.0);
                Assert.LessOrEqual(v, 1.0);
            }
        }

        [Test]
        public void Every_curve_is_clamped_outside_its_range()
        {
            // Frames overshoot. A curve asked for t = 1.3 must not start growing again.
            Assert.AreEqual(0.0, Easing.Pulse(1.3), 1e-12);
            Assert.AreEqual(0.0, Easing.Pulse(-0.3), 1e-12);
            Assert.AreEqual(0.0, Easing.Fade(1.3), 1e-12);
            Assert.AreEqual(1.0, Easing.Fade(-0.3), 1e-12);
            Assert.AreEqual(0.0, Easing.Shake(1.3), 1e-12);
            Assert.AreEqual(1.0, Easing.SmoothStep(1.3), 1e-12);
            Assert.AreEqual(0.0, Easing.SmoothStep(-0.3), 1e-12);
        }
    }
}
