using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// A fast regression guard over the balance, not the balance study itself — that is
    /// tools/balance.sh, five thousand runs a level, reported in docs/BALANCE_REPORT.md.
    ///
    /// What these tests are worth is worth being precise about. The bands for the weak and
    /// medium hands are *definitions* of what those skill levels mean; calibrating a bot to a
    /// band and then asserting the band is circular as evidence about the game. Their value is
    /// as a tripwire: a later change to the curve that moves these numbers shows up here.
    ///
    /// The one genuinely independent check is the strong hand, which has no noise at all: if it
    /// ever fails to clear a ring, the curve has produced something nobody could pass.
    /// </summary>
    public class BalanceSimulationTests
    {
        const int Runs = 400;
        const int MaxRings = 200;

        static readonly TunnelSettings Settings = TunnelSettings.Default;

        static BalanceSimulator Simulator =>
            new BalanceSimulator(Settings, new DifficultyCurve(Settings));

        static double MeanScore(Func<int, BotBrain> make, int runs = Runs)
        {
            var simulator = Simulator;
            long total = 0;
            for (int i = 0; i < runs; i++) total += simulator.Run(make(i), seed: i, maxRings: MaxRings).Score;
            return (double)total / runs;
        }

        [Test]
        public void A_flawless_hand_never_meets_a_ring_it_cannot_pass()
        {
            // The gate that is not circular: no noise, no misreading, so any failure is the
            // curve's fault rather than the hand's.
            var simulator = Simulator;
            for (int i = 0; i < 50; i++)
            {
                var result = simulator.Run(BotBrain.Strong(i), seed: i, maxRings: MaxRings);
                Assert.AreEqual(MaxRings, result.Score,
                    $"seed {i}: a flawless hand stopped at ring {result.Score}");
            }
        }

        [Test]
        public void The_generator_never_had_to_slow_a_ring_down()
        {
            // Clamping is the safety net. Needing it means the curve asked for a ring that
            // could not be caught, which would be a design error rather than a near miss.
            var simulator = Simulator;
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(0, simulator.Run(BotBrain.Strong(i), seed: i, maxRings: MaxRings).ClampedRings,
                    $"seed {i} needed the reachability clamp");
        }

        [Test]
        public void Skill_is_ordered_the_way_the_names_claim()
        {
            // The check that actually caught two bugs: a control law that could not track a
            // spinning ring, and a sign error in the term that tracks it. Both showed up as the
            // weak hand outscoring the strong one.
            double weak = MeanScore(BotBrain.Weak);
            double medium = MeanScore(BotBrain.Medium);
            double strong = MeanScore(BotBrain.Strong);

            Assert.Less(weak, medium, $"weak {weak:F1} did not score below medium {medium:F1}");
            Assert.Less(medium, strong, $"medium {medium:F1} did not score below strong {strong:F1}");
        }

        [Test]
        public void The_average_hand_lands_in_the_band_it_is_defined_by()
        {
            double mean = MeanScore(BotBrain.Medium);
            Assert.GreaterOrEqual(mean, 25.0, $"medium averages {mean:F1}, under the band");
            Assert.LessOrEqual(mean, 60.0, $"medium averages {mean:F1}, over the band");
        }

        [Test]
        public void The_weakest_hand_lands_in_the_band_it_is_defined_by()
        {
            double mean = MeanScore(BotBrain.Weak);
            Assert.GreaterOrEqual(mean, 8.0, $"weak averages {mean:F1}, under the band");
            Assert.LessOrEqual(mean, 22.0, $"weak averages {mean:F1}, over the band");
        }

        [Test]
        public void The_same_seed_replays_the_same_run()
        {
            var simulator = Simulator;
            for (int i = 0; i < 20; i++)
            {
                int first = simulator.Run(BotBrain.Medium(i), seed: i, maxRings: MaxRings).Score;
                int second = simulator.Run(BotBrain.Medium(i), seed: i, maxRings: MaxRings).Score;
                Assert.AreEqual(first, second, $"seed {i} did not replay");
            }
        }

        [Test]
        public void A_run_that_ends_early_ended_because_time_ran_out()
        {
            var simulator = Simulator;
            var result = simulator.Run(BotBrain.Weak(1), seed: 1, maxRings: MaxRings);
            Assert.Less(result.Score, MaxRings, "the weak hand was expected to lose eventually");
            Assert.IsTrue(result.EndedOnTime, "a lost run should be a lost race against the squeeze");
        }

        [Test]
        public void The_opening_has_to_stay_lined_up_for_the_whole_fall()
        {
            // Lining the gap up for an instant is not a pass: the ball has to travel a ring's
            // thickness plus its own diameter, from rest, before it is through.
            var simulator = Simulator;
            double expected = Math.Sqrt(2.0 * (Settings.Thickness + Settings.BallRadius * 2.0)
                                        / BalanceSimulator.Gravity);
            Assert.AreEqual(expected, simulator.FallSeconds, 1e-9);
            Assert.Greater(simulator.FallSeconds, 0.3, "the fall window looks implausibly short");
        }

        [Test]
        public void A_wider_opening_forgives_worse_aim()
        {
            var simulator = Simulator;
            Assert.Greater(simulator.ToleranceDeg(3.2), simulator.ToleranceDeg(1.6));
            Assert.AreEqual(0.0, simulator.ToleranceDeg(Settings.BallRadius * 2.0),
                "an opening exactly the ball's width leaves no room for error");
        }
    }
}
