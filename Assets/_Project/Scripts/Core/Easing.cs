using System;

namespace HR.Core
{
    public static class Easing
    {
        public static double Pulse(double t)
        {
            if (t <= 0.0 || t >= 1.0) return 0.0;
            return Math.Sin(t * Math.PI);
        }

        public static double Fade(double t)
        {
            if (t <= 0.0) return 1.0;
            if (t >= 1.0) return 0.0;
            double inverse = 1.0 - t;
            return inverse * inverse;
        }

        public static double Shake(double t, double frequency = 18.0)
        {
            if (t <= 0.0 || t >= 1.0) return 0.0;
            return Math.Sin(t * frequency) * (1.0 - t) * (1.0 - t);
        }

        public static double SmoothStep(double t)
        {
            if (t <= 0.0) return 0.0;
            if (t >= 1.0) return 1.0;
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
