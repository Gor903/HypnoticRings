using System;

namespace HR.Core
{
    /// <summary>
    /// Every geometric rule the tunnel obeys, as pure functions.
    /// Engine-free on purpose: this is what makes the rules testable in
    /// milliseconds instead of by standing up a player. See CONTRACT.md §2.
    /// </summary>
    public static class TunnelMath
    {
        public const double DegPerRad = 180.0 / Math.PI;

        /// <summary>Folds any angle into (-180, 180].</summary>
        public static double NormalizeDeg(double deg)
        {
            // Math.IEEERemainder already lands in [-180, 180]; the branch moves the
            // open end so that -180 reads as +180 and the range stays half-open.
            double r = Math.IEEERemainder(deg, 360.0);
            return r <= -180.0 ? r + 360.0 : r;
        }

        /// <summary>Signed shortest rotation from <paramref name="fromDeg"/> to <paramref name="toDeg"/>.</summary>
        public static double ShortestDeltaDeg(double fromDeg, double toDeg)
        {
            return NormalizeDeg(toDeg - fromDeg);
        }

        /// <summary>
        /// Angular width of a gap whose arc length is <paramref name="arcWidth"/> on a ring of
        /// radius <paramref name="radius"/>. This is the rule that keeps a shrinking tunnel fair:
        /// the arc length is what the designer sets, so a smaller radius widens the angle instead
        /// of narrowing the opening.
        /// </summary>
        public static double GapWidthDeg(double arcWidth, double radius)
        {
            if (radius <= 0.0) throw new ArgumentOutOfRangeException(nameof(radius));
            return arcWidth / radius * DegPerRad;
        }

        /// <summary>True when <paramref name="angleDeg"/> falls inside the gap.</summary>
        public static bool IsInsideGap(double angleDeg, double gapCentreDeg, double arcWidth, double radius)
        {
            double half = GapWidthDeg(arcWidth, radius) * 0.5;
            return Math.Abs(ShortestDeltaDeg(gapCentreDeg, angleDeg)) <= half;
        }

        /// <summary>
        /// Shrink speed shared by every ring. The pull term is what retracts the tunnel after a
        /// pass: clearing a ring raises R_E by one step, which spikes v_eff for about a second.
        /// </summary>
        public static double CompressionSpeed(double vBase, double kPull, double vMax,
                                              double ballRingRadius, double targetRadius)
        {
            double v = vBase + kPull * (ballRingRadius - targetRadius);
            if (v < vBase) v = vBase;
            return v > vMax ? vMax : v;
        }

        /// <summary>Free radius of the disc inside a ring — the space the ball still has.</summary>
        public static double FreeRadius(double ringRadius, double thickness)
        {
            return ringRadius - thickness * 0.5;
        }

        /// <summary>
        /// Crushed when the cavity inside the ball's ring has closed to the ball's own size.
        /// Geometric by design: collision-driven detection fires on glancing contact too.
        /// Only valid once the ring has no ring left inside it.
        /// </summary>
        public static bool IsCrushed(double ringRadius, double thickness, double ballRadius, double crushMargin)
        {
            return FreeRadius(ringRadius, thickness) <= ballRadius + crushMargin;
        }

        /// <summary>Bug guard: the ball should never get this far out.</summary>
        public static bool IsOutOfBounds(double distanceToCentre, double ballRingRadius, double ringStep)
        {
            return distanceToCentre > ballRingRadius + 2.0 * ringStep;
        }

        /// <summary>
        /// First half of "the ball cleared ring E": its centre is past the ring's outer edge.
        /// The caller still has to confirm the ball reached the next corridor before scoring.
        /// </summary>
        public static bool HasClearedRing(double distanceToCentre, double ringRadius,
                                          double thickness, double passMargin)
        {
            return distanceToCentre > ringRadius + thickness * 0.5 + passMargin;
        }

        /// <summary>
        /// Seconds from an equilibrium pass to being crushed. The main difficulty dial — it is the
        /// time the player has to line a gap up under the ball.
        /// </summary>
        public static double TimeBudget(double targetRadius, double thickness, double ballRadius,
                                        double crushMargin, double vBase)
        {
            if (vBase <= 0.0) throw new ArgumentOutOfRangeException(nameof(vBase));
            return (targetRadius - thickness * 0.5 - ballRadius - crushMargin) / vBase;
        }

        /// <summary>
        /// A ring spinning at or past comfortable finger speed can never be caught, so the
        /// generator slows it rather than emitting an impossible ring.
        /// </summary>
        public static double ClampAutoSpin(double autoSpinDeg, double comfortSpinDeg)
        {
            if (Math.Abs(autoSpinDeg) < comfortSpinDeg) return autoSpinDeg;
            return Math.Sign(autoSpinDeg) * 0.5 * comfortSpinDeg;
        }

        /// <summary>
        /// The generator's hard rule: a gap this far away, on a ring spinning this fast, must be
        /// reachable inside 60% of the time budget. Pass <paramref name="autoSpinDeg"/> already
        /// clamped — see <see cref="ClampAutoSpin"/>.
        /// </summary>
        public static bool IsReachable(double deltaDeg, double autoSpinDeg, double comfortSpinDeg,
                                       double timeBudget)
        {
            double available = comfortSpinDeg - Math.Abs(autoSpinDeg);
            if (available <= 0.0) return false;
            return Math.Abs(NormalizeDeg(deltaDeg)) / available <= 0.6 * timeBudget;
        }
    }
}
