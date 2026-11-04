using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class UiRoleTests
    {
        [Test]
        public void A_control_is_visible_against_the_screen_behind_it()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);
                double ratio = Contrast.Ratio(palette.Surface, palette.Background);

                Assert.GreaterOrEqual(ratio, ThemePalette.SurfaceVisible,
                    $"{theme}: a button fill at {ratio:F2} against its background cannot be seen");
            }
        }

        [Test]
        public void Text_on_a_control_is_readable()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);
                double ratio = Contrast.Ratio(palette.SurfaceText, palette.Surface);

                Assert.GreaterOrEqual(ratio, Contrast.Required,
                    $"{theme}: label at {ratio:F2} on its own button");
            }
        }

        [Test]
        public void The_accent_stands_out_and_carries_its_own_label()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);

                double againstScreen = Contrast.Ratio(palette.Accent, palette.Background);
                Assert.GreaterOrEqual(againstScreen, Contrast.Required,
                    $"{theme}: the accent at {againstScreen:F2} does not stand out at all");

                double label = Contrast.Ratio(palette.AccentText, palette.Accent);
                Assert.GreaterOrEqual(label, ThemePalette.LargeTextRequired,
                    $"{theme}: accent label at {label:F2}");
            }
        }

        [Test]
        public void The_accent_is_not_the_same_colour_as_an_ordinary_button()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);
                double ratio = Contrast.Ratio(palette.Accent, palette.Surface);

                Assert.GreaterOrEqual(ratio, ThemePalette.SurfaceVisible,
                    $"{theme}: the main action is indistinguishable from a secondary one");
            }
        }

        [Test]
        public void A_switch_that_is_off_is_still_something_you_can_see()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);
                double off = Contrast.Ratio(palette.TrackOff, palette.Background);

                Assert.GreaterOrEqual(off, ThemePalette.SurfaceVisible,
                    $"{theme}: an off switch at {off:F2} against the background");
            }
        }

        [Test]
        public void On_and_off_do_not_look_alike()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);
                double between = Contrast.Ratio(palette.TrackOn, palette.TrackOff);

                Assert.GreaterOrEqual(between, ThemePalette.SurfaceVisible,
                    $"{theme}: on and off differ by {between:F2}, which nobody will notice");
            }
        }

        [Test]
        public void The_outline_is_visible_without_shouting()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);
                double edge = Contrast.Ratio(palette.Outline, palette.Background);

                Assert.GreaterOrEqual(edge, ThemePalette.SurfaceVisible,
                    $"{theme}: the edge of a control at {edge:F2}");
                Assert.LessOrEqual(edge, Contrast.Ratio(palette.Ui, palette.Background),
                    $"{theme}: the edge is louder than the text it surrounds");
            }
        }

        [Test]
        public void Every_role_is_a_real_colour_on_every_theme()
        {
            foreach (var theme in ThemePalette.Every)
            {
                var palette = ThemePalette.For(theme);

                Assert.AreNotEqual(palette.Background.ToString(), palette.Surface.ToString(),
                    $"{theme}: a control is the same colour as the screen");
                Assert.AreNotEqual(palette.Surface.ToString(), palette.SurfaceText.ToString(),
                    $"{theme}: a label is the same colour as the button under it");
                Assert.AreNotEqual(palette.Accent.ToString(), palette.AccentText.ToString(),
                    $"{theme}: the accent label is the same colour as the accent");
            }
        }
    }
}
