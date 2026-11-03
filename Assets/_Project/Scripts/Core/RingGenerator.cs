using System;

namespace HR.Core
{
    public enum RingKind
    {
        Plain = 0,
        MultiGap = 1,
    }

    public struct RingSpec
    {
        public RingGaps Gaps;
        public RingKind Kind;

        public double AutoSpinDeg;
        public int ColourIndex;

        public double GapCentreDeg => Gaps.A.CentreDeg;
        public double GapArcWidth => Gaps.A.Opening;
    }

    public readonly struct DifficultySnapshot
    {
        public readonly double BaseCompressionSpeed;

        public readonly double MaxCompressionSpeed;
        public readonly double GapArcWidth;
        public readonly double AutoSpinDeg;
        public readonly bool AlternateSpinDirection;

        public readonly double TimeBudget;

        public readonly double ComfortSpinDeg;

        public readonly double MultiGapChance;

        public readonly double TripleShare;

        public readonly double Thickness;
        public readonly double ReferenceRadius;

        public readonly double Clearance;

        public readonly double BallRadius;

        public readonly double Step;

        public DifficultySnapshot(double baseCompressionSpeed, double maxCompressionSpeed,
                                  double gapArcWidth, double autoSpinDeg,
                                  bool alternateSpinDirection,
                                  double timeBudget, double comfortSpinDeg,
                                  double clearance, double step,
                                  double multiGapChance = 0.0, double tripleShare = 0.0,
                                  double thickness = 0.18, double referenceRadius = 3.0,
                                  double ballRadius = 0.27)
        {
            BallRadius = ballRadius;
            MultiGapChance = multiGapChance;
            TripleShare = tripleShare;
            Thickness = thickness;
            ReferenceRadius = referenceRadius;
            BaseCompressionSpeed = baseCompressionSpeed;
            MaxCompressionSpeed = maxCompressionSpeed;
            GapArcWidth = gapArcWidth;
            AutoSpinDeg = autoSpinDeg;
            AlternateSpinDirection = alternateSpinDirection;
            TimeBudget = timeBudget;
            ComfortSpinDeg = comfortSpinDeg;
            Clearance = clearance;
            Step = step;
        }

        public static DifficultySnapshot Opening =>
            new DifficultyCurve(TunnelSettings.Default).SnapshotAt(0);
    }

    public sealed class RingGenerator
    {
        readonly int _seed;
        Random _random;
        int _produced;

        public RingGenerator(int seed)
        {
            _seed = seed;
            _random = new Random(seed);
        }

        public int Seed => _seed;
        public int Produced => _produced;

        public int Clamped { get; private set; }

        readonly double[] _angles = new double[RingGaps.MaxGaps];

        int _multiGapRun;

        public const int MaxMultiGapRun = 2;

        public RingSpec Next(in DifficultySnapshot difficulty, double previousAutoSpinDeg)
        {
            int gapCount = ChooseGapCount(difficulty);

            var spec = new RingSpec
            {
                Gaps = BuildGaps(difficulty, gapCount),
                Kind = gapCount > 1 ? RingKind.MultiGap : RingKind.Plain,
                AutoSpinDeg = SpinFor(difficulty, previousAutoSpinDeg),
                ColourIndex = _produced,
            };

            _multiGapRun = gapCount > 1 ? _multiGapRun + 1 : 0;
            _produced++;
            return spec;
        }

        int ChooseGapCount(in DifficultySnapshot difficulty)
        {
            if (_multiGapRun >= MaxMultiGapRun) return 1;
            if (difficulty.MultiGapChance <= 0.0) return 1;
            if (_random.NextDouble() >= difficulty.MultiGapChance) return 1;

            int wanted = _random.NextDouble() < difficulty.TripleShare ? 3 : 2;

            double single = GapSplit.AngleFor(difficulty.GapArcWidth,
                                              difficulty.Thickness, difficulty.ReferenceRadius);
            return GapSplit.LargestAffordableCount(single, wanted, MinGapDegFor(difficulty));
        }

        static double MinGapDegFor(in DifficultySnapshot difficulty) =>
            GapSplit.MinGapDegFor(difficulty.BallRadius, difficulty.Thickness,
                                  difficulty.ReferenceRadius);

        RingGaps BuildGaps(in DifficultySnapshot difficulty, int count)
        {
            double single = GapSplit.AngleFor(difficulty.GapArcWidth,
                                              difficulty.Thickness, difficulty.ReferenceRadius);
            GapSplit.Split(single, count, _angles, MinGapDegFor(difficulty));

            var gaps = new RingGaps { Count = count };
            double cursor = _random.NextDouble() * 360.0;

            for (int i = 0; i < count; i++)
            {
                double opening = GapSplit.OpeningFor(_angles[i], difficulty.Thickness,
                                                     difficulty.ReferenceRadius);
                gaps.Set(i, new GapSpec(Wrap360(cursor), opening));

                double remaining = 360.0 / count;
                cursor += remaining * (0.6 + _random.NextDouble() * 0.8);
            }

            return gaps;
        }

        public void Reset()
        {
            _random = new Random(_seed);
            _produced = 0;
            Clamped = 0;
            _multiGapRun = 0;
        }

        static double Wrap360(double deg)
        {
            double wrapped = deg % 360.0;
            return wrapped < 0.0 ? wrapped + 360.0 : wrapped;
        }

        public const double NeighbourSeparation = 0.2;

        double SpinFor(in DifficultySnapshot difficulty, double previousAutoSpinDeg)
        {
            double magnitude = difficulty.AutoSpinDeg;
            if (magnitude <= 0.0) return 0.0;

            double direction = difficulty.AlternateSpinDirection
                ? (previousAutoSpinDeg >= 0.0 ? -1.0 : 1.0)
                : (_random.NextDouble() < 0.5 ? -1.0 : 1.0);

            double chosen = magnitude * (0.75 + _random.NextDouble() * 0.5);

            double previous = Math.Abs(previousAutoSpinDeg);
            if (previous > 1e-6)
            {
                double low = previous * (1.0 - NeighbourSeparation);
                double high = previous * (1.0 + NeighbourSeparation);
                if (chosen > low && chosen < high)
                    chosen = chosen >= previous ? high : low;
            }

            double ceiling = TunnelMath.MaxReachableSpinDeg(
                difficulty.ComfortSpinDeg, difficulty.TimeBudget);
            if (chosen > ceiling)
            {
                chosen = ceiling;
                Clamped++;
            }

            return direction * chosen;
        }
    }
}
