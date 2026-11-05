using System;

namespace HR.Core
{
    public enum CountdownPhase
    {
        Counting = 0,
        Dissolving = 1,
        Done = 2,
    }

    public sealed class CountdownClock
    {
        public const double DigitSeconds = 1.0;

        public const double DissolveSeconds = 0.25;

        public const int FirstDigit = 3;

        public static double Total => FirstDigit * DigitSeconds + DissolveSeconds;

        double _elapsed;

        public double Elapsed => _elapsed;

        public void Restart() => _elapsed = 0.0;

        public void Advance(double dt)
        {
            if (double.IsNaN(dt) || dt <= 0.0) return;
            _elapsed = Math.Min(Total, _elapsed + dt);
        }

        public CountdownPhase Phase
        {
            get
            {
                if (_elapsed >= Total) return CountdownPhase.Done;
                if (_elapsed >= FirstDigit * DigitSeconds) return CountdownPhase.Dissolving;
                return CountdownPhase.Counting;
            }
        }

        public bool Finished => Phase == CountdownPhase.Done;

        public int Digit
        {
            get
            {
                if (Phase != CountdownPhase.Counting) return 0;
                return FirstDigit - (int)(_elapsed / DigitSeconds);
            }
        }

        public bool JustCrossedDissolve(double dt) =>
            Phase == CountdownPhase.Dissolving && _elapsed - dt < FirstDigit * DigitSeconds;
    }

    public readonly struct RunSnapshot
    {
        public readonly int Score;
        public readonly GameMode Mode;
        public readonly double Step;
        public readonly double CompressionSpeed;
        public readonly int ContinuesUsed;

        public RunSnapshot(int score, GameMode mode, double step, double compressionSpeed,
                           int continuesUsed)
        {
            Score = score;
            Mode = mode;
            Step = step;
            CompressionSpeed = compressionSpeed;
            ContinuesUsed = continuesUsed;
        }

        public const int ContinuesPerRun = 1;

        public bool CanContinue => ContinuesUsed < ContinuesPerRun;

        public RunSnapshot Spent() =>
            new RunSnapshot(Score, Mode, Step, CompressionSpeed, ContinuesUsed + 1);
    }
}
