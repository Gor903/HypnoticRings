using System;

namespace HR.Core
{
    /// <summary>
    /// Plays whole runs without an engine, to answer how far a given hand gets.
    ///
    /// It models the question difficulty actually poses — can this hand bring this gap to the
    /// bottom before the tunnel closes — and nothing else. The physical layer (contact, falling,
    /// not tunnelling through an arc) is proven separately by the play-mode gates, and running
    /// five thousand runs through Box2D would take about nine hours against a few seconds here.
    ///
    /// What this therefore does NOT cover, stated plainly so nobody reads more into the numbers:
    /// friction dragging the ball off the bottom, bounce, and anything else that depends on
    /// contact. Those are the play-mode tests' job.
    /// </summary>
    public sealed class BalanceSimulator
    {
        public struct RunResult
        {
            public int Seed;
            public int Score;
            /// <summary>Rings the generator had to slow down to keep catchable.</summary>
            public int ClampedRings;
            /// <summary>True if the run ended because the hand ran out of time, which is the honest way to lose.</summary>
            public bool EndedOnTime;
        }

        readonly TunnelSettings _settings;
        readonly DifficultyCurve _curve;
        readonly double _fallSeconds;

        public BalanceSimulator(in TunnelSettings settings, DifficultyCurve curve)
        {
            _settings = settings;
            _curve = curve;

            // Time the ball needs to drop clear: from resting against the inner surface it must
            // travel one thickness plus its own diameter, from rest, under gravity. The gap has
            // to stay lined up for all of it — lining up for an instant is not a pass.
            double distance = _settings.Thickness + _settings.BallRadius * 2.0;
            _fallSeconds = Math.Sqrt(2.0 * distance / Gravity);
        }

        /// <summary>Must match ProjectSetup.Gravity; CONTRACT.md §2.1.</summary>
        public const double Gravity = 14.0;

        /// <summary>Seconds the opening must stay under the ball for it to get through.</summary>
        public double FallSeconds => _fallSeconds;

        /// <summary>
        /// How far off centre the opening may be and still admit the ball, in degrees. The gap
        /// is an arc length, the ball has width, and what is left over is the aiming tolerance.
        /// </summary>
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

        /// <summary>One ring: can the hand hold the opening under the ball long enough, in time.</summary>
        bool TryClear(BotBrain bot, in RingSpec spec, in DifficultySnapshot difficulty,
                      ref double userAngle, double dt)
        {
            bot.Reset();

            double tolerance = ToleranceDeg(spec.GapArcWidth);
            double budget = difficulty.TimeBudget;
            double ringSpin = 0.0;
            double aligned = 0.0;

            for (double t = 0.0; t < budget; t += dt)
            {
                // The ring turns on its own whatever the hand does; the hand has to out-turn it.
                ringSpin += spec.AutoSpinDeg * dt;
                double gapWorld = spec.GapCentreDeg + ringSpin + userAngle;

                userAngle += bot.Decide(TunnelMath.BottomAngleDeg, gapWorld, dt);

                double error = Math.Abs(TunnelMath.ShortestDeltaDeg(
                    spec.GapCentreDeg + ringSpin + userAngle, TunnelMath.BottomAngleDeg));

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
