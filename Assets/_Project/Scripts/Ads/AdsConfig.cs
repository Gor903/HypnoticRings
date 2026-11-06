using UnityEngine;

namespace HR.Ads
{
    [CreateAssetMenu(menuName = "Hypnotic Rings/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        [Header("Google's own test units — they serve real ads and earn nothing")]
        public const string TestBannerUnit = "ca-app-pub-3940256099942544/6300978111";
        public const string TestRewardedUnit = "ca-app-pub-3940256099942544/5224354917";

        [Tooltip("Never ship with this off until the AdMob account exists.")]
        public bool useTestUnits = true;

        [Header("Live ids, taken from the AdMob panel")]
        public string bannerUnitId = string.Empty;
        public string mrecUnitId = string.Empty;
        public string rewardedUnitId = string.Empty;

        [Header("What is allowed where")]
        [Tooltip("A banner on the playing screen swallows swipes and AdMob discourages it.")]
        public bool bannerInPlay = false;

        public bool bannerOnPause = true;
        public bool bannerOnGameOver = true;
        public bool mrecOnPause = true;
        public bool mrecOnGameOver = true;
        public bool rewardedContinue = true;

        [Min(1)] public int maxContinuesPerRun = 1;

        [Min(60)] public int bannerRefreshSeconds = 60;

        public string Banner => useTestUnits ? TestBannerUnit : bannerUnitId;

        public string Mrec => useTestUnits ? TestBannerUnit : mrecUnitId;

        public string Rewarded => useTestUnits ? TestRewardedUnit : rewardedUnitId;

        public bool Ready =>
            useTestUnits
            || (!string.IsNullOrEmpty(bannerUnitId)
                && !string.IsNullOrEmpty(mrecUnitId)
                && !string.IsNullOrEmpty(rewardedUnitId));
    }
}
