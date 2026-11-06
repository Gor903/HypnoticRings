using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using HR.Core;
using UnityEngine;

namespace HR.Ads
{
    public sealed class GoogleAdService : IAdService
    {
        public const int BannerDp = 50;

        public const int MrecWidthDp = 300;

        public const int MrecHeightDp = 250;

        public const int BannerWidthDp = 320;

        public const double RetrySeconds = 8.0;

        public const int RetryLimit = 4;

        readonly AdsConfig _config;

        BannerView _banner;
        BannerView _mrec;
        RewardedAd _rewarded;

        bool _started;
        bool _bannerWanted;
        bool _mrecWanted;

        Vector2Int? _bannerAt;
        Vector2Int? _mrecAt;

        double _retryClock;
        int _bannerTries;
        int _mrecTries;
        int _rewardedTries;

        public GoogleAdService(AdsConfig config) =>
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));

        public bool CanRequestAds { get; private set; }

        public bool RewardedReady => _rewarded != null && _rewarded.CanShowAd();

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus
            == PrivacyOptionsRequirementStatus.Required;

        public bool Loaded(AdSlot slot) =>
            slot == AdSlot.Banner ? _banner != null : _mrec != null;

        public event Action<AdSlot, bool> SlotLoadedChanged;

        public event Action RewardedEarned;

        public event Action RewardedClosed;

        public void Initialise(Action done)
        {
            var request = new ConsentRequestParameters();

            ConsentInformation.Update(request, error =>
            {
                if (error != null) Debug.LogWarning($"[HR] consent update: {error}");

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning($"[HR] consent form: {formError}");
                    Start(done);
                });
            });
        }

        void Start(Action done)
        {
            CanRequestAds = ConsentInformation.CanRequestAds();
            if (!CanRequestAds) { done?.Invoke(); return; }

            MobileAds.Initialize(_ =>
            {
                _started = true;
                LoadBanner();
                LoadMrec();
                LoadRewarded();
                done?.Invoke();
            });
        }

        public void Tick(double dt)
        {
            if (!_started || dt <= 0.0) return;

            _retryClock += dt;
            if (_retryClock < RetrySeconds) return;
            _retryClock = 0.0;

            if (_banner == null && _bannerTries < RetryLimit) LoadBanner();
            if (_mrec == null && _mrecTries < RetryLimit) LoadMrec();
            if (!RewardedReady && _rewardedTries < RetryLimit) LoadRewarded();
        }

        void LoadBanner()
        {
            if (!_started) return;

            _bannerTries++;
            Drop(ref _banner, AdSlot.Banner);

            var view = new BannerView(_config.Banner, AdSize.Banner, 0, 0);
            Watch(view, AdSlot.Banner, () => _banner = view, () => _bannerWanted);
            view.LoadAd(new AdRequest());
        }

        void LoadMrec()
        {
            if (!_started) return;

            _mrecTries++;
            Drop(ref _mrec, AdSlot.Mrec);

            var size = new AdSize(MrecWidthDp, MrecHeightDp);
            var view = new BannerView(_config.Mrec, size, 0, 0);
            Watch(view, AdSlot.Mrec, () => _mrec = view, () => _mrecWanted);
            view.LoadAd(new AdRequest());
        }

        void Watch(BannerView view, AdSlot slot, Action keep, Func<bool> wanted)
        {
            view.OnBannerAdLoaded += () =>
            {
                keep();
                Reposition(slot);
                if (!wanted()) view.Hide();
                SlotLoadedChanged?.Invoke(slot, true);
            };

            view.OnBannerAdLoadFailed += error =>
            {
                Debug.LogWarning($"[HR] {slot} failed: {error}");
                view.Destroy();
                SlotLoadedChanged?.Invoke(slot, false);
            };

            view.Hide();
        }

        void Drop(ref BannerView view, AdSlot slot)
        {
            if (view == null) return;

            if (slot == AdSlot.Banner) _bannerAt = null; else _mrecAt = null;

            view.Destroy();
            view = null;
            SlotLoadedChanged?.Invoke(slot, false);
        }

        void LoadRewarded()
        {
            if (!_started || !_config.rewardedContinue) return;

            _rewardedTries++;
            if (_rewarded != null) { _rewarded.Destroy(); _rewarded = null; }

            RewardedAd.Load(_config.Rewarded, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[HR] rewarded failed: {error}");
                    return;
                }

                _rewardedTries = 0;
                _rewarded = ad;
                Listen(ad);
            });
        }

        void Listen(RewardedAd ad)
        {
            ad.OnAdFullScreenContentClosed += () =>
            {
                _rewarded = null;
                ad.Destroy();
                RewardedClosed?.Invoke();
                LoadRewarded();
            };

            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogWarning($"[HR] rewarded could not open: {error}");
                _rewarded = null;
                ad.Destroy();
                RewardedClosed?.Invoke();
                LoadRewarded();
            };
        }

        public void ShowBanner(bool visible)
        {
            _bannerWanted = visible;
            if (_banner == null) return;

            if (visible) _banner.Show();
            else _banner.Hide();
        }

        public void ShowMrec(bool visible)
        {
            _mrecWanted = visible;
            if (_mrec == null) return;

            if (visible) _mrec.Show();
            else _mrec.Hide();
        }

        public bool TryShowRewarded()
        {
            if (!RewardedReady) return false;

            _rewarded.Show(_ => RewardedEarned?.Invoke());
            return true;
        }

        public AdInsets GetInsetsInCanvasUnits(double canvasScaleFactor)
        {
            if (canvasScaleFactor <= 0.0) return AdInsets.None;

            double banner = _banner != null ? _banner.GetHeightInPixels() / canvasScaleFactor : 0.0;
            double mrec = _mrec != null ? _mrec.GetHeightInPixels() / canvasScaleFactor : 0.0;

            return new AdInsets(banner, mrec);
        }

        public void OpenPrivacyOptions()
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) Debug.LogWarning($"[HR] privacy form: {error}");
            });
        }

        public float DeviceScale
        {
            get
            {
                float scale = MobileAds.Utils.GetDeviceScale();
                return scale > 0f ? scale : 1f;
            }
        }

        public void Place(AdSlot slot, int x, int y)
        {
            var at = new Vector2Int(x, y);
            if (slot == AdSlot.Banner) _bannerAt = at; else _mrecAt = at;
            Reposition(slot);
        }

        void Reposition(AdSlot slot)
        {
            var view = slot == AdSlot.Banner ? _banner : _mrec;
            var at = slot == AdSlot.Banner ? _bannerAt : _mrecAt;
            if (view == null || !at.HasValue) return;

            view.SetPosition(at.Value.x, at.Value.y);
        }

        public void Close()
        {
            Drop(ref _banner, AdSlot.Banner);
            Drop(ref _mrec, AdSlot.Mrec);

            if (_rewarded != null) { _rewarded.Destroy(); _rewarded = null; }
            _started = false;
        }
    }
}
