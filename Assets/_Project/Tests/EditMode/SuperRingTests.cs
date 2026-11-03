using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// The reward ring: where it lands, what it looks like, and what it does to the rings after
    /// it. All of it is the generator's arithmetic, so none of it needs a scene.
    /// </summary>
    public sealed class SuperRingTests
    {
        static DifficultySnapshot Busy(double multiGapChance = 1.0) =>
            new DifficultySnapshot(0.45, 2.0, 4.32, 0.0, false, 4.8, 300.0, 1.10, 1.28,
                                   multiGapChance, 0.0, 0.18, 3.0);

        [Test]
        public void Every_twelfth_ring_is_the_reward_and_no_other_is()
        {
            var generator = new RingGenerator(7);
            for (int i = 0; i < 120; i++)
            {
                var spec = generator.Next(Busy(multiGapChance: 0.0), 0.0);
                bool shouldBeSuper = i > 0 && i % RingGenerator.SuperRingEvery == 0;

                Assert.AreEqual(shouldBeSuper, spec.Kind == RingKind.Super,
                    $"ring {i} came out as {spec.Kind}");
            }
        }

        [Test]
        public void The_first_ring_of_a_run_is_never_the_reward()
        {
            // Ordinal zero is divisible by twelve, and a run that opens on its own reward
            // teaches the player that rings are free.
            var generator = new RingGenerator(3);
            Assert.AreNotEqual(RingKind.Super, generator.Next(Busy(0.0), 0.0).Kind);
        }

        [Test]
        public void The_reward_is_wider_than_the_curve_asks_and_does_not_spin()
        {
            var generator = new RingGenerator(11);
            var difficulty = Busy(multiGapChance: 0.0);

            RingSpec super = default;
            for (int i = 0; i <= RingGenerator.SuperRingEvery; i++)
                super = generator.Next(difficulty, 40.0);

            Assert.AreEqual(RingKind.Super, super.Kind);
            Assert.AreEqual(1, super.Gaps.Count, "a rest ring with a choice to make is not a rest");
            Assert.AreEqual(difficulty.GapArcWidth * RingGenerator.SuperGapBonus,
                            super.Gaps.A.Opening, 1e-9);
            Assert.AreEqual(0.0, super.AutoSpinDeg, "the reward ring turns itself");
        }

        [Test]
        public void The_two_rings_after_the_reward_are_plain()
        {
            // The wave takes the rings just outside the reward. Handing the player a three-way
            // ring the instant the wave ends turns the rest into a scramble.
            var generator = new RingGenerator(5);
            var difficulty = Busy(multiGapChance: 1.0);

            for (int i = 0; i < RingGenerator.SuperRingEvery; i++) generator.Next(difficulty, 0.0);
            Assert.AreEqual(RingKind.Super, generator.Next(difficulty, 0.0).Kind);

            for (int i = 0; i < RingGenerator.PlainRingsAfterSuper; i++)
                Assert.AreEqual(RingKind.Plain, generator.Next(difficulty, 0.0).Kind,
                    $"ring {i} after the reward was not plain");
        }

        [Test]
        public void Dissolved_rings_are_worth_a_point_each()
        {
            // The score has to stay equal to the number of the ring the ball is in, or the n in
            // the difficulty curve and the n on the screen stop being the same number.
            var score = new ScoreKeeper(best: 0);
            score.BeginRun();

            score.AddRing();
            score.AddRings(3);

            Assert.AreEqual(4, score.Score);
        }

        [Test]
        public void Nothing_is_scored_for_an_empty_wave()
        {
            var score = new ScoreKeeper(best: 0);
            score.BeginRun();
            score.AddRings(0);
            score.AddRings(-2);

            Assert.AreEqual(0, score.Score);
        }

        [Test]
        public void The_reward_replays_from_a_seed_like_everything_else()
        {
            var a = new RingGenerator(99);
            var b = new RingGenerator(99);

            for (int i = 0; i < 40; i++)
            {
                var x = a.Next(Busy(), 0.0);
                var y = b.Next(Busy(), 0.0);
                Assert.AreEqual(x.Kind, y.Kind, $"ring {i} kind diverged");
                Assert.AreEqual(x.Gaps.Count, y.Gaps.Count, $"ring {i} gap count diverged");
                Assert.AreEqual(x.Gaps.A.CentreDeg, y.Gaps.A.CentreDeg, 1e-12);
            }
        }
    }
}
