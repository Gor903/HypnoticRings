using System;

namespace HR.Core
{
    public struct GapSpec
    {
        public double CentreDeg;

        public double Opening;

        public GapSpec(double centreDeg, double opening)
        {
            CentreDeg = centreDeg;
            Opening = opening;
        }
    }

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

    public static class GapSplit
    {
        public const double MinClearBalls = 2.0;

        public static double MinGapDegFor(double ballRadius, double thickness, double radius) =>
            AngleFor(ballRadius * 2.0 * MinClearBalls, thickness, radius);

        public const double TwoGapShare = 0.85;

        public const double ThreeGapShare = 0.75;

        static readonly double[] TwoWay = { 0.55, 0.45 };
        static readonly double[] ThreeWay = { 0.40, 0.33, 0.27 };

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

        public static double TotalFor(double singleGapDeg, int count)
        {
            if (count <= 1) return singleGapDeg;
            return singleGapDeg * (count == 2 ? TwoGapShare : ThreeGapShare);
        }

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

                if (widest < 0) continue;
                angles[widest] = Math.Max(minGapDeg, angles[widest] - deficit);
            }
        }

        public static double AngleFor(double opening, double thickness, double referenceRadius)
            => TunnelMath.GapWidthDeg(opening + thickness, referenceRadius);

        public static double OpeningFor(double angleDeg, double thickness, double referenceRadius)
            => angleDeg / TunnelMath.DegPerRad * referenceRadius - thickness;
    }
}
