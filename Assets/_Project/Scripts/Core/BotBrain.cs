using System;

namespace HR.Core
{
    public sealed class BotBrain
    {
        public readonly double ReactionDelay;
        public readonly double MaxGestureSpeedDeg;
        public readonly double NoiseDegPerSecond;

        public readonly double PerceptionNoiseDeg;

        public const double ProportionalGain = 4.0;

        public const double DriftSmoothing = 0.25;

        readonly Random _random;

        const int Capacity = 512;
        readonly double[] _time = new double[Capacity];
        readonly double[] _ball = new double[Capacity];
        readonly double[] _gap = new double[Capacity];

        readonly double[] _applied = new double[Capacity];

        int _head = -1;
        int _count;
        double _clock;
        double _appliedTotal;
        double _driftEstimate;

        public BotBrain(double reactionDelay, double maxGestureSpeedDeg, double noiseDegPerSecond,
                        int seed, double perceptionNoiseDeg = 0.0)
        {
            if (reactionDelay < 0.0) throw new ArgumentOutOfRangeException(nameof(reactionDelay));
            if (maxGestureSpeedDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxGestureSpeedDeg));
            if (noiseDegPerSecond < 0.0) throw new ArgumentOutOfRangeException(nameof(noiseDegPerSecond));
            if (perceptionNoiseDeg < 0.0) throw new ArgumentOutOfRangeException(nameof(perceptionNoiseDeg));

            ReactionDelay = reactionDelay;
            MaxGestureSpeedDeg = maxGestureSpeedDeg;
            NoiseDegPerSecond = noiseDegPerSecond;
            PerceptionNoiseDeg = perceptionNoiseDeg;
            _random = new Random(seed);
        }

        public static BotBrain Strong(int seed) => new BotBrain(0.05, 220.0, 0.0, seed, 0.0);

        public static BotBrain Medium(int seed) => new BotBrain(0.15, 170.0, 18.0, seed, 12.0);

        public static BotBrain Weak(int seed) => new BotBrain(0.30, 120.0, 45.0, seed, 22.0);

        public double LastPerceivedErrorDeg { get; private set; }

        public double Decide(double targetAngleDeg, double gapWorldAngleDeg, double dt)
        {
            if (dt <= 0.0) return 0.0;

            _clock += dt;

            double perceived = gapWorldAngleDeg;
            if (PerceptionNoiseDeg > 0.0)
                perceived += (_random.NextDouble() * 2.0 - 1.0) * PerceptionNoiseDeg;

            Record(_clock, targetAngleDeg, perceived);

            int index = SampleAt(_clock - ReactionDelay);
            UpdateDrift(index, dt);

            double error = TunnelMath.ShortestDeltaDeg(_gap[index], _ball[index]);
            LastPerceivedErrorDeg = error;

            double rate = error * ProportionalGain - _driftEstimate;
            if (rate > MaxGestureSpeedDeg) rate = MaxGestureSpeedDeg;
            else if (rate < -MaxGestureSpeedDeg) rate = -MaxGestureSpeedDeg;

            double step = rate * dt;

            if (NoiseDegPerSecond > 0.0)
                step += (_random.NextDouble() * 2.0 - 1.0) * NoiseDegPerSecond * dt;

            _appliedTotal += step;
            return step;
        }

        public void Reset()
        {
            _head = -1;
            _count = 0;
            _clock = 0.0;
            _appliedTotal = 0.0;
            _driftEstimate = 0.0;
            LastPerceivedErrorDeg = 0.0;
        }

        public double DriftEstimateDeg => _driftEstimate;

        void UpdateDrift(int index, double dt)
        {
            if (_count < 2) return;

            if (_count < Capacity && index == 0) return;

            int previous = (index - 1 + Capacity) % Capacity;
            double span = _time[index] - _time[previous];
            if (span <= 0.0) return;

            double moved = TunnelMath.ShortestDeltaDeg(_gap[previous], _gap[index]);
            double mine = _applied[index] - _applied[previous];
            double observed = (moved - mine) / span;

            _driftEstimate += (observed - _driftEstimate) * DriftSmoothing;
        }

        void Record(double time, double ball, double gap)
        {
            _head = (_head + 1) % Capacity;
            _time[_head] = time;
            _ball[_head] = ball;
            _gap[_head] = gap;
            _applied[_head] = _appliedTotal;
            if (_count < Capacity) _count++;
        }

        int SampleAt(double at)
        {
            int index = _head;
            for (int i = 0; i < _count; i++)
            {
                int candidate = (_head - i + Capacity) % Capacity;
                index = candidate;
                if (_time[candidate] <= at) return candidate;
            }
            return index;
        }
    }
}
