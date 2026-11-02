using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class ContrastTests
    {
        [Test]
        public void Black_on_white_is_the_textbook_maximum()
        {
            Assert.AreEqual(21.0, Contrast.Ratio(Rgb.Parse("#000000"), Rgb.Parse("#FFFFFF")), 0.01);
        }

        [Test]
        public void A_colour_against_itself_is_one()
        {
            Assert.AreEqual(1.0, Contrast.Ratio(Rgb.Parse("#2D7FF0"), Rgb.Parse("#2D7FF0")), 1e-9);
        }

        [Test]
        public void The_ratio_does_not_care_which_way_round_it_is_asked()
        {
            var a = Rgb.Parse("#123456");
            var b = Rgb.Parse("#FEDCBA");
            Assert.AreEqual(Contrast.Ratio(a, b), Contrast.Ratio(b, a), 1e-12);
        }

        [Test]
        public void Hex_parsing_accepts_both_spellings_and_rejects_nonsense()
        {
            Assert.AreEqual("#2D7FF0", Rgb.Parse("#2D7FF0").ToString());
            Assert.AreEqual("#2D7FF0", Rgb.Parse("2D7FF0").ToString());
            Assert.Throws<FormatException>(() => Rgb.Parse("#FFF"));
            Assert.Throws<ArgumentNullException>(() => Rgb.Parse(null));
        }

        [Test]
        public void An_outline_can_carry_the_separation_colour_cannot()
        {
            var ink = Rgb.Parse("#111111");
            var paper = Rgb.Parse("#FAFAFA");

            // Identical ball and ring: colour alone fails, the outline rescues it.
            Assert.AreEqual(1.0, Contrast.Ratio(ink, ink), 1e-9);
            Assert.IsTrue(Contrast.BallReadsAgainstRing(ink, ink, paper));

            // An outline that matches them both rescues nothing.
            Assert.IsFalse(Contrast.BallReadsAgainstRing(ink, ink, ink));
        }
    }

    public class ThemePaletteTests
    {
        [Test]
        public void Every_theme_is_readable()
        {
            // The gate. Checked by measurement rather than by looking, because a palette stays
            // correct only if something fails when a hue is nudged.
            foreach (var theme in ThemePalette.All)
            {
                var problems = ThemePalette.For(theme).Validate();
                Assert.IsEmpty(problems, $"{theme}: {string.Join("; ", problems)}");
            }
        }

        [Test]
        public void The_monochrome_ball_really_is_the_same_ink_as_its_rings()
        {
            // Guards the reasoning, not just the outcome. If this ever stops being true, the
            // outline rule is no longer carrying Monochrome and the exception can go.
            var palette = ThemePalette.For(Theme.Monochrome);
            Assert.AreEqual(1.0, Contrast.Ratio(palette.Ball, palette.RingColourAt(0)), 1e-9);
            Assert.GreaterOrEqual(Contrast.Ratio(palette.BallOutline, palette.Ball), Contrast.Required);
        }

        [Test]
        public void A_palette_that_hides_its_rings_is_rejected()
        {
            // The spec's original Pastel rings, against its near-white background.
            var bad = new ThemePalette(Theme.Pastel, "#FDF6F0",
                new[] { "#F4A6A6", "#A6D8F4", "#B8E6C1", "#FFE0A3" }, "#5C5470",
                "#FDF6F0", "#5C5470", 0.5, 0.0);

            var problems = bad.Validate();
            Assert.IsNotEmpty(problems, "pale rings on near-white should not pass");
            Assert.AreEqual(4, problems.Count, "all four rings should be reported, not just the first");
        }

        [Test]
        public void No_palette_pulses_faster_than_a_player_can_be_asked_to_look_at()
        {
            // Plan [G-34] item 2 and [X-14]. The player stares straight into this tunnel for
            // minutes at a time; a palette that flickers is a health question rather than taste,
            // and the floor is the only thing standing between the two.
            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                if (palette.ShimmerSeconds <= 0.0) continue;

                Assert.GreaterOrEqual(palette.ShimmerSeconds, ThemePalette.MinShimmerSeconds,
                    $"{theme} pulses every {palette.ShimmerSeconds:F1}s");
                Assert.LessOrEqual(palette.ShimmerDepth, 0.25,
                    $"{theme} moves its colours by {palette.ShimmerDepth:P0} a cycle");
            }
        }

        [Test]
        public void A_shimmer_only_ever_makes_a_ring_easier_to_see()
        {
            // Swinging both ways was the obvious reading of "shimmer" and it cost Neon's purple
            // ring its legibility at the bottom of every cycle: 2.92 against a floor of 3.0, a
            // tenth of a second at a time, for as long as the theme was on. The shimmer now
            // moves away from the background and never toward it, and this is the test that
            // keeps it that way.
            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                if (palette.ShimmerDepth <= 0.0) continue;

                var away = Contrast.Luminance(palette.Background) < 0.5
                    ? new Rgb(255, 255, 255) : new Rgb(0, 0, 0);

                for (int i = 0; i < palette.RingColours.Length; i++)
                {
                    var ink = palette.RingColours[i];
                    double still = Contrast.Ratio(ink, palette.Background);

                    // Across the whole cycle, not only at its ends.
                    for (double t = 0.0; t <= 1.0001; t += 0.1)
                    {
                        var shifted = Rgb.Mix(ink, away, t * palette.ShimmerDepth);
                        double moved = Contrast.Ratio(shifted, palette.Background);

                        Assert.GreaterOrEqual(moved, Contrast.Required,
                            $"{theme} ring {i} drops under the floor at {t:P0} of its shimmer");
                        Assert.GreaterOrEqual(moved, still - 1e-9,
                            $"{theme} ring {i} got harder to see at {t:P0} of its shimmer");
                    }
                }
            }
        }

        [Test]
        public void Ring_colours_cycle_in_both_directions()
        {
            var palette = ThemePalette.For(Theme.Neon);
            int n = palette.RingColours.Length;
            Assert.AreEqual(palette.RingColourAt(0).ToString(), palette.RingColourAt(n).ToString());
            Assert.AreEqual(palette.RingColourAt(n - 1).ToString(), palette.RingColourAt(-1).ToString());
        }

        [Test]
        public void A_theme_has_no_way_to_change_how_thick_a_ring_is()
        {
            // Not a behaviour test — a shape test. Thickness is physics and identical in every
            // theme; a palette able to change it could change the game, so the field is absent
            // and this records that its absence is deliberate.
            foreach (var property in typeof(ThemePalette).GetFields())
                StringAssert.DoesNotContain("hickness", property.Name,
                    "a theme must not carry a thickness");
        }

        [Test]
        public void Every_theme_is_defined_and_distinct()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                Assert.AreEqual(theme, palette.Theme);
                Assert.IsTrue(seen.Add(palette.Background.ToString() + palette.Ball),
                    $"{theme} looks identical to another theme");
            }
            Assert.AreEqual(6, seen.Count);
        }

        [Test]
        public void An_unknown_theme_is_an_error_rather_than_a_default()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ThemePalette.For((Theme)99));
        }

        [Test]
        public void The_three_free_themes_are_the_ones_available_from_the_start()
        {
            // Spec §8: three themes unlocked immediately, so "switch between three themes" is
            // satisfiable on a fresh install.
            var score = new ScoreKeeper();
            int free = 0;
            foreach (var theme in ThemePalette.All) if (score.IsUnlocked(theme)) free++;
            Assert.AreEqual(3, free);
        }
    }
}
