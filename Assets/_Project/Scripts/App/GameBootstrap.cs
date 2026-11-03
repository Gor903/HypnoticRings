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
    /// <summary>
    /// Wires the game together: save, settings, tunnel, input, themes and interface.
    ///
    /// This is the one place that knows about every layer, which is why it is its own assembly.
    /// CONTRACT.md §6 keeps the layers from reaching sideways into each other — the interface
    /// does not know about the finger, the tunnel does not know about the save — and something
    /// has to join them up. That job is composition, and it lives here rather than being
    /// smuggled into whichever layer happened to need it first.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] GameConfig config;
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

        /// <summary>The numbers the scene was raised from, so a test can check against the source.</summary>
        public GameConfig Config => config;

        public GameManager Game => _game;
        public GameUi Ui => _ui;
        public TunnelController Tunnel => _tunnel;
        public InputController Input => _input;
        public ThemeApplier Themes => _themes;
        public SaveData Settings => _settings;
        public AudioManager Audio => _audio;
        public Effects Effects => _effects;
        public ScreenJuice Juice => _juice;
        public Haptics Haptics => _haptics;

        /// <summary>The red circle at the centre, so a test can read what it was painted.</summary>
        public CentreMarker Centre => _centre;

        /// <summary>The system back button, so a test can press it without a phone.</summary>
        public AndroidBackButton Back => _back;

        bool _built;

        SaveService _configuredSaves;
        Camera _configuredCamera;

        /// <summary>
        /// Supplies the config, and optionally the save location, before Awake runs. Create the
        /// object inactive, configure it, then activate.
        ///
        /// The save location has to be settable here rather than afterwards: Awake fires the
        /// moment the object is activated, and a Build call made after that arrives too late to
        /// change anything. Without it a test reads and writes the player's real save — which it
        /// did, silently, until a value from a previous test run turned up in an assertion.
        /// </summary>
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

        /// <summary>Public so a test can point the save somewhere disposable.</summary>
        public void Build(SaveService saves)
        {
            if (_built) return;
            _built = true;
            _saves = saves;
            _settings = _saves.Load();
            foreach (string repair in _saves.LastReport) Debug.LogWarning($"[HR] save: {repair}");

            Strings.Apply(_settings.Language, DeviceLanguage());

            // Camera.main is whichever enabled camera happens to be tagged MainCamera, and a
            // test that has loaded a scene has two. The background then gets themed on one
            // camera while the picture is taken through the other, which looks exactly like a
            // theme that failed to apply — it is not, and it cost a round of screenshots.
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

            // The tunnel is driven by GameManager and by nothing else. Left to step itself it
            // advanced TWICE per physics frame — once from its own FixedUpdate, once from the
            // game loop — so compression and retraction ran at double the speed the difficulty
            // curve asks for. And its own FixedUpdate knows nothing about pause, so the tunnel
            // kept closing in behind the pause panel.
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

            // Registered once the screens exist, because the first press can arrive immediately.
            _back = new AndroidBackButton();
            _back.Pressed += SystemBack;

            Wire();
            _themes.Apply(_settings.Theme);
            _ui.ApplyTheme(_themes.Palette);
            ApplyThemeToEffects();
            _ui.Show(GameState.Menu);
            ShowWorld(GameState.Menu);
        }

        PhysicsMaterial2D _ringPhysics;

        /// <summary>
        /// Points the camera at the tunnel the way the config asks. Done here rather than only
        /// in SceneBuilder because the framing is a number to be tuned, and tuning it should not
        /// mean regenerating the scenes — both read the same asset, so neither can drift.
        /// </summary>
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

            // Retry keeps the mode it was playing: a player who just lost a hardcore run wants
            // another hardcore run, and silently dropping them into the ordinary game would be
            // the worst possible answer to pressing the biggest button on the screen.
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
                _themes.Apply(_settings.Theme);
                _ui.ApplyTheme(_themes.Palette);
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
                if (!_settings.hintSeen) { _settings.hintSeen = true; _ui.ShowHint(false); Persist(); }
            };

            _game.KilledByTouch += ring =>
            {
                _haptics.Lose();
                _audio.PlayLose();
            };

            _tunnel.BallTouchedRing += touches =>
            {
                // The ordinary game already knocks on contact through BallController.Struck.
                // This is the hardcore warning: the touch that spends the allowance sounds
                // different from the one that does not.
                if (_game.Mode == GameMode.Hardcore && touches == GameManager.HardcoreTouchAllowance)
                    _haptics.Pass();
            };

            _game.StateChanged += (_, to) =>
            {
                _ui.Show(to);
                ShowWorld(to);
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

                // The curtain closes on the way out of a run and opens on the way in, which is
                // the whole of spec 11.4's menu-to-game transition.
                if (to == GameState.Playing || to == GameState.Menu) _juice.Fade(toClear: true);
            };
        }

        /// <summary>
        /// The ball belongs to a run. Left visible on the menu it sits over the buttons, which
        /// is exactly what the first captured frame showed.
        /// </summary>
        void ShowWorld(GameState state)
        {
            // Frozen before it is hidden, thawed after it is back. A body on a disabled object
            // reads as being at rest and accepts no velocity at all, so doing either on the
            // wrong side of the switch loses the throw: freezing after hiding stored a zero,
            // thawing before showing wrote one into nothing. Both resumed the game by dropping
            // the ball straight down, and both looked like a physics bug rather than an order.
            bool playing = state == GameState.Playing;
            if (!playing)
            {
                if (_ball != null) _ball.SetFrozen(true);
            }

            // The whole playfield goes away behind the pause card, tunnel and ball together.
            // Left showing, pause is a free look at where every gap sits — the one thing the
            // player is supposed to be reading under time pressure. Game over keeps it: spec
            // 10.3 asks for the frozen scene there, and by then there is nothing left to learn.
            bool inPlay = state == GameState.Playing || state == GameState.GameOver;
            if (_ball != null) _ball.gameObject.SetActive(inPlay);
            if (_centre != null) _centre.gameObject.SetActive(inPlay);
            if (_tunnel != null) _tunnel.gameObject.SetActive(state != GameState.Paused);

            // The trail is its own pool and outlives whatever it was following, so hiding the
            // ball left four of its own dots hanging over an otherwise empty screen. Game over
            // keeps them: there the trail is part of the frozen moment the spec asks for.
            if (state == GameState.Paused && _effects != null) _effects.Clear();

            // Only a running game simulates. Pause and the game-over screen are pictures of a
            // moment, and a ball that carries on flying through either is the clearest possible
            // proof that the game never actually stopped.
            if (!playing) return;
            if (_ball != null) _ball.SetFrozen(false);
        }

        /// <summary>
        /// One switch for every effect, so "Reduce effects" is a single decision rather than a
        /// setting each effect remembers to consult.
        /// </summary>
        void ApplyEffectSettings()
        {
            bool wanted = !_settings.reduceEffects;

            _effects.Enabled = wanted;
            _juice.Enabled = wanted;
            _haptics.Enabled = _settings.vibrationEnabled;

            // The interface reads the flag only while it is being painted, so switching the
            // setting has to repaint it. Left out, "reduce effects" took hold everywhere except
            // on the screen the player was looking at when they pressed it.
            if (_ui != null && _themes != null) _ui.ApplyTheme(_themes.Palette);

            if (!wanted)
            {
                _effects.Clear();
                _juice.Clear();
                _tunnel.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Switches theme the way the menu does: palette, tunnel, interface, effects and save,
        /// in that order and in one place.
        ///
        /// Public because the screenshot rig needs the same path, and reaching past it is how a
        /// picture of something the game never shows gets taken. Applying the palette alone left
        /// the transition curtain in the previous theme's colour — a flat pale wash over the
        /// whole frame, which cost an hour of looking for a glow that was not there.
        /// </summary>
        public void ChooseTheme(Theme theme)
        {
            if (_game == null || !_game.ScoreKeeper.IsUnlocked(theme)) return;

            _settings.Theme = theme;
            _themes.Apply(theme);
            _ui.ApplyTheme(_themes.Palette);
            ApplyThemeToEffects();
            Persist();
        }

        void ApplyThemeToEffects()
        {
            var palette = _themes.Palette;
            _effects.TrailColour = ThemeApplier.ToColor(palette.RingColourAt(0));
            _juice.CurtainColour = ThemeApplier.ToColor(palette.Background);
            _centre.Apply(ThemeApplier.ToColor(palette.Background));
        }

        void StartRun()
        {
            _game.StartRun(seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : seed);
            _input.ResetAngle();

            // The menu left the tunnel turning; a run starts square.
            _tunnel.transform.localRotation = Quaternion.identity;
            _effects.Clear();

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = _ringPhysics;

            _ui.SetScore(0);
            _ui.RefreshHud();
            _ui.ShowHint(!_settings.hintSeen);
            _ui.Show(_game.State);
        }

        /// <summary>Slow enough to be hypnotic rather than busy.</summary>
        const float MenuSpinDegPerSecond = 6f;

        void Update()
        {
            if (_game == null) return;

            if (_game.State == GameState.Playing)
            {
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
                _effects.Tick(Time.deltaTime);
            }
            else if (_game.State == GameState.GameOver)
            {
                // Still ticking after the loss: this is the one effect that plays out when the
                // game itself has stopped.
                _effects.Tick(Time.deltaTime);
            }
            // The palette breathes wherever the tunnel is visible, menu included — it is the
            // same tunnel, and a theme that only moved during a run would read as a glitch the
            // moment the player pressed Play.
            if (_effects.Enabled) _themes.Tick(Time.deltaTime);

            if (_game.State == GameState.Menu && _effects.Enabled)
            {
                // Spec 10.2 asks for slowly turning rings behind the menu. Rotating the whole
                // tunnel object is purely visual — no ring moves relative to another, and the
                // physics it carries is asleep while the menu is up.
                _tunnel.transform.Rotate(0f, 0f, MenuSpinDegPerSecond * Time.deltaTime);
            }

            // Escape, which is what Unity makes of a back *key*. Android 13 and up send the
            // gesture to a callback instead, and AndroidBackButton picks that up; both land on
            // SystemBack, which is where the one decision lives.
            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                SystemBack();
        }

        int _lastBackFrame = -1;

        /// <summary>
        /// One press of back, from whichever path the device uses. A device can deliver both at
        /// once, so the second within a frame is dropped — otherwise one press closes two screens.
        /// </summary>
        public void SystemBack()
        {
            if (_game == null || Time.frameCount == _lastBackFrame) return;
            _lastBackFrame = Time.frameCount;

            if (_game.State == GameState.Menu) Application.Quit();
            else { _game.Back(); _ui.Show(_game.State); }
        }

        /// <summary>Backgrounded or interrupted: pause, drop the gesture, and keep the record.</summary>
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

        /// <summary>The callback outlives the scene unless it is handed back.</summary>
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
            _settings.bestScore = _game != null ? _game.Best : _settings.bestScore;
            _saves.Save(_settings);
        }

        static Language DeviceLanguage() =>
            Application.systemLanguage == SystemLanguage.Russian ? Language.Russian : Language.English;
    }
}
