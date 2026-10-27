using System;

namespace HR.Core
{
    /// <summary>The five themes from CONTRACT.md §2.9. Order is the unlock order.</summary>
    public enum Theme
    {
        Monochrome = 0,
        Midnight = 1,
        Neon = 2,
        Pastel = 3,
        Earth = 4,
    }

    /// <summary>
    /// Score, best, and what the best unlocks. One point per ring, nothing else — no combos and
    /// no multipliers (CONTRACT.md §2.9), which keeps the only number on screen honest.
    /// </summary>
    public sealed class ScoreKeeper
    {
        /// <summary>Best score each theme requires. Three are free so three are always switchable.</summary>
        public static int RequiredBestFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Monochrome:
                case Theme.Midnight:
                case Theme.Neon: return 0;
                case Theme.Pastel: return 25;
                case Theme.Earth: return 50;
                default: throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        int _best;

        public int Score { get; private set; }

        /// <summary>
        /// The best to display during a run — the one the run started with.
        ///
        /// CONTRACT.md G-06: updating it live would mean the player watches their own record
        /// being beaten in real time, and then "NEW BEST" on the game-over screen announces
        /// something they already saw.
        /// </summary>
        public int DisplayedBest { get; private set; }

        public int Best => _best;

        /// <summary>True once this run has gone past the best it started with.</summary>
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

        public void AddRing()
        {
            Score++;
            if (Score > _best) _best = Score;
        }

        /// <summary>Themes unlocked by the best, in order.</summary>
        public bool IsUnlocked(Theme theme) => _best >= RequiredBestFor(theme);

        /// <summary>
        /// Themes this run unlocked, for the game-over screen. Compares against the best the run
        /// started with, so the announcement happens once and in the run that earned it.
        /// </summary>
        public bool UnlockedThisRun(Theme theme)
        {
            int required = RequiredBestFor(theme);
            return required > DisplayedBest && _best >= required;
        }

        /// <summary>Wipes progress. The settings screen asks first (CONTRACT.md §4).</summary>
        public void ResetProgress()
        {
            _best = 0;
            Score = 0;
            DisplayedBest = 0;
        }

        /// <summary>Restores a loaded save. Clamped, because a save file is not trusted input.</summary>
        public void RestoreBest(int best)
        {
            _best = Math.Max(0, best);
            DisplayedBest = _best;
        }
    }
}
