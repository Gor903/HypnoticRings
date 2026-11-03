using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class ToneLadderTests
    {
        [Test]
        public void The_first_pass_of_a_run_sounds_the_lowest_note()
        {
            Assert.AreEqual(0, new ToneLadder().Pass(0.0));
        }

        [Test]
        public void Passes_inside_the_window_climb_one_step_each()
        {
            var ladder = new ToneLadder();
            Assert.AreEqual(0, ladder.Pass(0.0));
            Assert.AreEqual(1, ladder.Pass(1.0));
            Assert.AreEqual(2, ladder.Pass(2.5));
            Assert.AreEqual(3, ladder.Pass(5.0));
        }

        [Test]
        public void Exactly_three_seconds_still_counts_as_a_streak()
        {
            var ladder = new ToneLadder();
            ladder.Pass(0.0);
            Assert.AreEqual(1, ladder.Pass(ToneLadder.StreakWindowSeconds));
        }

        [Test]
        public void A_gap_longer_than_the_window_drops_back_to_the_bottom()
        {
            var ladder = new ToneLadder();
            ladder.Pass(0.0);
            ladder.Pass(1.0);
            Assert.AreEqual(1, ladder.Step);

            Assert.AreEqual(0, ladder.Pass(1.0 + ToneLadder.StreakWindowSeconds + 0.001));
        }

        [Test]
        public void The_ladder_stops_at_one_octave()
        {
            var ladder = new ToneLadder();
            for (int i = 0; i < 40; i++) ladder.Pass(i * 0.5);

            Assert.AreEqual(ToneLadder.Steps - 1, ladder.Step,
                "the streak should cap at the octave, not keep climbing");
        }

        [Test]
        public void A_pause_or_a_loss_ends_the_streak_however_recent_the_last_pass()
        {
            var ladder = new ToneLadder();
            ladder.Pass(0.0);
            ladder.Pass(0.5);
            Assert.AreEqual(1, ladder.Step);

            ladder.Reset();

            Assert.AreEqual(0, ladder.Pass(0.6));
        }
    }
}
