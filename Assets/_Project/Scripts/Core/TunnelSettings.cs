using System.Collections.Generic;

namespace HR.Core
{
    public readonly struct TunnelSettings
    {
        public readonly double Thickness;
        public readonly double Clearance;
        public readonly double TargetRadius;
        public readonly double VanishRadius;
        public readonly double BallRadius;
        public readonly double RedRadius;
        public readonly double PassMargin;
        public readonly double MinGapWidth;

        public readonly double PullPerStep;
        public readonly double MaxCompressionSpeed;
        public readonly double ComfortSpinDeg;
        public readonly double MaxBallSpeed;
        public readonly double MaxArcStepDeg;
        public readonly double MaxSegmentSweepDeg;
        public readonly int RingsAhead;
        public readonly int CapPoints;

        public TunnelSettings(double thickness, double clearance, double targetRadius, double vanishRadius,
                              double ballRadius, double redRadius, double passMargin, double minGapWidth,
                              double pullPerStep, double maxCompressionSpeed, double comfortSpinDeg,
                              double maxBallSpeed, double maxArcStepDeg, double maxSegmentSweepDeg,
                              int ringsAhead, int capPoints)
        {
            Thickness = thickness;
            Clearance = clearance;
            TargetRadius = targetRadius;
            VanishRadius = vanishRadius;
            BallRadius = ballRadius;
            RedRadius = redRadius;
            PassMargin = passMargin;
            MinGapWidth = minGapWidth;
            PullPerStep = pullPerStep;
            MaxCompressionSpeed = maxCompressionSpeed;
            ComfortSpinDeg = comfortSpinDeg;
            MaxBallSpeed = maxBallSpeed;
            MaxArcStepDeg = maxArcStepDeg;
            MaxSegmentSweepDeg = maxSegmentSweepDeg;
            RingsAhead = ringsAhead;
            CapPoints = capPoints;
        }

        public const double CorridorFloorInDiameters = 1.3;

        public double Step => Clearance + Thickness;

        public double StepFor(double clearance) => clearance + Thickness;

        public double CrushRadius => RedRadius + Thickness * 0.5;

        public static TunnelSettings Default => new TunnelSettings(
            thickness: 0.18, clearance: 1.10, targetRadius: 3.0, vanishRadius: 1.05,
            ballRadius: 0.27, redRadius: 0.75, passMargin: 0.22, minGapWidth: 0.80,
            pullPerStep: 4.05, maxCompressionSpeed: 5.0, comfortSpinDeg: 300.0,
            maxBallSpeed: 14.0, maxArcStepDeg: 7.5, maxSegmentSweepDeg: 120.0,
            ringsAhead: 9, capPoints: 4);

        public List<string> Validate()
        {
            var problems = new List<string>();

            if (Thickness <= 0) problems.Add("thickness must be positive");
            if (BallRadius <= 0) problems.Add("ball radius must be positive");

            if (Clearance < BallRadius * 2 * CorridorFloorInDiameters)
                problems.Add($"clearance {Clearance} is under {CorridorFloorInDiameters} ball "
                             + $"diameters ({BallRadius * 2 * CorridorFloorInDiameters:F3})");

            if (MinGapWidth < BallRadius * 2)
                problems.Add($"minimum gap {MinGapWidth} is narrower than the ball");

            if (RedRadius < BallRadius)
                problems.Add($"red circle {RedRadius} is smaller than the ball {BallRadius}");

            if (VanishRadius <= CrushRadius)
                problems.Add($"vanish radius {VanishRadius} is at or inside the losing radius {CrushRadius}");

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
