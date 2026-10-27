using System;

namespace HR.Core
{
    /// <summary>
    /// Which note a pass plays.
    ///
    /// CONTRACT.md G-13: two passes within three seconds of each other climb one step of the
    /// pentatonic ladder; a longer gap, a pause or a loss drops back to the bottom, and the
    /// ceiling is one octave. The rule is the whole reward for playing well in a row, so it is
    /// kept here — knowing nothing about audio — where it can be tested at the millisecond
    /// rather than listened to.
    /// </summary>
    public sealed class ToneLadder
    {
        /// <summary>Longer than this between passes and the streak is over.</summary>
        public const double StreakWindowSeconds = 3.0;

        /// <summary>Minor pentatonic plus the octave that caps it.</summary>
        public const int Steps = 6;

        int _step;
        double _lastPass;
        bool _started;

        public int Step => _step;

        /// <summary>Records a pass and returns the step it should sound at.</summary>
        public int Pass(double time)
        {
            bool inStreak = _started && time - _lastPass <= StreakWindowSeconds;
            _step = inStreak ? Math.Min(_step + 1, Steps - 1) : 0;

            _started = true;
            _lastPass = time;
            return _step;
        }

        /// <summary>A pause or a loss ends the streak outright, however recent the last pass.</summary>
        public void Reset()
        {
            _step = 0;
            _started = false;
            _lastPass = 0.0;
        }
    }
}
