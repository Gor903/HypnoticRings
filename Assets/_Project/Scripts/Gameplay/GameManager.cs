using System;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// Runs the game: holds the state machine, the score and the difficulty curve, and decides
    /// when the tunnel advances and when the run is over.
    ///
    /// Takes the gesture angle as a value rather than reaching for an InputController, so
    /// HR.Gameplay stays independent of HR.Input (CONTRACT.md §6). Something at scene level
    /// connects the two.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        readonly GameStateMachine _states = new GameStateMachine();

        GameConfig _config;
        TunnelSettings _settings;
        TunnelController _tunnel;
        BallController _ball;
        ScoreKeeper _score;
        DifficultyCurve _curve;
        DifficultyCurve _normalCurve;
        DifficultyCurve _hardcoreCurve;

        int _seed;
        int _appliedDifficultyForScore = -1;

        /// <summary>
        /// Which rules this run is played under. Set before StartRun and never mid-run: a mode
        /// that could change halfway would make the score mean two different things.
        /// </summary>
        public GameMode Mode { get; set; } = GameMode.Normal;

        /// <summary>
        /// Touches a ring is allowed before it kills. One in hardcore, and effectively no limit
        /// otherwise — the ordinary game never ends a run on contact at all.
        /// </summary>
        public const int HardcoreTouchAllowance = 1;

        /// <summary>Fires when a run ends on a second touch, with the ring's number.</summary>
        public event Action<int> KilledByTouch;

        /// <summary>Fires on every state change, with the state left behind.</summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Fires when a ring is cleared, with the new score.</summary>
        public event Action<int> Scored;

        public GameState State => _states.State;
        public int Score => _score?.Score ?? 0;
        public int Best => _score?.Best ?? 0;
        public int DisplayedBest => _score?.DisplayedBest ?? 0;
        public bool IsNewBest => _score?.IsNewBest ?? false;
        public ScoreKeeper ScoreKeeper => _score;
        public GameStateMachine States => _states;

        /// <summary>Runs since Initialise. Used by the restart test to prove full cleanup.</summary>
        public int RunCount { get; private set; }

        public void Initialise(GameConfig config, TunnelController tunnel, BallController ball, int best = 0)
        {
            _config = config;
            _settings = config.ToSettings();
            _tunnel = tunnel;
            _ball = ball;
            _score = new ScoreKeeper(best);
            _normalCurve = new DifficultyCurve(
                _settings, config.difficulty != null ? config.difficulty.ToAnchors() : null);
            _hardcoreCurve = new DifficultyCurve(_settings, DifficultyTable.HardcoreAnchors());
            _curve = _normalCurve;

            _states.Changed += (from, to) => StateChanged?.Invoke(from, to);
            _tunnel.RingPassed += OnRingPassed;
            _tunnel.BallTouchedRing += OnBallTouchedRing;
            _states.Fire(GameTrigger.Loaded);
        }

        void OnDestroy()
        {
            if (_tunnel == null) return;
            _tunnel.RingPassed -= OnRingPassed;
            _tunnel.BallTouchedRing -= OnBallTouchedRing;
        }

        /// <summary>Begins a run. Safe to call again — that is what Retry is.</summary>
        public void StartRun(int seed)
        {
            _seed = seed;
            RunCount++;
            _grace = 0.0;

            // Picked once per run, from the mode set before it started. Swapping curves mid-run
            // would change what the score counts while it was being counted.
            _curve = Mode == GameMode.Hardcore ? _hardcoreCurve : _normalCurve;

            _score.BeginRun();
            _appliedDifficultyForScore = -1;
            ApplyDifficulty();

            // BeginRun returns every ring to the pool and replaces the ball, so nothing from the
            // previous run survives into this one.
            _tunnel.BeginRun(seed);

            if (_states.State == GameState.GameOver) _states.Fire(GameTrigger.Retry);
            else if (_states.State == GameState.Menu) _states.Fire(GameTrigger.Play);
        }

        /// <summary>CONTRACT.md G-02 and G-08: the grace a player gets back instead of a countdown.</summary>
        public const double ResumeGraceSeconds = 0.3;

        double _grace;

        /// <summary>True while the run has resumed but the tunnel is still held still.</summary>
        public bool IsWarmingUp => _grace > 0.0;

        public bool Pause() => _states.Fire(GameTrigger.Pause);

        /// <summary>
        /// Resumes, but holds the tunnel still for a moment first.
        ///
        /// Without it the first step after a pause lands on a player whose finger is not back on
        /// the glass yet, and a tunnel that was mid-squeeze kills them for it. The contract rules
        /// out a 3-2-1 countdown, so the grace is silent and the physics simply does not advance.
        /// </summary>
        public bool Resume()
        {
            bool resumed = _states.Fire(GameTrigger.Resume);
            if (resumed) _grace = ResumeGraceSeconds;
            return resumed;
        }
        public bool GoHome() => _states.Fire(GameTrigger.Home);
        public bool Back() => _states.Fire(GameTrigger.Back);

        /// <summary>Off in tests, which drive Tick explicitly. See TunnelController.AutoStep.</summary>
        public bool AutoStep { get; set; } = true;

        void FixedUpdate()
        {
            if (!AutoStep) return;
            Tick(Time.fixedDeltaTime, _tunnel != null ? _tunnel.UserAngleDeg : 0.0);
        }

        /// <summary>
        /// One step of the game. Public and explicit about dt so tests drive it deterministically.
        /// </summary>
        public void Tick(double dt, double userAngleDeg)
        {
            if (_tunnel == null) return;

            // Everything outside Playing freezes the tunnel. That is what makes a pause a pause
            // and what stops a crush being registered on the game-over screen.
            if (!_states.TunnelRuns) return;

            // Clamped here as well as in the tunnel: the grace below counts down in seconds, and
            // one stalled frame would otherwise spend all of it at once.
            if (double.IsNaN(dt) || dt <= 0.0) return;
            if (dt > TunnelController.MaxStepSeconds) dt = TunnelController.MaxStepSeconds;

            // The grace runs on the clock but nothing else does, so an instant death on the first
            // frame back is impossible by construction rather than by being unlikely.
            if (_grace > 0.0)
            {
                _grace -= dt;
                return;
            }

            _tunnel.UserAngleDeg = userAngleDeg;
            _tunnel.Step(dt);

            ApplyDifficulty();

            if (_tunnel.IsBallOutOfBounds()) { _states.Fire(GameTrigger.Crushed); return; }

            // A ring reaching the red circle is the end of the run. It used to be a question —
            // a second ball could be in play, and the ring might burst taking only the ball
            // inside it — but the spare ball is gone and with it the only case where a crush
            // was survivable (D-088).
            if (_tunnel.RingClosedOnCentre() != null) _states.Fire(GameTrigger.Crushed);
        }

        void OnRingPassed(int ringIndex)
        {
            _score.AddRing();
            Scored?.Invoke(_score.Score);
        }

        /// <summary>
        /// Rings the super ring's wave took. Worth a point each, because the score is a count of
        /// rings and these were rings — paying a bonus instead would split the score from the n
        /// the difficulty curve and the unlock thresholds are written in.
        /// </summary>
        /// <summary>
        /// The hardcore rule, and the only place it lives.
        ///
        /// Deliberately not inside the tunnel: the tunnel counts touches whatever mode is on,
        /// because the ordinary game wants the same count for its knock sound. What a count
        /// MEANS is a rule, and rules belong where the run is owned.
        /// </summary>
        void OnBallTouchedRing(int touches)
        {
            if (Mode != GameMode.Hardcore) return;

            // Hot at the allowance, dead past it. The warning has to arrive on the touch that
            // spends the allowance, not on the one that kills.
            if (touches == HardcoreTouchAllowance) { _tunnel.SetHotRing(0, true); return; }
            if (touches < HardcoreTouchAllowance) return;
            if (!_states.TunnelRuns) return;

            var ring = _tunnel.ResolveBallRing();
            KilledByTouch?.Invoke(ring != null ? ring.SpawnIndex : -1);
            _states.Fire(GameTrigger.Crushed);
        }

        /// <summary>
        /// Moves difficulty to match the score. Only touched when the score actually changed —
        /// the curve is cheap but the snapshot is handed to the generator, and churning it every
        /// step would make "which difficulty made this ring" unanswerable.
        /// </summary>
        void ApplyDifficulty()
        {
            if (_score.Score == _appliedDifficultyForScore) return;
            _appliedDifficultyForScore = _score.Score;

            var snapshot = _curve.SnapshotAt(_score.Score);
            _tunnel.SetDifficulty(snapshot);
            _tunnel.BaseCompressionSpeed = snapshot.BaseCompressionSpeed;
        }

        /// <summary>The difficulty the next ring will be made at. For reports and tests.</summary>
        public DifficultySnapshot CurrentDifficulty => _curve.SnapshotAt(_score?.Score ?? 0);
    }
}
