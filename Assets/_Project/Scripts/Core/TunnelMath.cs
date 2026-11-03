using System;

namespace HR.Core
{
    public static class TunnelMath
    {
        public const double DegPerRad = 180.0 / Math.PI;

        public const double BottomAngleDeg = -90.0;

        public static double NormalizeDeg(double deg)
        {
            double r = Math.IEEERemainder(deg, 360.0);
            return r <= -180.0 ? r + 360.0 : r;
        }

        public static double ShortestDeltaDeg(double fromDeg, double toDeg)
        {
            return NormalizeDeg(toDeg - fromDeg);
        }

        public static double GapWidthDeg(double arcWidth, double radius)
        {
            if (radius <= 0.0) throw new ArgumentOutOfRangeException(nameof(radius));
            return arcWidth / radius * DegPerRad;
        }

        public static double HeldGapWidthDeg(double opening, double thickness, double referenceRadius,
                                             double radius, double minOpening)
        {
            double held = GapWidthDeg(opening + thickness, referenceRadius);

            if (radius >= referenceRadius) return held;

            double floor = GapWidthDeg(minOpening + thickness, radius);
            return held > floor ? held : floor;
        }

        public static double PullFor(double pullPerStep, double step)
        {
            if (step <= 0.0) throw new ArgumentOutOfRangeException(nameof(step));
            return pullPerStep / step;
        }

        public static double CompressionSpeed(double vBase, double kPull, double vMax,
                                              double ballRingRadius, double targetRadius)
        {
            double v = vBase + kPull * (ballRingRadius - targetRadius);
            if (v < vBase) v = vBase;
            return v > vMax ? vMax : v;
        }

        public static double FreeRadius(double ringRadius, double thickness)
        {
            return ringRadius - thickness * 0.5;
        }

        public static bool HasReachedRed(double ringRadius, double thickness, double redRadius)
        {
            return FreeRadius(ringRadius, thickness) <= redRadius;
        }

        public static bool IsOutOfBounds(double distanceToCentre, double ballRingRadius, double ringStep)
        {
            return distanceToCentre > ballRingRadius + 2.0 * ringStep;
        }

        public static bool HasClearedRing(double distanceToCentre, double ringRadius,
                                          double thickness, double passMargin)
        {
            return distanceToCentre > ringRadius + thickness * 0.5 + passMargin;
        }

        public static double TimeBudget(double targetRadius, double thickness, double redRadius,
                                        double vBase)
        {
            if (vBase <= 0.0) throw new ArgumentOutOfRangeException(nameof(vBase));
            return (targetRadius - thickness * 0.5 - redRadius) / vBase;
        }

        public static double ClampAutoSpin(double autoSpinDeg, double comfortSpinDeg)
        {
            if (Math.Abs(autoSpinDeg) < comfortSpinDeg) return autoSpinDeg;
            return Math.Sign(autoSpinDeg) * 0.5 * comfortSpinDeg;
        }

        public static bool IsReachable(double deltaDeg, double autoSpinDeg, double comfortSpinDeg,
                                       double timeBudget)
        {
            double available = comfortSpinDeg - Math.Abs(autoSpinDeg);
            if (available <= 0.0) return false;
            return Math.Abs(NormalizeDeg(deltaDeg)) / available <= 0.6 * timeBudget;
        }

        public static double MaxReachableSpinDeg(double comfortSpinDeg, double timeBudget,
                                                 double worstCaseDeltaDeg = 180.0)
        {
            if (timeBudget <= 0.0) return 0.0;
            double needed = Math.Abs(worstCaseDeltaDeg) / (0.6 * timeBudget);
            double allowed = comfortSpinDeg - needed;
            return allowed > 0.0 ? allowed : 0.0;
        }
    }
}
