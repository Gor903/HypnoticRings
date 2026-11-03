using System;
using System.Collections.Generic;

namespace HR.Core
{
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

        /// <summary>
        /// Seconds for one shimmer cycle, or zero for a still palette.
        ///
        /// Floored well above the photosensitivity limit the plan sets ([G-34] item 2 and
        /// [X-14]): a tunnel that pulses faster than this is a health question, not a style, and
        /// the player is staring straight at it.
        /// </summary>
        public readonly double ShimmerSeconds;

        /// <summary>How far the shimmer moves a ring's colour, 0 to 1. Small on purpose.</summary>
        public readonly double ShimmerDepth;

        /// <summary>Slowest a palette may pulse, in seconds per cycle.</summary>
        public const double MinShimmerSeconds = 1.2;

        // --- Interface roles -------------------------------------------------------------
        //
        // A theme used to know two colours: ink for text and paper for the background. Every
        // interface element was painted with the ink and told to keep its own alpha, so a
        // button's fill was the text colour at eight per cent — which is not a colour, it is a
        // rumour. The buttons were invisible, and no amount of styling could have fixed it
        // while the palette had nothing else to offer.
        //
        // These are derived rather than authored so that no theme has to be rewritten and a new
        // one is usable the moment its ink and paper exist. A theme that wants to disagree can
        // override them later; until then the arithmetic is the same for everybody.

        /// <summary>Fill of a button, a card, a panel — the paper a control is cut from.</summary>
        public Rgb Surface => MixUntilVisible(0.14, SurfaceVisible);

        /// <summary>Text on that fill. The theme's ink, unchanged.</summary>
        public Rgb SurfaceText => Ui;

        /// <summary>
        /// The edge of a control. Strong enough to say "this is a thing you press" on its own,
        /// so a quiet fill is a choice rather than the only option.
        /// </summary>
        public Rgb Outline => MixUntilVisible(0.45, SurfaceVisible);

        /// <summary>
        /// The one action a screen is really for: Play, Retry.
        ///
        /// Taken from the theme's first ring colour rather than invented. It is the colour the
        /// player already associates with this theme, it is guaranteed to clear the contrast
        /// floor against the background (every ring colour is checked), and it means a new theme
        /// gets a sensible accent without anybody choosing one.
        /// </summary>
        public Rgb Accent => RingColourAt(0);

        /// <summary>
        /// Text on the accent — whichever of ink and paper stands out more against it. Picked
        /// rather than fixed, because an accent taken from the rings can land anywhere on the
        /// scale and the right answer flips with it.
        /// </summary>
        public Rgb AccentText =>
            Contrast.Ratio(Background, Accent) >= Contrast.Ratio(Ui, Accent) ? Background : Ui;

        /// <summary>A switch that is off. Visible, which the old one was not at all.</summary>
        public Rgb TrackOff => MixUntilVisible(0.22, SurfaceVisible);

        /// <summary>A switch that is on.</summary>
        public Rgb TrackOn => Accent;

        /// <summary>
        /// Walks a colour away from the background until it can actually be seen against it.
        ///
        /// A fixed share of ink is a number that happens to work for the themes in front of you.
        /// Pastel proved it: 14% of a dark plum into a near-white paper came out at 1.23 against
        /// a floor of 1.25 — close enough to look deliberate and still invisible. Stepping until
        /// the floor is met makes the rule true for every palette, including ones nobody has
        /// written yet.
        /// </summary>
        Rgb MixUntilVisible(double from, double floor)
        {
            // Never past halfway: a control that keeps walking toward the ink eventually becomes
            // the text, and then the label on it disappears instead.
            const double Ceiling = 0.5;
            const double Step = 0.02;

            for (double amount = from; amount <= Ceiling; amount += Step)
            {
                var candidate = Rgb.Mix(Background, Ui, amount);
                if (Contrast.Ratio(candidate, Background) >= floor) return candidate;
            }

            return Rgb.Mix(Background, Ui, Ceiling);
        }

        /// <summary>Smallest contrast a control's fill needs against the screen behind it.</summary>
        public const double SurfaceVisible = 1.25;

        /// <summary>Button labels are large type, so the large-text floor applies.</summary>
        public const double LargeTextRequired = 3.0;

        /// <summary>
        /// How strongly rings behind the ball fade out, 0 to 1.
        ///
        /// Scales the depth cue rather than replacing it: a theme can say how much of the tunnel
        /// it wants to keep visible, but not which ring is nearest.
        /// </summary>
        public readonly double DepthFade;

        /// <summary>
        /// Halo around the ball and the rings, 0 to 1. Drawn as geometry, never as a
        /// full-screen pass — a bloom pass on a phone costs more than the whole tunnel.
        /// </summary>
        public readonly double Glow;

        public ThemePalette(Theme theme, string background, string[] ringColours, string ball,
                            string ballOutline, string ui, double depthFade, double glow,
                            double shimmerSeconds = 0.0, double shimmerDepth = 0.0)
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
                    // Deepened from the spec's pale originals, which sat at 1.2–1.8 against a
                    // near-white background — the rings were barely visible, let alone readable.
                    // Hue and saturation are kept; only value moved. DECISIONS D-026.
                    return new ThemePalette(theme, "#FDF6F0",
                        new[] { "#B97E7E", "#6F91A3", "#76937C", "#9E8B65" }, "#5C5470",
                        "#FDF6F0", "#5C5470", 0.5, 0.0);

                case Theme.Earth:
                    return new ThemePalette(theme, "#EADDC8",
                        new[] { "#3F5E3A", "#6B4F3A", "#8A7A52" }, "#C2562B",
                        "#EADDC8", "#3B2F26", 0.5, 0.0);

                case Theme.Aurora:
                    // Cold greens and violets on near-black, the way the thing it is named after
                    // sits over a night sky. The slowest shimmer of the five that have one: the
                    // point is a tunnel that breathes, not one that flickers.
                    return new ThemePalette(theme, "#06121A",
                        new[] { "#3FE0A8", "#5BC8F5", "#9B6BF0" }, "#F2F7FF",
                        "#06121A", "#DFF5EC", 0.55, 0.45,
                        shimmerSeconds: 3.6, shimmerDepth: 0.14);

                default:
                    throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        public static Theme[] All => new[]
        {
            Theme.Monochrome, Theme.Midnight, Theme.Neon, Theme.Pastel, Theme.Earth, Theme.Aurora,
        };
    }
}
