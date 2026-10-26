using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// CONTRACT.md §6.3 lists the gesture cases that must not break. Each one is here, because
    /// every one of them is a bug that feels like "the controls are broken" rather than like a
    /// crash, and none of them would be caught by playing normally.
    /// </summary>
    public class GestureTrackerTests
    {
        const double Dt = 1.0 / 60.0;
        const double R = 2.0;   // comfortably outside the 0.6u dead zone

        static GestureTracker Make(double sensitivity = 1.0, double maxSpeed = 540.0,
                                   double decay = 5.0, double deadZone = 0.6, bool inverted = false)
            => new GestureTracker(new GestureSettings(sensitivity, maxSpeed, decay, deadZone, inverted));

        static double X(double deg) => R * Math.Cos(deg * Math.PI / 180.0);
        static double Y(double deg) => R * Math.Sin(deg * Math.PI / 180.0);

        static void Drag(GestureTracker t, double fromDeg, double toDeg, int steps = 1)
        {
            t.Begin(X(fromDeg), Y(fromDeg));
            for (int i = 1; i <= steps; i++)
            {
                double a = fromDeg + (toDeg - fromDeg) * i / steps;
                t.Move(X(a), Y(a), Dt);
            }
        }

        [Test]
        public void Dragging_counter_clockwise_turns_the_tunnel_the_same_way()
        {
            var t = Make();
            Drag(t, 0.0, 30.0, steps: 10);
            Assert.AreEqual(30.0, t.AngleDeg, 1e-6);
        }

        [Test]
        public void Dragging_clockwise_turns_the_other_way()
        {
            var t = Make();
            Drag(t, 0.0, -30.0, steps: 10);
            Assert.AreEqual(-30.0, t.AngleDeg, 1e-6);
        }

        [Test]
        public void Sensitivity_scales_the_result()
        {
            var t = Make(sensitivity: 1.6);
            Drag(t, 0.0, 30.0, steps: 10);
            Assert.AreEqual(48.0, t.AngleDeg, 1e-6);
        }

        [Test]
        public void Inversion_flips_the_direction_and_nothing_else()
        {
            var normal = Make();
            var inverted = Make(inverted: true);
            Drag(normal, 0.0, 30.0, steps: 10);
            Drag(inverted, 0.0, 30.0, steps: 10);
            Assert.AreEqual(-normal.AngleDeg, inverted.AngleDeg, 1e-6);
        }

        [Test]
        public void A_violent_flick_is_capped_at_the_speed_limit()
        {
            // Without the cap a single frame of input could spin the tunnel most of a turn, which
            // is both unplayable and a way to shove the ball through geometry.
            var t = Make(maxSpeed: 540.0);
            Drag(t, 0.0, 170.0, steps: 1);
            Assert.AreEqual(540.0 * Dt, t.AngleDeg, 1e-6);
        }

        [Test]
        public void Crossing_the_wrap_reads_as_a_small_step_forward()
        {
            // 179 to -179 is two degrees onward, not 358 degrees backward. Getting this wrong
            // makes the tunnel lurch the wrong way whenever a gesture passes the left of screen.
            var t = Make();
            Drag(t, 179.0, -179.0, steps: 1);
            Assert.AreEqual(2.0, t.AngleDeg, 1e-6);
        }

        [Test]
        public void Crossing_the_wrap_backwards_also_reads_small()
        {
            var t = Make();
            Drag(t, -179.0, 179.0, steps: 1);
            Assert.AreEqual(-2.0, t.AngleDeg, 1e-6);
        }

        [Test]
        public void A_touch_on_the_exact_centre_does_nothing_and_stays_finite()
        {
            var t = Make();
            t.Begin(0.0, 0.0);
            double moved = t.Move(0.0, 0.0, Dt);

            Assert.IsFalse(t.IsTracking, "a touch in the dead zone should not be tracked");
            Assert.AreEqual(0.0, moved);
            Assert.AreEqual(0.0, t.AngleDeg);
            Assert.IsFalse(double.IsNaN(t.AngleDeg) || double.IsInfinity(t.AngleDeg));
        }

        [Test]
        public void Dragging_through_the_centre_resumes_without_a_jump()
        {
            // Near the centre a pixel is a huge angle. The gesture must pause and re-anchor on
            // the far side rather than snap the tunnel round.
            var t = Make();
            t.Begin(X(0.0), Y(0.0));
            t.Move(X(0.0), Y(0.0), Dt);

            t.Move(0.0, 0.0, Dt);                 // through the dead zone
            double before = t.AngleDeg;
            t.Move(X(180.0), Y(180.0), Dt);       // reappears on the opposite side

            Assert.AreEqual(before, t.AngleDeg, 1e-9, "re-anchoring moved the tunnel");
            Assert.IsTrue(t.IsTracking, "tracking should resume once clear of the dead zone");
        }

        [Test]
        public void A_released_finger_leaves_momentum_that_fades()
        {
            var t = Make(decay: 5.0);
            Drag(t, 0.0, 20.0, steps: 4);
            double atRelease = t.AngleDeg;
            double speed = t.VelocityDeg;
            Assert.Greater(speed, 0.0, "a moving finger should leave some speed behind");

            t.End();
            for (int i = 0; i < 10; i++) t.Tick(Dt);

            Assert.Greater(t.AngleDeg, atRelease, "inertia did not carry the tunnel onward");
            Assert.Less(t.VelocityDeg, speed, "inertia did not decay");
        }

        [Test]
        public void Momentum_comes_to_a_complete_stop_rather_than_drifting_forever()
        {
            var t = Make(decay: 5.0);
            Drag(t, 0.0, 20.0, steps: 4);
            t.End();

            for (int i = 0; i < 2000; i++) t.Tick(Dt);

            Assert.AreEqual(0.0, t.VelocityDeg, "an exponential decay never reaches zero on its own");
            double resting = t.AngleDeg;
            t.Tick(Dt);
            Assert.AreEqual(resting, t.AngleDeg, "the tunnel is still creeping");
        }

        [Test]
        public void Losing_focus_mid_gesture_throws_the_momentum_away()
        {
            // Returning from a phone call to a tunnel still spinning from before it is not a
            // gesture the player made.
            var t = Make();
            Drag(t, 0.0, 20.0, steps: 4);
            double atCancel = t.AngleDeg;

            t.Cancel();
            for (int i = 0; i < 10; i++) t.Tick(Dt);

            Assert.AreEqual(0.0, t.VelocityDeg);
            Assert.AreEqual(atCancel, t.AngleDeg, 1e-9, "the tunnel kept turning after a cancel");
            Assert.IsFalse(t.IsTracking);
        }

        [Test]
        public void Inertia_does_not_fight_a_finger_that_is_still_down()
        {
            var t = Make();
            Drag(t, 0.0, 20.0, steps: 4);
            double held = t.AngleDeg;

            for (int i = 0; i < 10; i++) t.Tick(Dt);

            Assert.AreEqual(held, t.AngleDeg, 1e-9, "inertia ran while the finger was the authority");
        }

        [Test]
        public void A_finger_that_leaves_the_screen_coasts_rather_than_stopping_dead()
        {
            // The platform reports this as the touch ending, so it must read as a release.
            var t = Make();
            Drag(t, 0.0, 20.0, steps: 4);
            t.End();
            t.Tick(Dt);
            Assert.Greater(t.VelocityDeg, 0.0);
        }

        [Test]
        public void A_zero_length_step_changes_nothing()
        {
            var t = Make();
            t.Begin(X(0.0), Y(0.0));
            Assert.AreEqual(0.0, t.Move(X(45.0), Y(45.0), 0.0));
            Assert.AreEqual(0.0, t.AngleDeg);
        }

        [Test]
        public void Reset_returns_to_a_standing_start()
        {
            var t = Make();
            Drag(t, 0.0, 45.0, steps: 5);
            t.End();
            t.Reset();

            Assert.AreEqual(0.0, t.AngleDeg);
            Assert.AreEqual(0.0, t.VelocityDeg);
            Assert.IsFalse(t.IsTracking);
        }

        [Test]
        public void A_nonsense_configuration_is_rejected_at_construction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GestureTracker(new GestureSettings(0.0, 540.0, 5.0, 0.6, false)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GestureTracker(new GestureSettings(1.0, 0.0, 5.0, 0.6, false)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GestureTracker(new GestureSettings(1.0, 540.0, -1.0, 0.6, false)));
        }

        [Test]
        public void A_long_gesture_stays_exact_rather_than_accumulating_drift()
        {
            // The angle is accumulated every frame for the whole run; a biased step would show
            // up here as a slow drift that nothing else would explain.
            // 5 degrees a step, deliberately under the 9 degrees that 540 deg/s allows at 1/60 s,
            // so this measures accumulated error and not the speed cap.
            var t = Make();
            t.Begin(X(0.0), Y(0.0));
            for (int turn = 0; turn < 10; turn++)
                for (int i = 1; i <= 72; i++)
                    t.Move(X(turn * 360.0 + i * 5.0), Y(turn * 360.0 + i * 5.0), Dt);

            Assert.AreEqual(3600.0, t.AngleDeg, 1e-6);
        }
    }
}
