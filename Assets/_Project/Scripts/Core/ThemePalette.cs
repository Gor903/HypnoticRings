using System;
using System.Collections.Generic;

namespace HR.Core
{
    /// <summary>How a segment's ends are drawn. Physics always rounds them; this is looks only.</summary>
    public enum EndCapStyle
    {
        Round = 0,
        Flat = 1,
    }

    /// <summary>
    /// A theme's colours and the few looks-only switches that go with them, as plain data.
    ///
    /// Engine-free on purpose: every palette is then checkable by a test, which is the only way
    /// one stays readable after somebody nudges a hue. CONTRACT.md §2.11 and spec §11.3.
    ///
    /// Note what is absent: there is no visual thickness. Thickness is physics, it is identical
    /// in every theme, and giving a theme a way to change it would let a palette change the
    /// game.
    /// </summary>
    public sealed class ThemePalette
    {
        public readonly Theme Theme;
        public readonly Rgb Background;
        public readonly Rgb[] RingColours;
        public readonly Rgb Ball;

        /// <summary>
        /// Ring around the ball, in a colour that separates it from whatever it is resting on.
        /// Not decoration — for Monochrome it is the only thing that distinguishes ball from
        /// ring, since both are the same ink by design.
        /// </summary>
        public readonly Rgb BallOutline;

        public readonly Rgb Ui;
        public readonly EndCapStyle EndCaps;
        public readonly bool SegmentSeams;

        /// <summary>How strongly rings behind the ball fade out, 0 to 1.</summary>
        public readonly double DepthFade;

        /// <summary>Halo around the ball, 0 to 1. Drawn as geometry, never as a full-screen pass.</summary>
        public readonly double Glow;

        public ThemePalette(Theme theme, string background, string[] ringColours, string ball,
                            string ballOutline, string ui, EndCapStyle endCaps, bool segmentSeams,
                            double depthFade, double glow)
        {
            if (ringColours == null || ringColours.Length == 0)
                throw new ArgumentException("a theme needs at least one ring colour", nameof(ringColours));

            Theme = theme;
            Background = Rgb.Parse(background);
            RingColours = Array.ConvertAll(ringColours, Rgb.Parse);
            Ball = Rgb.Parse(ball);
            BallOutline = Rgb.Parse(ballOutline);
            Ui = Rgb.Parse(ui);
            EndCaps = endCaps;
            SegmentSeams = segmentSeams;
            DepthFade = depthFade;
            Glow = glow;
        }

        public Rgb RingColourAt(int index) =>
            RingColours[((index % RingColours.Length) + RingColours.Length) % RingColours.Length];

        /// <summary>
        /// Everything about this palette that is unreadable, empty when it is fine.
        ///
        /// Three relationships have to hold: the ball against the background, each ring against
        /// the background, and the ball against each ring — the last of which may be carried by
        /// the outline instead of by colour.
        /// </summary>
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

        /// <summary>The five palettes from spec §11.3, with the adjustments DECISIONS records.</summary>
        public static ThemePalette For(Theme theme)
        {
            switch (theme)
            {
                case Theme.Monochrome:
                    // Ball and rings are the same ink on purpose — the reference image is black
                    // rings on white. The background-coloured outline is what keeps them apart.
                    return new ThemePalette(theme, "#FAFAFA", new[] { "#111111" }, "#111111",
                        "#FAFAFA", "#111111", EndCapStyle.Round, false, 0.55, 0.0);

                case Theme.Midnight:
                    return new ThemePalette(theme, "#0B1020", new[] { "#E8ECF4" }, "#FFB703",
                        "#0B1020", "#E8ECF4", EndCapStyle.Round, false, 0.6, 0.0);

                case Theme.Neon:
                    return new ThemePalette(theme, "#121A33",
                        new[] { "#2D7FF0", "#FFEC6B", "#2D7FF0", "#8B3FF0" }, "#C77DF2",
                        "#121A33", "#FFFFFF", EndCapStyle.Flat, true, 0.5, 0.6);

                case Theme.Pastel:
                    // Deepened from the spec's pale originals, which sat at 1.2–1.8 against a
                    // near-white background — the rings were barely visible, let alone readable.
                    // Hue and saturation are kept; only value moved. DECISIONS D-026.
                    return new ThemePalette(theme, "#FDF6F0",
                        new[] { "#B97E7E", "#6F91A3", "#76937C", "#9E8B65" }, "#5C5470",
                        "#FDF6F0", "#5C5470", EndCapStyle.Round, false, 0.5, 0.0);

                case Theme.Earth:
                    return new ThemePalette(theme, "#EADDC8",
                        new[] { "#3F5E3A", "#6B4F3A", "#8A7A52" }, "#C2562B",
                        "#EADDC8", "#3B2F26", EndCapStyle.Round, false, 0.5, 0.0);

                default:
                    throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        public static Theme[] All => new[]
        {
            Theme.Monochrome, Theme.Midnight, Theme.Neon, Theme.Pastel, Theme.Earth,
        };
    }
}
