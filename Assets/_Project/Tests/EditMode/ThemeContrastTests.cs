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
                "#FDF6F0", "#5C5470", EndCapStyle.Round, false, 0.5, 0.0);

            var problems = bad.Validate();
            Assert.IsNotEmpty(problems, "pale rings on near-white should not pass");
            Assert.AreEqual(4, problems.Count, "all four rings should be reported, not just the first");
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
            Assert.AreEqual(5, seen.Count);
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
