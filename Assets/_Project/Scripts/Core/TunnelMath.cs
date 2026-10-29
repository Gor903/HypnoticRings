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

        /// <summary>
        /// Where the bot aims a gap, and nothing more.
        ///
        /// It used to be the only way out: the ball lay at the bottom of its ring and could
        /// leave only straight down. The ball now flies, so any gap it reaches is an exit and
        /// no rule in the game refers to this angle. The bot and the balance simulation still
        /// need somewhere to aim, and the bottom is as good as any.
        /// </summary>
        public const double BottomAngleDeg = -90.0;

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
        /// radius <paramref name="radius"/>. Pure conversion between the two ways of saying how
        /// wide an opening is; which of them a ring holds constant is <see cref="HeldGapWidthDeg"/>.
        /// </summary>
        public static double GapWidthDeg(double arcWidth, double radius)
        {
            if (radius <= 0.0) throw new ArgumentOutOfRangeException(nameof(radius));
            return arcWidth / radius * DegPerRad;
        }

        /// <summary>
        /// The angle a ring actually cuts at its current radius.
        ///
        /// The designer's number is an opening in world units, read at <paramref name="referenceRadius"/>,
        /// and what is held constant as the ring shrinks is the ANGLE rather than that arc. Holding
        /// the arc is what the tunnel used to do, and it means a ring has to open wider in degrees
        /// the smaller it gets — by the time one reaches the middle of the screen a third of it is
        /// missing, which reads as the ring falling apart rather than closing in. See DECISIONS D-059.
        ///
        /// The floor is the single exception, and it is physical rather than aesthetic: an opening
        /// narrower than <paramref name="minOpening"/> is a wall, so once a fixed angle would pinch
        /// below that the angle grows again — at exactly the rate that keeps the way out open, and
        /// no faster.
        /// </summary>
        public static double HeldGapWidthDeg(double opening, double thickness, double referenceRadius,
                                             double radius, double minOpening)
        {
            double held = GapWidthDeg(opening + thickness, referenceRadius);

            // At or outside the reference radius a held angle already delivers at least the
            // opening it was drawn with, so there is nothing to rescue — and applying the floor
            // anyway would quietly widen a ring that was deliberately built too narrow to pass.
            if (radius >= referenceRadius) return held;

            double floor = GapWidthDeg(minOpening + thickness, radius);
            return held > floor ? held : floor;
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
        /// The run is lost when a ring's inner edge reaches the red circle drawn at the centre.
        ///
        /// Geometric, and deliberately independent of where the ball is: the circle is drawn at
        /// exactly this radius, so the player watches the boundary close rather than being told
        /// afterwards that something touched something.
        /// </summary>
        public static bool HasReachedRed(double ringRadius, double thickness, double redRadius)
        {
            return FreeRadius(ringRadius, thickness) <= redRadius;
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
        /// Seconds from an equilibrium pass to the ring reaching the red circle. The main
        /// difficulty dial — it is how long the player has to turn a gap into the flying ball's
        /// path before the ring closes on the centre.
        /// </summary>
        public static double TimeBudget(double targetRadius, double thickness, double redRadius,
                                        double vBase)
        {
            if (vBase <= 0.0) throw new ArgumentOutOfRangeException(nameof(vBase));
            return (targetRadius - thickness * 0.5 - redRadius) / vBase;
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
        ///
        /// Worst case is still measured as half a turn away, which was exact while the ball lay
        /// at the bottom. A flying ball meets gaps at other angles too, so the rule is now
        /// conservative rather than exact — see DECISIONS D-058.
        /// </summary>
        public static bool IsReachable(double deltaDeg, double autoSpinDeg, double comfortSpinDeg,
                                       double timeBudget)
        {
            double available = comfortSpinDeg - Math.Abs(autoSpinDeg);
            if (available <= 0.0) return false;
            return Math.Abs(NormalizeDeg(deltaDeg)) / available <= 0.6 * timeBudget;
        }

        /// <summary>
        /// Fastest a ring may spin and still be catchable from the worst starting position.
        ///
        /// Solving the reachability rule for the spin rather than testing a particular gap is
        /// what makes the guarantee unconditional: a ring under this limit can be caught wherever
        /// its gap happens to be, so the generator never has to know where the ball will be when
        /// the ball finally gets there — which, several passes after the ring was made, it cannot.
        ///
        /// Returns zero when even a still ring could not be caught in time; that is a statement
        /// about the difficulty settings, not about this ring.
        /// </summary>
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
