using System;
using System.Collections.Generic;
using HR.Core;
using HR.Themes;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// Every screen the game has, built in code and switched by state.
    ///
    /// Holds no literals: all text comes from Strings, so a language change refreshes the whole
    /// interface without a reload and nothing can ship untranslated by being typed in here.
    /// The playfield is never covered — the score sits at the top, the buttons at the bottom,
    /// and the middle band where the finger works stays clear (spec §10.1).
    /// </summary>
    public sealed class GameUi : MonoBehaviour
    {
        public event Action PlayPressed;
        public event Action PausePressed;
        public event Action ResumePressed;
        public event Action HomePressed;
        public event Action RetryPressed;
        public event Action ThemesPressed;
        public event Action SettingsPressed;
        public event Action AboutPressed;
        public event Action BackPressed;
        public event Action ResetProgressConfirmed;
        public event Action<Theme> ThemeChosen;
        public event Action SettingsChanged;

        /// <summary>Raised by any button, so the app can give it a sound.</summary>
        public event Action Clicked;

        readonly Dictionary<GameState, RectTransform> _screens = new Dictionary<GameState, RectTransform>();
        readonly List<Action> _stringRefreshers = new List<Action>();
        readonly List<Graphic> _tinted = new List<Graphic>();
        readonly List<Image> _scrims = new List<Image>();
        readonly Dictionary<Theme, Text> _themeLocks = new Dictionary<Theme, Text>();
        readonly Dictionary<Theme, Button> _themeButtons = new Dictionary<Theme, Button>();
        readonly Dictionary<Theme, Text> _themeLabels = new Dictionary<Theme, Text>();

        SaveData _settings;
        ScoreKeeper _score;

        Text _scoreLabel;
        Text _hudBestLabel;
        Text _menuBestLabel;
        Text _overScoreLabel;
        Text _overBestLabel;
        Text _overUnlockLabel;
        Text _hintLabel;
        Button _pauseButton;
        RectTransform _confirmPanel;
        RectTransform _aboutPanel;

        Canvas _canvas;
        SafeAreaFitter _safeArea;

        public Canvas Canvas => _canvas;

        /// <summary>The score, so the juice can make it swell on a pass.</summary>
        public Text ScoreLabel => _scoreLabel;

        public GameState Current { get; private set; } = GameState.Boot;

        /// <summary>
        /// Lays the interface out at an exact size, regardless of the window it is running in.
        ///
        /// The screenshot rig cannot do without this. Screen.SetResolution is a no-op in batch
        /// mode, so the canvas keeps the batch window's shape — measured at 1663x1247, landscape —
        /// and every frame captured as "1080x2400" is that one landscape layout stretched into a
        /// differently-shaped texture. Thirty such frames were taken before anything checked, and
        /// they showed five shapes while proving one. World space is what takes the size away
        /// from the window and gives it to the caller.
        /// </summary>
        /// <summary>
        /// What CanvasScaler would pick for this screen, reproduced rather than approximated.
        /// Unity interpolates in log space, so a plain average of the two ratios is not the
        /// same number and would drift most on the shapes furthest from the reference.
        /// </summary>
        public static float ScaleFactorFor(int width, int height, float match = 0.5f)
        {
            float logWidth = Mathf.Log(width / UiBuilder.ReferenceWidth, 2f);
            float logHeight = Mathf.Log(height / UiBuilder.ReferenceHeight, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
        }

        public void OverrideLayoutShape(Camera camera, int width, int height,
                                        Rect? safeArea = null)
        {
            if (_canvas == null || camera == null) return;

            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = camera;

            // The device's pixels are not the layout's units. A 1536x2048 tablet does not give
            // the interface 2048 units of height — the scaler divides by 1.23 and hands it 1663.
            // Sizing the canvas in raw pixels quietly grants every shape more room than it has,
            // which is the opposite of what a layout gate is for: the tablet, the one shape most
            // likely to overflow, was the one being tested most leniently.
            float factor = ScaleFactorFor(width, height);
            float units = height / factor;

            var rect = (RectTransform)_canvas.transform;
            rect.sizeDelta = new Vector2(width / factor, units);

            // One reference unit becomes this many world units, so the canvas height exactly
            // spans what an orthographic camera sees.
            float unit = 2f * camera.orthographicSize / units;
            rect.localScale = new Vector3(unit, unit, unit);
            rect.rotation = camera.transform.rotation;
            rect.position = camera.transform.position + camera.transform.forward;

            // The device's safe area belongs to the batch window; this shape gets the whole frame.
            // A caller that names a safe area gets exactly it, in layout units; a caller that
            // does not gets the whole frame. The notch is the interesting case and it has to be
            // askable for, because no batch-mode window will ever have one.
            var size = rect.sizeDelta;
            var area = safeArea ?? new Rect(0f, 0f, size.x, size.y);
            _safeArea?.Pin(area, new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y)));

            Canvas.ForceUpdateCanvases();
        }

        public void Initialise(SaveData settings, ScoreKeeper score, Camera camera = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _score = score ?? throw new ArgumentNullException(nameof(score));

            _canvas = UiBuilder.CreateCanvas("UI", transform, camera);
            var canvas = _canvas;
            var safe = UiBuilder.CreateSafeArea(canvas.transform);
            _safeArea = safe.GetComponent<SafeAreaFitter>();

            BuildMenu(safe);
            BuildHud(safe);
            BuildPause(safe);
            BuildGameOver(safe);
            BuildThemes(safe);
            BuildSettings(safe);

            Strings.Changed += RefreshStrings;
            Show(GameState.Menu);
        }

        void OnDestroy() => Strings.Changed -= RefreshStrings;

        /// <summary>Shows the one screen this state wants and hides the rest.</summary>
        public void Show(GameState state)
        {
            Current = state;

            // About and Settings share a screen; Boot shows nothing at all.
            var wanted = state == GameState.Settings ? GameState.Settings : state;

            foreach (var pair in _screens)
                pair.Value.gameObject.SetActive(pair.Key == wanted);

            // The score stays up through a pause, because losing it is disorienting. Everything
            // else on the HUD goes: a pause button that pauses nothing, a hint for a gesture that
            // is not being made, and a record line that the pause card cuts in half.
            if (state == GameState.Playing || state == GameState.Paused)
                _screens[GameState.Playing].gameObject.SetActive(true);
            SetHudDetail(state == GameState.Playing);

            if (state == GameState.Menu) RefreshMenu();
            if (state == GameState.GameOver) RefreshGameOver();
            if (state == GameState.ThemeSelect) RefreshThemes();
            if (_confirmPanel != null) _confirmPanel.gameObject.SetActive(false);
            if (_aboutPanel != null) _aboutPanel.gameObject.SetActive(false);
        }

        public void SetScore(int score)
        {
            if (_scoreLabel != null) _scoreLabel.text = score.ToString();
        }

        /// <summary>Paints the interface in the theme's ink.</summary>
        public void ApplyTheme(ThemePalette palette)
        {
            if (palette == null) return;
            var ink = new Color(palette.Ui.R / 255f, palette.Ui.G / 255f, palette.Ui.B / 255f, 1f);
            var paper = new Color(palette.Background.R / 255f, palette.Background.G / 255f,
                                  palette.Background.B / 255f, 1f);

            // Scrims take the theme's own background rather than black: dimming a light theme
            // with black turns a pale, airy screen into a muddy grey one.
            foreach (var scrim in _scrims)
            {
                if (scrim == null) continue;
                var tinted = paper;
                tinted.a = scrim.color.a;
                scrim.color = tinted;
            }

            foreach (var graphic in _tinted)
            {
                if (graphic == null) continue;
                var faded = ink;
                faded.a = graphic.color.a;
                graphic.color = faded;
            }
        }

        void SetHudDetail(bool visible)
        {
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(visible);
            if (_hudBestLabel != null) _hudBestLabel.gameObject.SetActive(visible && _score.Best > 0);
            if (_hintLabel != null && !visible) _hintLabel.gameObject.SetActive(false);
        }

        public void RefreshStrings()
        {
            foreach (var refresh in _stringRefreshers) refresh();
            RefreshMenu();
            RefreshThemes();
        }

        // ---------------------------------------------------------------- screens

        void BuildMenu(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Menu", parent, new Color(1f, 1f, 1f, 0.88f));
            _screens[GameState.Menu] = screen;
            Scrim(screen);

            var column = UiBuilder.CreateColumn("Column", screen);

            var title = Label(column.transform, "app.title", UiBuilder.TitleSize);
            _menuBestLabel = UiBuilder.CreateLabel("Best", column.transform, string.Empty, UiBuilder.BodySize);
            Track(_menuBestLabel);

            Button(column.transform, "menu.play", () => PlayPressed?.Invoke(), UiBuilder.TouchTarget * 1.4f);
            Button(column.transform, "menu.themes", () => ThemesPressed?.Invoke());
            Button(column.transform, "menu.settings", () => SettingsPressed?.Invoke());

            Track(title);
        }

        void BuildHud(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Hud", parent);
            _screens[GameState.Playing] = screen;

            _scoreLabel = UiBuilder.CreateLabel("Score", screen, "0", UiBuilder.ScoreSize);
            UiBuilder.PinTop((RectTransform)_scoreLabel.transform, 220f, 40f);
            Track(_scoreLabel);

            _hudBestLabel = UiBuilder.CreateLabel("Best", screen, string.Empty, UiBuilder.BodySize);
            UiBuilder.PinTop((RectTransform)_hudBestLabel.transform, 70f, 270f);
            // Sixty per cent, per spec §10.1 — present but never competing with the score. The
            // alpha is set before tracking because the tint keeps whatever alpha it finds.
            _hudBestLabel.color = new Color(1f, 1f, 1f, 0.6f);
            Track(_hudBestLabel);

            var pause = UiBuilder.CreateButton("Pause", screen, "II", () => PausePressed?.Invoke());
            _pauseButton = pause;
            Track(pause.targetGraphic);
            Track(pause.GetComponentInChildren<Text>());
            var pauseRect = (RectTransform)pause.transform;
            pauseRect.anchorMin = new Vector2(1f, 1f);
            pauseRect.anchorMax = new Vector2(1f, 1f);
            pauseRect.pivot = new Vector2(1f, 1f);
            pauseRect.anchoredPosition = new Vector2(-32f, -32f);
            pauseRect.sizeDelta = new Vector2(UiBuilder.TouchTarget, UiBuilder.TouchTarget);

            _hintLabel = UiBuilder.CreateLabel("Hint", screen, Strings.Get("hint.swipe"), UiBuilder.BodySize);
            var hintRect = (RectTransform)_hintLabel.transform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.offsetMin = new Vector2(0f, 180f);
            hintRect.offsetMax = new Vector2(0f, 260f);
            _stringRefreshers.Add(() => _hintLabel.text = Strings.Get("hint.swipe"));
            Track(_hintLabel);
        }

        void BuildPause(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Pause", parent, new Color(0f, 0f, 0f, 0.4f));
            _screens[GameState.Paused] = screen;

            var column = UiBuilder.CreateScrollColumn("Column", screen);
            Scrim(UiBuilder.AddCard(column, 0.92f));
            Track(Label(column.transform, "pause.title", UiBuilder.TitleSize));
            Button(column.transform, "pause.resume", () => ResumePressed?.Invoke(), UiBuilder.TouchTarget * 1.3f);
            Button(column.transform, "pause.home", () => HomePressed?.Invoke());
        }

        void BuildGameOver(Transform parent)
        {
            // Spec §10.3: the frozen scene dimmed by forty per cent.
            var screen = UiBuilder.CreatePanel("GameOver", parent, new Color(0f, 0f, 0f, 0.4f));
            _screens[GameState.GameOver] = screen;

            var column = UiBuilder.CreateScrollColumn("Column", screen);
            Scrim(UiBuilder.AddCard(column, 0.92f));

            _overScoreLabel = UiBuilder.CreateLabel("Score", column.transform, "0", UiBuilder.ScoreSize);
            _overBestLabel = UiBuilder.CreateLabel("Best", column.transform, string.Empty, UiBuilder.BodySize);
            _overUnlockLabel = UiBuilder.CreateLabel("Unlocked", column.transform, string.Empty, UiBuilder.BodySize);
            Track(_overScoreLabel); Track(_overBestLabel); Track(_overUnlockLabel);

            // Retry is the big one and sits lowest, where the thumb already is.
            Button(column.transform, "over.retry", () => RetryPressed?.Invoke(), UiBuilder.TouchTarget * 1.4f);
            Button(column.transform, "over.home", () => HomePressed?.Invoke());
            Button(column.transform, "over.themes", () => ThemesPressed?.Invoke());
        }

        void BuildThemes(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Themes", parent, new Color(1f, 1f, 1f, 0.92f));
            _screens[GameState.ThemeSelect] = screen;
            Scrim(screen);

            var column = UiBuilder.CreateColumn("Column", screen, 24f);

            var title = Label(column.transform, "themes.title", UiBuilder.TitleSize);
            ((RectTransform)title.transform).sizeDelta = new Vector2(0f, 130f);
            Track(title);

            // Spec 10.4: a sideways carousel of cards, not a list of words.
            var carousel = UiBuilder.CreateCarousel("Carousel", column.transform, 720f);

            foreach (var theme in ThemePalette.All)
            {
                var captured = theme;
                var card = UiBuilder.CreateButton($"Theme{theme}", carousel.content,
                    string.Empty,
                    () => { Clicked?.Invoke(); ThemeChosen?.Invoke(captured); }, 700f);

                var cardRect = (RectTransform)card.transform;
                cardRect.sizeDelta = new Vector2(540f, 700f);
                Track(card.targetGraphic);

                var palette = ThemePalette.For(theme);
                var rings = new Color[palette.RingColours.Length];
                for (int i = 0; i < rings.Length; i++)
                    rings[i] = ThemeApplier.ToColor(palette.RingColours[i]);

                // Left untracked on purpose: a preview shows its own theme's colours, not the
                // colours of whichever theme happens to be active.
                var preview = UiBuilder.CreateRingPreview("Preview", card.transform, 400f,
                    ThemeApplier.ToColor(palette.Background), rings, ThemeApplier.ToColor(palette.Ball));
                preview.anchorMin = new Vector2(0.5f, 1f);
                preview.anchorMax = new Vector2(0.5f, 1f);
                preview.pivot = new Vector2(0.5f, 1f);
                preview.anchoredPosition = new Vector2(0f, -40f);

                var label = card.GetComponentInChildren<Text>();
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.offsetMin = new Vector2(16f, 150f);
                labelRect.offsetMax = new Vector2(-16f, 230f);
                label.text = Strings.ThemeName(theme);

                var lockLabel = UiBuilder.CreateLabel("Lock", card.transform, string.Empty,
                    UiBuilder.BodySize);
                var lockRect = (RectTransform)lockLabel.transform;
                lockRect.anchorMin = new Vector2(0f, 0f);
                lockRect.anchorMax = new Vector2(1f, 0f);
                lockRect.pivot = new Vector2(0.5f, 0f);
                lockRect.offsetMin = new Vector2(16f, 60f);
                lockRect.offsetMax = new Vector2(-16f, 130f);

                _themeButtons[theme] = card;
                _themeLabels[theme] = label;
                _themeLocks[theme] = lockLabel;
                Track(label);
                Track(lockLabel);
            }

            var back = Button(column.transform, "common.back", () => BackPressed?.Invoke());
            ((RectTransform)back.transform).sizeDelta = new Vector2(0f, UiBuilder.TouchTarget);

            // Correct from the moment they exist, not from the first time the screen is shown.
            // A button that is briefly interactable when it should not be is a button someone
            // can press.
            RefreshThemes();
        }

        void BuildSettings(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Settings", parent, new Color(1f, 1f, 1f, 0.95f));
            _screens[GameState.Settings] = screen;
            Scrim(screen);

            var column = UiBuilder.CreateScrollColumn("Column", screen, 16f);

            // Spec 10.5 asks for a switch and a level for each, independently. The switch alone
            // is the difference between "quieter" and "off", and a player on a bus wants both.
            Toggle(column.transform, "set.sound", _settings.soundEnabled,
                v => { _settings.soundEnabled = v; SettingsChanged?.Invoke(); });
            Level(column.transform, "set.sound.level", _settings.soundVolume,
                v => { _settings.soundVolume = v; SettingsChanged?.Invoke(); });

            Toggle(column.transform, "set.music", _settings.musicEnabled,
                v => { _settings.musicEnabled = v; SettingsChanged?.Invoke(); });
            Level(column.transform, "set.music.level", _settings.musicVolume,
                v => { _settings.musicVolume = v; SettingsChanged?.Invoke(); });

            Track(Label(column.transform, "set.sensitivity", UiBuilder.BodySize));
            var sensitivity = UiBuilder.CreateSlider("Sensitivity", column.transform,
                SaveData.MinSensitivity, SaveData.MaxSensitivity, _settings.sensitivity,
                v => { _settings.sensitivity = v; SettingsChanged?.Invoke(); });
            foreach (var part in sensitivity.GetComponentsInChildren<Image>(true)) Track(part);

            Toggle(column.transform, "set.invert", _settings.invertRotation,
                v => { _settings.invertRotation = v; SettingsChanged?.Invoke(); });
            Toggle(column.transform, "set.vibration", _settings.vibrationEnabled,
                v => { _settings.vibrationEnabled = v; SettingsChanged?.Invoke(); });
            Toggle(column.transform, "set.lowfx", _settings.reduceEffects,
                v => { _settings.reduceEffects = v; SettingsChanged?.Invoke(); });

            Button(column.transform, "set.language", CycleLanguage);
            Button(column.transform, "set.about", () => { ShowAbout(); AboutPressed?.Invoke(); });
            Button(column.transform, "set.reset", ShowResetConfirm);
            Button(column.transform, "common.back", () => BackPressed?.Invoke());

            BuildResetConfirm(screen);
            BuildAbout(screen);
        }

        /// <summary>Wiping a record asks first (CONTRACT.md §4) — and defaults to not doing it.</summary>
        void BuildResetConfirm(Transform parent)
        {
            _confirmPanel = UiBuilder.CreatePanel("ConfirmReset", parent, new Color(1f, 1f, 1f, 0.97f));
            Scrim(_confirmPanel);

            var column = UiBuilder.CreateColumn("Column", _confirmPanel);
            Track(Label(column.transform, "set.reset.confirm", UiBuilder.BodySize));

            Button(column.transform, "common.no", () => _confirmPanel.gameObject.SetActive(false));
            Button(column.transform, "common.yes", () =>
            {
                _confirmPanel.gameObject.SetActive(false);
                ResetProgressConfirmed?.Invoke();
            });

            _confirmPanel.gameObject.SetActive(false);
        }

        void ShowResetConfirm() => _confirmPanel.gameObject.SetActive(true);

        void BuildAbout(Transform parent)
        {
            _aboutPanel = UiBuilder.CreatePanel("About", parent, new Color(1f, 1f, 1f, 0.97f));
            Scrim(_aboutPanel);

            var column = UiBuilder.CreateScrollColumn("Column", _aboutPanel);
            Track(Label(column.transform, "app.title", UiBuilder.TitleSize));

            // Read from the player settings rather than written here, so it cannot disagree with
            // the version the store shows.
            var version = UiBuilder.CreateLabel("Version", column.transform,
                Application.version, UiBuilder.BodySize);
            Track(version);
            _stringRefreshers.Add(() => version.text = Application.version);

            // A paragraph, not a line: the default single-line height would let the next row
            // sit on top of its second line.
            var body = Label(column.transform, "about.body", UiBuilder.BodySize);
            ((RectTransform)body.transform).sizeDelta = new Vector2(0f, 340f);
            Track(body);
            Track(Label(column.transform, "about.privacy", UiBuilder.BodySize));

            Button(column.transform, "common.back", () => _aboutPanel.gameObject.SetActive(false));

            _aboutPanel.gameObject.SetActive(false);
        }

        void ShowAbout() => _aboutPanel.gameObject.SetActive(true);

        void CycleLanguage()
        {
            _settings.Language = _settings.Language switch
            {
                LanguageSetting.Auto => LanguageSetting.Russian,
                LanguageSetting.Russian => LanguageSetting.English,
                _ => LanguageSetting.Auto,
            };
            SettingsChanged?.Invoke();
        }

        // ---------------------------------------------------------------- refresh

        void RefreshMenu()
        {
            if (_menuBestLabel == null || _score == null) return;

            // CONTRACT.md G-04: an empty "Best: 0" on a fresh install is noise, so it is absent
            // until there is something to show.
            bool has = _score.Best > 0;
            _menuBestLabel.gameObject.SetActive(has);
            if (has) _menuBestLabel.text = Strings.Format("menu.best", _score.Best);
        }

        public void RefreshHud()
        {
            if (_hudBestLabel == null || _score == null) return;

            bool has = _score.DisplayedBest > 0;
            _hudBestLabel.gameObject.SetActive(has);
            if (has) _hudBestLabel.text = Strings.Format("hud.best", _score.DisplayedBest);
        }

        public void ShowHint(bool visible)
        {
            if (_hintLabel != null) _hintLabel.gameObject.SetActive(visible);
        }

        void RefreshGameOver()
        {
            if (_score == null) return;

            _overScoreLabel.text = _score.Score.ToString();
            _overBestLabel.text = _score.IsNewBest
                ? Strings.Get("over.newbest")
                : Strings.Format("over.best", _score.Best);

            // Announced in the run that earned it, once (CONTRACT.md G-07).
            Theme? unlocked = null;
            foreach (var theme in ThemePalette.All)
                if (_score.UnlockedThisRun(theme)) { unlocked = theme; break; }

            _overUnlockLabel.gameObject.SetActive(unlocked.HasValue);
            if (unlocked.HasValue)
                _overUnlockLabel.text = Strings.Format("over.unlocked", Strings.ThemeName(unlocked.Value));
        }

        void RefreshThemes()
        {
            if (_score == null) return;

            foreach (var theme in ThemePalette.All)
            {
                if (!_themeButtons.TryGetValue(theme, out var button)) continue;

                bool unlocked = _score.IsUnlocked(theme);
                button.interactable = unlocked;

                // A locked card reads as locked by its own weight. Greying only the fill leaves
                // a full-strength name on it, which is the one part the eye actually goes to.
                var label = _themeLabels[theme];
                label.text = Strings.ThemeName(theme);
                var colour = label.color;
                colour.a = unlocked ? 1f : 0.45f;
                label.color = colour;

                // The condition lives on its own line under the name rather than being glued to
                // it: on a card there is room, and "Earth · Best 50" read as part of the name.
                var padlock = _themeLocks[theme];
                padlock.text = unlocked
                    ? string.Empty
                    : Strings.Format("theme.locked", ScoreKeeper.RequiredBestFor(theme));
                var lockColour = padlock.color;
                lockColour.a = unlocked ? 0f : 0.7f;
                padlock.color = lockColour;
            }
        }

        // ---------------------------------------------------------------- helpers

        Text Label(Transform parent, string key, int size)
        {
            var label = UiBuilder.CreateLabel(key, parent, Strings.Get(key), size);
            _stringRefreshers.Add(() => label.text = Strings.Get(key));
            return label;
        }

        Button Button(Transform parent, string key, Action onClick, float height = UiBuilder.TouchTarget)
        {
            var button = UiBuilder.CreateButton(key, parent, Strings.Get(key),
                () => { Clicked?.Invoke(); onClick?.Invoke(); }, height);
            var label = button.GetComponentInChildren<Text>();
            _stringRefreshers.Add(() => label.text = Strings.Get(key));
            Track(label);
            // The fill is tinted as well, not just the text. Left white it is invisible against
            // a light theme, and the buttons read as loose words floating on the background.
            Track(button.targetGraphic);
            return button;
        }

        /// <summary>A labelled 0..1 slider, tinted like everything else.</summary>
        Slider Level(Transform parent, string key, float value, Action<float> onChanged)
        {
            Track(Label(parent, key, UiBuilder.BodySize));

            var slider = UiBuilder.CreateSlider(key, parent, 0f, 1f, value, onChanged);
            foreach (var part in slider.GetComponentsInChildren<Image>(true)) Track(part);
            return slider;
        }

        Toggle Toggle(Transform parent, string key, bool value, Action<bool> onChanged)
        {
            var toggle = UiBuilder.CreateToggle(key, parent, Strings.Get(key), value, onChanged);
            var label = toggle.GetComponentInChildren<Text>();
            _stringRefreshers.Add(() => label.text = Strings.Get(key));
            Track(label);
            Track(toggle.targetGraphic);
            Track(toggle.graphic);
            return toggle;
        }

        void Track(Graphic graphic) { if (graphic != null) _tinted.Add(graphic); }

        void Scrim(RectTransform panel)
        {
            var image = panel.GetComponent<Image>();
            if (image != null) _scrims.Add(image);
        }

        void Scrim(Image image) { if (image != null) _scrims.Add(image); }

        /// <summary>Every interactive element, for the touch-target test.</summary>
        public IEnumerable<RectTransform> InteractiveElements()
        {
            foreach (var selectable in GetComponentsInChildren<Selectable>(includeInactive: true))
                yield return (RectTransform)selectable.transform;
        }

        public RectTransform ScreenFor(GameState state) =>
            _screens.TryGetValue(state, out var screen) ? screen : null;
    }
}
