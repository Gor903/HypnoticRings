using System;
using System.Collections.Generic;

namespace HR.Core
{
    public enum Language
    {
        Russian = 0,
        English = 1,
    }

    /// <summary>
    /// Every string the player can see, in one table, one entry per string.
    ///
    /// Views and controllers hold no literals at all — a string discovered later is a string
    /// that shipped untranslated once. CONTRACT.md §4 is the list; Validate checks the two
    /// editions still agree.
    /// </summary>
    public static class Strings
    {
        static readonly Dictionary<string, string> Russian;
        static readonly Dictionary<string, string> English;

        /// <summary>
        /// Built in a static constructor rather than field initialisers on purpose: those run
        /// in textual order, so a table declared below this point would still be null while the
        /// one above was being filled, and every lookup into it would throw.
        /// </summary>
        static Strings()
        {
            Russian = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["app.title"] = "Hypnotic Rings",
                ["menu.play"] = "ИГРАТЬ",
                ["menu.themes"] = "Темы",
                ["menu.settings"] = "Настройки",
                ["menu.best"] = "Рекорд: {0}",
                ["hud.best"] = "Рекорд {0}",
                ["hint.swipe"] = "Проведи пальцем по кругу",
                ["pause.title"] = "Пауза",
                ["themes.title"] = "Темы",
                ["pause.resume"] = "Продолжить",
                ["pause.home"] = "В меню",
                ["over.newbest"] = "НОВЫЙ РЕКОРД!",
                ["over.best"] = "Рекорд {0}",
                ["over.retry"] = "ЕЩЁ РАЗ",
                ["over.home"] = "В меню",
                ["over.themes"] = "Темы",
                ["over.unlocked"] = "Открыта тема «{0}»",
                ["theme.monochrome"] = "Монохром",
                ["theme.midnight"] = "Полночь",
                ["theme.neon"] = "Неон",
                ["theme.pastel"] = "Пастель",
                ["theme.earth"] = "Земля",
                ["theme.locked"] = "Рекорд {0}",
                ["set.sound"] = "Звук",
                ["set.music"] = "Музыка",
                ["set.sound.level"] = "Громкость звука",
                ["set.music.level"] = "Громкость музыки",
                ["set.sensitivity"] = "Чувствительность",
                ["set.invert"] = "Инверсия вращения",
                ["set.vibration"] = "Вибрация",
                ["set.lowfx"] = "Снизить эффекты",
                ["set.language"] = "Язык",
                ["set.lang.auto"] = "Авто",
                ["set.reset"] = "Сбросить прогресс",
                ["set.reset.confirm"] = "Сбросить рекорд и настройки?",
                ["set.about"] = "Об игре",
                ["common.yes"] = "Да",
                ["common.no"] = "Нет",
                ["common.back"] = "Назад",
                ["about.body"] = "Игра не собирает данные, не содержит рекламы и покупок "
                                 + "и работает без интернета.",
                ["about.privacy"] = "Политика конфиденциальности",
            };

            English = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["app.title"] = "Hypnotic Rings",
                ["menu.play"] = "PLAY",
                ["menu.themes"] = "Themes",
                ["menu.settings"] = "Settings",
                ["menu.best"] = "Best: {0}",
                ["hud.best"] = "Best {0}",
                ["hint.swipe"] = "Swipe in a circle",
                ["pause.title"] = "Paused",
                ["themes.title"] = "Themes",
                ["pause.resume"] = "Resume",
                ["pause.home"] = "Home",
                ["over.newbest"] = "NEW BEST!",
                ["over.best"] = "Best {0}",
                ["over.retry"] = "RETRY",
                ["over.home"] = "Home",
                ["over.themes"] = "Themes",
                ["over.unlocked"] = "Theme “{0}” unlocked",
                ["theme.monochrome"] = "Monochrome",
                ["theme.midnight"] = "Midnight",
                ["theme.neon"] = "Neon",
                ["theme.pastel"] = "Pastel",
                ["theme.earth"] = "Earth",
                ["theme.locked"] = "Best {0}",
                ["set.sound"] = "Sound",
                ["set.music"] = "Music",
                ["set.sound.level"] = "Sound volume",
                ["set.music.level"] = "Music volume",
                ["set.sensitivity"] = "Sensitivity",
                ["set.invert"] = "Invert rotation",
                ["set.vibration"] = "Vibration",
                ["set.lowfx"] = "Reduce effects",
                ["set.language"] = "Language",
                ["set.lang.auto"] = "Auto",
                ["set.reset"] = "Reset progress",
                ["set.reset.confirm"] = "Reset best score and settings?",
                ["set.about"] = "About",
                ["common.yes"] = "Yes",
                ["common.no"] = "No",
                ["common.back"] = "Back",
                ["about.body"] = "This game collects no data, has no ads or purchases, "
                                 + "and works offline.",
                ["about.privacy"] = "Privacy policy",
            };

            Current = Language.English;
        }

        public static Language Current { get; private set; }

        /// <summary>Raised when the language changes, so screens can refresh without a reload.</summary>
        public static event Action Changed;

        public static void Use(Language language)
        {
            if (Current == language) return;
            Current = language;
            Changed?.Invoke();
        }

        /// <summary>
        /// Applies the player's choice. Auto follows the device, and anything that is not
        /// Russian falls back to English — the contract's rule, stated once, here.
        /// </summary>
        public static void Apply(LanguageSetting setting, Language deviceLanguage)
        {
            switch (setting)
            {
                case LanguageSetting.Russian: Use(Language.Russian); break;
                case LanguageSetting.English: Use(Language.English); break;
                default: Use(deviceLanguage); break;
            }
        }

        public static string Get(string key)
        {
            var table = Current == Language.Russian ? Russian : English;
            if (table.TryGetValue(key, out string value)) return value;

            // Falling back to English beats showing a blank label; showing the key beats
            // showing nothing at all, and it names the missing entry on sight.
            if (English.TryGetValue(key, out string fallback)) return fallback;
            return key;
        }

        public static string Format(string key, params object[] arguments) =>
            string.Format(Get(key), arguments);

        public static IEnumerable<string> Keys => English.Keys;

        /// <summary>Entries one edition has and the other does not. Empty when they agree.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();

            foreach (var key in English.Keys)
                if (!Russian.ContainsKey(key)) problems.Add($"missing from Russian: {key}");

            foreach (var key in Russian.Keys)
                if (!English.ContainsKey(key)) problems.Add($"missing from English: {key}");

            foreach (var pair in English)
            {
                if (!Russian.TryGetValue(pair.Key, out string russian)) continue;

                // A format slot present in one edition and not the other throws at runtime, in
                // whichever language nobody tested.
                if (CountSlots(pair.Value) != CountSlots(russian))
                    problems.Add($"format slots differ for {pair.Key}: "
                                 + $"English {CountSlots(pair.Value)}, Russian {CountSlots(russian)}");

                if (string.IsNullOrWhiteSpace(russian)) problems.Add($"Russian entry is blank: {pair.Key}");
                if (string.IsNullOrWhiteSpace(pair.Value)) problems.Add($"English entry is blank: {pair.Key}");
            }

            return problems;
        }

        static int CountSlots(string value)
        {
            int count = 0;
            for (int i = 0; i + 2 < value.Length; i++)
                if (value[i] == '{' && char.IsDigit(value[i + 1]) && value[i + 2] == '}') count++;
            return count;
        }

        /// <summary>The display name of a theme, for the carousel and the unlock message.</summary>
        public static string ThemeName(Theme theme) => Get("theme." + theme.ToString().ToLowerInvariant());
    }
}
