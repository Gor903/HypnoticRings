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

        /// <summary>
        /// How badly this hand misreads where the gap is, in degrees.
        ///
        /// This is the handicap that actually separates skill levels. Motor noise alone barely
        /// matters — proportional control irons it out — whereas misreading the gap feeds the
        /// error term and, through the rate estimate that differentiates it, the tracking too.
        /// Without it the model is a machine with perfect eyesight, and a machine with perfect
        /// eyesight never loses at any difficulty the game could reasonably set.
        /// </summary>
        public readonly double PerceptionNoiseDeg;

        /// <summary>
        /// How hard the hand corrects, per second of error. Low enough to settle instead of
        /// oscillating; high enough that a half-turn still saturates the speed limit.
        /// </summary>
        public const double ProportionalGain = 4.0;

        /// <summary>How quickly the estimate of the ring's own rotation catches up.</summary>
        public const double DriftSmoothing = 0.25;

        readonly Random _random;

        // Ring buffer of what the bot could have seen, so it can act on a stale view.
        const int Capacity = 512;
        readonly double[] _time = new double[Capacity];
        readonly double[] _ball = new double[Capacity];
        readonly double[] _gap = new double[Capacity];

        // How much of the gap's movement this hand is responsible for. Subtracting it is what
        // isolates the ring's own rotation from the hand's corrections.
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

        /// <summary>
        /// Barely human: quick, fast, steady, and deliberately given perfect eyesight. This is
        /// the reference that answers "is this tunnel solvable at all" — the plan's "strong bot
        /// without noise" — so it is the one level that must never be handicapped.
        /// </summary>
        public static BotBrain Strong(int seed) => new BotBrain(0.05, 220.0, 0.0, seed, 0.0);

        /// <summary>The player the balance is aimed at.</summary>
        public static BotBrain Medium(int seed) => new BotBrain(0.15, 170.0, 18.0, seed, 12.0);

        /// <summary>Slow to notice, slow to turn, shaky. The floor the difficulty must not drop below.</summary>
        public static BotBrain Weak(int seed) => new BotBrain(0.30, 120.0, 45.0, seed, 22.0);

        /// <summary>Most recent error the bot actually perceived, for diagnostics.</summary>
        public double LastPerceivedErrorDeg { get; private set; }

        /// <summary>
        /// How far the finger should sweep this step, in degrees. Positive turns the tunnel the
        /// way angles increase.
        ///
        /// <paramref name="targetAngleDeg"/> is where the gap has to end up.
        ///
        /// It has to be a fixed point, and the reason changed with D-058 without the answer
        /// changing. It used to be that chasing the ball's live angle dragged the resting ball
        /// round by friction and the hand then chased the ball it had just moved. Friction is
        /// zero now and the ball flies, so that loop is gone — but a flying ball sweeps the
        /// whole circle by itself, which makes waiting strictly better than chasing with a
        /// hand that lags. Measured: chasing clears 238/300.
        /// </summary>
        public double Decide(double targetAngleDeg, double gapWorldAngleDeg, double dt)
        {
            if (dt <= 0.0) return 0.0;

            _clock += dt;

            // Misread once, on the way in, so the same bad reading feeds both the error and the
            // rate estimate — as it would for a person looking at the ring.
            double perceived = gapWorldAngleDeg;
            if (PerceptionNoiseDeg > 0.0)
                perceived += (_random.NextDouble() * 2.0 - 1.0) * PerceptionNoiseDeg;

            Record(_clock, targetAngleDeg, perceived);

            int index = SampleAt(_clock - ReactionDelay);
            UpdateDrift(index, dt);

            // The gap has to end up under the ball, and turning the rings moves the gap, so the
            // error is simply how far the gap is from the ball — measured the short way round.
            double error = TunnelMath.ShortestDeltaDeg(_gap[index], _ball[index]);
            LastPerceivedErrorDeg = error;

            // Proportional, not all-or-nothing. Turning at full speed for any error at all makes
            // the hand drag the ball round by friction and then chase the ball it just moved —
            // a runaway that whips the ball to the top of the tunnel and never lets it settle.
            // Easing off near the target is both stable and what a person actually does.
            //
            // The drift term is what lets it hold a ring that is turning on its own. Correction
            // proportional to error alone settles at an error of spin/gain and never closes it,
            // so past about forty rings no hand could ever line a gap up however fast it moved —
            // every skill level died at the same ring for the same arithmetic reason. A person
            // tracking a rotating ring matches its rate first and then nudges to centre, which
            // is exactly this.
            // Minus, because the drift is the gap running away and the hand has to run after it.
            // Adding it instead doubles the standing error rather than cancelling it — the error
            // then parks at a constant offset and never closes, which looks exactly like the
            // tunnel being too hard and is nothing of the kind.
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

        /// <summary>Current estimate of how fast the ring is turning by itself, in degrees a second.</summary>
        public double DriftEstimateDeg => _driftEstimate;

        /// <summary>
        /// Estimates the ring's own rotation from two neighbouring observations, with this hand's
        /// own contribution removed. Read through the delayed window like everything else, so a
        /// slow hand is slow to notice a change of rate too.
        /// </summary>
        void UpdateDrift(int index, double dt)
        {
            if (_count < 2) return;

            // The slot before the oldest sample has never been written. Reading it compares a
            // real observation against a zeroed one, which looks like the ring jumping half a
            // turn in a single step — an estimate of some sixteen thousand degrees a second,
            // smoothed in and poisoning the hand for the rest of the ring. While the buffer is
            // still filling there is simply no earlier sample, and saying so is the fix.
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
