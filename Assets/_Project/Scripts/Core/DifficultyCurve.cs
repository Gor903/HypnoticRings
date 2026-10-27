using System;

namespace HR.Core
{
    /// <summary>
    /// Turns "how many rings have been cleared" into how hard the next one is.
    ///
    /// The shape comes from CONTRACT.md §2.7, which gives four anchor points and requires the
    /// result to be smooth: monotonic, and never stepping far between neighbouring rings. Both
    /// are properties this class is tested against rather than hoped for.
    ///
    /// Engine-free, so a balance sweep can run thousands of curves without a scene.
    /// </summary>
    public sealed class DifficultyCurve
    {
        /// <summary>One row of the contract's table.</summary>
        readonly struct Anchor
        {
            public readonly int Rings;
            public readonly double CompressionSpeed;
            public readonly double GapArcWidth;
            public readonly double AutoSpinDeg;

            public Anchor(int rings, double compressionSpeed, double gapArcWidth, double autoSpinDeg)
            {
                Rings = rings;
                CompressionSpeed = compressionSpeed;
                GapArcWidth = gapArcWidth;
                AutoSpinDeg = autoSpinDeg;
            }
        }

        // CONTRACT.md §2.7. The last anchor is the ceiling: past it nothing gets harder, because
        // an endless game that keeps tightening eventually becomes a coin flip rather than a test.
        static readonly Anchor[] Anchors =
        {
            new Anchor(0, 0.15, 3.2, 0.0),
            new Anchor(10, 0.22, 2.8, 12.0),
            new Anchor(30, 0.38, 2.2, 35.0),
            new Anchor(80, 0.60, 1.6, 70.0),
        };

        /// <summary>Rings played with the training wheels on — no spin at all (CONTRACT.md §2.7).</summary>
        public const int TutorialRings = 3;

        /// <summary>Neighbouring rings start alternating direction here, and go random later.</summary>
        public const int AlternatingFrom = 10;
        public const int RandomDirectionFrom = 30;

        readonly TunnelSettings _settings;

        public DifficultyCurve(in TunnelSettings settings)
        {
            _settings = settings;
        }

        public static int Ceiling => Anchors[Anchors.Length - 1].Rings;

        public double CompressionSpeedAt(int rings) => Sample(rings, a => a.CompressionSpeed);
        public double GapArcWidthAt(int rings) => Math.Max(_settings.MinGapWidth, Sample(rings, a => a.GapArcWidth));

        public double AutoSpinAt(int rings)
        {
            if (rings < TutorialRings) return 0.0;
            return Sample(rings, a => a.AutoSpinDeg);
        }

        /// <summary>Seconds from an equilibrium pass to being crushed, at this point in the run.</summary>
        public double TimeBudgetAt(int rings) => TunnelMath.TimeBudget(
            _settings.TargetRadius, _settings.Thickness, _settings.BallRadius,
            _settings.CrushMargin, CompressionSpeedAt(rings));

        public DifficultySnapshot SnapshotAt(int rings) => new DifficultySnapshot(
            CompressionSpeedAt(rings),
            GapArcWidthAt(rings),
            AutoSpinAt(rings),
            rings >= AlternatingFrom && rings < RandomDirectionFrom,
            TimeBudgetAt(rings),
            _settings.ComfortSpinDeg);

        /// <summary>
        /// Smooth step between anchors. Linear would satisfy the letter of "no large jumps" but
        /// would change gradient abruptly at every anchor, which reads as the game lurching.
        /// </summary>
        double Sample(int rings, Func<Anchor, double> field)
        {
            if (rings <= Anchors[0].Rings) return field(Anchors[0]);

            for (int i = 1; i < Anchors.Length; i++)
            {
                if (rings > Anchors[i].Rings) continue;

                var from = Anchors[i - 1];
                var to = Anchors[i];
                double t = (double)(rings - from.Rings) / (to.Rings - from.Rings);
                return field(from) + (field(to) - field(from)) * SmoothStep(t);
            }

            return field(Anchors[Anchors.Length - 1]);
        }

        /// <summary>Ease in and out, flat gradient at both ends so anchors do not show as corners.</summary>
        static double SmoothStep(double t) => t * t * (3.0 - 2.0 * t);
    }
}
