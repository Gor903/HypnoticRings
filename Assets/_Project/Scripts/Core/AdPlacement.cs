using System;

namespace HR.Core
{
    public static class AdPlacement
    {
        public static int SlotTopDp(double unitsBelowSafeTop, double canvasFactor,
                                    double safeTopPixels, double deviceScale) =>
            Dp(safeTopPixels + unitsBelowSafeTop * canvasFactor, deviceScale);

        public static int BannerTopDp(double screenHeightPixels, double safeBottomPixels,
                                      double bannerPixels, double deviceScale) =>
            Dp(screenHeightPixels - safeBottomPixels - bannerPixels, deviceScale);

        public static int WidthDp(double screenWidthPixels, double deviceScale)
        {
            if (deviceScale <= 0.0 || screenWidthPixels <= 0.0) return 0;

            return (int)Math.Floor(screenWidthPixels / deviceScale);
        }

        public static int CentreDp(double slotWidthDp, double screenWidthPixels, double deviceScale)
        {
            if (deviceScale <= 0.0) return 0;

            double free = screenWidthPixels / deviceScale - slotWidthDp;
            return free <= 0.0 ? 0 : (int)Math.Round(free * 0.5, MidpointRounding.AwayFromZero);
        }

        static int Dp(double pixels, double deviceScale)
        {
            if (deviceScale <= 0.0) return 0;
            if (pixels < 0.0) pixels = 0.0;

            return (int)Math.Round(pixels / deviceScale, MidpointRounding.AwayFromZero);
        }
    }
}
