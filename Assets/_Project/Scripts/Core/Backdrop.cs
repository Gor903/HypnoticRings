using System;
using System.Collections.Generic;

namespace HR.Core
{
    public enum BackdropShape
    {
        Disc = 0,
        Ring = 1,
        Arc = 2,
        Wedge = 3,
        Rect = 4,
        Triangle = 5,
    }

    public enum BackdropMotion
    {
        Still = 0,
        Twinkle = 1,
        Rise = 2,
    }

    public readonly struct BackdropElement
    {
        public readonly BackdropShape Shape;

        public readonly double X;
        public readonly double Y;

        public readonly double Size;
        public readonly double Thickness;

        public readonly double StartDegrees;
        public readonly double SweepDegrees;

        public readonly Rgb Colour;
        public readonly double Alpha;

        public readonly BackdropMotion Motion;
        public readonly double Period;
        public readonly double Swing;
        public readonly double Speed;

        public BackdropElement(BackdropShape shape, double x, double y, double size, Rgb colour,
                               double alpha, double thickness = 0.0, double startDegrees = 0.0,
                               double sweepDegrees = 360.0,
                               BackdropMotion motion = BackdropMotion.Still,
                               double period = 0.0, double swing = 0.0, double speed = 0.0)
        {
            Shape = shape;
            X = x;
            Y = y;
            Size = size;
            Thickness = thickness;
            StartDegrees = startDegrees;
            SweepDegrees = sweepDegrees;
            Colour = colour;
            Alpha = alpha;
            Motion = motion;
            Period = period;
            Swing = swing;
            Speed = speed;
        }

        public double PeakAlpha => Math.Min(1.0, Alpha + Swing);

        public double Coverage
        {
            get
            {
                switch (Shape)
                {
                    case BackdropShape.Ring:
                    case BackdropShape.Arc: return Thickness;
                    case BackdropShape.Rect: return Math.Min(Size, Thickness);
                    case BackdropShape.Wedge:
                        return Size * Math.Sin(SweepDegrees * Math.PI / 180.0);
                    default: return Size;
                }
            }
        }
    }

    public static class Backdrop
    {
        public const double MaxDrift = 0.04;

        public const double MinPeriod = 2.4;

        public const double FrameLine = 1.0 / 1920.0;

        public const double WashCoverage = 0.02;

        public static BackdropElement[] For(Theme theme)
        {
            switch (theme)
            {
                case Theme.MinimalLight:
                case Theme.MinimalDark: return Array.Empty<BackdropElement>();

                case Theme.Neon: return NeonCosmos();
                case Theme.Deco: return ArtDeco();
                case Theme.Sumie: return SumiE();
                case Theme.Ocean: return DeepOcean();
                case Theme.Bauhaus: return BauhausShapes();
                case Theme.Crt: return RetroCrt();
                case Theme.Ember: return EmberSparks();

                default: throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        static BackdropElement[] NeonCosmos()
        {
            var random = new Random(9041);
            var parts = new List<BackdropElement>
            {
                new BackdropElement(BackdropShape.Disc, -0.18, -0.20, 0.34,
                    Rgb.Parse("#8B5CFF"), 0.13),
                new BackdropElement(BackdropShape.Disc, 0.22, 0.06, 0.29,
                    Rgb.Parse("#19F0FF"), 0.07),
            };

            var star = Rgb.Parse("#FFFFFF");
            for (int i = 0; i < 46; i++)
            {
                double alpha = 0.3 + random.NextDouble() * 0.6;
                parts.Add(new BackdropElement(BackdropShape.Disc,
                    Spread(random, 0.42), Spread(random, 0.52),
                    0.0025 + random.NextDouble() * 0.0021, star, alpha,
                    motion: BackdropMotion.Twinkle,
                    period: MinPeriod + random.NextDouble() * 2.6,
                    swing: Math.Min(0.2, 1.0 - alpha)));
            }

            return parts.ToArray();
        }

        static BackdropElement[] ArtDeco()
        {
            var gold = Rgb.Parse("#D9B25F");
            var parts = new List<BackdropElement>();

            for (int i = 0; i < 12; i++)
                parts.Add(new BackdropElement(BackdropShape.Wedge, 0.0, 0.22, 0.75, gold, 0.06,
                    startDegrees: i * 30.0, sweepDegrees: 7.5));

            AddFrame(parts, 0.03, FrameLine, gold, 0.5);
            return parts.ToArray();
        }

        static BackdropElement[] SumiE()
        {
            var random = new Random(5157);
            var parts = new List<BackdropElement>
            {
                new BackdropElement(BackdropShape.Arc, 0.0, 0.08, 0.42, Rgb.Parse("#1B1B1B"), 0.05,
                    thickness: 0.057, startDegrees: 54.0, sweepDegrees: 288.0),
            };

            var grain = Rgb.Parse("#3A2F20");
            for (int i = 0; i < 70; i++)
                parts.Add(new BackdropElement(BackdropShape.Disc,
                    Spread(random, 0.42), Spread(random, 0.52),
                    0.002 + random.NextDouble() * 0.002, grain,
                    0.08 + random.NextDouble() * 0.12));

            const double seal = 22.0 / 1920.0;
            parts.Add(new BackdropElement(BackdropShape.Rect, 0.20, -0.40, seal,
                Rgb.Parse("#D7381F"), 1.0, thickness: seal));
            parts.Add(new BackdropElement(BackdropShape.Rect, 0.20, -0.40, seal * 0.55,
                Rgb.Parse("#F1EAD9"), 1.0, thickness: seal * 0.55));

            return parts.ToArray();
        }

        static BackdropElement[] DeepOcean()
        {
            var random = new Random(3188);
            var shaft = Rgb.Parse("#5AB8FF");
            var parts = new List<BackdropElement>
            {
                new BackdropElement(BackdropShape.Wedge, -0.20, 0.62, 1.2, shaft, 0.05,
                    startDegrees: 256.0, sweepDegrees: 7.0),
                new BackdropElement(BackdropShape.Wedge, 0.04, 0.62, 1.2, shaft, 0.05,
                    startDegrees: 262.0, sweepDegrees: 5.0),
                new BackdropElement(BackdropShape.Wedge, 0.24, 0.62, 1.2, shaft, 0.04,
                    startDegrees: 268.0, sweepDegrees: 6.0),
            };

            var bubble = Rgb.Parse("#9FFFF0");
            for (int i = 0; i < 16; i++)
            {
                double radius = 0.007 + random.NextDouble() * 0.011;
                parts.Add(new BackdropElement(BackdropShape.Ring,
                    Spread(random, 0.40), Spread(random, 0.52), radius, bubble, 0.4,
                    thickness: radius * 0.3,
                    motion: BackdropMotion.Rise, speed: 0.03));
            }

            return parts.ToArray();
        }

        static BackdropElement[] BauhausShapes() => new[]
        {
            new BackdropElement(BackdropShape.Disc, 0.19, -0.33, 0.16,
                Rgb.Parse("#F2B705"), 0.55),
            new BackdropElement(BackdropShape.Triangle, -0.30, -0.46, 0.34,
                Rgb.Parse("#1E40AF"), 0.15),
        };

        static BackdropElement[] RetroCrt()
        {
            var phosphor = Rgb.Parse("#4DFF7A");
            var parts = new List<BackdropElement>();

            const double step = 0.0143;
            for (double y = -0.52; y <= 0.52; y += step)
                parts.Add(new BackdropElement(BackdropShape.Rect, 0.0, y, 0.6, phosphor, 0.06,
                    thickness: 0.005));

            return parts.ToArray();
        }

        static BackdropElement[] EmberSparks()
        {
            var random = new Random(7714);
            var spark = Rgb.Parse("#FFB02E");
            var parts = new List<BackdropElement>();

            for (int i = 0; i < 22; i++)
            {
                double y = Spread(random, 0.52);
                double low = (0.52 - y) / 1.04;
                parts.Add(new BackdropElement(BackdropShape.Disc,
                    Spread(random, 0.42), y,
                    0.003 + random.NextDouble() * 0.004, spark,
                    0.2 + low * 0.6,
                    motion: BackdropMotion.Rise, speed: 0.04));
            }

            return parts.ToArray();
        }

        static void AddFrame(List<BackdropElement> parts, double inset, double thickness,
                             Rgb colour, double alpha)
        {
            const double half = 0.52;
            double outer = half - inset;
            double mid = outer - thickness * 0.5;

            parts.Add(new BackdropElement(BackdropShape.Rect, 0.0, mid, 0.6, colour, alpha,
                thickness: thickness));
            parts.Add(new BackdropElement(BackdropShape.Rect, 0.0, -mid, 0.6, colour, alpha,
                thickness: thickness));
            parts.Add(new BackdropElement(BackdropShape.Rect, -0.58, 0.0, thickness, colour, alpha,
                thickness: 1.04));
            parts.Add(new BackdropElement(BackdropShape.Rect, 0.58, 0.0, thickness, colour, alpha,
                thickness: 1.04));
        }

        static double Spread(Random random, double reach) =>
            (random.NextDouble() * 2.0 - 1.0) * reach;

        public static List<string> Validate(Theme theme)
        {
            var problems = new List<string>();
            var palette = ThemePalette.For(theme);

            foreach (var part in For(theme))
            {
                if (part.Motion == BackdropMotion.Rise && part.Speed > MaxDrift)
                    problems.Add($"{theme}: a part drifts {part.Speed:P1} of the height a second, "
                                 + $"the ceiling is {MaxDrift:P0}");

                if (part.Motion == BackdropMotion.Twinkle && part.Period < MinPeriod)
                    problems.Add($"{theme}: a part changes every {part.Period:F1}s, "
                                 + $"the floor is {MinPeriod:F1}s");

                if (part.Coverage < WashCoverage) continue;

                var over = Rgb.Mix(palette.Background, part.Colour, part.PeakAlpha);
                for (int i = 0; i < palette.RingColours.Length; i++)
                {
                    double ratio = Contrast.Ratio(palette.RingColours[i], over);
                    if (ratio < Contrast.Required)
                        problems.Add($"{theme}: ring {i} ({palette.RingColours[i]}) reads "
                                     + $"{ratio:F2} over {part.Colour} at {part.PeakAlpha:P0}, "
                                     + $"needs {Contrast.Required:F1}");
                }
            }

            return problems;
        }
    }
}
