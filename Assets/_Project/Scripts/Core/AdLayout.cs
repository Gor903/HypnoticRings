using System;

namespace HR.Core
{
    public enum AdScreen
    {
        Paused = 0,
        GameOver = 1,
    }

    public readonly struct AdLayoutRequest
    {
        public readonly AdScreen Screen;
        public readonly double SafeHeight;
        public readonly double BannerHeight;
        public readonly double MrecHeight;
        public readonly bool RewardButton;
        public readonly bool ThemeUnlocked;

        public AdLayoutRequest(AdScreen screen, double safeHeight, double bannerHeight,
                               double mrecHeight, bool rewardButton = false,
                               bool themeUnlocked = false)
        {
            Screen = screen;
            SafeHeight = safeHeight;
            BannerHeight = bannerHeight;
            MrecHeight = mrecHeight;
            RewardButton = rewardButton;
            ThemeUnlocked = themeUnlocked;
        }
    }

    public readonly struct AdLayoutPlan
    {
        public readonly bool ShowsMrec;
        public readonly bool ShowsBanner;
        public readonly double Gap;
        public readonly double Flex;
        public readonly double CardHeight;
        public readonly double TopBlock;

        public AdLayoutPlan(bool showsMrec, bool showsBanner, double gap, double flex,
                            double cardHeight, double topBlock)
        {
            ShowsMrec = showsMrec;
            ShowsBanner = showsBanner;
            Gap = gap;
            Flex = flex;
            CardHeight = cardHeight;
            TopBlock = topBlock;
        }

        public bool OldLayout => !ShowsMrec;
    }

    public static class AdLayout
    {
        public const double AdGap = 64.0;

        public const double AdGapMin = 48.0;

        public const double AdLabelSize = 28.0;

        public static double AdLabelBand => Math.Ceiling(AdLabelSize * 1.45);

        public const double AdScoreBand = 200.0;

        public const double TopInset = 40.0;

        public const double RecordBand = 64.0;

        public const double RecordGap = 24.0;

        public const double CardInset = 72.0;

        public const double PauseCard = 581.0;

        public const double GameOverCard = 498.0;

        public const double RewardBlock = 166.0;

        public const double PauseFlexMin = 0.0;

        public const double GameOverFlexMin = 16.0;

        public const double OldPauseCard = 629.0;

        public static double TopBlockOf(AdScreen screen) =>
            screen == AdScreen.GameOver
                ? TopInset + AdScoreBand + RecordGap + RecordBand
                : TopInset + AdScoreBand;

        public static double FlexMinOf(AdScreen screen) =>
            screen == AdScreen.GameOver ? GameOverFlexMin : PauseFlexMin;

        public static double CardOf(AdLayoutRequest request)
        {
            if (request.Screen == AdScreen.Paused) return PauseCard;
            return request.RewardButton ? GameOverCard : GameOverCard - RewardBlock;
        }

        public static AdLayoutPlan For(in AdLayoutRequest request)
        {
            double banner = request.BannerHeight > 0.0 ? request.BannerHeight : 0.0;
            bool showsBanner = banner > 0.0;

            double top = TopBlockOf(request.Screen);
            double flexMin = FlexMinOf(request.Screen);
            double card = CardOf(request);

            bool wantsMrec = request.MrecHeight > 0.0 && !request.ThemeUnlocked;

            if (wantsMrec)
            {
                double mrec = AdLabelBand + request.MrecHeight;

                for (double gap = AdGap; gap >= AdGapMin; gap -= AdGap - AdGapMin)
                {
                    double fixedPart = top + mrec + gap + card
                                       + (showsBanner ? gap + banner : CardInset);
                    double flex = request.SafeHeight - fixedPart;

                    if (flex >= flexMin)
                        return new AdLayoutPlan(true, showsBanner, gap, flex, card, top);

                    if (gap <= AdGapMin) break;
                }
            }

            return new AdLayoutPlan(false, showsBanner, AdGap, 0.0, OldCardOf(request), 0.0);
        }

        static double OldCardOf(AdLayoutRequest request) =>
            request.Screen == AdScreen.Paused ? OldPauseCard : 0.0;

        public static double MinimumHeight(AdScreen screen, double bannerHeight, double mrecHeight,
                                           bool rewardButton, bool floorGaps)
        {
            var request = new AdLayoutRequest(screen, 0.0, bannerHeight, mrecHeight, rewardButton);
            double gap = floorGaps ? AdGapMin : AdGap;

            return TopBlockOf(screen) + FlexMinOf(screen) + AdLabelBand + mrecHeight
                   + gap + CardOf(request) + gap + bannerHeight;
        }
    }
}
