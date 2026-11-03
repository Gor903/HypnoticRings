using System;

namespace HR.Core
{
    public readonly struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }

        public static Rgb Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex)) throw new ArgumentNullException(nameof(hex));
            string s = hex[0] == '#' ? hex.Substring(1) : hex;
            if (s.Length != 6) throw new FormatException($"expected #RRGGBB, got '{hex}'");

            return new Rgb(
                Convert.ToByte(s.Substring(0, 2), 16),
                Convert.ToByte(s.Substring(2, 2), 16),
                Convert.ToByte(s.Substring(4, 2), 16));
        }

        public static Rgb Mix(in Rgb from, in Rgb to, double amount)
        {
            double t = amount < 0.0 ? 0.0 : (amount > 1.0 ? 1.0 : amount);
            return new Rgb(
                (byte)Math.Round(from.R + (to.R - from.R) * t),
                (byte)Math.Round(from.G + (to.G - from.G) * t),
                (byte)Math.Round(from.B + (to.B - from.B) * t));
        }

        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
    }

    public static class Contrast
    {
        public const double Required = 3.0;

        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        public static double Luminance(in Rgb colour) =>
            0.2126 * Channel(colour.R) + 0.7152 * Channel(colour.G) + 0.0722 * Channel(colour.B);

        public static double Ratio(in Rgb a, in Rgb b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        public static bool BallReadsAgainstRing(in Rgb ball, in Rgb ring, in Rgb outline)
        {
            if (Ratio(ball, ring) >= Required) return true;
            return Ratio(outline, ball) >= Required && Ratio(outline, ring) >= Required;
        }
    }
}
