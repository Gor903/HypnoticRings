using System;

namespace HR.Core
{
    public sealed class ToneLadder
    {
        public const double StreakWindowSeconds = 3.0;

        public const int Steps = 6;

        int _step;
        double _lastPass;
        bool _started;

        public int Step => _step;

        public int Pass(double time)
        {
            bool inStreak = _started && time - _lastPass <= StreakWindowSeconds;
            _step = inStreak ? Math.Min(_step + 1, Steps - 1) : 0;

            _started = true;
            _lastPass = time;
            return _step;
        }

        public void Reset()
        {
            _step = 0;
            _started = false;
            _lastPass = 0.0;
        }
    }
}
