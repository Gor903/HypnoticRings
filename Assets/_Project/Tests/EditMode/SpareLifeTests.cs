using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// The whole table from plan [G-29], as a test. Three lines of rule, and the reason it is
    /// worth its own file: getting it wrong either makes a second ball into immunity or makes it
    /// worth nothing, and neither failure shows up as an exception.
    /// </summary>
    public sealed class SpareLifeTests
    {
        [Test]
        public void One_ball_is_the_whole_run()
        {
            Assert.AreEqual(CrushOutcome.Lost, SpareLife.Resolve(ballsAlive: 1, ballsInside: 1));
            Assert.AreEqual(CrushOutcome.Lost, SpareLife.Resolve(ballsAlive: 1, ballsInside: 0));
        }

        [Test]
        public void Two_balls_in_the_same_ring_are_one_mistake_and_not_two_chances()
        {
            // The case that keeps a spare ball from being immunity: both were in the ring that
            // burst, so there is nothing left outside it to carry on with.
            Assert.AreEqual(CrushOutcome.Lost, SpareLife.Resolve(ballsAlive: 2, ballsInside: 2));
        }

        [Test]
        public void A_ball_outside_the_burst_ring_carries_the_run_on()
        {
            Assert.AreEqual(CrushOutcome.BallSacrificed,
                            SpareLife.Resolve(ballsAlive: 2, ballsInside: 1));
        }

        [Test]
        public void A_ring_nobody_is_inside_still_costs_nothing_but_the_ring()
        {
            // Can happen when the innermost ring has been left by everyone but has not finished
            // dissolving. Ending the run on it would be a death with no ball involved.
            Assert.AreEqual(CrushOutcome.BallSacrificed,
                            SpareLife.Resolve(ballsAlive: 2, ballsInside: 0));
        }

        [Test]
        public void No_balls_at_all_is_a_lost_run_rather_than_an_exception()
        {
            Assert.AreEqual(CrushOutcome.Lost, SpareLife.Resolve(ballsAlive: 0, ballsInside: 0));
        }
    }
}
