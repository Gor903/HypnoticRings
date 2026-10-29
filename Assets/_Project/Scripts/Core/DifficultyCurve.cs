using System;
using System.Collections.Generic;

namespace HR.Core
{
    /// <summary>One row of the balance table, engine-free.</summary>
    public readonly struct CurveAnchor
    {
        public readonly int Rings;
        public readonly double CompressionSpeed;
        public readonly double MaxCompressionSpeed;
        public readonly double GapArcWidth;
        public readonly double AutoSpinDeg;

        public CurveAnchor(int rings, double compressionSpeed, double maxCompressionSpeed,
                           double gapArcWidth, double autoSpinDeg)
        {
            Rings = rings;
            CompressionSpeed = compressionSpeed;
            MaxCompressionSpeed = maxCompressionSpeed;
            GapArcWidth = gapArcWidth;
            AutoSpinDeg = autoSpinDeg;
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
            new CurveAnchor(0, 0.45, 2.0, 4.10, 0.0),
            new CurveAnchor(10, 0.55, 3.0, 3.70, 15.0),
            new CurveAnchor(30, 0.70, 4.0, 3.27, 40.0),
            new CurveAnchor(80, 0.85, 5.0, 2.60, 70.0),
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
                if (anchors[i].CompressionSpeed <= 0.0)
                    problems.Add($"row {i} has a compression speed of {anchors[i].CompressionSpeed}");
                if (anchors[i].MaxCompressionSpeed < anchors[i].CompressionSpeed)
                    problems.Add($"row {i} caps retraction at {anchors[i].MaxCompressionSpeed}, "
                                 + $"below its own speed {anchors[i].CompressionSpeed}");
                if (anchors[i].GapArcWidth <= 0.0)
                    problems.Add($"row {i} has an opening of {anchors[i].GapArcWidth}");
            }

            return problems;
        }

        public int Ceiling => _anchors[_anchors.Length - 1].Rings;

        public double CompressionSpeedAt(int rings) => Sample(rings, a => a.CompressionSpeed);
        public double MaxCompressionSpeedAt(int rings) => Sample(rings, a => a.MaxCompressionSpeed);
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
            _settings.ComfortSpinDeg);

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
