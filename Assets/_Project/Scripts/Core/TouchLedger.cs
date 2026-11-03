using System;

namespace HR.Core
{
    public sealed class TouchLedger
    {
        public const double DebounceSeconds = 0.060;

        public const double MinReversal = 0.05;

        int _ringId = -1;
        int _touches;
        double _lastTouchTime = double.NegativeInfinity;
        double _previousRadialVelocity;
        bool _seenAnything;

        public int Touches => _touches;

        public int RingId => _ringId;

        public void Watch(int ringId)
        {
            if (ringId == _ringId) return;

            _ringId = ringId;
            _touches = 0;
            _lastTouchTime = double.NegativeInfinity;
            _seenAnything = false;
        }

        public void Reset()
        {
            _ringId = -1;
            _touches = 0;
            _lastTouchTime = double.NegativeInfinity;
            _seenAnything = false;
            _previousRadialVelocity = 0.0;
        }

        public bool Step(double time, double radialVelocity, bool inContact)
        {
            double previous = _previousRadialVelocity;
            _previousRadialVelocity = radialVelocity;

            if (!_seenAnything)
            {
                _seenAnything = true;
                return false;
            }

            if (!inContact) return false;
            if (_ringId < 0) return false;

            bool reversed = previous > 0.0 && radialVelocity < 0.0;
            if (!reversed) return false;

            double strength = Math.Abs(previous) + Math.Abs(radialVelocity);
            if (strength < MinReversal) return false;

            if (time - _lastTouchTime < DebounceSeconds) return false;

            _lastTouchTime = time;
            _touches++;
            return true;
        }

        public bool IsOver(int allowed) => _touches > allowed;
    }
}
