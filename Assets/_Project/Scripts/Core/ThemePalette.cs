using System;
using System.Collections.Generic;

namespace HR.Core
{
    public sealed class ThemePalette
    {
        public readonly Theme Theme;
        public readonly Rgb Background;
        public readonly Rgb[] RingColours;
        public readonly Rgb Ball;

        public readonly Rgb BallOutline;

        public readonly Rgb Ui;

        public readonly double ShimmerSeconds;

        public readonly double ShimmerDepth;

        public const double MinShimmerSeconds = 1.2;

        public Rgb Surface => MixUntilVisible(0.14, SurfaceVisible);

        public Rgb SurfaceText => Ui;

        public Rgb Outline => MixUntilVisible(0.45, SurfaceVisible);

        public Rgb Accent => RingColourAt(0);

        public Rgb AccentText =>
            Contrast.Ratio(Background, Accent) >= Contrast.Ratio(Ui, Accent) ? Background : Ui;

        public Rgb TrackOff => MixUntilVisible(0.22, SurfaceVisible);

        public Rgb TrackOn => Accent;

        Rgb MixUntilVisible(double from, double floor)
        {
            const double Ceiling = 0.5;
            const double Step = 0.02;

            for (double amount = from; amount <= Ceiling; amount += Step)
            {
                var candidate = Rgb.Mix(Background, Ui, amount);
                if (Contrast.Ratio(candidate, Background) >= floor) return candidate;
            }

            return Rgb.Mix(Background, Ui, Ceiling);
        }

        public const double SurfaceVisible = 1.25;

        public const double LargeTextRequired = 3.0;

        public readonly double DepthFade;

        public readonly double Glow;

        public readonly Rgb TrailHot;

        public readonly Rgb TrailCool;

        public ThemePalette(Theme theme, string background, string[] ringColours, string ball,
                            string ballOutline, string ui, double depthFade, double glow,
                            double shimmerSeconds = 0.0, double shimmerDepth = 0.0,
                            string trailHot = null, string trailCool = null)
        {
            ShimmerSeconds = shimmerSeconds;
            ShimmerDepth = shimmerDepth;
            if (ringColours == null || ringColours.Length == 0)
                throw new ArgumentException("a theme needs at least one ring colour", nameof(ringColours));

            Theme = theme;
            Background = Rgb.Parse(background);
            RingColours = Array.ConvertAll(ringColours, Rgb.Parse);
            Ball = Rgb.Parse(ball);
            BallOutline = Rgb.Parse(ballOutline);
            Ui = Rgb.Parse(ui);
            DepthFade = depthFade;
            Glow = glow;
            TrailHot = trailHot != null ? Rgb.Parse(trailHot) : RingColours[0];
            TrailCool = trailCool != null ? Rgb.Parse(trailCool) : RingColours[0];
        }

        public Rgb RingColourAt(int index) =>
            RingColours[((index % RingColours.Length) + RingColours.Length) % RingColours.Length];

        public List<string> Validate()
        {
            var problems = new List<string>();

            double ballOnBackground = Contrast.Ratio(Ball, Background);
            if (ballOnBackground < Contrast.Required)
                problems.Add($"ball on background is {ballOnBackground:F2}, needs {Contrast.Required:F1}");

            for (int i = 0; i < RingColours.Length; i++)
            {
                double ringOnBackground = Contrast.Ratio(RingColours[i], Background);
                if (ringOnBackground < Contrast.Required)
                    problems.Add($"ring {i} ({RingColours[i]}) on background is {ringOnBackground:F2}, "
                                 + $"needs {Contrast.Required:F1}");

                if (!Contrast.BallReadsAgainstRing(Ball, RingColours[i], BallOutline))
                    problems.Add($"ball is lost against ring {i} ({RingColours[i]}): colour ratio "
                                 + $"{Contrast.Ratio(Ball, RingColours[i]):F2} and the outline "
                                 + $"{BallOutline} does not separate them either");
            }

            double uiOnBackground = Contrast.Ratio(Ui, Background);
            if (uiOnBackground < Contrast.Required)
                problems.Add($"interface on background is {uiOnBackground:F2}, needs {Contrast.Required:F1}");

            return problems;
        }

        public static ThemePalette For(Theme theme)
        {
            switch (theme)
            {
                case Theme.Monochrome:

                    return new ThemePalette(theme, "#FAFAFA", new[] { "#111111" }, "#111111",
                        "#FAFAFA", "#111111", 0.55, 0.0);

                case Theme.Midnight:
                    return new ThemePalette(theme, "#0B1020", new[] { "#E8ECF4" }, "#FFB703",
                        "#0B1020", "#E8ECF4", 0.6, 0.15);

                case Theme.Neon:
                    return new ThemePalette(theme, "#121A33",
                        new[] { "#2D7FF0", "#FFEC6B", "#2D7FF0", "#8B3FF0" }, "#C77DF2",
                        "#121A33", "#FFFFFF", 0.5, 0.6,
                        shimmerSeconds: 2.4, shimmerDepth: 0.10);

                case Theme.Pastel:

                    return new ThemePalette(theme, "#FDF6F0",
                        new[] { "#B97E7E", "#6F91A3", "#76937C", "#9E8B65" }, "#5C5470",
                        "#FDF6F0", "#5C5470", 0.5, 0.0);

                case Theme.Earth:
                    return new ThemePalette(theme, "#EADDC8",
                        new[] { "#3F5E3A", "#6B4F3A", "#8A7A52" }, "#C2562B",
                        "#EADDC8", "#3B2F26", 0.5, 0.0);

                case Theme.Aurora:

                    return new ThemePalette(theme, "#06121A",
                        new[] { "#3FE0A8", "#5BC8F5", "#9B6BF0" }, "#F2F7FF",
                        "#06121A", "#DFF5EC", 0.55, 0.45,
                        shimmerSeconds: 3.6, shimmerDepth: 0.14);

                case Theme.Ember:

                    return new ThemePalette(theme, "#140604",
                        new[] { "#FF7A1A", "#FFB02E", "#E8431F" }, "#FFF4D9",
                        "#140604", "#FFE4CC", 0.50, 0.70,
                        trailHot: "#FFF4D9", trailCool: "#8A1403");

                default:
                    throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        public static Theme[] All => new[]
        {
            Theme.Monochrome, Theme.Midnight, Theme.Neon, Theme.Pastel, Theme.Earth, Theme.Aurora,
        };

        public static Theme[] Every => new[]
        {
            Theme.Monochrome, Theme.Midnight, Theme.Neon, Theme.Pastel, Theme.Earth, Theme.Aurora,
            Theme.Ember,
        };
    }
}
