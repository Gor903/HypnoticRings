using System;
using System.Collections.Generic;

namespace HR.Core
{
    /// <summary>What the player picked, or Auto to follow the device.</summary>
    public enum LanguageSetting
    {
        Auto = 0,
        Russian = 1,
        English = 2,
    }

    /// <summary>
    /// Everything that survives the app closing. Plain fields and no engine types, so the shape
    /// can be versioned, migrated and tested without a player.
    ///
    /// What is deliberately NOT here: the run in progress. CONTRACT.md G-01 — a run lasts
    /// seconds to minutes, and restoring a ball mid-fall is both harder and less honest than
    /// starting again.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bumped whenever the shape changes. Older files are migrated, never rejected.</summary>
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

        /// <summary>The swipe hint shows once, ever (CONTRACT.md §3).</summary>
        public bool hintSeen;

        /// <summary>CONTRACT.md G-05 lists every one of these.</summary>
        public static SaveData Defaults() => new SaveData
        {
            version = CurrentVersion,
            bestScore = 0,
            selectedTheme = (int)Theme.Monochrome,
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
            get => Enum.IsDefined(typeof(Theme), selectedTheme) ? (Theme)selectedTheme : Theme.Monochrome;
            set => selectedTheme = (int)value;
        }

        public LanguageSetting Language
        {
            get => Enum.IsDefined(typeof(LanguageSetting), language)
                ? (LanguageSetting)language : LanguageSetting.Auto;
            set => language = (int)value;
        }

        /// <summary>
        /// Brings a loaded file into a usable state and reports every repair it made.
        ///
        /// This is the version-migration seam, and it is also the only thing standing between a
        /// hand-edited or truncated file and the game reading nonsense. A save written by an
        /// older build arrives with zeroes in fields it never had — and a zero is a perfectly
        /// plausible-looking value for a volume or a sensitivity, which is exactly why the
        /// repairs have to be deliberate rather than left to defaults falling into place.
        /// </summary>
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
                // Version 0 predates every setting below; they arrive as zeroes, which would
                // read as sound off, music off and a sensitivity of nothing at all.
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

            if (!Enum.IsDefined(typeof(Theme), selectedTheme))
            {
                repairs.Add($"unknown theme {selectedTheme}");
                selectedTheme = (int)Core.Theme.Monochrome;
            }

            if (!Enum.IsDefined(typeof(LanguageSetting), language))
            {
                repairs.Add($"unknown language {language}");
                language = (int)LanguageSetting.Auto;
            }

            // A theme the best score has not earned must not stay selected, or a wiped record
            // would leave the player looking at something they no longer own.
            if (bestScore < ScoreKeeper.RequiredBestFor(Theme))
            {
                repairs.Add($"theme {Theme} is not unlocked at best {bestScore}");
                selectedTheme = (int)Core.Theme.Monochrome;
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

        /// <summary>Spec §10.5 puts the slider between these.</summary>
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
