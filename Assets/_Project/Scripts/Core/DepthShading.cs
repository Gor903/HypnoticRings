namespace HR.Core
{
    public static class DepthShading
    {
        public static double AlphaFor(double ringRadius, double ballRingRadius, double vanishRadius,
                                      double step, int ringsAhead, double fade = 1.0)
        {
            double alpha = RawAlphaFor(ringRadius, ballRingRadius, vanishRadius, step, ringsAhead);
            if (fade >= 1.0) return alpha;
            if (fade <= 0.0) return 1.0;
            return 1.0 - (1.0 - alpha) * fade;
        }

        static double RawAlphaFor(double ringRadius, double ballRingRadius, double vanishRadius,
                                  double step, int ringsAhead)
        {
            if (step <= 0.0) return 1.0;

            double nextRadius = ballRingRadius + step;

            if (ringRadius <= nextRadius + 1e-9 && ringRadius >= ballRingRadius - 1e-9) return 1.0;

            if (ringRadius < ballRingRadius)
            {
                double span = ballRingRadius - vanishRadius;
                if (span <= 0.0) return 0.0;
                return Clamp01((ringRadius - vanishRadius) / span);
            }

            double outermost = ballRingRadius + step * (ringsAhead > 0 ? ringsAhead : 1);
            return Clamp01((outermost - ringRadius) / step);
        }

        static double Clamp01(double value) => value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
    }
}
