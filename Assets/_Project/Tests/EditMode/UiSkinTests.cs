using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// The skin: what kind of thing a control is, as opposed to what colour it is.
    ///
    /// All arithmetic and all data, so it belongs here rather than in front of a renderer. What
    /// a rounded corner actually looks like is a question for the frame grab; whether every
    /// theme asks for a sane one is a question for a test.
    /// </summary>
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
            // Past a half the two corners of one side meet, and the renderer starts shrinking
            // the whole sprite to fit them — a control that silently gets smaller the rounder
            // it is asked to be.
            foreach (var theme in ThemePalette.All)
                Assert.LessOrEqual(UiSkin.For(theme).CornerRadius, 0.5,
                    $"{theme} asks for a corner larger than a capsule");
        }

        [Test]
        public void A_theme_that_says_nothing_looks_like_the_one_we_designed_against()
        {
            // A blank default would make an unfinished theme look broken rather than plain,
            // and "plain" is a legitimate answer that Monochrome already gives.
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
            // The whole point of a skin. If every theme agreed on shape, this would be a
            // palette swap wearing a larger word.
            var corners = new System.Collections.Generic.HashSet<double>();
            foreach (var theme in ThemePalette.All) corners.Add(UiSkin.For(theme).CornerRadius);

            Assert.Greater(corners.Count, 3,
                $"only {corners.Count} distinct corner radii across six themes");
        }

        [Test]
        public void A_soft_palette_does_not_wear_a_hard_edge()
        {
            // Pastel is the softest set of colours in the game; an outline around everything
            // would be the one line in the design shouting.
            Assert.AreEqual(0.0, UiSkin.For(Theme.Pastel).OutlineWidth, 1e-9);
            Assert.AreEqual(0.5, UiSkin.For(Theme.Pastel).CornerRadius, 1e-9);
        }

        [Test]
        public void Dark_themes_do_not_sink_and_light_ones_do()
        {
            // A flash needs somewhere dark to read against; on a near-white screen it is a
            // control that briefly disappears. A sink works anywhere and says nothing about the
            // palette, which is why the light themes get it.
            Assert.AreEqual(PressKind.Flash, UiSkin.For(Theme.Neon).Press);
            Assert.AreEqual(PressKind.Flash, UiSkin.For(Theme.Midnight).Press);

            // Aurora is the exception, and deliberately: the slowest, quietest palette in the
            // set can afford a response that spreads from the finger instead of blinking.
            Assert.AreEqual(PressKind.Ripple, UiSkin.For(Theme.Aurora).Press);

            Assert.AreEqual(PressKind.Sink, UiSkin.For(Theme.Monochrome).Press);
            Assert.AreEqual(PressKind.Sink, UiSkin.For(Theme.Pastel).Press);
        }
    }
}
