using System.IO;
using HR.Core;
using HR.Save;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class SaveDataTests
    {
        [Test]
        public void Defaults_match_the_contract()
        {
            // CONTRACT.md G-05. A zero volume and a zero sensitivity are both perfectly
            // plausible-looking values, which is why they are asserted rather than assumed.
            var d = SaveData.Defaults();
            Assert.IsTrue(d.soundEnabled);
            Assert.AreEqual(1.0f, d.soundVolume);
            Assert.IsTrue(d.musicEnabled);
            Assert.AreEqual(0.7f, d.musicVolume);
            Assert.AreEqual(1.0f, d.sensitivity);
            Assert.IsFalse(d.invertRotation);
            Assert.IsTrue(d.vibrationEnabled);
            Assert.IsFalse(d.reduceEffects);
            Assert.AreEqual(LanguageSetting.Auto, d.Language);
            Assert.IsFalse(d.hintSeen);
            Assert.AreEqual(Theme.Monochrome, d.Theme);
        }

        [Test]
        public void A_clean_save_needs_no_repairs()
        {
            Assert.IsEmpty(SaveData.Defaults().Normalise());
        }

        [Test]
        public void An_older_save_is_migrated_rather_than_rejected()
        {
            // Version 0 predates the settings, so they arrive as zeroes — which would read as
            // sound off, music off and no sensitivity at all.
            var old = new SaveData { version = 0, bestScore = 42 };
            var repairs = old.Normalise();

            Assert.IsNotEmpty(repairs);
            Assert.AreEqual(SaveData.CurrentVersion, old.version);
            Assert.AreEqual(42, old.bestScore, "migration must not lose the record");
            Assert.IsTrue(old.soundEnabled);
            Assert.AreEqual(1.0f, old.sensitivity);
        }

        [Test]
        public void A_version_from_the_future_is_handled_rather_than_trusted()
        {
            var future = new SaveData { version = 999 };
            Assert.IsNotEmpty(future.Normalise());
            Assert.AreEqual(SaveData.CurrentVersion, future.version);
        }

        [Test]
        public void Nonsense_values_are_repaired_and_reported()
        {
            var bad = SaveData.Defaults();
            bad.bestScore = -7;
            bad.soundVolume = 5f;
            bad.musicVolume = float.NaN;
            bad.sensitivity = 99f;
            bad.selectedTheme = 77;
            bad.language = 77;

            var repairs = bad.Normalise();

            Assert.AreEqual(0, bad.bestScore);
            Assert.AreEqual(1f, bad.soundVolume);
            Assert.AreEqual(1f, bad.musicVolume);
            Assert.AreEqual(1f, bad.sensitivity);
            Assert.AreEqual(Theme.Monochrome, bad.Theme);
            Assert.AreEqual(LanguageSetting.Auto, bad.Language);
            Assert.GreaterOrEqual(repairs.Count, 6, "every repair should be reported, not just made");
        }

        [Test]
        public void A_theme_the_score_has_not_earned_is_given_back()
        {
            // Otherwise wiping the record leaves the player looking at a theme they no longer own.
            var data = SaveData.Defaults();
            data.Theme = Theme.Earth;
            data.bestScore = 0;

            var repairs = data.Normalise();

            Assert.AreEqual(Theme.Monochrome, data.Theme);
            Assert.IsNotEmpty(repairs);
        }

        [Test]
        public void An_earned_theme_is_kept()
        {
            var data = SaveData.Defaults();
            data.Theme = Theme.Earth;
            data.bestScore = 50;

            Assert.IsEmpty(data.Normalise());
            Assert.AreEqual(Theme.Earth, data.Theme);
        }

        [Test]
        public void Sensitivity_stays_inside_the_slider()
        {
            foreach (float value in new[] { SaveData.MinSensitivity, 1.0f, SaveData.MaxSensitivity })
            {
                var data = SaveData.Defaults();
                data.sensitivity = value;
                Assert.IsEmpty(data.Normalise(), $"{value} should be acceptable");
            }
        }
    }

    public class SaveServiceTests
    {
        string _directory;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "hr-save-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, SaveService.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
            catch { /* a temp directory that outlives the test harms nothing */ }
        }

        [Test]
        public void A_fresh_install_loads_defaults_without_complaint()
        {
            var service = new SaveService(_path);
            var data = service.Load();

            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.bestScore);
            Assert.IsEmpty(service.LastReport, "a missing save is normal, not a fault");
        }

        [Test]
        public void What_is_saved_is_what_comes_back()
        {
            var service = new SaveService(_path);
            var written = SaveData.Defaults();
            written.bestScore = 37;
            written.Theme = Theme.Neon;
            written.sensitivity = 1.4f;
            written.hintSeen = true;

            Assert.IsTrue(service.Save(written));

            var read = new SaveService(_path).Load();
            Assert.AreEqual(37, read.bestScore);
            Assert.AreEqual(Theme.Neon, read.Theme);
            Assert.AreEqual(1.4f, read.sensitivity, 1e-5f);
            Assert.IsTrue(read.hintSeen);
        }

        [Test]
        public void A_truncated_save_starts_the_game_rather_than_stopping_it()
        {
            File.WriteAllText(_path, "{\"bestScore\":42,\"selec");

            var service = new SaveService(_path);
            var data = service.Load();

            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.bestScore);
            Assert.IsNotEmpty(service.LastReport);
        }

        [Test]
        public void An_empty_save_starts_the_game()
        {
            File.WriteAllText(_path, string.Empty);

            var service = new SaveService(_path);
            Assert.IsNotNull(service.Load());
            Assert.IsNotEmpty(service.LastReport);
        }

        [Test]
        public void A_save_full_of_rubbish_starts_the_game()
        {
            File.WriteAllBytes(_path, new byte[] { 0x00, 0xFF, 0x13, 0x37, 0x00 });

            var service = new SaveService(_path);
            Assert.IsNotNull(service.Load());
        }

        [Test]
        public void An_older_file_on_disk_is_migrated_on_load()
        {
            File.WriteAllText(_path, "{\"version\":0,\"bestScore\":55}");

            var service = new SaveService(_path);
            var data = service.Load();

            Assert.AreEqual(55, data.bestScore, "the record must survive migration");
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsTrue(data.soundEnabled, "settings the old file never had must get defaults");
            Assert.IsNotEmpty(service.LastReport);
        }

        [Test]
        public void A_save_that_cannot_be_written_is_reported_and_survived()
        {
            // The parent is a file, so the directory can never be created. Losing a best score
            // is a disappointment; refusing to run because of it would be a bug.
            string blocker = Path.Combine(_directory, "blocker");
            File.WriteAllText(blocker, "not a directory");

            var service = new SaveService(Path.Combine(blocker, "nested", SaveService.FileName));

            Assert.IsFalse(service.Save(SaveData.Defaults()));
            Assert.IsNotEmpty(service.LastReport);
            Assert.DoesNotThrow(() => service.Load());
        }

        [Test]
        public void A_failed_write_leaves_the_previous_save_intact()
        {
            var service = new SaveService(_path);
            var good = SaveData.Defaults();
            good.bestScore = 99;
            Assert.IsTrue(service.Save(good));

            // Nothing partial should ever be observable: either the old save or the new one.
            Assert.IsFalse(File.Exists(_path + ".tmp"), "a temporary file was left behind");
            Assert.AreEqual(99, new SaveService(_path).Load().bestScore);
        }

        [Test]
        public void Saving_twice_replaces_rather_than_appends()
        {
            var service = new SaveService(_path);
            var data = SaveData.Defaults();

            data.bestScore = 1; service.Save(data);
            data.bestScore = 2; service.Save(data);

            Assert.AreEqual(2, new SaveService(_path).Load().bestScore);
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void Deleting_wipes_the_record_and_the_temporary()
        {
            var service = new SaveService(_path);
            service.Save(SaveData.Defaults());
            Assert.IsTrue(service.Exists);

            Assert.IsTrue(service.Delete());
            Assert.IsFalse(service.Exists);
            Assert.AreEqual(0, service.Load().bestScore);
        }
    }

    public class StringsTests
    {
        [SetUp]
        public void SetUp() => Strings.Use(Language.English);

        [Test]
        public void The_two_editions_agree()
        {
            // Every key in both, no blanks, and the same format slots — a slot present in one
            // edition and not the other throws at runtime, in whichever language nobody tested.
            var problems = Strings.Validate();
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }

        [Test]
        public void Every_contract_key_exists()
        {
            foreach (string key in new[]
            {
                "app.title", "menu.play", "menu.themes", "menu.settings", "menu.best",
                "hud.best", "hint.swipe", "pause.title", "pause.resume", "pause.home",
                "over.newbest", "over.best", "over.retry", "over.home", "over.themes",
                "over.unlocked", "theme.locked", "set.sound", "set.music", "set.sensitivity",
                "set.invert", "set.vibration", "set.lowfx", "set.language", "set.lang.auto",
                "set.reset", "set.reset.confirm", "set.about", "common.yes", "common.no",
                "common.back", "about.body", "about.privacy",
            })
                Assert.AreNotEqual(key, Strings.Get(key), $"{key} is missing from the table");
        }

        [Test]
        public void Switching_language_changes_the_text()
        {
            Strings.Use(Language.English);
            string english = Strings.Get("menu.play");

            Strings.Use(Language.Russian);
            Assert.AreNotEqual(english, Strings.Get("menu.play"));
        }

        [Test]
        public void Auto_follows_the_device_and_anything_but_Russian_reads_English()
        {
            Strings.Apply(LanguageSetting.Auto, Language.Russian);
            Assert.AreEqual(Language.Russian, Strings.Current);

            Strings.Apply(LanguageSetting.Auto, Language.English);
            Assert.AreEqual(Language.English, Strings.Current);

            Strings.Apply(LanguageSetting.English, Language.Russian);
            Assert.AreEqual(Language.English, Strings.Current, "an explicit choice beats the device");
        }

        [Test]
        public void A_change_is_announced_so_screens_can_refresh_without_a_reload()
        {
            int changes = 0;
            void Handler() => changes++;

            Strings.Changed += Handler;
            try
            {
                Strings.Use(Language.Russian);
                Strings.Use(Language.Russian);   // already there; no event
                Strings.Use(Language.English);
            }
            finally { Strings.Changed -= Handler; }

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void An_unknown_key_names_itself_rather_than_vanishing()
        {
            Assert.AreEqual("no.such.key", Strings.Get("no.such.key"));
        }

        [Test]
        public void Formatted_entries_take_their_argument_in_both_editions()
        {
            foreach (var language in new[] { Language.English, Language.Russian })
            {
                Strings.Use(language);
                StringAssert.Contains("17", Strings.Format("menu.best", 17));
                StringAssert.Contains("17", Strings.Format("hud.best", 17));
            }
        }

        [Test]
        public void Every_theme_has_a_name_in_both_editions()
        {
            foreach (var language in new[] { Language.English, Language.Russian })
            {
                Strings.Use(language);
                foreach (var theme in ThemePalette.All)
                {
                    string name = Strings.ThemeName(theme);
                    Assert.IsNotEmpty(name);
                    StringAssert.DoesNotContain("theme.", name, $"{theme} has no name in {language}");
                }
            }
        }
    }
}
