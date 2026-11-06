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

        public bool IsDark => Contrast.Luminance(Background) < 0.5;

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
            TrailHot = trailHot != null ? Rgb.Parse(trailHot) : Ball;
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
                case Theme.MinimalLight:

                    return new ThemePalette(theme, "#F7F7F5", new[] { "#141414" }, "#141414",
                        "#F7F7F5", "#141414", 0.55, 0.0);

                case Theme.MinimalDark:

                    return new ThemePalette(theme, "#0E0E10", new[] { "#F2F2F0" }, "#F2F2F0",
                        "#0E0E10", "#F2F2F0", 0.55, 0.0);

                case Theme.Neon:
                    return new ThemePalette(theme, "#05060F",
                        new[] { "#19F0FF", "#FF2BD6", "#8B5CFF" }, "#FFFFFF",
                        "#05060F", "#E8F0FF", 0.50, 0.80,
                        shimmerSeconds: 2.4, shimmerDepth: 0.10);

                case Theme.Deco:

                    return new ThemePalette(theme, "#0C1412",
                        new[] { "#D9B25F", "#F0E2B6", "#2E8B72" }, "#FFF6DC",
                        "#0C1412", "#F0E2B6", 0.55, 0.25);

                case Theme.Sumie:

                    return new ThemePalette(theme, "#F1EAD9",
                        new[] { "#1B1B1B", "#4A4A4A", "#6B6B6B" }, "#D7381F",
                        "#F1EAD9", "#1B1B1B", 0.50, 0.0);

                case Theme.Ocean:

                    return new ThemePalette(theme, "#03161F",
                        new[] { "#3DE8D0", "#5AB8FF", "#7A7CFF" }, "#FFD6A5",
                        "#03161F", "#D6F5F2", 0.55, 0.45,
                        shimmerSeconds: 3.6, shimmerDepth: 0.12);

                case Theme.Bauhaus:

                    return new ThemePalette(theme, "#F2E9DC",
                        new[] { "#D62828", "#1E40AF", "#111111" }, "#111111",
                        "#F2E9DC", "#111111", 0.50, 0.0);

                case Theme.Crt:

                    return new ThemePalette(theme, "#020A04",
                        new[] { "#4DFF7A", "#2BBF55", "#8CFFA8" }, "#FFB000",
                        "#020A04", "#B8FFC9", 0.50, 0.35);

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
            Theme.MinimalLight, Theme.Neon, Theme.Deco, Theme.Sumie, Theme.Ocean, Theme.Bauhaus,
            Theme.Crt,
        };

        public static Theme[] Every => new[]
        {
            Theme.MinimalLight, Theme.MinimalDark, Theme.Neon, Theme.Deco, Theme.Sumie,
            Theme.Ocean, Theme.Bauhaus, Theme.Crt, Theme.Ember,
        };
    }
}
