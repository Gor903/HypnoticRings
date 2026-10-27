using System;

namespace HR.Core
{
    /// <summary>
    /// The shapes the effects move along.
    ///
    /// Kept here, away from UnityEngine, because a curve is arithmetic and arithmetic can be
    /// checked at its endpoints and its peak without a frame ever being drawn. Every one of
    /// these is pure: same input, same output, no clock of its own.
    /// </summary>
    public static class Easing
    {
        /// <summary>
        /// A there-and-back pulse over a normalised time: 0 at both ends, 1 in the middle.
        ///
        /// Sine rather than a triangle because the score should swell, not flick. The ends are
        /// exactly zero, which is what stops a pulse from leaving the score permanently larger
        /// after a hundred of them.
        /// </summary>
        public static double Pulse(double t)
        {
            if (t <= 0.0 || t >= 1.0) return 0.0;
            return Math.Sin(t * Math.PI);
        }

        /// <summary>Fades from one to zero, steeper at the start so a trail reads as motion.</summary>
        public static double Fade(double t)
        {
            if (t <= 0.0) return 1.0;
            if (t >= 1.0) return 0.0;
            double inverse = 1.0 - t;
            return inverse * inverse;
        }

        /// <summary>
        /// A decaying wobble for the camera shake, in the range [-1, 1] and ending at zero.
        ///
        /// Deterministic rather than random: a shake built from Random is a shake that cannot be
        /// tested, and the camera must provably return to exactly where it started.
        /// </summary>
        public static double Shake(double t, double frequency = 18.0)
        {
            if (t <= 0.0 || t >= 1.0) return 0.0;
            return Math.Sin(t * frequency) * (1.0 - t) * (1.0 - t);
        }

        /// <summary>Smooth 0..1 ramp with zero slope at both ends, for fades between screens.</summary>
        public static double SmoothStep(double t)
        {
            if (t <= 0.0) return 0.0;
            if (t >= 1.0) return 1.0;
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
