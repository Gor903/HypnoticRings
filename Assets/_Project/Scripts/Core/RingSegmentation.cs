using System;

namespace HR.Core
{
    /// <summary>
    /// A gap in a ring, as the segmentation sees it: a centre and an angular width. How that
    /// width is arrived at — held from the reference radius, floored so the ball still fits — is
    /// <see cref="TunnelMath.HeldGapWidthDeg"/>, and it is stated there once.
    /// </summary>
    public struct Gap
    {
        public double CentreDeg;
        public double WidthDeg;

        public Gap(double centreDeg, double widthDeg) { CentreDeg = centreDeg; WidthDeg = widthDeg; }
    }

    /// <summary>One solid stretch of a ring, as an arc to tessellate.</summary>
    public struct ArcSpan
    {
        public double StartDeg;
        public double SweepDeg;

        public ArcSpan(double startDeg, double sweepDeg) { StartDeg = startDeg; SweepDeg = sweepDeg; }
        public double EndDeg => StartDeg + SweepDeg;
    }

    /// <summary>
    /// Turns "a ring with these gaps" into "these solid arcs", split so no single collider spans
    /// more than the configured sweep. Engine-free, allocation-free, and the only place that
    /// knows a ring is not a circle.
    /// </summary>
    public static class RingSegmentation
    {
        /// <summary>
        /// Safe output size for any ring with this many gaps. Each gap can start a new span, and
        /// splitting the full circle can never produce more than one piece per maximum sweep.
        /// </summary>
        public static int UpperBoundSegments(int gapCount, double maxSweepDeg)
        {
            if (maxSweepDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxSweepDeg));
            return Math.Max(1, gapCount) + (int)Math.Ceiling(360.0 / maxSweepDeg) + 1;
        }

        /// <summary>
        /// Writes the solid arcs into <paramref name="output"/> and returns how many were written.
        /// <paramref name="gaps"/> is sorted in place — the caller owns a small reusable array,
        /// so no copy is made.
        /// </summary>
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
                // Normalise forward: the last span wraps past 360 back to the first gap.
                while (sweep < 0.0) sweep += 360.0;
                while (sweep >= 360.0) sweep -= 360.0;

                // Gaps that have grown into each other leave nothing solid between them.
                if (sweep <= 1e-9) continue;

                written = Split(new ArcSpan(from, sweep), maxSweepDeg, output, written);
            }

            return written;
        }

        /// <summary>
        /// Clear opening left by a cut of this angular width, in world units — the inverse of
        /// <see cref="TunnelMath.HeldGapWidthDeg"/>, and what any measurement of built geometry
        /// should use.
        /// </summary>
        public static double OpeningFor(double widthDeg, double thickness, double radius)
            => widthDeg / TunnelMath.DegPerRad * radius - thickness;

        /// <summary>Total solid arc in degrees — what is left of the circle after the gaps.</summary>
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

        /// <summary>Insertion sort: gap counts are one or two, never enough to want anything else.</summary>
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
