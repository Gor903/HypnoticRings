using System;

namespace HR.Core
{
    /// <summary>One opening as the generator decides it: where it sits, and how wide it is.</summary>
    public struct GapSpec
    {
        /// <summary>Centre of the opening in the ring's own frame, degrees.</summary>
        public double CentreDeg;

        /// <summary>Clear opening in world units, read at the reference radius.</summary>
        public double Opening;

        public GapSpec(double centreDeg, double opening)
        {
            CentreDeg = centreDeg;
            Opening = opening;
        }
    }

    /// <summary>
    /// The openings of one ring: between one and three of them.
    ///
    /// Fixed slots rather than an array, because a ring is born every second or two for the whole
    /// life of a run and an array per ring is an allocation in the game loop, which the project
    /// forbids and the allocation gate would catch.
    /// </summary>
    public struct RingGaps
    {
        public const int MaxGaps = 3;

        public GapSpec A, B, C;
        public int Count;

        public static RingGaps One(double centreDeg, double opening) =>
            new RingGaps { A = new GapSpec(centreDeg, opening), Count = 1 };

        public GapSpec this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return A;
                    case 1: return B;
                    case 2: return C;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        /// <summary>
        /// Which opening is nearest a heading, once the ring has turned by
        /// <paramref name="offsetDeg"/>.
        ///
        /// Every measurement of a multi-gap ring has to go through here. Aiming at slot zero
        /// instead makes a ring with three ways out score as harder than a ring with one, which
        /// is the exact opposite of what it is.
        /// </summary>
        public int NearestIndex(double offsetDeg, double towardsDeg)
        {
            int best = 0;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < Count; i++)
            {
                double distance = Math.Abs(TunnelMath.ShortestDeltaDeg(
                    this[i].CentreDeg + offsetDeg, towardsDeg));
                if (distance >= bestDistance) continue;
                best = i;
                bestDistance = distance;
            }

            return best;
        }

        public void Set(int index, in GapSpec gap)
        {
            switch (index)
            {
                case 0: A = gap; break;
                case 1: B = gap; break;
                case 2: C = gap; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    /// <summary>
    /// How a ring's single opening becomes two or three of them.
    ///
    /// The rule that makes this a difficulty feature rather than a gift: the openings together
    /// are NARROWER than the one they replace. A flying ball leaves through whichever opening it
    /// reaches first, so its chance of getting out in a given second follows the TOTAL angle
    /// (CONTRACT §2.7 after D-058). Split a gap into three equal thirds and the ring is no easier
    /// to clear but much easier to aim at — the nearest way out is never far. Taking a share off
    /// the total is what pays for that convenience.
    /// </summary>
    public static class GapSplit
    {
        /// <summary>
        /// Narrowest opening worth cutting, in ball diameters.
        ///
        /// Stated in balls rather than in degrees, which is the whole of the fix. Twenty degrees
        /// at the reference radius sounds like a floor and is not one: it came out at 1.61 ball
        /// diameters against a single gap's 5.2 to 8.0, so every ring with more than one way out
        /// opened about a third as wide as the rings the player had spent the run learning. From
        /// the phone that reads as "almost impossible to get out of", and it was measured in a
        /// unit that could never have said so.
        ///
        /// It is the one rule allowed to push the total back above a single gap's angle — see
        /// plan [G-34] item 4.
        /// </summary>
        public const double MinClearBalls = 2.0;

        /// <summary>That floor in degrees, for a ring of this thickness at this radius.</summary>
        public static double MinGapDegFor(double ballRadius, double thickness, double radius) =>
            AngleFor(ballRadius * 2.0 * MinClearBalls, thickness, radius);

        /// <summary>Share of a single gap's angle that two openings get between them.</summary>
        public const double TwoGapShare = 0.85;

        /// <summary>Three openings get less: more ways out is worth more.</summary>
        public const double ThreeGapShare = 0.75;

        static readonly double[] TwoWay = { 0.55, 0.45 };
        static readonly double[] ThreeWay = { 0.40, 0.33, 0.27 };

        /// <summary>
        /// The most openings a gap this wide can actually afford, up to what was asked for.
        ///
        /// Without this the floor quietly turns a hard ring into an easy one. Late in a run the
        /// single gap is 57°, three ways out share 75% of it, and the narrowest share lands at
        /// 11.5° — under the floor. Lifting all three to 20° gives 60° of total opening where one
        /// gap would have given 57, so the ring meant to be the hardest in the game ends up the
        /// most generous. Asking for fewer openings instead keeps the rule intact: a ring gets
        /// three ways out only while three still cost something.
        /// </summary>
        public static int LargestAffordableCount(double singleGapDeg, int wanted, double minGapDeg)
        {
            for (int count = wanted; count > 1; count--)
            {
                var shares = count == 2 ? TwoWay : ThreeWay;
                double narrowest = TotalFor(singleGapDeg, count) * shares[count - 1];
                if (narrowest >= minGapDeg) return count;
            }

            return 1;
        }

        /// <summary>Total angle this many openings may use, given one gap's worth.</summary>
        public static double TotalFor(double singleGapDeg, int count)
        {
            if (count <= 1) return singleGapDeg;
            return singleGapDeg * (count == 2 ? TwoGapShare : ThreeGapShare);
        }

        /// <summary>
        /// Writes <paramref name="count"/> opening angles into <paramref name="into"/>.
        ///
        /// Deliberately uneven: equal openings put the ways out at tidy intervals, and a tunnel
        /// of evenly divided rings reads as a machine rather than as a place.
        /// </summary>
        public static int Split(double singleGapDeg, int count, double[] into, double minGapDeg)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (count < 1 || count > RingGaps.MaxGaps) throw new ArgumentOutOfRangeException(nameof(count));
            if (into.Length < count) throw new ArgumentException("buffer is too small", nameof(into));

            if (count == 1)
            {
                into[0] = singleGapDeg;
                return 1;
            }

            double total = TotalFor(singleGapDeg, count);
            var shares = count == 2 ? TwoWay : ThreeWay;
            for (int i = 0; i < count; i++) into[i] = total * shares[i];

            ApplyFloor(into, count, minGapDeg);
            return count;
        }

        /// <summary>
        /// Lifts anything under the floor and takes the difference off the widest.
        ///
        /// Off the widest rather than spread evenly, because the widest is the one that can
        /// afford it: shaving every opening equally is how all three end up at the floor.
        /// </summary>
        static void ApplyFloor(double[] angles, int count, double minGapDeg)
        {
            for (int i = 0; i < count; i++)
            {
                if (angles[i] >= minGapDeg) continue;

                double deficit = minGapDeg - angles[i];
                angles[i] = minGapDeg;

                int widest = -1;
                for (int j = 0; j < count; j++)
                {
                    if (j == i) continue;
                    if (widest < 0 || angles[j] > angles[widest]) widest = j;
                }

                // Everyone is already at the floor: the total grows past a single gap's angle,
                // which is the documented exception rather than a bug to paper over.
                if (widest < 0) continue;
                angles[widest] = Math.Max(minGapDeg, angles[widest] - deficit);
            }
        }

        /// <summary>
        /// The angle one opening of <paramref name="opening"/> units cuts at the reference radius.
        /// The inverse of <see cref="OpeningFor"/>, and the pair is how the generator moves
        /// between the units a designer writes and the angles this class reasons in.
        /// </summary>
        public static double AngleFor(double opening, double thickness, double referenceRadius)
            => TunnelMath.GapWidthDeg(opening + thickness, referenceRadius);

        /// <summary>Clear opening in units for an angle at the reference radius.</summary>
        public static double OpeningFor(double angleDeg, double thickness, double referenceRadius)
            => angleDeg / TunnelMath.DegPerRad * referenceRadius - thickness;
    }
}
