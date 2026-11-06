using System;

namespace HR.Core
{
    public enum Theme
    {
        MinimalLight = 10,
        MinimalDark = 11,
        Neon = 12,
        Deco = 13,
        Sumie = 14,
        Ocean = 15,
        Bauhaus = 16,
        Crt = 17,

        Ember = 18,
    }

    public sealed class ScoreKeeper
    {
        public const int NeverByScore = int.MaxValue;

        public static int RequiredBestFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.MinimalLight:
                case Theme.MinimalDark:
                case Theme.Neon: return 0;
                case Theme.Deco: return 25;
                case Theme.Sumie: return 50;
                case Theme.Ocean: return 75;
                case Theme.Bauhaus: return 100;
                case Theme.Crt: return 150;
                case Theme.Ember: return NeverByScore;
                default: throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        int _best;

        public int Score { get; private set; }

        public int DisplayedBest { get; private set; }

        public int Best => _best;

        public bool IsNewBest => Score > DisplayedBest;

        public ScoreKeeper(int best = 0)
        {
            _best = Math.Max(0, best);
            DisplayedBest = _best;
        }

        public void BeginRun()
        {
            Score = 0;
            DisplayedBest = _best;
        }

        public void AddRing() => AddRings(1);

        public void AddRings(int count)
        {
            if (count <= 0) return;
            Score += count;
            if (Score > _best) _best = Score;
        }

        public bool IsUnlocked(Theme theme) => _best >= RequiredBestFor(theme);

        public bool UnlockedThisRun(Theme theme)
        {
            int required = RequiredBestFor(theme);
            return required > DisplayedBest && _best >= required;
        }

        public void ResetProgress()
        {
            _best = 0;
            Score = 0;
            DisplayedBest = 0;
        }

        public void RestoreBest(int best)
        {
            _best = Math.Max(0, best);
            DisplayedBest = _best;
        }
    }
}
