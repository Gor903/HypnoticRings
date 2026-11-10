using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class AdPlacementTests
    {
        const double Screen = 2340.0;

        const double Width = 1080.0;

        const double Scale = 2.75;

        const double Factor = 1.104;

        const double Cutout = 94.0;

        const double GestureBar = 21.0;

        const double BannerPixels = 137.5;

        [Test]
        public void A_slot_asked_for_in_the_safe_frame_arrives_there_in_the_screen_frame()
        {
            const double units = 661.2;

            int dp = AdPlacement.SlotTopDp(units, Factor, Cutout, Scale);

            Assert.AreEqual(Cutout + units * Factor, dp * Scale, Scale,
                "the slot should land where the layout asked, within a device pixel's worth of dp");
        }

        [Test]
        public void A_slot_placed_without_the_inset_lands_one_cutout_too_high()
        {
            const double units = 661.2;

            int right = AdPlacement.SlotTopDp(units, Factor, Cutout, Scale);
            int frameless = AdPlacement.SlotTopDp(units, Factor, 0.0, Scale);

            Assert.AreEqual(Cutout, (right - frameless) * Scale, Scale,
                "the glue clamps y to the cutout, it does not offset by it — the inset is ours to add");
        }

        [Test]
        public void A_banner_sits_on_the_safe_bottom_and_not_under_the_gesture_bar()
        {
            int dp = AdPlacement.BannerTopDp(Screen, GestureBar, BannerPixels, Scale);

            Assert.AreEqual(Screen - GestureBar, dp * Scale + BannerPixels, Scale,
                "the banner's bottom edge belongs on the safe-area bottom");
        }

        [Test]
        public void A_taller_banner_climbs_instead_of_hanging_off_the_screen()
        {
            int fixedSize = AdPlacement.BannerTopDp(Screen, GestureBar, BannerPixels, Scale);
            int adaptive = AdPlacement.BannerTopDp(Screen, GestureBar, BannerPixels * 1.8, Scale);

            Assert.Less(adaptive, fixedSize, "an adaptive banner is taller, so its top edge is higher");
            Assert.AreEqual(BannerPixels * 0.8, (fixedSize - adaptive) * Scale, Scale);
        }

        [Test]
        public void A_slot_narrower_than_the_screen_is_centred_and_a_wider_one_is_not_pushed_off()
        {
            Assert.AreEqual(46, AdPlacement.CentreDp(300.0, Width, Scale),
                "300 dp inside 392 dp of screen leaves 46 dp either side");

            Assert.AreEqual(0, AdPlacement.CentreDp(Width / Scale, Width, Scale),
                "a full-width slot starts at the left edge");

            Assert.AreEqual(0, AdPlacement.CentreDp(600.0, Width, Scale),
                "a slot wider than the screen is not dragged into negative margins");
        }

        [Test]
        public void A_full_width_banner_asks_for_the_whole_screen_and_not_a_fraction_of_it()
        {
            int dp = AdPlacement.WidthDp(Width, Scale);

            Assert.AreEqual(392, dp, "1080 px at 2.75 is 392 dp of screen");
            Assert.LessOrEqual(dp * Scale, Width,
                "a banner wider than the screen would be clamped by the SDK and misplaced by us");
        }

        [Test]
        public void A_width_divided_by_the_density_twice_covers_barely_a_third_of_the_screen()
        {
            int once = AdPlacement.WidthDp(Width, Scale);
            int twice = AdPlacement.WidthDp(once, Scale);

            Assert.AreEqual(142, twice);
            Assert.Less(twice / (double)once, 0.4,
                "the SDK already hands back dp; dividing again is what left the banner a box in the corner");
        }

        [Test]
        public void A_missing_device_scale_places_nothing_rather_than_dividing_by_zero()
        {
            Assert.AreEqual(0, AdPlacement.SlotTopDp(600.0, Factor, Cutout, 0.0));
            Assert.AreEqual(0, AdPlacement.BannerTopDp(Screen, GestureBar, BannerPixels, 0.0));
            Assert.AreEqual(0, AdPlacement.CentreDp(300.0, Width, 0.0));
            Assert.AreEqual(0, AdPlacement.WidthDp(Width, 0.0));
        }

        [Test]
        public void A_slot_never_asks_for_a_negative_offset()
        {
            Assert.AreEqual(0, AdPlacement.BannerTopDp(100.0, 0.0, 400.0, Scale),
                "a banner taller than the screen clamps to the top, it does not wrap");
        }
    }
}
