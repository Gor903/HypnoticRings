using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class UiSkinTests
    {
        [Test]
        public void Every_theme_has_a_skin()
        {
            foreach (var theme in ThemePalette.All)
            {
                var skin = UiSkin.For(theme);
                Assert.GreaterOrEqual(skin.CornerRadius, 0.0, $"{theme} has a negative corner");
                Assert.GreaterOrEqual(skin.OutlineWidth, 0.0, $"{theme} has a negative edge");
            }
        }

        [Test]
        public void A_corner_never_exceeds_half_a_control()
        {
            foreach (var theme in ThemePalette.All)
                Assert.LessOrEqual(UiSkin.For(theme).CornerRadius, 0.5,
                    $"{theme} asks for a corner larger than a capsule");
        }

        [Test]
        public void A_theme_that_says_nothing_looks_like_the_one_we_designed_against()
        {
            var fallback = UiSkin.Default;
            var sumie = UiSkin.For(Theme.Sumie);

            Assert.AreEqual(sumie.CornerRadius, fallback.CornerRadius, 1e-9);
            Assert.AreEqual(sumie.OutlineWidth, fallback.OutlineWidth, 1e-9);
            Assert.AreEqual(sumie.Press, fallback.Press);
            Assert.AreEqual(sumie.Knob, fallback.Knob);
        }

        [Test]
        public void The_themes_do_not_all_look_the_same()
        {
            var shapes = new System.Collections.Generic.HashSet<(double, double)>();
            foreach (var theme in ThemePalette.All)
            {
                var skin = UiSkin.For(theme);
                shapes.Add((skin.CornerRadius, skin.Chamfer));
            }

            Assert.Greater(shapes.Count, 3,
                $"only {shapes.Count} distinct corner shapes across seven themes");
        }

        [Test]
        public void A_cut_corner_never_exceeds_half_a_control_either()
        {
            foreach (var theme in ThemePalette.Every)
                Assert.LessOrEqual(UiSkin.For(theme).Chamfer, 0.5,
                    $"{theme} asks for a cut deeper than half the control");
        }

        [Test]
        public void Only_art_deco_and_bauhaus_step_away_from_a_round_corner()
        {
            foreach (var theme in ThemePalette.All)
            {
                var skin = UiSkin.For(theme);
                bool round = skin.Chamfer <= 0.0 && skin.CornerRadius > 0.0;

                if (theme == Theme.Deco || theme == Theme.Bauhaus)
                    Assert.IsFalse(round, $"{theme} should not be a plain rounded box");
                else
                    Assert.IsTrue(round, $"{theme} should be a plain rounded box");
            }
        }

        [Test]
        public void A_hard_shadow_belongs_to_bauhaus_alone()
        {
            foreach (var theme in ThemePalette.Every)
            {
                double shadow = UiSkin.For(theme).HardShadow;
                if (theme == Theme.Bauhaus) Assert.Greater(shadow, 0.0);
                else Assert.AreEqual(0.0, shadow, 1e-9, $"{theme} grew a shadow");
            }
        }

        [Test]
        public void Dark_themes_do_not_sink_and_light_ones_do()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var press = UiSkin.For(theme).Press;

                if (ThemePalette.For(theme).IsDark)
                    Assert.AreNotEqual(PressKind.Sink, press, $"{theme} is dark and sinks");
                else
                    Assert.AreEqual(PressKind.Sink, press, $"{theme} is light and does not sink");
            }

            Assert.AreEqual(PressKind.Ripple, UiSkin.For(Theme.Ocean).Press);
        }
    }
}
