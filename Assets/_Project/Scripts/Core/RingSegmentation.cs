using System;

namespace HR.Core
{
    public struct Gap
    {
        public double CentreDeg;
        public double WidthDeg;

        public Gap(double centreDeg, double widthDeg) { CentreDeg = centreDeg; WidthDeg = widthDeg; }
    }

    public struct ArcSpan
    {
        public double StartDeg;
        public double SweepDeg;

        public ArcSpan(double startDeg, double sweepDeg) { StartDeg = startDeg; SweepDeg = sweepDeg; }
        public double EndDeg => StartDeg + SweepDeg;
    }

    public static class RingSegmentation
    {
        public static int UpperBoundSegments(int gapCount, double maxSweepDeg)
        {
            if (maxSweepDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxSweepDeg));
            return Math.Max(1, gapCount) + (int)Math.Ceiling(360.0 / maxSweepDeg) + 1;
        }

        public static int BuildSolidSegments(Gap[] gaps, int gapCount, double maxSweepDeg, ArcSpan[] output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (maxSweepDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxSweepDeg));
            if (gapCount < 0 || (gaps != null && gapCount > gaps.Length))
                throw new ArgumentOutOfRangeException(nameof(gapCount));

            if (gapCount == 0)
                return Split(new ArcSpan(0.0, 360.0), maxSweepDeg, output, 0);

            SortByCentre(gaps, gapCount);

            int written = 0;
            for (int i = 0; i < gapCount; i++)
            {
                var here = gaps[i];
                var next = gaps[(i + 1) % gapCount];

                double from = here.CentreDeg + here.WidthDeg * 0.5;
                double to = next.CentreDeg - next.WidthDeg * 0.5;

                double sweep = to - from;

                while (sweep < 0.0) sweep += 360.0;
                while (sweep >= 360.0) sweep -= 360.0;

                if (sweep <= 1e-9) continue;

                written = Split(new ArcSpan(from, sweep), maxSweepDeg, output, written);
            }

            return written;
        }

        public static double OpeningFor(double widthDeg, double thickness, double radius)
            => widthDeg / TunnelMath.DegPerRad * radius - thickness;

        public static double SolidSweep(ArcSpan[] spans, int count)
        {
            double total = 0.0;
            for (int i = 0; i < count; i++) total += spans[i].SweepDeg;
            return total;
        }

        static int Split(ArcSpan span, double maxSweepDeg, ArcSpan[] output, int written)
        {
            int pieces = Math.Max(1, (int)Math.Ceiling(span.SweepDeg / maxSweepDeg - 1e-9));
            if (written + pieces > output.Length)
                throw new ArgumentException(
                    $"output holds {output.Length}, needs at least {written + pieces}", nameof(output));

            double each = span.SweepDeg / pieces;
            for (int i = 0; i < pieces; i++)
                output[written++] = new ArcSpan(span.StartDeg + each * i, each);

            return written;
        }

        static void SortByCentre(Gap[] gaps, int count)
        {
            for (int i = 1; i < count; i++)
            {
                var key = gaps[i];
                double k = TunnelMath.NormalizeDeg(key.CentreDeg);
                int j = i - 1;
                while (j >= 0 && TunnelMath.NormalizeDeg(gaps[j].CentreDeg) > k)
                {
                    gaps[j + 1] = gaps[j];
                    j--;
                }
                gaps[j + 1] = key;
            }
        }
    }
}
