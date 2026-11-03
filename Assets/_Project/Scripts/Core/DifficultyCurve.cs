using System;
using System.Collections.Generic;

namespace HR.Core
{
    /// <summary>One row of the balance table, engine-free.</summary>
    public readonly struct CurveAnchor
    {
        public readonly int Rings;

        /// <summary>Corridor width between neighbouring rings. Tightens as a run goes on.</summary>
        public readonly double Clearance;

        /// <summary>
        /// Seconds a ring is meant to last. This is the dial a designer actually has an opinion
        /// about — "a ring every two seconds" — whereas the compression speed that produces it
        /// is arithmetic, and arithmetic that changes whenever the corridor does.
        /// </summary>
        public readonly double RingInterval;

        public readonly double MaxCompressionSpeed;
        public readonly double GapArcWidth;
        public readonly double AutoSpinDeg;

        /// <summary>Chance a ring is cut with more than one opening, 0 to 1.</summary>
        public readonly double MultiGapChance;

        /// <summary>Of those, the share cut with three rather than two.</summary>
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

    /// <summary>
    /// Turns "how many rings have been cleared" into how hard the next one is.
    ///
    /// The shape comes from CONTRACT.md §2.7, which gives four anchor points and requires the
    /// result to be smooth: monotonic, and never stepping far between neighbouring rings. Both
    /// are properties this class is tested against rather than hoped for.
    ///
    /// The rows themselves live in a DifficultyTable asset rather than here — they are balance
    /// numbers, and CLAUDE.md puts those in an asset. Defaults are kept so a test or a sweep can
    /// build a curve without one.
    ///
    /// Engine-free, so a balance sweep can run thousands of curves without a scene.
    /// </summary>
    public sealed class DifficultyCurve
    {
        /// <summary>
        /// CONTRACT.md §2.7 as it stands after D-058: the timer and the retraction ceiling are
        /// two dials rather than one. The last anchor is the ceiling — past it nothing gets
        /// harder, because an endless game that keeps tightening eventually becomes a coin flip
        /// rather than a test.
        /// </summary>
        public static IReadOnlyList<CurveAnchor> DefaultAnchors { get; } = new[]
        {
            new CurveAnchor(0, 1.10, 3.00, 2.0, 4.32, 0.0, 0.00, 0.00),
            new CurveAnchor(10, 1.05, 2.45, 3.0, 3.90, 15.0, 0.25, 0.00),
            new CurveAnchor(30, 0.96, 1.93, 4.0, 3.48, 40.0, 0.36, 0.25),
            new CurveAnchor(80, 0.72, 1.59, 5.0, 2.80, 70.0, 0.40, 0.25),
        };

        /// <summary>Rings played with the training wheels on — no spin at all (CONTRACT.md §2.7).</summary>
        public const int TutorialRings = 3;

        /// <summary>Neighbouring rings start alternating direction here, and go random later.</summary>
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

        /// <summary>
        /// What a table has to satisfy to be read at all. Ascending rows with positive speeds —
        /// anything else makes Sample either skip a row or divide by zero, and a balance pass
        /// that mistypes a row deserves to be told rather than to get a quietly wrong curve.
        /// </summary>
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

        /// <summary>
        /// What a table has to satisfy against a particular tunnel, as opposed to on its own.
        ///
        /// Separate from <see cref="Validate"/> because these two questions need the thickness
        /// and the ball, and a row cannot answer them alone: the same interval is a sensible
        /// speed on one tunnel and an impossible one on another.
        /// </summary>
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

        /// <summary>Corridor width at this point in a run, never below the playable floor.</summary>
        public double ClearanceAt(int rings)
        {
            double floor = _settings.BallRadius * 2.0 * TunnelSettings.CorridorFloorInDiameters;
            double sampled = Sample(rings, a => a.Clearance);
            return sampled < floor ? floor : sampled;
        }

        /// <summary>Distance between neighbouring rings at this point in a run.</summary>
        public double StepAt(int rings) => _settings.StepFor(ClearanceAt(rings));

        public double RingIntervalAt(int rings) => Sample(rings, a => a.RingInterval);

        /// <summary>
        /// Derived, not stored: the speed that delivers a ring every <see cref="RingIntervalAt"/>
        /// seconds across a corridor of <see cref="StepAt"/>. Storing it as well would be a third
        /// number that has to be kept consistent with the other two by hand, and the first time
        /// somebody tightened the corridor without touching it the tunnel would quietly change
        /// tempo.
        /// </summary>
        public double CompressionSpeedAt(int rings) => StepAt(rings) / RingIntervalAt(rings);

        public double MaxCompressionSpeedAt(int rings) => Sample(rings, a => a.MaxCompressionSpeed);

        /// <summary>
        /// Chance of more than one opening. Flat zero until the first anchor that asks for it,
        /// because a second way out on ring three teaches the wrong lesson about rings.
        /// </summary>
        public double MultiGapChanceAt(int rings) => Sample(rings, a => a.MultiGapChance);
        public double TripleShareAt(int rings) => Sample(rings, a => a.TripleShare);
        public double GapArcWidthAt(int rings) => Math.Max(_settings.MinGapWidth, Sample(rings, a => a.GapArcWidth));

        public double AutoSpinAt(int rings)
        {
            if (rings < TutorialRings) return 0.0;
            return Sample(rings, a => a.AutoSpinDeg);
        }

        /// <summary>Seconds from an equilibrium pass to the ring reaching the red circle.</summary>
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
            _settings.TargetRadius);

        /// <summary>
        /// Smooth step between anchors. Linear would satisfy the letter of "no large jumps" but
        /// would change gradient abruptly at every anchor, which reads as the game lurching.
        /// </summary>
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

        /// <summary>Ease in and out, flat gradient at both ends so anchors do not show as corners.</summary>
        static double SmoothStep(double t) => t * t * (3.0 - 2.0 * t);
    }
}
