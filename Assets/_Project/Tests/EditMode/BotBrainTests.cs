using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class BotBrainTests
    {
        const double Dt = 1.0 / 90.0;

        [Test]
        public void A_gap_already_under_the_ball_needs_no_movement()
        {
            var bot = new BotBrain(0.0, 200.0, 0.0, seed: 1);
            Assert.AreEqual(0.0, bot.Decide(270.0, 270.0, Dt), 1e-9);
        }

        [Test]
        public void It_turns_towards_the_gap_the_short_way()
        {
            var bot = new BotBrain(0.0, 200.0, 0.0, seed: 1);

            Assert.Greater(bot.Decide(270.0, 260.0, Dt), 0.0);

            bot.Reset();
            Assert.Less(bot.Decide(270.0, 280.0, Dt), 0.0);
        }

        [Test]
        public void It_takes_the_short_way_across_the_wrap()
        {
            var bot = new BotBrain(0.0, 200.0, 0.0, seed: 1);

            Assert.Greater(bot.Decide(10.0, 350.0, Dt), 0.0);
        }

        [Test]
        public void It_cannot_turn_faster_than_a_thumb()
        {
            var bot = new BotBrain(0.0, 200.0, 0.0, seed: 1);
            double step = bot.Decide(270.0, 90.0, Dt);
            Assert.AreEqual(200.0 * Dt, Math.Abs(step), 1e-9);
        }

        [Test]
        public void Reaction_delay_makes_it_act_on_a_stale_view()
        {
            var bot = new BotBrain(0.2, 200.0, 0.0, seed: 1);
            for (int i = 0; i < 10; i++) bot.Decide(270.0, 240.0, Dt);

            double step = bot.Decide(270.0, 270.0, Dt);
            Assert.Greater(Math.Abs(step), 0.0, "it reacted instantly despite a 0.2 s delay");
            Assert.AreEqual(30.0, bot.LastPerceivedErrorDeg, 1e-9, "it should still be seeing the old error");
        }

        [Test]
        public void Without_delay_it_sees_the_present()
        {
            var bot = new BotBrain(0.0, 200.0, 0.0, seed: 1);
            for (int i = 0; i < 10; i++) bot.Decide(270.0, 240.0, Dt);
            bot.Decide(270.0, 270.0, Dt);
            Assert.AreEqual(0.0, bot.LastPerceivedErrorDeg, 1e-9);
        }

        [Test]
        public void Noise_stays_within_its_stated_bound()
        {
            var bot = new BotBrain(0.0, 200.0, 40.0, seed: 7);
            double gap = 270.0, worst = 0.0;

            for (int i = 0; i < 2000; i++)
            {
                gap += bot.Decide(270.0, gap, Dt);
                double error = Math.Abs(TunnelMath.ShortestDeltaDeg(gap, 270.0));
                if (i > 100 && error > worst) worst = error;
            }

            Assert.Less(worst, 5.0, $"a shaky hand wandered {worst:F2} degrees off target");
        }

        [Test]
        public void A_noiseless_bot_is_perfectly_steady()
        {
            var bot = new BotBrain(0.0, 200.0, 0.0, seed: 7);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(0.0, bot.Decide(270.0, 270.0, Dt), 1e-12);
        }

        [Test]
        public void The_same_seed_replays_exactly()
        {
            var a = new BotBrain(0.1, 170.0, 20.0, seed: 4242);
            var b = new BotBrain(0.1, 170.0, 20.0, seed: 4242);

            for (int i = 0; i < 200; i++)
            {
                double ball = 270.0 + i * 0.3;
                double gap = 200.0 + i * 1.1;
                Assert.AreEqual(a.Decide(ball, gap, Dt), b.Decide(ball, gap, Dt), 1e-12);
            }
        }

        [Test]
        public void Different_seeds_diverge()
        {
            var a = new BotBrain(0.1, 170.0, 20.0, seed: 1);
            var b = new BotBrain(0.1, 170.0, 20.0, seed: 2);

            bool differed = false;
            for (int i = 0; i < 200 && !differed; i++)
                differed = Math.Abs(a.Decide(270.0, 270.0, Dt) - b.Decide(270.0, 270.0, Dt)) > 1e-9;

            Assert.IsTrue(differed, "two seeds produced identical noise");
        }

        [Test]
        public void The_three_presets_are_ordered_as_their_names_claim()
        {
            var weak = BotBrain.Weak(1);
            var medium = BotBrain.Medium(1);
            var strong = BotBrain.Strong(1);

            Assert.Greater(weak.ReactionDelay, medium.ReactionDelay);
            Assert.Greater(medium.ReactionDelay, strong.ReactionDelay);

            Assert.Less(weak.MaxGestureSpeedDeg, medium.MaxGestureSpeedDeg);
            Assert.Less(medium.MaxGestureSpeedDeg, strong.MaxGestureSpeedDeg);

            Assert.Greater(weak.NoiseDegPerSecond, medium.NoiseDegPerSecond);
            Assert.Greater(medium.NoiseDegPerSecond, strong.NoiseDegPerSecond);
        }

        [Test]
        public void Every_preset_stays_under_the_comfortable_speed_the_generator_assumes()
        {
            double comfort = TunnelSettings.Default.ComfortSpinDeg;
            foreach (var bot in new[] { BotBrain.Weak(1), BotBrain.Medium(1), BotBrain.Strong(1) })
                Assert.LessOrEqual(bot.MaxGestureSpeedDeg, comfort + 20.0,
                    "a bot faster than the comfort speed invalidates the reachability guarantee");
        }

        [Test]
        public void A_nonsense_configuration_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotBrain(-1.0, 200.0, 0.0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotBrain(0.0, 0.0, 0.0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotBrain(0.0, 200.0, -1.0, 1));
        }
    }
}
