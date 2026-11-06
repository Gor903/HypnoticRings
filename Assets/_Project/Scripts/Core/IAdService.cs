using System;

namespace HR.Core
{
    public enum AdSlot
    {
        Banner = 0,
        Mrec = 1,
    }

    public readonly struct AdInsets
    {
        public readonly double BannerHeight;
        public readonly double MrecHeight;

        public AdInsets(double bannerHeight, double mrecHeight)
        {
            BannerHeight = bannerHeight;
            MrecHeight = mrecHeight;
        }

        public static AdInsets None => new AdInsets(0.0, 0.0);

        public bool HasBanner => BannerHeight > 0.0;

        public bool HasMrec => MrecHeight > 0.0;
    }

    public interface IAdService
    {
        bool CanRequestAds { get; }

        bool RewardedReady { get; }

        bool PrivacyOptionsRequired { get; }

        bool Loaded(AdSlot slot);

        event Action<AdSlot, bool> SlotLoadedChanged;

        event Action RewardedEarned;

        event Action RewardedClosed;

        void Initialise(Action done);

        void ShowBanner(bool visible);

        void ShowMrec(bool visible);

        bool TryShowRewarded();

        AdInsets GetInsetsInCanvasUnits(double canvasScaleFactor);

        void OpenPrivacyOptions();

        void Tick(double dt);
    }
}
