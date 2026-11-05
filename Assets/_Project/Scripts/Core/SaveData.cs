using System;
using System.Collections.Generic;

namespace HR.Core
{
    public enum LanguageSetting
    {
        Auto = 0,
        Russian = 1,
        English = 2,
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version;

        public int bestScore;
        public int selectedTheme;

        public bool soundEnabled;
        public float soundVolume;
        public bool musicEnabled;
        public float musicVolume;

        public float sensitivity;
        public bool invertRotation;
        public bool vibrationEnabled;
        public bool reduceEffects;

        public int language;

        public bool hintSeen;

        public int hardcoreBest;

        public bool hardcoreHintSeen;

        public static SaveData Defaults() => new SaveData
        {
            version = CurrentVersion,
            bestScore = 0,
            selectedTheme = (int)Theme.MinimalLight,
            soundEnabled = true,
            soundVolume = 1.0f,
            musicEnabled = true,
            musicVolume = 0.7f,
            sensitivity = 1.0f,
            invertRotation = false,
            vibrationEnabled = true,
            reduceEffects = false,
            language = (int)LanguageSetting.Auto,
            hintSeen = false,
        };

        public Theme Theme
        {
            get => ThemeMigration.TryMap(selectedTheme, out var theme) ? theme : Theme.MinimalLight;
            set => selectedTheme = (int)value;
        }

        public LanguageSetting Language
        {
            get => Enum.IsDefined(typeof(LanguageSetting), language)
                ? (LanguageSetting)language : LanguageSetting.Auto;
            set => language = (int)value;
        }

        public List<string> Normalise()
        {
            var repairs = new List<string>();

            if (version < 0 || version > CurrentVersion)
            {
                repairs.Add($"unknown version {version}, treated as {CurrentVersion}");
                version = CurrentVersion;
            }

            if (version < CurrentVersion)
            {
                var defaults = Defaults();
                repairs.Add($"migrated from version {version} to {CurrentVersion}");
                soundEnabled = defaults.soundEnabled;
                soundVolume = defaults.soundVolume;
                musicEnabled = defaults.musicEnabled;
                musicVolume = defaults.musicVolume;
                sensitivity = defaults.sensitivity;
                vibrationEnabled = defaults.vibrationEnabled;
                version = CurrentVersion;
            }

            if (bestScore < 0) { repairs.Add($"negative best score {bestScore}"); bestScore = 0; }

            if (!ThemeMigration.IsCurrent(selectedTheme))
            {
                if (ThemeMigration.TryMap(selectedTheme, out var carried))
                {
                    repairs.Add($"theme {selectedTheme} carried over to {carried}");
                    selectedTheme = (int)carried;
                }
                else
                {
                    repairs.Add($"unknown theme {selectedTheme}");
                    selectedTheme = (int)Core.Theme.MinimalLight;
                }
            }

            if (!Enum.IsDefined(typeof(LanguageSetting), language))
            {
                repairs.Add($"unknown language {language}");
                language = (int)LanguageSetting.Auto;
            }

            if (bestScore < ScoreKeeper.RequiredBestFor(Theme))
            {
                repairs.Add($"theme {Theme} is not unlocked at best {bestScore}");
                selectedTheme = (int)Core.Theme.MinimalLight;
            }

            soundVolume = Clamp01(soundVolume, nameof(soundVolume), repairs);
            musicVolume = Clamp01(musicVolume, nameof(musicVolume), repairs);

            if (sensitivity < MinSensitivity || sensitivity > MaxSensitivity || float.IsNaN(sensitivity))
            {
                repairs.Add($"sensitivity {sensitivity} out of range");
                sensitivity = 1.0f;
            }

            return repairs;
        }

        public const float MinSensitivity = 0.6f;
        public const float MaxSensitivity = 1.6f;

        static float Clamp01(float value, string name, List<string> repairs)
        {
            if (float.IsNaN(value)) { repairs.Add($"{name} was not a number"); return 1.0f; }
            if (value < 0f) { repairs.Add($"{name} was {value}"); return 0f; }
            if (value > 1f) { repairs.Add($"{name} was {value}"); return 1f; }
            return value;
        }

        public SaveData Clone() => (SaveData)MemberwiseClone();
    }
}
