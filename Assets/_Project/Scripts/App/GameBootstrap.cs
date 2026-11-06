using HR.Ads;
using HR.Audio;
using HR.Core;
using HR.Gameplay;
using HR.Input;
using HR.Save;
using HR.Themes;
using HR.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HR.App
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] AdsConfig ads;
        [SerializeField] AudioBank audioBank;
        [SerializeField] int seed;

        SaveService _saves;
        SaveData _settings;
        GameManager _game;
        TunnelController _tunnel;
        BallController _ball;
        InputController _input;
        ThemeApplier _themes;
        GameUi _ui;
        AudioManager _audio;
        Effects _effects;
        ScreenJuice _juice;
        Haptics _haptics;
        AndroidBackButton _back;
        CentreMarker _centre;
        Camera _camera;

        public GameConfig Config => config;

        public GameManager Game => _game;
        public GameUi Ui => _ui;
        public TunnelController Tunnel => _tunnel;
        public BallController Ball => _ball;

        IAdService _ads = new NullAdService();

        public IAdService Ads => _ads;

        public void UseAdService(IAdService service) => _ads = service ?? new NullAdService();
        public InputController Input => _input;
        public ThemeApplier Themes => _themes;
        public SaveData Settings => _settings;
        public AudioManager Audio => _audio;
        public Effects Effects => _effects;
        public ScreenJuice Juice => _juice;
        public Haptics Haptics => _haptics;

        public CentreMarker Centre => _centre;

        public AndroidBackButton Back => _back;

        bool _built;

        SaveService _configuredSaves;
        Camera _configuredCamera;

        public void Configure(GameConfig gameConfig, int runSeed = 0, SaveService saves = null,
                              Camera view = null)
        {
            config = gameConfig;
            seed = runSeed;
            _configuredSaves = saves;
            _configuredCamera = view;
        }

        void Awake()
        {
            if (_built) return;

            if (config == null)
            {
                Debug.LogError("[HR] GameBootstrap has no GameConfig", this);
                enabled = false;
                return;
            }

            Build(_configuredSaves ?? SaveService.Default());
        }

        public void Build(SaveService saves)
        {
            if (_built) return;
            _built = true;
            _saves = saves;
            _settings = _saves.Load();
            foreach (string repair in _saves.LastReport) Debug.LogWarning($"[HR] save: {repair}");

            Strings.Apply(_settings.Language, DeviceLanguage());

            _camera = _configuredCamera != null ? _configuredCamera : Camera.main;
            if (_camera != null && _camera.GetComponent<UniversalAdditionalCameraData>() == null)
                _camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            ApplyFraming();

            var settings = config.ToSettings();
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", config.ringFriction, config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", config.ballFriction, config.ballBounce);

            _ball = TunnelFactory.CreateBall(config, transform, ballPhysics, material, settings);

            var centreGo = new GameObject("Centre");
            _centre = centreGo.AddComponent<CentreMarker>();
            _centre.Initialise(config, transform, material);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            var pool = new RingPool(config, tunnelGo.transform, material);
            _tunnel.Initialise(config, _ball, pool);

            _tunnel.AutoStep = false;
            _ringPhysics = ringPhysics;

            var gameGo = new GameObject("Game");
            gameGo.transform.SetParent(transform, false);
            _game = gameGo.AddComponent<GameManager>();
            _game.Initialise(config, _tunnel, _ball, _settings.bestScore);

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(config.ToGestureSettings(_settings.sensitivity, _settings.invertRotation), _camera);

            var themesGo = new GameObject("Themes");
            themesGo.transform.SetParent(transform, false);
            _themes = themesGo.AddComponent<ThemeApplier>();
            _themes.Initialise(_camera, _tunnel, _ball.GetComponent<Renderer>(),
                _ball.transform.Find("BallOutline")?.GetComponent<Renderer>());

            var effectsGo = new GameObject("Effects");
            effectsGo.transform.SetParent(transform, false);
            _effects = effectsGo.AddComponent<Effects>();
            _effects.Initialise(config, _camera, _ball.transform, material);

            _haptics = new Haptics(config.vibratePassMs, config.vibrateLoseMs);

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            _audio = audioGo.AddComponent<AudioManager>();
            _audio.Initialise(audioBank, _settings);

            var uiGo = new GameObject("Interface");
            uiGo.transform.SetParent(transform, false);
            _ui = uiGo.AddComponent<GameUi>();
            _ui.Initialise(_settings, _game.ScoreKeeper, _camera);

            _juice = uiGo.AddComponent<ScreenJuice>();
            _juice.Initialise(_ui.ScoreLabel, (RectTransform)_ui.Canvas.transform,
                config.scorePulseScale, config.scorePulseSeconds, config.transitionSeconds);

            ApplyEffectSettings();

            _back = new AndroidBackButton();
            _back.Pressed += SystemBack;

            Wire();
            _themes.Apply(_settings.Theme);
            _ui.ApplyTheme(_themes.Palette);
            ApplyThemeToEffects();
            _ui.Show(GameState.Menu);
            ShowWorld(GameState.Menu);
            StartAds();
        }

        PhysicsMaterial2D _ringPhysics;

        void ApplyFraming()
        {
            if (_camera == null) return;

            _camera.orthographic = true;
            _camera.orthographicSize = config.orthoSize;

            var position = _camera.transform.position;
            _camera.transform.position =
                new Vector3(transform.position.x, transform.position.y + config.CameraOffsetY, position.z);
        }

        void Wire()
        {
            _ui.PlayPressed += () => { _game.Mode = GameMode.Normal; StartRun(); };
            _ui.HardcorePressed += () => { _game.Mode = GameMode.Hardcore; StartRun(); };

            _ui.RetryPressed += StartRun;
            _ui.PausePressed += () => { _game.Pause(); _input.Cancel(); _ui.Show(_game.State); };
            _ui.ResumePressed += () => { _game.Resume(); _ui.Show(_game.State); };
            _ui.HomePressed += () => { _game.GoHome(); _ui.Show(_game.State); };
            _ui.ThemesPressed += () => { _game.States.Fire(GameTrigger.OpenThemes); _ui.Show(_game.State); };
            _ui.SettingsPressed += () => { _game.States.Fire(GameTrigger.OpenSettings); _ui.Show(_game.State); };
            _ui.BackPressed += () => { _game.Back(); _ui.Show(_game.State); };

            _ui.Clicked += () => _audio.PlayClick();

            _ui.ThemeChosen += ChooseTheme;

            _ui.SettingsChanged += () =>
            {
                _audio.ApplySettings();
                ApplyEffectSettings();
                Strings.Apply(_settings.Language, DeviceLanguage());
                _input.Initialise(config.ToGestureSettings(_settings.sensitivity, _settings.invertRotation), _camera);
                Persist();
            };

            _ui.ResetProgressConfirmed += () =>
            {
                _game.ScoreKeeper.ResetProgress();
                _settings = SaveData.Defaults();
                _saves.Save(_settings);
                ApplyLook(_settings.Theme);
                _ui.RefreshStrings();
            };

            _ball.Struck += _ => _audio.PlayKnock();

            _game.Scored += score =>
            {
                _audio.PlayPass(Time.timeAsDouble);
                _effects.Flash(_ball.transform.position, _effects.TrailColour);
                _juice.Pulse();
                _haptics.Pass();
                _ui.SetScore(score);
                if (_game.Mode == GameMode.Hardcore)
                {
                    if (!_settings.hardcoreHintSeen)
                    {
                        _settings.hardcoreHintSeen = true;
                        _ui.ShowHint(false, GameUi.HardcoreHintKey);
                        Persist();
                    }
                }
                else if (!_settings.hintSeen)
                {
                    _settings.hintSeen = true;
                    _ui.ShowHint(false);
                    Persist();
                }
            };

            _game.KilledByTouch += ring =>
            {
                _haptics.Lose();
                _audio.PlayLose();
            };

            _tunnel.BallTouchedRing += touches =>
            {
                if (_game.Mode == GameMode.Hardcore && touches == GameManager.HardcoreTouchAllowance)
                    _haptics.Pass();
            };

            _game.StateChanged += (from, to) =>
            {
                ApplyModeLook(to);
                _ui.Show(to);
                ShowWorld(to);
                ShowAds(to);

                if (to == GameState.Countdown) BeginCountdown();
                if (from == GameState.Countdown) _tunnel.EndCountdown(dissolve: false);

                if (to == GameState.Paused) _audio.ResetStreak();
                if (to == GameState.GameOver)
                {
                    if (_game.Mode == GameMode.Hardcore && _game.Score > _settings.hardcoreBest)
                        _settings.hardcoreBest = _game.Score;
                    _audio.PlayLose();
                    _effects.Shake();
                    _effects.Collapse(_tunnel.transform, _ball.transform.position);
                    _haptics.Lose();
                    Persist();
                }

                if (to == GameState.Countdown || to == GameState.Menu) _juice.Fade(toClear: true);
            };
        }

        void StartAds()
        {
            if (ads == null || !ads.Ready) return;
            if (Application.platform != RuntimePlatform.Android) return;
            if (!(_ads is NullAdService)) return;

            var service = new GoogleAdService(ads);
            _ads = service;
            service.Initialise(() => ShowAds(_game != null ? _game.State : GameState.Menu));
        }

        void ShowAds(GameState state)
        {
            bool resting = state == GameState.Paused || state == GameState.GameOver;
            bool allowed = resting && ads != null;

            _ads.ShowBanner(allowed && (state == GameState.Paused
                                        ? ads.bannerOnPause : ads.bannerOnGameOver));
            _ads.ShowMrec(allowed && (state == GameState.Paused
                                      ? ads.mrecOnPause : ads.mrecOnGameOver));
        }

        void ShowWorld(GameState state)
        {
            bool flying = state == GameState.Playing || state == GameState.Countdown;
            if (!flying)
            {
                if (_ball != null) _ball.SetFrozen(true);
            }

            bool inPlay = state == GameState.Playing || state == GameState.GameOver
                          || state == GameState.Countdown;
            if (_ball != null) _ball.gameObject.SetActive(inPlay);
            if (_centre != null) _centre.gameObject.SetActive(inPlay);
            if (_tunnel != null) _tunnel.gameObject.SetActive(state != GameState.Paused);

            if (state == GameState.Paused && _effects != null) _effects.Clear();

            if (!flying) return;
            if (_ball != null) _ball.SetFrozen(false);
        }

        void ApplyEffectSettings()
        {
            bool wanted = !_settings.reduceEffects;

            _effects.Enabled = wanted;
            _juice.Enabled = wanted;
            _themes.SetEffectsWanted(wanted);
            _haptics.Enabled = _settings.vibrationEnabled;

            if (_ui != null && _themes != null) _ui.ApplyTheme(_themes.Palette);

            if (!wanted)
            {
                _effects.Clear();
                _juice.Clear();
                _tunnel.transform.localRotation = Quaternion.identity;
            }
        }

        public void ChooseTheme(Theme theme)
        {
            if (_game == null || !_game.ScoreKeeper.IsUnlocked(theme)) return;

            _settings.Theme = theme;
            ApplyLook(theme);
            Persist();
        }

        void ApplyLook(Theme theme)
        {
            _themes.Apply(theme);
            _ui.ApplyTheme(_themes.Palette);
            ApplyThemeToEffects();
        }

        public static Theme LookFor(GameMode mode, GameState state, Theme chosen) =>
            mode == GameMode.Hardcore && GameStateMachine.IsInRun(state) ? Theme.Ember : chosen;

        void ApplyModeLook(GameState state)
        {
            if (!GameStateMachine.IsInRun(state) && _game.ScoreKeeper.Best != _settings.bestScore)
                _game.ScoreKeeper.RestoreBest(_settings.bestScore);

            var wanted = LookFor(_game.Mode, state, _settings.Theme);
            if (_themes.Current != wanted) ApplyLook(wanted);
        }

        int RecordForMode() =>
            _game.Mode == GameMode.Hardcore ? _settings.hardcoreBest : _settings.bestScore;

        void ApplyThemeToEffects()
        {
            var palette = _themes.Palette;
            _effects.TrailColour = ThemeApplier.ToColor(palette.TrailHot);
            _effects.TrailColourCool = ThemeApplier.ToColor(palette.TrailCool);
            _juice.CurtainColour = ThemeApplier.ToColor(palette.Background);
            _centre.Apply(ThemeApplier.ToColor(palette.Background));
        }

        void StartRun()
        {
            _game.ScoreKeeper.RestoreBest(RecordForMode());
            _ui.AwardsThemes = _game.Mode != GameMode.Hardcore;

            _game.StartRun(seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : seed);
            _input.ResetAngle();

            _tunnel.transform.localRotation = Quaternion.identity;
            _effects.Clear();

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = _ringPhysics;

            _ui.SetScore(0);
            _ui.RefreshHud();

            bool hardcore = _game.Mode == GameMode.Hardcore;
            _ui.ShowHint(hardcore ? !_settings.hardcoreHintSeen : !_settings.hintSeen,
                         hardcore ? GameUi.HardcoreHintKey : GameUi.SwipeHintKey);

            _ui.Show(_game.State);
        }

        const float MenuSpinDegPerSecond = 6f;

        readonly CountdownClock _countdown = new CountdownClock();

        public CountdownClock Countdown => _countdown;

        void BeginCountdown()
        {
            _countdown.Restart();
            _tunnel.BeginCountdown(config.countdownRingRatio, _game.Seed);
            _ui.SetCountdown(CountdownClock.FirstDigit);
        }

        void TickCountdown(float dt)
        {
            double before = _countdown.Elapsed;
            _countdown.Advance(dt);

            if (_countdown.JustCrossedDissolve(_countdown.Elapsed - before))
            {
                _tunnel.EndCountdown(dissolve: true);
                _ui.SetCountdown(0);
            }
            else if (_countdown.Phase == CountdownPhase.Counting)
            {
                _ui.SetCountdown(_countdown.Digit);
            }

            bool effects = _settings == null || !_settings.reduceEffects;
            _ui.SetCountdownScale(effects ? (float)_countdown.BeatScale : 1f);

            if (_countdown.Finished) _game.StartPlaying();
        }

        void Update()
        {
            if (_game == null) return;

            if (_game.State == GameState.Countdown)
            {
                TickCountdown(Time.deltaTime);
                return;
            }

            if (_game.State == GameState.Playing)
            {
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
                _effects.Tick(Time.deltaTime);
            }
            else if (_game.State == GameState.GameOver)
            {
                _effects.Tick(Time.deltaTime);
            }

            _ads.Tick(Time.unscaledDeltaTime);

            if (_effects.Enabled) _themes.Tick(Time.deltaTime);

            if (_game.State == GameState.Menu && _effects.Enabled)
            {
                _tunnel.transform.Rotate(0f, 0f, MenuSpinDegPerSecond * Time.deltaTime);
            }

            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                SystemBack();
        }

        int _lastBackFrame = -1;

        public void SystemBack()
        {
            if (_game == null || Time.frameCount == _lastBackFrame) return;
            _lastBackFrame = Time.frameCount;

            if (_game.State == GameState.Menu) Application.Quit();
            else { _game.Back(); _ui.Show(_game.State); }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused || _game == null) return;
            if (_game.State == GameState.Playing) { _game.Pause(); _ui.Show(_game.State); }
            _input?.Cancel();
            Persist();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused || _game == null) return;
            if (_game.State == GameState.Playing) { _game.Pause(); _ui.Show(_game.State); }
            _input?.Cancel();
        }

        void OnDestroy()
        {
            if (_back == null) return;
            _back.Pressed -= SystemBack;
            _back.Dispose();
            _back = null;
        }

        void Persist()
        {
            if (_saves == null || _settings == null) return;
            if (_game != null && _game.Mode != GameMode.Hardcore) _settings.bestScore = _game.Best;
            _saves.Save(_settings);
        }

        static Language DeviceLanguage() =>
            Application.systemLanguage == SystemLanguage.Russian ? Language.Russian : Language.English;
    }
}
