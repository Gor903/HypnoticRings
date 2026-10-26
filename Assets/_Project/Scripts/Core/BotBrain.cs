using System;

namespace HR.Core
{
    /// <summary>
    /// A simulated player's hand. Decides how far the finger should move this step to bring a
    /// ring's gap under the ball.
    ///
    /// It is deliberately not good at it. A bot that reads the exact state and turns instantly
    /// would prove the tunnel is solvable by a machine, which nobody is asking. The three things
    /// that make it a useful stand-in for a person are all here: it reacts late, it cannot turn
    /// faster than a thumb, and its aim wanders.
    ///
    /// Engine-free, seeded, and it never touches a ring — it only produces finger movement,
    /// which goes through the same GestureTracker a real touch does.
    /// </summary>
    public sealed class BotBrain
    {
        public readonly double ReactionDelay;
        public readonly double MaxGestureSpeedDeg;
        public readonly double NoiseDegPerSecond;

        readonly Random _random;

        // Ring buffer of what the bot could have seen, so it can act on a stale view.
        const int Capacity = 512;
        readonly double[] _time = new double[Capacity];
        readonly double[] _ball = new double[Capacity];
        readonly double[] _gap = new double[Capacity];
        int _head = -1;
        int _count;
        double _clock;

        public BotBrain(double reactionDelay, double maxGestureSpeedDeg, double noiseDegPerSecond, int seed)
        {
            if (reactionDelay < 0.0) throw new ArgumentOutOfRangeException(nameof(reactionDelay));
            if (maxGestureSpeedDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxGestureSpeedDeg));
            if (noiseDegPerSecond < 0.0) throw new ArgumentOutOfRangeException(nameof(noiseDegPerSecond));

            ReactionDelay = reactionDelay;
            MaxGestureSpeedDeg = maxGestureSpeedDeg;
            NoiseDegPerSecond = noiseDegPerSecond;
            _random = new Random(seed);
        }

        /// <summary>Barely human: quick, fast, steady. Used to prove a tunnel is solvable at all.</summary>
        public static BotBrain Strong(int seed) => new BotBrain(0.05, 220.0, 0.0, seed);

        /// <summary>The player the balance is aimed at.</summary>
        public static BotBrain Medium(int seed) => new BotBrain(0.15, 170.0, 18.0, seed);

        /// <summary>Slow to notice, slow to turn, shaky. The floor the difficulty must not drop below.</summary>
        public static BotBrain Weak(int seed) => new BotBrain(0.30, 120.0, 45.0, seed);

        /// <summary>Most recent error the bot actually perceived, for diagnostics.</summary>
        public double LastPerceivedErrorDeg { get; private set; }

        /// <summary>
        /// How far the finger should sweep this step, in degrees. Positive turns the tunnel the
        /// way angles increase.
        /// </summary>
        public double Decide(double ballAngleDeg, double gapWorldAngleDeg, double dt)
        {
            if (dt <= 0.0) return 0.0;

            _clock += dt;
            Record(_clock, ballAngleDeg, gapWorldAngleDeg);

            int index = SampleAt(_clock - ReactionDelay);

            // The gap has to end up under the ball, and turning the rings moves the gap, so the
            // error is simply how far the gap is from the ball — measured the short way round.
            double error = TunnelMath.ShortestDeltaDeg(_gap[index], _ball[index]);
            LastPerceivedErrorDeg = error;

            double step = error;
            double limit = MaxGestureSpeedDeg * dt;
            if (step > limit) step = limit;
            else if (step < -limit) step = -limit;

            if (NoiseDegPerSecond > 0.0)
                step += (_random.NextDouble() * 2.0 - 1.0) * NoiseDegPerSecond * dt;

            return step;
        }

        public void Reset()
        {
            _head = -1;
            _count = 0;
            _clock = 0.0;
            LastPerceivedErrorDeg = 0.0;
        }

        void Record(double time, double ball, double gap)
        {
            _head = (_head + 1) % Capacity;
            _time[_head] = time;
            _ball[_head] = ball;
            _gap[_head] = gap;
            if (_count < Capacity) _count++;
        }

        /// <summary>
        /// Newest sample at or before <paramref name="at"/>. Falls back to the oldest it still
        /// holds, so a delay longer than the buffer degrades into "very stale" rather than
        /// silently becoming "no delay at all".
        /// </summary>
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
