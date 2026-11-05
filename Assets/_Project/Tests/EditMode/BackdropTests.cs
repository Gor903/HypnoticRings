using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class BackdropTests
    {
        [Test]
        public void No_backdrop_costs_a_ring_its_readability()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var problems = Backdrop.Validate(theme);
                Assert.IsEmpty(problems, $"{theme}: {string.Join("; ", problems)}");
            }
        }

        [Test]
        public void Nothing_drifts_faster_than_the_ceiling()
        {
            foreach (var theme in ThemePalette.Every)
                foreach (var part in Backdrop.For(theme))
                {
                    if (part.Motion != BackdropMotion.Rise) continue;

                    Assert.Greater(part.Speed, 0.0, $"{theme} has a riser standing still");
                    Assert.LessOrEqual(part.Speed, Backdrop.MaxDrift,
                        $"{theme} drifts {part.Speed:P1} of the height a second");
                }
        }

        [Test]
        public void Nothing_changes_brightness_faster_than_the_floor()
        {
            foreach (var theme in ThemePalette.Every)
                foreach (var part in Backdrop.For(theme))
                {
                    if (part.Motion != BackdropMotion.Twinkle) continue;

                    Assert.GreaterOrEqual(part.Period, Backdrop.MinPeriod,
                        $"{theme} changes every {part.Period:F1}s");
                    Assert.LessOrEqual(part.PeakAlpha, 1.0);
                }
        }

        [Test]
        public void The_plain_theme_has_nothing_behind_it()
        {
            Assert.IsEmpty(Backdrop.For(Theme.MinimalLight));
            Assert.IsEmpty(Backdrop.For(Theme.MinimalDark));
        }

        [Test]
        public void Every_other_theme_brings_something()
        {
            foreach (var theme in ThemePalette.Every)
            {
                if (theme == Theme.MinimalLight || theme == Theme.MinimalDark) continue;

                Assert.IsNotEmpty(Backdrop.For(theme), $"{theme} has an empty backdrop");
            }
        }

        [Test]
        public void The_same_theme_is_laid_out_the_same_way_twice()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var first = Backdrop.For(theme);
                var again = Backdrop.For(theme);

                Assert.AreEqual(first.Length, again.Length, $"{theme} changed its part count");
                for (int i = 0; i < first.Length; i++)
                {
                    Assert.AreEqual(first[i].X, again[i].X, 1e-12, $"{theme} part {i} moved in x");
                    Assert.AreEqual(first[i].Y, again[i].Y, 1e-12, $"{theme} part {i} moved in y");
                    Assert.AreEqual(first[i].Size, again[i].Size, 1e-12,
                        $"{theme} part {i} changed size");
                    Assert.AreEqual(first[i].Alpha, again[i].Alpha, 1e-12,
                        $"{theme} part {i} changed alpha");
                }
            }
        }

        [Test]
        public void A_part_thinner_than_a_ring_is_not_judged_as_a_wash()
        {
            var star = new BackdropElement(BackdropShape.Disc, 0.0, 0.0, 0.004,
                Rgb.Parse("#FFFFFF"), 0.9);
            Assert.Less(star.Coverage, Backdrop.WashCoverage);

            var nebula = new BackdropElement(BackdropShape.Disc, 0.0, 0.0, 0.34,
                Rgb.Parse("#8B5CFF"), 0.13);
            Assert.GreaterOrEqual(nebula.Coverage, Backdrop.WashCoverage);

            var scanline = new BackdropElement(BackdropShape.Rect, 0.0, 0.0, 0.6,
                Rgb.Parse("#4DFF7A"), 0.06, thickness: 0.005);
            Assert.Less(scanline.Coverage, Backdrop.WashCoverage);
        }

        [Test]
        public void An_unknown_theme_is_an_error_rather_than_an_empty_sky()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Backdrop.For((Theme)99));
        }

        [Test]
        public void A_backdrop_that_hides_its_rings_is_rejected()
        {
            var palette = ThemePalette.For(Theme.Crt);
            var wash = new BackdropElement(BackdropShape.Disc, 0.0, 0.0, 0.4,
                Rgb.Parse("#4DFF7A"), 0.9);

            var over = Rgb.Mix(palette.Background, wash.Colour, wash.PeakAlpha);
            Assert.Less(Contrast.Ratio(palette.RingColours[0], over), Contrast.Required,
                "a bright wash under its own ring colour should be caught by the rule");
        }
    }
}
