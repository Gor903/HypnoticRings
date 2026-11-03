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
            var monochrome = UiSkin.For(Theme.Monochrome);

            Assert.AreEqual(monochrome.CornerRadius, fallback.CornerRadius, 1e-9);
            Assert.AreEqual(monochrome.OutlineWidth, fallback.OutlineWidth, 1e-9);
            Assert.AreEqual(monochrome.Press, fallback.Press);
            Assert.AreEqual(monochrome.Knob, fallback.Knob);
        }

        [Test]
        public void The_themes_do_not_all_look_the_same()
        {
            var corners = new System.Collections.Generic.HashSet<double>();
            foreach (var theme in ThemePalette.All) corners.Add(UiSkin.For(theme).CornerRadius);

            Assert.Greater(corners.Count, 3,
                $"only {corners.Count} distinct corner radii across six themes");
        }

        [Test]
        public void A_soft_palette_does_not_wear_a_hard_edge()
        {
            Assert.AreEqual(0.0, UiSkin.For(Theme.Pastel).OutlineWidth, 1e-9);
            Assert.AreEqual(0.5, UiSkin.For(Theme.Pastel).CornerRadius, 1e-9);
        }

        [Test]
        public void Dark_themes_do_not_sink_and_light_ones_do()
        {
            Assert.AreEqual(PressKind.Flash, UiSkin.For(Theme.Neon).Press);
            Assert.AreEqual(PressKind.Flash, UiSkin.For(Theme.Midnight).Press);

            Assert.AreEqual(PressKind.Ripple, UiSkin.For(Theme.Aurora).Press);

            Assert.AreEqual(PressKind.Sink, UiSkin.For(Theme.Monochrome).Press);
            Assert.AreEqual(PressKind.Sink, UiSkin.For(Theme.Pastel).Press);
        }
    }
}
