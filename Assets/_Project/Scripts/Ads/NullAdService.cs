using System;
using HR.Core;

namespace HR.Ads
{
    public sealed class NullAdService : IAdService
    {
        public bool CanRequestAds => false;

        public bool RewardedReady => false;

        public bool PrivacyOptionsRequired => false;

        public bool Loaded(AdSlot slot) => false;

        public event Action<AdSlot, bool> SlotLoadedChanged;

        public event Action RewardedEarned;

        public event Action RewardedClosed;

        public void Initialise(Action done) => done?.Invoke();

        public void ShowBanner(bool visible) { }

        public void ShowMrec(bool visible) { }

        public bool TryShowRewarded() => false;

        public AdInsets GetInsetsInCanvasUnits(double canvasScaleFactor) => AdInsets.None;

        public void OpenPrivacyOptions() { }

        public void Tick(double dt) { }

        public void Quiet()
        {
            SlotLoadedChanged = null;
            RewardedEarned = null;
            RewardedClosed = null;
        }
    }
}
