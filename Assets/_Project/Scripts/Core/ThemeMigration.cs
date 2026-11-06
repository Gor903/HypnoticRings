using System;

namespace HR.Core
{
    public static class ThemeMigration
    {
        public const int LegacyMonochrome = 0;
        public const int LegacyMidnight = 1;
        public const int LegacyNeon = 2;
        public const int LegacyPastel = 3;
        public const int LegacyEarth = 4;
        public const int LegacyAurora = 5;
        public const int LegacyEmber = 6;

        public static bool IsLegacy(int saved) =>
            saved >= LegacyMonochrome && saved <= LegacyEmber;

        public static bool IsCurrent(int saved) => Enum.IsDefined(typeof(Theme), saved);

        public static Theme Map(int saved)
        {
            if (!TryMap(saved, out var theme))
                throw new ArgumentOutOfRangeException(nameof(saved), saved,
                    "neither a current theme nor a legacy one");

            return theme;
        }

        public static bool TryMap(int saved, out Theme theme)
        {
            if (IsCurrent(saved))
            {
                theme = (Theme)saved;
                return true;
            }

            switch (saved)
            {
                case LegacyMonochrome: theme = Theme.MinimalLight; return true;
                case LegacyMidnight: theme = Theme.MinimalDark; return true;
                case LegacyNeon: theme = Theme.Neon; return true;
                case LegacyPastel: theme = Theme.Deco; return true;
                case LegacyEarth: theme = Theme.Sumie; return true;
                case LegacyAurora: theme = Theme.Ocean; return true;
                case LegacyEmber: theme = Theme.MinimalLight; return true;
                default: theme = Theme.MinimalLight; return false;
            }
        }
    }
}
