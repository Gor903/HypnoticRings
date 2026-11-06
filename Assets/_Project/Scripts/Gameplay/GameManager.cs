using System;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    public sealed class GameManager : MonoBehaviour
    {
        readonly GameStateMachine _states = new GameStateMachine();

        GameConfig _config;
        TunnelSettings _settings;
        TunnelController _tunnel;
        BallController _ball;
        ScoreKeeper _score;
        DifficultyCurve _curve;

        int _seed;
        int _appliedDifficultyForScore = -1;

        public GameMode Mode { get; set; } = GameMode.Normal;

        public const int HardcoreTouchAllowance = 1;

        public event Action<int> KilledByTouch;

        public event Action<GameState, GameState> StateChanged;

        public event Action<int> Scored;

        public GameState State => _states.State;
        public int Score => _score?.Score ?? 0;
        public int Best => _score?.Best ?? 0;
        public int DisplayedBest => _score?.DisplayedBest ?? 0;
        public bool IsNewBest => _score?.IsNewBest ?? false;
        public ScoreKeeper ScoreKeeper => _score;
        public GameStateMachine States => _states;

        public int RunCount { get; private set; }

        public void Initialise(GameConfig config, TunnelController tunnel, BallController ball, int best = 0)
        {
            _config = config;
            _settings = config.ToSettings();
            _tunnel = tunnel;
            _ball = ball;
            _score = new ScoreKeeper(best);
            _curve = new DifficultyCurve(
                _settings, config.difficulty != null ? config.difficulty.ToAnchors() : null);

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

        public void StartRun(int seed)
        {
            _seed = seed;
            RunCount++;
            _grace = 0.0;

            _score.BeginRun();
            _appliedDifficultyForScore = -1;
            ApplyDifficulty();

            _tunnel.BeginRun(seed);

            if (_states.State == GameState.GameOver) _states.Fire(GameTrigger.Retry);
            else if (_states.State == GameState.Menu) _states.Fire(GameTrigger.Play);
        }

        public int Seed => _seed;

        public bool StartPlaying() => _states.Fire(GameTrigger.Started);

        public RunSnapshot Snapshot(int continuesUsed)
        {
            var difficulty = CurrentDifficulty;
            return new RunSnapshot(Score, Mode, difficulty.Step, difficulty.MaxCompressionSpeed,
                                   continuesUsed);
        }

        public bool Continue(in RunSnapshot snapshot)
        {
            if (_states.State != GameState.GameOver) return false;

            Mode = snapshot.Mode;
            StartRun(_seed);

            _score.AddRings(snapshot.Score);
            _appliedDifficultyForScore = -1;
            ApplyDifficulty();
            return true;
        }

        public const double ResumeGraceSeconds = 0.3;

        double _grace;

        public bool IsWarmingUp => _grace > 0.0;

        public bool Pause() => _states.Fire(GameTrigger.Pause);

        public bool Resume()
        {
            bool resumed = _states.Fire(GameTrigger.Resume);
            if (resumed) _grace = ResumeGraceSeconds;
            return resumed;
        }
        public bool GoHome() => _states.Fire(GameTrigger.Home);
        public bool Back() => _states.Fire(GameTrigger.Back);

        public bool AutoStep { get; set; } = true;

        void FixedUpdate()
        {
            if (!AutoStep) return;
            Tick(Time.fixedDeltaTime, _tunnel != null ? _tunnel.UserAngleDeg : 0.0);
        }

        public void Tick(double dt, double userAngleDeg)
        {
            if (_tunnel == null) return;

            if (!_states.TunnelRuns) return;

            if (double.IsNaN(dt) || dt <= 0.0) return;
            if (dt > TunnelController.MaxStepSeconds) dt = TunnelController.MaxStepSeconds;

            if (_grace > 0.0)
            {
                _grace -= dt;
                return;
            }

            _tunnel.UserAngleDeg = userAngleDeg;
            _tunnel.Step(dt);

            ApplyDifficulty();

            if (_tunnel.IsBallOutOfBounds()) { _states.Fire(GameTrigger.Crushed); return; }

            if (_tunnel.RingClosedOnCentre() != null) _states.Fire(GameTrigger.Crushed);
        }

        void OnRingPassed(int ringIndex)
        {
            _score.AddRing();
            Scored?.Invoke(_score.Score);
        }

        public const int FreeRingIndex = 0;

        void OnBallTouchedRing(int touches)
        {
            if (Mode != GameMode.Hardcore) return;

            var startRing = _tunnel.ResolveBallRing();
            if (startRing != null && startRing.SpawnIndex == FreeRingIndex) return;

            if (touches == HardcoreTouchAllowance) { _tunnel.SetHotRing(0, true); return; }
            if (touches < HardcoreTouchAllowance) return;
            if (!_states.TunnelRuns) return;

            var ring = _tunnel.ResolveBallRing();
            KilledByTouch?.Invoke(ring != null ? ring.SpawnIndex : -1);
            _states.Fire(GameTrigger.Crushed);
        }

        void ApplyDifficulty()
        {
            if (_score.Score == _appliedDifficultyForScore) return;
            _appliedDifficultyForScore = _score.Score;

            var snapshot = _curve.SnapshotAt(_score.Score);
            _tunnel.SetDifficulty(snapshot);
            _tunnel.BaseCompressionSpeed = snapshot.BaseCompressionSpeed;
        }

        public DifficultySnapshot CurrentDifficulty => _curve.SnapshotAt(_score?.Score ?? 0);
    }
}
