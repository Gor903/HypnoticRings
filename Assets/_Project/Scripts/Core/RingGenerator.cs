using System;

namespace HR.Core
{
    /// <summary>What kind of ring the generator decided to make.</summary>
    public enum RingKind
    {
        Plain = 0,
        MultiGap = 1,

        /// <summary>A rest, not a test: one wide opening, and clearing it dissolves the next three.</summary>
        Super = 2,
    }

    /// <summary>Everything that distinguishes one ring from the next.</summary>
    public struct RingSpec
    {
        public RingGaps Gaps;
        public RingKind Kind;

        /// <summary>
        /// Which opening carries the bead, or -1. The generator marks it; whether a bead is
        /// actually put there is the tunnel's call, because that depends on how many balls are
        /// already in play and the generator has to stay a function of its seed.
        /// </summary>
        public int BeadGap;
        public double AutoSpinDeg;
        public int ColourIndex;

        /// <summary>
        /// The first opening. Kept as a name because most of the game only ever has one, and
        /// because the bot and the balance simulation aim at a ring rather than at a slot.
        /// </summary>
        public double GapCentreDeg => Gaps.A.CentreDeg;
        public double GapArcWidth => Gaps.A.Opening;
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

        /// <summary>Chance the next ring is cut with more than one opening, 0 to 1.</summary>
        public readonly double MultiGapChance;

        /// <summary>Of those, the share that get three openings rather than two.</summary>
        public readonly double TripleShare;

        /// <summary>Ring thickness and the radius openings are measured at, for angle maths.</summary>
        public readonly double Thickness;
        public readonly double ReferenceRadius;

        /// <summary>Corridor width right now. Was a constant until the corridor started closing.</summary>
        public readonly double Clearance;

        /// <summary>
        /// Distance between neighbouring rings right now.
        ///
        /// It lives here rather than in TunnelSettings because it is no longer a property of the
        /// tunnel but of how far the run has got. Everything that spaces, fades or spawns rings
        /// reads it from the snapshot, so there is exactly one answer per step.
        /// </summary>
        public readonly double Step;

        public DifficultySnapshot(double baseCompressionSpeed, double maxCompressionSpeed,
                                  double gapArcWidth, double autoSpinDeg,
                                  bool alternateSpinDirection,
                                  double timeBudget, double comfortSpinDeg,
                                  double clearance, double step,
                                  double multiGapChance = 0.0, double tripleShare = 0.0,
                                  double thickness = 0.18, double referenceRadius = 3.0)
        {
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
        // One buffer for the life of the generator: a ring is born every second or two, and a
        // three-element array per ring is an allocation in the game loop.
        readonly double[] _angles = new double[RingGaps.MaxGaps];

        /// <summary>How many multi-gap rings have come out back to back, for the run-length rule.</summary>
        int _multiGapRun;

        /// <summary>Most multi-gap rings allowed in a row, from plan [G-27].</summary>
        public const int MaxMultiGapRun = 2;

        /// <summary>Every this many rings, one is a super ring. Plan [G-32].</summary>
        public const int SuperRingEvery = 12;

        /// <summary>One in this many multi-gap rings carries a bead. Plan [G-27].</summary>
        public const int BeadEveryMultiGap = 3;

        /// <summary>How much narrower the bead's own opening is. The prize has a price.</summary>
        public const double BeadGapNarrowing = 0.7;

        /// <summary>Plain rings guaranteed after a super one, so the reward lands cleanly.</summary>
        public const int PlainRingsAfterSuper = 2;

        /// <summary>A super ring's opening, as a share of what the curve asks for.</summary>
        public const double SuperGapBonus = 1.3;

        /// <summary>Whether the ring at this ordinal is the rest the curve owes the player.</summary>
        public static bool IsSuperOrdinal(int ordinal) =>
            ordinal > 0 && ordinal % SuperRingEvery == 0;

        public RingSpec Next(in DifficultySnapshot difficulty, double previousAutoSpinDeg)
        {
            if (IsSuperOrdinal(_produced)) return NextSuper(difficulty);

            int gapCount = ChooseGapCount(difficulty);

            int beadGap = -1;
            if (gapCount > 1)
            {
                _multiGapSeen++;
                // The first multi-gap ring of a run always carries one, so the mechanic is seen
                // rather than waited for; after that, every third.
                if (_multiGapSeen == 1 || _multiGapSeen % BeadEveryMultiGap == 0)
                    beadGap = gapCount - 1;   // the narrowest, which is what makes it a choice
            }

            var spec = new RingSpec
            {
                Gaps = BuildGaps(difficulty, gapCount, beadGap),
                Kind = gapCount > 1 ? RingKind.MultiGap : RingKind.Plain,
                BeadGap = beadGap,
                AutoSpinDeg = SpinFor(difficulty, previousAutoSpinDeg),
                ColourIndex = _produced,
            };

            _multiGapRun = gapCount > 1 ? _multiGapRun + 1 : 0;
            _produced++;
            return spec;
        }

        /// <summary>
        /// The rest ring: one opening, wider than the curve asks, and never spinning.
        ///
        /// Its ordinal decides it rather than a die roll, so a player can learn the rhythm — a
        /// reward that arrives at random is indistinguishable from luck, and a run that happens
        /// to get none feels unfair without the player ever knowing why.
        /// </summary>
        RingSpec NextSuper(in DifficultySnapshot difficulty)
        {
            var spec = new RingSpec
            {
                Gaps = RingGaps.One(_random.NextDouble() * 360.0,
                                    difficulty.GapArcWidth * SuperGapBonus),
                Kind = RingKind.Super,
                BeadGap = -1,
                AutoSpinDeg = 0.0,
                ColourIndex = _produced,
            };

            _multiGapRun = 0;
            _plainRingsOwed = PlainRingsAfterSuper;
            _produced++;
            return spec;
        }

        /// <summary>Plain rings still owed after the last super ring.</summary>
        int _plainRingsOwed;

        /// <summary>
        /// One, two or three openings. The run-length rule is drawn before the dice rather than
        /// after, so a streak is refused outright instead of being rerolled into a different
        /// streak — rerolling would bias the chances the table asks for.
        /// </summary>
        int ChooseGapCount(in DifficultySnapshot difficulty)
        {
            if (_plainRingsOwed > 0) { _plainRingsOwed--; return 1; }
            if (_multiGapRun >= MaxMultiGapRun) return 1;
            if (difficulty.MultiGapChance <= 0.0) return 1;
            if (_random.NextDouble() >= difficulty.MultiGapChance) return 1;

            int wanted = _random.NextDouble() < difficulty.TripleShare ? 3 : 2;

            // A narrow ring cannot afford what it was asked for: see LargestAffordableCount.
            double single = GapSplit.AngleFor(difficulty.GapArcWidth,
                                              difficulty.Thickness, difficulty.ReferenceRadius);
            return GapSplit.LargestAffordableCount(single, wanted);
        }

        /// <summary>
        /// Places the openings around the ring and gives each its width.
        ///
        /// Spacing is deliberately uneven. Two openings exactly opposite each other mean the
        /// ball is never more than a quarter turn from a way out wherever it is, which hands the
        /// player the thing the ring was supposed to ask of them.
        /// </summary>
        /// <summary>Multi-gap rings produced so far, for spacing the beads out.</summary>
        int _multiGapSeen;

        RingGaps BuildGaps(in DifficultySnapshot difficulty, int count, int beadGap = -1)
        {
            double single = GapSplit.AngleFor(difficulty.GapArcWidth,
                                              difficulty.Thickness, difficulty.ReferenceRadius);
            GapSplit.Split(single, count, _angles);

            var gaps = new RingGaps { Count = count };
            double cursor = _random.NextDouble() * 360.0;

            for (int i = 0; i < count; i++)
            {
                double angle = _angles[i];

                // The bead sits in the tightest way out there is. Taking the easy opening and
                // taking the prize have to be different decisions, or the bead is just a bonus
                // for doing what you were going to do anyway.
                if (i == beadGap)
                    angle = System.Math.Max(GapSplit.MinGapDeg, angle * BeadGapNarrowing);

                double opening = GapSplit.OpeningFor(angle, difficulty.Thickness,
                                                     difficulty.ReferenceRadius);
                gaps.Set(i, new GapSpec(Wrap360(cursor), opening));

                // Walk on by an uneven share of what is left, never letting two openings touch.
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
            _plainRingsOwed = 0;
            _multiGapSeen = 0;
        }

        /// <summary>
        /// Folds an angle into [0, 360). Not NormalizeDeg: that lands in (-180, 180], and gap
        /// centres are stated in the positive turn everywhere else in the project.
        /// </summary>
        static double Wrap360(double deg)
        {
            double wrapped = deg % 360.0;
            return wrapped < 0.0 ? wrapped + 360.0 : wrapped;
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
