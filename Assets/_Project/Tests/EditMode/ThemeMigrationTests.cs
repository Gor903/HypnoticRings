using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class ThemeMigrationTests
    {
        static readonly (int Saved, Theme Wanted, int OldThreshold)[] Carried =
        {
            (0, Theme.MinimalLight, 0),
            (1, Theme.MinimalDark, 0),
            (2, Theme.Neon, 0),
            (3, Theme.Deco, 25),
            (4, Theme.Sumie, 50),
            (5, Theme.Ocean, 100),
            (6, Theme.MinimalLight, 0),
        };

        [Test]
        public void The_old_numbers_and_the_new_ones_cannot_be_confused()
        {
            foreach (var theme in ThemePalette.Every)
                Assert.IsFalse(ThemeMigration.IsLegacy((int)theme),
                    $"{theme} reuses the number of a theme that is already saved on phones");
        }

        [Test]
        public void Every_old_theme_becomes_a_new_one()
        {
            foreach (var (saved, wanted, _) in Carried)
                Assert.AreEqual(wanted, ThemeMigration.Map(saved), $"saved theme {saved}");
        }

        [Test]
        public void A_carried_theme_is_already_unlocked_at_the_record_that_earned_it()
        {
            foreach (var (saved, wanted, oldThreshold) in Carried)
            {
                int needed = ScoreKeeper.RequiredBestFor(ThemeMigration.Map(saved));
                Assert.LessOrEqual(needed, oldThreshold,
                    $"saved theme {saved} became {wanted}, which asks for {needed} "
                    + $"where the old one asked for {oldThreshold}");
            }
        }

        [Test]
        public void A_current_theme_is_left_alone()
        {
            foreach (var theme in ThemePalette.Every)
                Assert.AreEqual(theme, ThemeMigration.Map((int)theme));
        }

        [Test]
        public void Nonsense_throws_rather_than_guessing()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ThemeMigration.Map(77));
            Assert.Throws<ArgumentOutOfRangeException>(() => ThemeMigration.Map(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ThemeMigration.Map(9));
        }

        [Test]
        public void A_save_carries_its_theme_over_without_losing_the_settings()
        {
            var data = SaveData.Defaults();
            data.bestScore = 60;
            data.selectedTheme = 4;
            data.soundEnabled = false;
            data.musicVolume = 0.3f;
            data.sensitivity = 1.4f;
            data.vibrationEnabled = false;

            var repairs = data.Normalise();

            Assert.AreEqual(Theme.Sumie, data.Theme);
            Assert.IsNotEmpty(repairs, "carrying a theme over is worth reporting");

            Assert.IsFalse(data.soundEnabled, "the migration reset the sound");
            Assert.AreEqual(0.3f, data.musicVolume, 1e-6f, "the migration reset the music");
            Assert.AreEqual(1.4f, data.sensitivity, 1e-6f, "the migration reset the sensitivity");
            Assert.IsFalse(data.vibrationEnabled, "the migration reset the vibration");
            Assert.AreEqual(60, data.bestScore);
        }

        [Test]
        public void A_save_from_a_phone_that_never_had_the_old_themes_is_untouched()
        {
            var data = SaveData.Defaults();
            data.bestScore = 200;
            data.Theme = Theme.Crt;

            Assert.IsEmpty(data.Normalise());
            Assert.AreEqual(Theme.Crt, data.Theme);
        }
    }
}
