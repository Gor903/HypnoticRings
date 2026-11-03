using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// The rule that makes several openings a difficulty feature rather than a present.
    /// Everything here is arithmetic, so it is checked here rather than by watching a bot.
    /// </summary>
    public sealed class GapSplitTests
    {
        readonly double[] _buffer = new double[RingGaps.MaxGaps];

        /// <summary>
        /// The floor these cases are written against, pinned rather than read.
        ///
        /// It is no longer a constant in degrees — it is derived per ring from the ball — so a
        /// test that wants to check the splitting rule has to say which floor it means, or it
        /// would quietly re-measure itself every time the ball changed size.
        /// </summary>
        const double Floor = 20.0;

        [Test]
        public void One_opening_gets_the_whole_angle()
        {
            GapSplit.Split(86.0, 1, _buffer, Floor);
            Assert.AreEqual(86.0, _buffer[0], 1e-9);
        }

        [Test]
        public void Several_openings_are_narrower_together_than_the_one_they_replace()
        {
            // The load-bearing property. A flying ball leaves through whichever way out it
            // reaches, so its chance of clearing a ring follows the total angle: split a gap
            // into equal halves and the ring is no harder to clear but far easier to aim at.
            foreach (double single in new[] { 86.0, 78.0, 70.0, 57.0 })
            {
                for (int wanted = 2; wanted <= RingGaps.MaxGaps; wanted++)
                {
                    // Only counts the ring could afford: asking for three ways out of a 57° gap
                    // is refused upstream rather than cut and then floored back up.
                    int count = GapSplit.LargestAffordableCount(single, wanted, Floor);
                    if (count < 2) continue;
                    GapSplit.Split(single, count, _buffer, Floor);

                    double total = 0.0;
                    for (int i = 0; i < count; i++) total += _buffer[i];

                    Assert.Less(total, single,
                        $"{count} openings out of {single}° came to {total:F1}°, which is a gift");
                }
            }
        }

        [Test]
        public void Three_openings_give_up_more_than_two()
        {
            GapSplit.Split(86.0, 2, _buffer, Floor);
            double two = _buffer[0] + _buffer[1];

            GapSplit.Split(86.0, 3, _buffer, Floor);
            double three = _buffer[0] + _buffer[1] + _buffer[2];

            Assert.Less(three, two, "three ways out should cost more total angle than two");
        }

        [Test]
        public void No_opening_is_cut_under_the_floor()
        {
            // A narrow late-game ring split three ways is where the floor earns its keep.
            for (double single = 20.0; single <= 90.0; single += 0.5)
            {
                for (int count = 1; count <= RingGaps.MaxGaps; count++)
                {
                    GapSplit.Split(single, count, _buffer, Floor);
                    for (int i = 0; i < count; i++)
                        Assert.GreaterOrEqual(_buffer[i], Floor - 1e-9,
                            $"opening {i} of {count} came out at {_buffer[i]:F1}° from {single:F1}°");
                }
            }
        }

        [Test]
        public void The_floor_is_paid_for_by_the_widest_opening()
        {
            // 85° three ways is 63.75° of total, shared 40/33/27 — only the last one lands
            // under the floor, so the difference has to come off the widest and not off
            // everybody. Below about 67° all three are under it and the ring should never have
            // been cut three ways at all, which LargestAffordableCount is what prevents.
            GapSplit.Split(85.0, 3, _buffer, Floor);

            Assert.AreEqual(Floor, _buffer[2], 1e-9, "the narrowest was not lifted");
            Assert.Less(_buffer[0], 63.75 * 0.40, "the widest did not pay for it");
            Assert.GreaterOrEqual(_buffer[0], _buffer[1], "the shares changed order");
        }

        [Test]
        public void Openings_convert_between_units_and_angle_without_drift()
        {
            var settings = TunnelSettings.Default;
            foreach (double opening in new[] { 2.80, 3.48, 3.90, 4.32 })
            {
                double angle = GapSplit.AngleFor(opening, settings.Thickness, settings.TargetRadius);
                double back = GapSplit.OpeningFor(angle, settings.Thickness, settings.TargetRadius);
                Assert.AreEqual(opening, back, 1e-9);
            }
        }

        [Test]
        public void The_nearest_way_out_is_the_one_that_is_nearest()
        {
            var gaps = new RingGaps { Count = 3 };
            gaps.Set(0, new GapSpec(0.0, 1.0));
            gaps.Set(1, new GapSpec(120.0, 1.0));
            gaps.Set(2, new GapSpec(240.0, 1.0));

            Assert.AreEqual(0, gaps.NearestIndex(0.0, 10.0));
            Assert.AreEqual(1, gaps.NearestIndex(0.0, 110.0));
            Assert.AreEqual(2, gaps.NearestIndex(0.0, -110.0));

            // Turning the ring moves the answer with it, which is the whole point of the offset.
            Assert.AreEqual(2, gaps.NearestIndex(120.0, 10.0));
        }
    }
}
