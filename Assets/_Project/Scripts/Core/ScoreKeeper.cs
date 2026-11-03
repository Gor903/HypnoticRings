using System;

namespace HR.Core
{
    public enum Theme
    {
        Monochrome = 0,
        Midnight = 1,
        Neon = 2,
        Pastel = 3,
        Earth = 4,
        Aurora = 5,
    }

    public sealed class ScoreKeeper
    {
        public static int RequiredBestFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Monochrome:
                case Theme.Midnight:
                case Theme.Neon: return 0;
                case Theme.Pastel: return 25;
                case Theme.Earth: return 50;
                case Theme.Aurora: return 100;
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
