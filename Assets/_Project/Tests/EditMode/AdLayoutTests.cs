using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class AdLayoutTests
    {
        const double Mrec = 750.0;
        const double Banner = 150.0;

        static AdLayoutRequest Pause(double height, double mrec = Mrec, double banner = Banner) =>
            new AdLayoutRequest(AdScreen.Paused, height, banner, mrec);

        static AdLayoutRequest Over(double height, bool reward = true, double mrec = Mrec,
                                    double banner = Banner, bool unlocked = false) =>
            new AdLayoutRequest(AdScreen.GameOver, height, banner, mrec, reward, unlocked);

        [Test]
        public void The_label_band_is_the_one_the_document_drew()
        {
            Assert.AreEqual(41.0, AdLayout.AdLabelBand, 1e-9);
        }

        [Test]
        public void The_minimum_heights_are_the_ones_the_document_counted()
        {
            Assert.AreEqual(1890.0,
                AdLayout.MinimumHeight(AdScreen.Paused, Banner, Mrec, false, false), 1e-9,
                "Pause at the target gaps");

            Assert.AreEqual(1858.0,
                AdLayout.MinimumHeight(AdScreen.Paused, Banner, Mrec, false, true), 1e-9,
                "Pause squeezed to the floor");

            Assert.AreEqual(1911.0,
                AdLayout.MinimumHeight(AdScreen.GameOver, Banner, Mrec, true, false), 1e-9,
                "Game over with the reward button");

            Assert.AreEqual(1879.0,
                AdLayout.MinimumHeight(AdScreen.GameOver, Banner, Mrec, true, true), 1e-9,
                "Game over squeezed to the floor");

            Assert.AreEqual(1745.0,
                AdLayout.MinimumHeight(AdScreen.GameOver, Banner, Mrec, false, false), 1e-9,
                "Game over without the reward button");
        }

        [Test]
        public void A_tall_screen_keeps_the_target_gap_and_spends_the_rest_on_flex()
        {
            var plan = AdLayout.For(Pause(1920.0));

            Assert.IsTrue(plan.ShowsMrec);
            Assert.AreEqual(AdLayout.AdGap, plan.Gap, 1e-9, "a tall screen should not squeeze");
            Assert.AreEqual(30.0, plan.Flex, 1e-9, "the spare height is the flex gap");
        }

        [Test]
        public void The_flex_goes_first_and_the_gaps_only_after_it()
        {
            var plan = AdLayout.For(Pause(1890.0));
            Assert.IsTrue(plan.ShowsMrec);
            Assert.AreEqual(AdLayout.AdGap, plan.Gap, 1e-9, "the gaps shrank before the flex did");
            Assert.AreEqual(0.0, plan.Flex, 1e-9);

            var squeezed = AdLayout.For(Pause(1870.0));
            Assert.IsTrue(squeezed.ShowsMrec);
            Assert.AreEqual(AdLayout.AdGapMin, squeezed.Gap, 1e-9, "the gaps should be at the floor");
        }

        [Test]
        public void The_mrec_is_dropped_rather_than_squeezed_past_the_floor()
        {
            var plan = AdLayout.For(Pause(1857.0));

            Assert.IsFalse(plan.ShowsMrec, "one unit under the floor should drop the MREC");
            Assert.IsTrue(plan.OldLayout);
        }

        [Test]
        public void A_gap_is_never_smaller_than_the_floor_on_any_height()
        {
            for (double height = 1200.0; height <= 2600.0; height += 1.0)
            {
                foreach (var screen in new[] { AdScreen.Paused, AdScreen.GameOver })
                {
                    var plan = AdLayout.For(new AdLayoutRequest(screen, height, Banner, Mrec, true));

                    Assert.GreaterOrEqual(plan.Gap, AdLayout.AdGapMin - 1e-9,
                        $"{screen} at {height} squeezed below the floor");
                    Assert.GreaterOrEqual(plan.Flex, -1e-9,
                        $"{screen} at {height} produced a negative flex gap");
                }
            }
        }

        [Test]
        public void A_run_that_unlocked_a_theme_keeps_the_old_card()
        {
            var plan = AdLayout.For(Over(2400.0, unlocked: true));

            Assert.IsFalse(plan.ShowsMrec,
                "the unlock line lives in the old card, so the MREC steps aside");
        }

        [Test]
        public void An_mrec_that_never_loaded_is_simply_absent()
        {
            var plan = AdLayout.For(Over(2400.0, mrec: 0.0));

            Assert.IsFalse(plan.ShowsMrec);
            Assert.IsTrue(plan.ShowsBanner, "the banner is a separate slot and stays");
        }

        [Test]
        public void A_missing_banner_does_not_take_the_mrec_with_it()
        {
            var plan = AdLayout.For(Pause(1920.0, banner: 0.0));

            Assert.IsFalse(plan.ShowsBanner);
            Assert.IsTrue(plan.ShowsMrec, "losing the banner should not cost the MREC");
        }

        [Test]
        public void Hiding_the_reward_button_gives_its_height_to_the_flex_gap()
        {
            var withReward = AdLayout.For(Over(2000.0));
            var without = AdLayout.For(Over(2000.0, reward: false));

            Assert.AreEqual(AdLayout.RewardBlock, without.Flex - withReward.Flex, 1e-9,
                "the 166 units the button left behind went somewhere else");
            Assert.AreEqual(AdLayout.AdGap, without.Gap, 1e-9, "the MREC stays 64 above the card");
        }

        [Test]
        public void The_shortest_screen_that_still_shows_everything_is_the_documented_one()
        {
            Assert.IsTrue(AdLayout.For(Over(1879.0)).ShowsMrec,
                "1879 is the floor Game over is supposed to survive");

            Assert.IsFalse(AdLayout.For(Over(1878.0)).ShowsMrec,
                "a unit under the floor should fall back");
        }
    }
}
