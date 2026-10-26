using System.Collections.Generic;

namespace HR.Core
{
    /// <summary>
    /// Every number the tunnel's rules depend on, in one engine-free snapshot. The GameConfig
    /// asset is the editable source; this is what the rules actually read, so the rules can be
    /// tested without an asset and the asset can be checked against the contract.
    ///
    /// Values are CONTRACT.md §2 starting values. They are meant to be tuned by bot simulation;
    /// the invariants in Validate are not.
    /// </summary>
    public readonly struct TunnelSettings
    {
        public readonly double Thickness;
        public readonly double Clearance;
        public readonly double TargetRadius;
        public readonly double VanishRadius;
        public readonly double BallRadius;
        public readonly double CrushMargin;
        public readonly double PassMargin;
        public readonly double MinGapWidth;
        public readonly double PullToCentre;
        public readonly double MaxCompressionSpeed;
        public readonly double ComfortSpinDeg;
        public readonly double MaxBallSpeed;
        public readonly double MaxArcStepDeg;
        public readonly double MaxSegmentSweepDeg;
        public readonly int RingsAhead;
        public readonly int CapPoints;

        public TunnelSettings(double thickness, double clearance, double targetRadius, double vanishRadius,
                              double ballRadius, double crushMargin, double passMargin, double minGapWidth,
                              double pullToCentre, double maxCompressionSpeed, double comfortSpinDeg,
                              double maxBallSpeed, double maxArcStepDeg, double maxSegmentSweepDeg,
                              int ringsAhead, int capPoints)
        {
            Thickness = thickness;
            Clearance = clearance;
            TargetRadius = targetRadius;
            VanishRadius = vanishRadius;
            BallRadius = ballRadius;
            CrushMargin = crushMargin;
            PassMargin = passMargin;
            MinGapWidth = minGapWidth;
            PullToCentre = pullToCentre;
            MaxCompressionSpeed = maxCompressionSpeed;
            ComfortSpinDeg = comfortSpinDeg;
            MaxBallSpeed = maxBallSpeed;
            MaxArcStepDeg = maxArcStepDeg;
            MaxSegmentSweepDeg = maxSegmentSweepDeg;
            RingsAhead = ringsAhead;
            CapPoints = capPoints;
        }

        /// <summary>Distance between the centre lines of neighbouring rings.</summary>
        public double Step => Clearance + Thickness;

        /// <summary>Radius at which a ring holding the ball crushes it.</summary>
        public double CrushRadius => BallRadius + CrushMargin + Thickness * 0.5;

        public static TunnelSettings Default => new TunnelSettings(
            thickness: 0.30, clearance: 1.25, targetRadius: 3.0, vanishRadius: 0.9,
            ballRadius: 0.5, crushMargin: 0.05, passMargin: 0.3, minGapWidth: 1.5,
            pullToCentre: 3.0, maxCompressionSpeed: 4.0, comfortSpinDeg: 200.0,
            maxBallSpeed: 12.0, maxArcStepDeg: 7.5, maxSegmentSweepDeg: 120.0,
            ringsAhead: 3, capPoints: 4);

        /// <summary>
        /// Relationships that must hold whatever the balance pass does to the numbers. An empty
        /// list means the configuration is playable; anything else would produce a tunnel the
        /// ball cannot physically use, which is far cheaper to catch here than in a simulation.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            if (Thickness <= 0) problems.Add("thickness must be positive");
            if (BallRadius <= 0) problems.Add("ball radius must be positive");

            // The corridor has to admit the ball with room to move, or it is a wall.
            if (Clearance <= BallRadius * 2)
                problems.Add($"clearance {Clearance} does not fit a ball of diameter {BallRadius * 2}");

            // Narrower than the ball and no gap is ever passable, whatever the difficulty curve says.
            if (MinGapWidth < BallRadius * 2)
                problems.Add($"minimum gap {MinGapWidth} is narrower than the ball");

            // A ring must be able to reach the crush radius before it would be culled, or the
            // lose condition can never fire.
            if (VanishRadius <= CrushRadius)
                problems.Add($"vanish radius {VanishRadius} is at or inside the crush radius {CrushRadius}");

            if (TargetRadius <= CrushRadius)
                problems.Add($"target radius {TargetRadius} leaves no time budget");

            if (RingsAhead < 1) problems.Add("at least one ring ahead is required");
            if (MaxArcStepDeg <= 0) problems.Add("arc step must be positive");
            if (MaxSegmentSweepDeg <= 0 || MaxSegmentSweepDeg > 180)
                problems.Add("segment sweep must be in (0, 180]");
            if (CapPoints < 0) problems.Add("cap points cannot be negative");
            if (MaxBallSpeed <= 0) problems.Add("ball speed limit must be positive");
            if (MaxCompressionSpeed <= 0) problems.Add("compression cap must be positive");
            if (ComfortSpinDeg <= 0) problems.Add("comfortable spin must be positive");

            return problems;
        }
    }
}
