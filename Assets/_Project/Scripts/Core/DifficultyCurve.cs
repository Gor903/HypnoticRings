using System;
using System.Collections.Generic;

namespace HR.Core
{
    public readonly struct CurveAnchor
    {
        public readonly int Rings;

        public readonly double Clearance;

        public readonly double RingInterval;

        public readonly double MaxCompressionSpeed;
        public readonly double GapArcWidth;
        public readonly double AutoSpinDeg;

        public readonly double MultiGapChance;

        public readonly double TripleShare;

        public CurveAnchor(int rings, double clearance, double ringInterval,
                           double maxCompressionSpeed, double gapArcWidth, double autoSpinDeg,
                           double multiGapChance = 0.0, double tripleShare = 0.0)
        {
            Rings = rings;
            Clearance = clearance;
            RingInterval = ringInterval;
            MaxCompressionSpeed = maxCompressionSpeed;
            GapArcWidth = gapArcWidth;
            AutoSpinDeg = autoSpinDeg;
            MultiGapChance = multiGapChance;
            TripleShare = tripleShare;
        }
    }

    public sealed class DifficultyCurve
    {
        public static IReadOnlyList<CurveAnchor> DefaultAnchors { get; } = new[]
        {
            new CurveAnchor(0, 1.10, 3.00, 2.0, 4.32, 0.0, 0.00, 0.00),
            new CurveAnchor(10, 1.05, 2.45, 3.0, 3.90, 15.0, 0.25, 0.00),
            new CurveAnchor(30, 0.96, 1.93, 4.0, 3.48, 40.0, 0.36, 0.25),
            new CurveAnchor(80, 0.72, 1.59, 5.0, 2.80, 70.0, 0.40, 0.25),
        };

        public const int TutorialRings = 3;

        public const int AlternatingFrom = 10;
        public const int RandomDirectionFrom = 30;

        readonly TunnelSettings _settings;
        readonly CurveAnchor[] _anchors;

        public DifficultyCurve(in TunnelSettings settings, IReadOnlyList<CurveAnchor> anchors = null)
        {
            _settings = settings;

            var rows = anchors ?? DefaultAnchors;
            var problems = Validate(rows);
            if (problems.Count > 0)
                throw new ArgumentException("difficulty table is unusable: " + string.Join("; ", problems));

            _anchors = new CurveAnchor[rows.Count];
            for (int i = 0; i < rows.Count; i++) _anchors[i] = rows[i];

            var against = ValidateAgainstSettings();
            if (against.Count > 0)
                throw new ArgumentException("difficulty table does not fit this tunnel: "
                                            + string.Join("; ", against));
        }

        public static List<string> Validate(IReadOnlyList<CurveAnchor> anchors)
        {
            var problems = new List<string>();
            if (anchors == null || anchors.Count == 0)
            {
                problems.Add("a table needs at least one row");
                return problems;
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                if (i > 0 && anchors[i].Rings <= anchors[i - 1].Rings)
                    problems.Add($"row {i} is at {anchors[i].Rings} rings, not past {anchors[i - 1].Rings}");
                if (anchors[i].RingInterval <= 0.0)
                    problems.Add($"row {i} asks for a ring every {anchors[i].RingInterval} seconds");
                if (anchors[i].Clearance <= 0.0)
                    problems.Add($"row {i} has a corridor of {anchors[i].Clearance}");
                if (anchors[i].MaxCompressionSpeed <= 0.0)
                    problems.Add($"row {i} caps retraction at {anchors[i].MaxCompressionSpeed}");
                if (anchors[i].GapArcWidth <= 0.0)
                    problems.Add($"row {i} has an opening of {anchors[i].GapArcWidth}");
            }

            return problems;
        }

        List<string> ValidateAgainstSettings()
        {
            var problems = new List<string>();
            double floor = _settings.BallRadius * 2.0 * TunnelSettings.CorridorFloorInDiameters;

            for (int i = 0; i < _anchors.Length; i++)
            {
                var row = _anchors[i];
                if (row.Clearance < floor)
                    problems.Add($"row {i} corridor {row.Clearance} is under the floor {floor:F3}");

                double vBase = _settings.StepFor(row.Clearance) / row.RingInterval;
                if (row.MaxCompressionSpeed < vBase)
                    problems.Add($"row {i} caps retraction at {row.MaxCompressionSpeed}, below the "
                                 + $"{vBase:F3} its own corridor and interval produce");
            }

            return problems;
        }

        public int Ceiling => _anchors[_anchors.Length - 1].Rings;

        public double ClearanceAt(int rings)
        {
            double floor = _settings.BallRadius * 2.0 * TunnelSettings.CorridorFloorInDiameters;
            double sampled = Sample(rings, a => a.Clearance);
            return sampled < floor ? floor : sampled;
        }

        public double StepAt(int rings) => _settings.StepFor(ClearanceAt(rings));

        public double RingIntervalAt(int rings) => Sample(rings, a => a.RingInterval);

        public double CompressionSpeedAt(int rings) => StepAt(rings) / RingIntervalAt(rings);

        public double MaxCompressionSpeedAt(int rings) => Sample(rings, a => a.MaxCompressionSpeed);

        public double MultiGapChanceAt(int rings) => Sample(rings, a => a.MultiGapChance);
        public double TripleShareAt(int rings) => Sample(rings, a => a.TripleShare);
        public double GapArcWidthAt(int rings) => Math.Max(_settings.MinGapWidth, Sample(rings, a => a.GapArcWidth));

        public double AutoSpinAt(int rings)
        {
            if (rings < TutorialRings) return 0.0;
            return Sample(rings, a => a.AutoSpinDeg);
        }

        public double TimeBudgetAt(int rings) => TunnelMath.TimeBudget(
            _settings.TargetRadius, _settings.Thickness, _settings.RedRadius,
            CompressionSpeedAt(rings));

        public DifficultySnapshot SnapshotAt(int rings) => new DifficultySnapshot(
            CompressionSpeedAt(rings),
            MaxCompressionSpeedAt(rings),
            GapArcWidthAt(rings),
            AutoSpinAt(rings),
            rings >= AlternatingFrom && rings < RandomDirectionFrom,
            TimeBudgetAt(rings),
            _settings.ComfortSpinDeg,
            ClearanceAt(rings),
            StepAt(rings),
            MultiGapChanceAt(rings),
            TripleShareAt(rings),
            _settings.Thickness,
            _settings.TargetRadius,
            _settings.BallRadius);

        double Sample(int rings, Func<CurveAnchor, double> field)
        {
            if (rings <= _anchors[0].Rings) return field(_anchors[0]);

            for (int i = 1; i < _anchors.Length; i++)
            {
                if (rings > _anchors[i].Rings) continue;

                var from = _anchors[i - 1];
                var to = _anchors[i];
                double t = (double)(rings - from.Rings) / (to.Rings - from.Rings);
                return field(from) + (field(to) - field(from)) * SmoothStep(t);
            }

            return field(_anchors[_anchors.Length - 1]);
        }

        static double SmoothStep(double t) => t * t * (3.0 - 2.0 * t);
    }
}
