using System;

namespace HR.Core
{
    public enum GameState
    {
        Boot,
        Menu,
        Playing,
        Paused,
        GameOver,
        Settings,
        ThemeSelect,
    }

    /// <summary>What the player (or the game) did, rather than where it should end up.</summary>
    public enum GameTrigger
    {
        Loaded,
        Play,
        Pause,
        Resume,
        Home,
        Crushed,
        Retry,
        OpenSettings,
        OpenThemes,
        Back,
    }

    /// <summary>
    /// The screen flow from CONTRACT.md §3, as an explicit table rather than scattered ifs.
    ///
    /// Written as "which trigger is legal in which state" so that an illegal transition is a
    /// loud failure instead of a silent one. The cases that actually bite — a pause arriving
    /// while already paused, a crush arriving after the run already ended, a Back press on the
    /// menu — are then answerable by looking at one table.
    /// </summary>
    public sealed class GameStateMachine
    {
        public GameState State { get; private set; } = GameState.Boot;

        /// <summary>Raised after a transition, with the state left behind.</summary>
        public event Action<GameState, GameState> Changed;

        /// <summary>Where ThemeSelect returns to — it is reachable from two places.</summary>
        GameState _themeReturn = GameState.Menu;

        public bool IsPlaying => State == GameState.Playing;

        /// <summary>True when the tunnel should be advancing. Everything else freezes it.</summary>
        public bool TunnelRuns => State == GameState.Playing;

        /// <summary>
        /// Applies a trigger. Returns false when the trigger does not apply in this state, which
        /// is normal rather than exceptional: a second pause while paused, a crush reported one
        /// step after the run already ended.
        /// </summary>
        public bool Fire(GameTrigger trigger)
        {
            GameState next;

            switch (State)
            {
                case GameState.Boot:
                    if (trigger != GameTrigger.Loaded) return false;
                    next = GameState.Menu;
                    break;

                case GameState.Menu:
                    switch (trigger)
                    {
                        case GameTrigger.Play: next = GameState.Playing; break;
                        case GameTrigger.OpenSettings: next = GameState.Settings; break;
                        case GameTrigger.OpenThemes: _themeReturn = GameState.Menu; next = GameState.ThemeSelect; break;
                        default: return false;
                    }
                    break;

                case GameState.Playing:
                    switch (trigger)
                    {
                        // Back, losing focus and the pause button are the same event here. The
                        // platform reports them differently; the game must not care.
                        case GameTrigger.Pause:
                        case GameTrigger.Back: next = GameState.Paused; break;
                        case GameTrigger.Crushed: next = GameState.GameOver; break;
                        default: return false;
                    }
                    break;

                case GameState.Paused:
                    switch (trigger)
                    {
                        case GameTrigger.Resume:
                        case GameTrigger.Back: next = GameState.Playing; break;
                        case GameTrigger.Home: next = GameState.Menu; break;
                        default: return false;
                    }
                    break;

                case GameState.GameOver:
                    switch (trigger)
                    {
                        case GameTrigger.Retry: next = GameState.Playing; break;
                        case GameTrigger.Home:
                        case GameTrigger.Back: next = GameState.Menu; break;
                        case GameTrigger.OpenThemes: _themeReturn = GameState.GameOver; next = GameState.ThemeSelect; break;
                        default: return false;
                    }
                    break;

                case GameState.Settings:
                    if (trigger != GameTrigger.Back && trigger != GameTrigger.Home) return false;
                    next = GameState.Menu;
                    break;

                case GameState.ThemeSelect:
                    if (trigger != GameTrigger.Back && trigger != GameTrigger.Home) return false;
                    next = _themeReturn;
                    break;

                default:
                    return false;
            }

            var previous = State;
            State = next;
            Changed?.Invoke(previous, next);
            return true;
        }

        /// <summary>Back to a cold start, for a test or a full reset.</summary>
        public void Reset()
        {
            State = GameState.Boot;
            _themeReturn = GameState.Menu;
        }
    }
}
