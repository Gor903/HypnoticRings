using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
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
            var simulator = Simulator;
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(0, simulator.Run(BotBrain.Strong(i), seed: i, maxRings: MaxRings).ClampedRings,
                    $"seed {i} needed the reachability clamp");
        }

        [Test]
        public void Skill_is_ordered_the_way_the_names_claim()
        {
            double weak = MeanScore(BotBrain.Weak);
            double medium = MeanScore(BotBrain.Medium);
            double strong = MeanScore(BotBrain.Strong);

            Assert.Less(weak, medium, $"weak {weak:F1} did not score below medium {medium:F1}");
            Assert.Less(medium, strong, $"medium {medium:F1} did not score below strong {strong:F1}");
        }

        [Test]
        public void A_better_hand_is_worth_a_sensible_multiple_of_a_worse_one()
        {
            double weak = MeanScore(BotBrain.Weak);
            double medium = MeanScore(BotBrain.Medium);

            double ratio = medium / weak;
            Assert.Greater(ratio, 1.2, $"medium {medium:F1} is barely above weak {weak:F1}");
            Assert.Less(ratio, 20.0, $"medium {medium:F1} is {ratio:F1} times weak {weak:F1}");
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
