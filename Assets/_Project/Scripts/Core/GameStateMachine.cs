using System;

namespace HR.Core
{
    public enum GameMode
    {
        Normal = 0,

        Hardcore = 1,
    }

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

    public sealed class GameStateMachine
    {
        public GameState State { get; private set; } = GameState.Boot;

        public event Action<GameState, GameState> Changed;

        GameState _themeReturn = GameState.Menu;

        public bool IsPlaying => State == GameState.Playing;

        public bool TunnelRuns => State == GameState.Playing;

        public static bool IsInRun(GameState state) =>
            state == GameState.Playing || state == GameState.Paused || state == GameState.GameOver;

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

        public void Reset()
        {
            State = GameState.Boot;
            _themeReturn = GameState.Menu;
        }
    }
}
