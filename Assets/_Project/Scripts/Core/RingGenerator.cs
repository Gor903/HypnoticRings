using System;

namespace HR.Core
{
    /// <summary>Everything that distinguishes one ring from the next.</summary>
    public struct RingSpec
    {
        public double GapCentreDeg;
        public double GapArcWidth;
        public double AutoSpinDeg;
        public int ColourIndex;
    }

    /// <summary>
    /// What difficulty asks of the next ring. A plain snapshot rather than a reference to a
    /// curve, so the generator can be driven from a test with fixed numbers and phase 4 can
    /// change how the numbers are produced without touching generation.
    /// </summary>
    public readonly struct DifficultySnapshot
    {
        public readonly double BaseCompressionSpeed;

        /// <summary>Ceiling on the retraction after a pass. Separate dial from the timer above.</summary>
        public readonly double MaxCompressionSpeed;
        public readonly double GapArcWidth;
        public readonly double AutoSpinDeg;
        public readonly bool AlternateSpinDirection;

        /// <summary>Seconds available to line a gap up, at this point in the run.</summary>
        public readonly double TimeBudget;

        /// <summary>Angular speed a player can comfortably sustain.</summary>
        public readonly double ComfortSpinDeg;

        public DifficultySnapshot(double baseCompressionSpeed, double maxCompressionSpeed,
                                  double gapArcWidth, double autoSpinDeg,
                                  bool alternateSpinDirection,
                                  double timeBudget, double comfortSpinDeg)
        {
            BaseCompressionSpeed = baseCompressionSpeed;
            MaxCompressionSpeed = maxCompressionSpeed;
            GapArcWidth = gapArcWidth;
            AutoSpinDeg = autoSpinDeg;
            AlternateSpinDirection = alternateSpinDirection;
            TimeBudget = timeBudget;
            ComfortSpinDeg = comfortSpinDeg;
        }

        /// <summary>
        /// The tutorial setting: still rings, wide openings, slow squeeze — asked of the curve
        /// rather than copied from it. Copied, it was a third place to remember whenever the
        /// table moved, and the time budget in it was a rounded number that did not quite match
        /// the one the game goes on to compute.
        /// </summary>
        public static DifficultySnapshot Opening =>
            new DifficultyCurve(TunnelSettings.Default).SnapshotAt(0);
    }

    /// <summary>
    /// Decides where each new ring's gap goes. Seeded and deterministic: a balance report that
    /// cannot be replayed cannot be told apart from luck, and a bug report without a seed is a
    /// bug nobody can reproduce.
    ///
    /// Reachability — the rule that stops it emitting a ring no hand could catch — arrives in
    /// phase 4 along with the difficulty curve. The seam for it is here.
    /// </summary>
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

        /// <summary>Rings whose spin had to be cut to keep them catchable. Reported, not hidden.</summary>
        public int Clamped { get; private set; }

        /// <summary>
        /// Next ring's parameters. <paramref name="previousAutoSpinDeg"/> is the neighbour's
        /// spin, which this one has to differ from — identical neighbours read as one rigid
        /// object rather than a tunnel.
        /// </summary>
        public RingSpec Next(in DifficultySnapshot difficulty, double previousAutoSpinDeg)
        {
            var spec = new RingSpec
            {
                GapCentreDeg = _random.NextDouble() * 360.0,
                GapArcWidth = difficulty.GapArcWidth,
                AutoSpinDeg = SpinFor(difficulty, previousAutoSpinDeg),
                ColourIndex = _produced,
            };

            _produced++;
            return spec;
        }

        public void Reset()
        {
            _random = new Random(_seed);
            _produced = 0;
            Clamped = 0;
        }

        /// <summary>Smallest separation between neighbouring spin rates, as a fraction.</summary>
        public const double NeighbourSeparation = 0.2;

        double SpinFor(in DifficultySnapshot difficulty, double previousAutoSpinDeg)
        {
            double magnitude = difficulty.AutoSpinDeg;
            if (magnitude <= 0.0) return 0.0;

            double direction = difficulty.AlternateSpinDirection
                ? (previousAutoSpinDeg >= 0.0 ? -1.0 : 1.0)
                : (_random.NextDouble() < 0.5 ? -1.0 : 1.0);

            double chosen = magnitude * (0.75 + _random.NextDouble() * 0.5);

            // CONTRACT.md §2.8: neighbours differ by at least a fifth. Two rings turning at the
            // same rate read as one rigid object rather than as a tunnel of separate rings, and
            // the effect is strong enough that it is worth forcing rather than hoping for.
            double previous = Math.Abs(previousAutoSpinDeg);
            if (previous > 1e-6)
            {
                double low = previous * (1.0 - NeighbourSeparation);
                double high = previous * (1.0 + NeighbourSeparation);
                if (chosen > low && chosen < high)
                    chosen = chosen >= previous ? high : low;
            }

            // The reachability guarantee, and the last word over every rule above it. A ring
            // spinning faster than this cannot be caught from its worst starting position, and
            // the generator is not allowed to emit one however much the curve or the spacing
            // rule would prefer it.
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
