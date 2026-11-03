using System;

namespace HR.Core
{
    public sealed class BalanceSimulator
    {
        public struct RunResult
        {
            public int Seed;
            public int Score;

            public int ClampedRings;

            public bool EndedOnTime;
        }

        readonly TunnelSettings _settings;
        readonly DifficultyCurve _curve;
        readonly double _fallSeconds;

        public BalanceSimulator(in TunnelSettings settings, DifficultyCurve curve)
        {
            _settings = settings;
            _curve = curve;

            double distance = _settings.Thickness + _settings.BallRadius * 2.0;
            _fallSeconds = Math.Sqrt(2.0 * distance / Gravity);
        }

        public const double Gravity = 14.0;

        public double FallSeconds => _fallSeconds;

        public double ToleranceDeg(double gapArcWidth)
        {
            double slack = gapArcWidth - _settings.BallRadius * 2.0;
            if (slack <= 0.0) return 0.0;
            return slack * 0.5 / _settings.TargetRadius * TunnelMath.DegPerRad;
        }

        public RunResult Run(BotBrain bot, int seed, double dt = 1.0 / 90.0, int maxRings = 300)
        {
            var generator = new RingGenerator(seed);
            double userAngle = 0.0;
            double previousSpin = 0.0;
            int score = 0;

            for (; score < maxRings; score++)
            {
                var difficulty = _curve.SnapshotAt(score);
                var spec = generator.Next(difficulty, previousSpin);
                previousSpin = spec.AutoSpinDeg;

                if (!TryClear(bot, spec, difficulty, ref userAngle, dt))
                    return new RunResult
                    {
                        Seed = seed,
                        Score = score,
                        ClampedRings = generator.Clamped,
                        EndedOnTime = true,
                    };
            }

            return new RunResult
            {
                Seed = seed,
                Score = score,
                ClampedRings = generator.Clamped,
                EndedOnTime = false,
            };
        }

        bool TryClear(BotBrain bot, in RingSpec spec, in DifficultySnapshot difficulty,
                      ref double userAngle, double dt)
        {
            bot.Reset();

            double budget = difficulty.TimeBudget;
            double ringSpin = 0.0;
            double aligned = 0.0;

            for (double t = 0.0; t < budget; t += dt)
            {
                ringSpin += spec.AutoSpinDeg * dt;

                int nearest = spec.Gaps.NearestIndex(ringSpin + userAngle, TunnelMath.BottomAngleDeg);
                double tolerance = ToleranceDeg(spec.Gaps[nearest].Opening);
                double gapWorld = spec.Gaps[nearest].CentreDeg + ringSpin + userAngle;

                userAngle += bot.Decide(TunnelMath.BottomAngleDeg, gapWorld, dt);

                double error = Math.Abs(TunnelMath.ShortestDeltaDeg(
                    spec.Gaps[nearest].CentreDeg + ringSpin + userAngle, TunnelMath.BottomAngleDeg));

                if (error <= tolerance)
                {
                    aligned += dt;
                    if (aligned >= _fallSeconds) return true;
                }
                else
                {
                    aligned = 0.0;
                }
            }

            return false;
        }
    }
}
