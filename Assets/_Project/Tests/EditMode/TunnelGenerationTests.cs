using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class RingGeneratorTests
    {
        // A generous time budget so these tests exercise the spacing and direction rules rather
        // than tripping over the reachability ceiling, which has its own tests below.
        static DifficultySnapshot Spinning(double spin, bool alternate = false)
            => new DifficultySnapshot(0.3, 2.0, 2.2, spin, alternate, 20.0, 200.0);

        [Test]
        public void The_same_seed_produces_the_same_tunnel()
        {
            // A balance report nobody can replay cannot be told apart from luck, and a bug
            // report without a seed is a bug nobody can reproduce.
            var a = new RingGenerator(1234);
            var b = new RingGenerator(1234);

            double spinA = 0.0, spinB = 0.0;
            for (int i = 0; i < 200; i++)
            {
                var x = a.Next(Spinning(35.0), spinA);
                var y = b.Next(Spinning(35.0), spinB);
                Assert.AreEqual(x.GapCentreDeg, y.GapCentreDeg, 1e-12);
                Assert.AreEqual(x.AutoSpinDeg, y.AutoSpinDeg, 1e-12);
                spinA = x.AutoSpinDeg;
                spinB = y.AutoSpinDeg;
            }
        }

        [Test]
        public void Different_seeds_produce_different_tunnels()
        {
            var a = new RingGenerator(1);
            var b = new RingGenerator(2);
            bool differed = false;
            for (int i = 0; i < 50 && !differed; i++)
                differed = Math.Abs(a.Next(Spinning(35.0), 0.0).GapCentreDeg
                                    - b.Next(Spinning(35.0), 0.0).GapCentreDeg) > 1e-9;
            Assert.IsTrue(differed);
        }

        [Test]
        public void Reset_replays_the_run_exactly()
        {
            var generator = new RingGenerator(77);
            var first = new double[50];
            for (int i = 0; i < first.Length; i++) first[i] = generator.Next(Spinning(35.0), 0.0).GapCentreDeg;

            generator.Reset();
            Assert.AreEqual(0, generator.Produced);
            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], generator.Next(Spinning(35.0), 0.0).GapCentreDeg, 1e-12);
        }

        [Test]
        public void Gap_centres_land_all_the_way_round()
        {
            var generator = new RingGenerator(5);
            var buckets = new int[8];
            for (int i = 0; i < 4000; i++)
            {
                double centre = generator.Next(Spinning(0.0), 0.0).GapCentreDeg;
                Assert.GreaterOrEqual(centre, 0.0);
                Assert.Less(centre, 360.0);
                buckets[(int)(centre / 45.0)]++;
            }

            // Not a statistics test — just that no eighth of the circle is unreachable, which a
            // sign error or a stray clamp would produce.
            foreach (int count in buckets) Assert.Greater(count, 0, "part of the circle never gets a gap");
        }

        [Test]
        public void The_tutorial_rings_do_not_spin_at_all()
        {
            var generator = new RingGenerator(5);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(0.0, generator.Next(DifficultySnapshot.Opening, 0.0).AutoSpinDeg);
        }

        [Test]
        public void Neighbouring_rings_never_turn_at_the_same_rate()
        {
            // Identical neighbours read as one rigid object instead of a tunnel of rings.
            var generator = new RingGenerator(31);
            double previous = 0.0;

            for (int i = 0; i < 2000; i++)
            {
                double spin = generator.Next(Spinning(35.0), previous).AutoSpinDeg;
                if (Math.Abs(previous) > 1e-6)
                {
                    double ratio = Math.Abs(spin) / Math.Abs(previous);
                    Assert.IsTrue(Math.Abs(ratio - 1.0) >= RingGenerator.NeighbourSeparation - 1e-9,
                        $"ring {i} turns at {spin:F3} next to {previous:F3} — a ratio of {ratio:F3}");
                }
                previous = spin;
            }
        }

        [Test]
        public void Alternating_difficulty_flips_the_direction_every_ring()
        {
            var generator = new RingGenerator(9);
            double previous = 12.0;
            for (int i = 0; i < 50; i++)
            {
                double spin = generator.Next(Spinning(35.0, alternate: true), previous).AutoSpinDeg;
                Assert.AreNotEqual(Math.Sign(previous), Math.Sign(spin), $"ring {i} kept the direction");
                previous = spin;
            }
        }

        [Test]
        public void Gap_width_comes_straight_from_difficulty()
        {
            var generator = new RingGenerator(3);
            var difficulty = new DifficultySnapshot(0.3, 2.0, 2.2, 0.0, false, 20.0, 200.0);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(2.2, generator.Next(difficulty, 0.0).GapArcWidth, 1e-12);
        }
    }

    public class DepthShadingTests
    {
        const double Vanish = 0.9;
        const double Step = 1.55;
        const int Ahead = 3;

        static double Alpha(double radius, double ballRing = 3.0)
            => DepthShading.AlphaFor(radius, ballRing, Vanish, Step, Ahead);

        [Test]
        public void The_ball_ring_and_the_next_are_fully_visible()
        {
            // These are the two the player is actually reading.
            Assert.AreEqual(1.0, Alpha(3.0), 1e-9);
            Assert.AreEqual(1.0, Alpha(3.0 + Step), 1e-9);
        }

        [Test]
        public void A_ring_collapsing_into_the_centre_fades_to_nothing()
        {
            Assert.AreEqual(0.0, Alpha(Vanish), 1e-9);
            Assert.Greater(Alpha(2.0), 0.0);
            Assert.Less(Alpha(2.0), 1.0);
        }

        [Test]
        public void The_newest_ring_arrives_invisible_and_fades_in()
        {
            double outermost = 3.0 + Step * Ahead;
            Assert.AreEqual(0.0, Alpha(outermost), 1e-9);
            Assert.Greater(Alpha(outermost - Step * 0.5), 0.0);
            Assert.AreEqual(1.0, Alpha(outermost - Step), 1e-9);
        }

        [Test]
        public void Nothing_ever_pops_in_or_out()
        {
            // Walk the whole visible range and assert no step in opacity is large enough to read
            // as an appearance or a disappearance.
            double previous = Alpha(Vanish);
            for (double r = Vanish; r <= 3.0 + Step * Ahead; r += 0.01)
            {
                double alpha = Alpha(r);
                Assert.LessOrEqual(Math.Abs(alpha - previous), 0.05, $"opacity jumped at R={r:F2}");
                previous = alpha;
            }
        }

        [Test]
        public void Opacity_stays_within_range_everywhere()
        {
            for (double r = 0.0; r <= 12.0; r += 0.05)
            {
                double alpha = Alpha(r);
                Assert.GreaterOrEqual(alpha, 0.0);
                Assert.LessOrEqual(alpha, 1.0);
            }
        }

        [Test]
        public void A_degenerate_step_does_not_divide_by_zero()
        {
            Assert.AreEqual(1.0, DepthShading.AlphaFor(3.0, 3.0, Vanish, 0.0, Ahead));
        }
    }
}
