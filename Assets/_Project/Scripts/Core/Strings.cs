using System;
using System.Collections.Generic;

namespace HR.Core
{
    public enum Language
    {
        Russian = 0,
        English = 1,
    }

    public static class Strings
    {
        static readonly Dictionary<string, string> Russian;
        static readonly Dictionary<string, string> English;

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
                ["theme.minimal"] = "Минимал",
                ["theme.light"] = "Светлая",
                ["theme.dark"] = "Тёмная",
                ["theme.neon"] = "Неон-космос",
                ["theme.deco"] = "Арт-деко",
                ["theme.sumie"] = "Суми-э",
                ["theme.ocean"] = "Глубина",
                ["theme.bauhaus"] = "Баухаус",
                ["theme.crt"] = "Ретро ЭЛТ",
                ["theme.ember"] = "Жар",
                ["menu.hardcore"] = "ХАРДКОР",
                ["hardcore.locked"] = "Рекорд в обычной игре {0}",
                ["hardcore.hint"] = "Каждого кольца можно коснуться только раз",
                ["over.touched"] = "Второе касание кольца № {0}",
                ["theme.locked"] = "Рекорд {0}",
                ["set.title"] = "Настройки",
                ["set.sound"] = "Звук",
                ["set.music"] = "Музыка",
                ["set.sensitivity"] = "Чувствительность",
                ["set.invert"] = "Инверсия вращения",
                ["set.vibration"] = "Вибрация",
                ["set.lowfx"] = "Снизить эффекты",
                ["set.language"] = "Язык",
                ["set.lang.auto"] = "Авто",

                ["set.lang.ru"] = "Русский",
                ["set.lang.en"] = "English",
                ["set.reset"] = "Сбросить прогресс",
                ["set.reset.confirm"] = "Сбросить рекорд и настройки?",
                ["set.about"] = "Об игре",
                ["common.yes"] = "Да",
                ["common.no"] = "Нет",
                ["common.back"] = "Назад",
                ["about.body"] = "Игра показывает рекламу и использует интернет для её "
                                 + "загрузки. Без сети игра работает, но без рекламы.",
                ["about.privacy"] = "Политика конфиденциальности",
                ["about.fonts"] = "Шрифты: Unbounded и Manrope (SIL OFL 1.1)",
                ["ad.label"] = "Реклама",
                ["set.privacy"] = "Параметры конфиденциальности",
                ["over.continue"] = "▶ Продолжить · реклама",
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
                ["theme.minimal"] = "Minimal",
                ["theme.light"] = "Light",
                ["theme.dark"] = "Dark",
                ["theme.neon"] = "Neon cosmos",
                ["theme.deco"] = "Art deco",
                ["theme.sumie"] = "Sumi-e",
                ["theme.ocean"] = "Deep ocean",
                ["theme.bauhaus"] = "Bauhaus",
                ["theme.crt"] = "Retro CRT",
                ["theme.ember"] = "Ember",
                ["menu.hardcore"] = "HARDCORE",
                ["hardcore.locked"] = "Best {0} in the normal game",
                ["hardcore.hint"] = "Each ring may be touched only once",
                ["over.touched"] = "Second touch on ring {0}",
                ["theme.locked"] = "Best {0}",
                ["set.title"] = "Settings",
                ["set.sound"] = "Sound",
                ["set.music"] = "Music",
                ["set.sensitivity"] = "Sensitivity",
                ["set.invert"] = "Invert rotation",
                ["set.vibration"] = "Vibration",
                ["set.lowfx"] = "Reduce effects",
                ["set.language"] = "Language",
                ["set.lang.auto"] = "Auto",
                ["set.lang.ru"] = "Русский",
                ["set.lang.en"] = "English",
                ["set.reset"] = "Reset progress",
                ["set.reset.confirm"] = "Reset best score and settings?",
                ["set.about"] = "About",
                ["common.yes"] = "Yes",
                ["common.no"] = "No",
                ["common.back"] = "Back",
                ["about.body"] = "This game shows ads and uses the internet to load them. "
                                 + "Without a connection the game still works, just without ads.",
                ["about.privacy"] = "Privacy policy",
                ["about.fonts"] = "Fonts: Unbounded and Manrope (SIL OFL 1.1)",
                ["ad.label"] = "Ad",
                ["set.privacy"] = "Privacy settings",
                ["over.continue"] = "▶ Continue · ad",
            };

            Current = Language.English;
        }

        public static Language Current { get; private set; }

        public static event Action Changed;

        public static void Use(Language language)
        {
            if (Current == language) return;
            Current = language;
            Changed?.Invoke();
        }

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

            if (English.TryGetValue(key, out string fallback)) return fallback;
            return key;
        }

        public static string Format(string key, params object[] arguments) =>
            string.Format(Get(key), arguments);

        public static IEnumerable<string> Keys => English.Keys;

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

        public static string ThemeName(Theme theme) => Get(ThemeKey(theme));

        public static string ThemeKey(Theme theme) =>
            theme == Theme.MinimalLight || theme == Theme.MinimalDark
                ? "theme.minimal"
                : "theme." + theme.ToString().ToLowerInvariant();

        public static string VariantKey(Theme theme) =>
            theme == Theme.MinimalDark ? "theme.dark" : "theme.light";
    }
}
