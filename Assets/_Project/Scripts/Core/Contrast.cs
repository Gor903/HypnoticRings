using System;

namespace HR.Core
{
    /// <summary>A colour as plain bytes, so palettes can be reasoned about without the engine.</summary>
    public readonly struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }

        /// <summary>Parses "#RRGGBB" or "RRGGBB".</summary>
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

        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>
    /// Readability of a palette, by the measure CONTRACT.md §2.11 asks for.
    ///
    /// Engine-free so every theme can be checked by a test rather than by looking at it, which
    /// is the only way a palette stays correct after someone nudges a hue.
    /// </summary>
    public static class Contrast
    {
        /// <summary>The smallest ratio the design calls readable.</summary>
        public const double Required = 3.0;

        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>Relative luminance, as the accessibility guidelines define it.</summary>
        public static double Luminance(in Rgb colour) =>
            0.2126 * Channel(colour.R) + 0.7152 * Channel(colour.G) + 0.0722 * Channel(colour.B);

        /// <summary>Contrast ratio between two colours: 1 for identical, 21 for black on white.</summary>
        public static double Ratio(in Rgb a, in Rgb b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        /// <summary>
        /// Whether the ball can be told apart from a ring.
        ///
        /// Colour alone is not the only way, and insisting on it would be wrong: Monochrome's
        /// ball and rings are deliberately the same ink, and the spec gives that theme a ball
        /// outline in the background colour precisely so the two still read apart. So an outline
        /// that separates from both counts — which is the same rule, honoured by a different
        /// means, rather than an exemption from it.
        /// </summary>
        public static bool BallReadsAgainstRing(in Rgb ball, in Rgb ring, in Rgb outline)
        {
            if (Ratio(ball, ring) >= Required) return true;
            return Ratio(outline, ball) >= Required && Ratio(outline, ring) >= Required;
        }
    }
}
