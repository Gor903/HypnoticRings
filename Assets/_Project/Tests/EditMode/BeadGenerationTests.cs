using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>Where the generator puts beads, and what they cost the opening they sit in.</summary>
    public sealed class BeadGenerationTests
    {
        static DifficultySnapshot Busy() =>
            new DifficultySnapshot(0.45, 2.0, 4.32, 0.0, false, 4.8, 300.0, 1.10, 1.28,
                                   multiGapChance: 1.0, tripleShare: 0.0,
                                   thickness: 0.18, referenceRadius: 3.0);

        [Test]
        public void A_plain_ring_never_carries_one()
        {
            var generator = new RingGenerator(2);
            var calm = new DifficultySnapshot(0.45, 2.0, 4.32, 0.0, false, 4.8, 300.0, 1.10, 1.28,
                                              multiGapChance: 0.0, tripleShare: 0.0,
                                              thickness: 0.18, referenceRadius: 3.0);

            for (int i = 0; i < 40; i++)
                Assert.AreEqual(-1, generator.Next(calm, 0.0).BeadGap,
                    $"ring {i} had one opening and a bead slot");
        }

        [Test]
        public void The_first_multi_gap_ring_of_a_run_always_carries_one()
        {
            // Seen rather than waited for: a mechanic that first appears on ring thirty is a
            // mechanic most runs never meet.
            var generator = new RingGenerator(8);

            for (int i = 0; i < 40; i++)
            {
                var spec = generator.Next(Busy(), 0.0);
                if (spec.Kind != RingKind.MultiGap) continue;

                Assert.GreaterOrEqual(spec.BeadGap, 0, "the first multi-gap ring had no bead");
                return;
            }

            Assert.Fail("no multi-gap ring in forty tries");
        }

        [Test]
        public void The_bead_sits_in_the_tightest_way_out()
        {
            // The prize has a price: taking the easy opening and taking the bead have to be
            // different decisions, or the bead rewards doing nothing in particular.
            var generator = new RingGenerator(8);

            for (int i = 0; i < 40; i++)
            {
                var spec = generator.Next(Busy(), 0.0);
                if (spec.BeadGap < 0) continue;

                double bead = spec.Gaps[spec.BeadGap].Opening;
                for (int g = 0; g < spec.Gaps.Count; g++)
                {
                    if (g == spec.BeadGap) continue;
                    Assert.Less(bead, spec.Gaps[g].Opening,
                        $"the bead's opening {bead:F2} is not tighter than opening {g}");
                }
                return;
            }

            Assert.Fail("no bead in forty rings");
        }

        [Test]
        public void Beads_are_spaced_out_rather_than_on_every_ring()
        {
            var generator = new RingGenerator(21);
            int multiGap = 0, beads = 0;

            for (int i = 0; i < 200; i++)
            {
                var spec = generator.Next(Busy(), 0.0);
                if (spec.Kind != RingKind.MultiGap) continue;
                multiGap++;
                if (spec.BeadGap >= 0) beads++;
            }

            Assert.Greater(multiGap, 10, "not enough multi-gap rings to judge spacing");
            Assert.Less(beads, multiGap, "every multi-gap ring carried a bead");
            Assert.Greater(beads, 0, "no ring carried one at all");
        }

        [Test]
        public void The_bead_opening_never_drops_under_the_floor()
        {
            var generator = new RingGenerator(33);
            var tight = new DifficultySnapshot(0.85, 5.0, 2.80, 0.0, false, 2.4, 300.0, 0.72, 0.90,
                                               multiGapChance: 1.0, tripleShare: 1.0,
                                               thickness: 0.18, referenceRadius: 3.0);

            for (int i = 0; i < 200; i++)
            {
                var spec = generator.Next(tight, 0.0);
                if (spec.BeadGap < 0) continue;

                double angle = GapSplit.AngleFor(spec.Gaps[spec.BeadGap].Opening, 0.18, 3.0);
                Assert.GreaterOrEqual(angle, GapSplit.MinGapDeg - 1e-9,
                    $"a bead opening came out at {angle:F1}°, under the floor");
            }
        }

        [Test]
        public void The_reward_ring_never_carries_one()
        {
            // A super ring is a rest. A choice between the easy way out and the prize is the
            // opposite of a rest.
            var generator = new RingGenerator(13);
            for (int i = 0; i <= RingGenerator.SuperRingEvery * 3; i++)
            {
                var spec = generator.Next(Busy(), 0.0);
                if (spec.Kind != RingKind.Super) continue;
                Assert.AreEqual(-1, spec.BeadGap, "the rest ring was given a decision to make");
            }
        }
    }
}
