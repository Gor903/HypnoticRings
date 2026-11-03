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

        /// <summary>Play, but under the hardcore rules. A separate button, not a toggle:
        /// a mode you can leave switched on by accident is a mode you lose runs to.</summary>
        public event Action HardcorePressed;
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
        // One list per role rather than one list for everything. The old single list painted
        // every element with the text colour and let it keep its own alpha, which is how a
        // button's fill ended up being the text colour at eight per cent — invisible, and
        // unfixable by styling while the palette knew only ink and paper.
        readonly List<Graphic> _tinted = new List<Graphic>();
        readonly List<Graphic> _surfaces = new List<Graphic>();
        readonly List<Graphic> _accents = new List<Graphic>();
        readonly List<Graphic> _accentTexts = new List<Graphic>();
        readonly List<Image> _scrims = new List<Image>();

        // Shape is tracked separately from colour because it needs a number colour does not:
        // the control's height, which is what a corner radius is a share of.
        readonly List<(Image Image, float Height)> _shaped = new List<(Image, float)>();
        readonly List<ToggleVisual> _switches = new List<ToggleVisual>();
        readonly List<PressFeedback> _presses = new List<PressFeedback>();
        readonly List<Image> _knobs = new List<Image>();
        readonly List<Image> _tracks = new List<Image>();
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
        Button _languageChip;
        CarouselSnapping _carousel;

        /// <summary>The palette in force, for the few things painted outside the role lists.</summary>
        ThemePalette _palette;
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

            // Raised with the canvas, not alongside it: a screen nobody can press is not an
            // interface, and the two have no business being able to exist without each other.
            UiBuilder.EnsureEventSystem(transform);

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
            if (state == GameState.ThemeSelect) { RefreshThemes(); FocusChosenTheme(); }
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
            _palette = palette;
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

            // Roles are painted at full strength. Keeping the element's own alpha is what made
            // a fill invisible in the first place — alpha is how "quiet" was expressed when
            // there was only one colour to be quiet with, and there is no longer only one.
            Paint(_surfaces, ThemeApplier.ToColor(palette.Surface));
            Paint(_accents, ThemeApplier.ToColor(palette.Accent));
            Paint(_accentTexts, ThemeApplier.ToColor(palette.AccentText));

            // Shape travels with the theme, not with the screen. A palette swap that left every
            // control the same grey rectangle would be a recolour; the point of six themes is
            // that they feel like six places.
            var skin = UiSkin.For(palette.Theme);
            foreach (var (image, height) in _shaped) UiPainter.Shape(image, height, skin);

            var trackOff = ThemeApplier.ToColor(palette.TrackOff);
            var trackOn = ThemeApplier.ToColor(palette.TrackOn);
            var knobInk = ThemeApplier.ToColor(palette.Background);

            foreach (var visual in _switches)
            {
                if (visual == null) continue;
                UiPainter.Shape(visual.Track, UiBuilder.TrackHeight, skin);
                UiPainter.ShapeKnob(visual.Knob, skin);
                visual.SetColours(trackOff, trackOn, knobInk);
            }

            foreach (var track in _tracks)
            {
                if (track == null) continue;
                track.color = trackOff;
                UiPainter.Shape(track, UiBuilder.SliderTrackHeight, skin);
            }

            foreach (var knob in _knobs)
            {
                if (knob == null) continue;
                knob.color = ThemeApplier.ToColor(palette.Accent);
                UiPainter.ShapeKnob(knob, skin);
            }

            // Pressing has to be re-taught after a repaint: a flash returns to the colour it
            // remembered, and a colour remembered before a theme change is the old theme's.
            foreach (var press in _presses)
            {
                if (press == null) continue;
                press.Configure(skin, press.Fill);
            }

            // The pause button is round in every skin: it is the only control on the screen
            // that has to be found by the thumb without the eye leaving the tunnel.
            if (_pauseFill != null)
            {
                _pauseFill.sprite = UiBuilder.Disc;
                _pauseFill.type = Image.Type.Simple;
            }

            if (_scoreOutline != null) _scoreOutline.effectColor = paper;
            if (_bestOutline != null) _bestOutline.effectColor = paper;

            // Last, and after the roles: the theme cards are painted by which one is chosen,
            // and that answer is not in any of the lists above.
            RefreshThemes();
        }

        static void Paint(List<Graphic> graphics, Color colour)
        {
            foreach (var graphic in graphics)
            {
                if (graphic == null) continue;
                graphic.color = colour;
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
            RefreshLanguageChip();
        }

        /// <summary>The chip shows the setting, not the language the interface came out in.</summary>
        void RefreshLanguageChip()
        {
            if (_languageChip == null || _settings == null) return;

            var label = _languageChip.GetComponentInChildren<Text>();
            if (label == null) return;

            label.text = Strings.Get(_settings.Language switch
            {
                LanguageSetting.Russian => "set.lang.ru",
                LanguageSetting.English => "set.lang.en",
                _ => "set.lang.auto",
            });
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

            // Four stacked buttons of one size all claimed the same importance, and the screen
            // read as a list of four things rather than as one thing with three ways out of it.
            // Play is the biggest and the only accent; hardcore is a plain button under it;
            // themes and settings share a single row, which demotes them without hiding them.
            Button(column.transform, "menu.play", () => PlayPressed?.Invoke(),
                   UiBuilder.TouchTarget * 1.6f, primary: true);

            _hardcoreButton = Button(column.transform, "menu.hardcore",
                                     () => HardcorePressed?.Invoke());
            _hardcoreLockLabel = UiBuilder.CreateLabel("HardcoreLock", column.transform,
                                                       string.Empty, UiBuilder.BodySize);
            Track(_hardcoreLockLabel);

            var utilities = UiBuilder.CreateRow("Utilities", column.transform);
            Button(utilities.transform, "menu.themes", () => ThemesPressed?.Invoke());
            Button(utilities.transform, "menu.settings", () => SettingsPressed?.Invoke());

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

            // Both sit directly on the tunnel with nothing behind them, and a ring crossing a
            // glyph takes it apart. An outline in the background's own colour is what the rings
            // already use for the ball, costs four extra quads, and needs no panel that would
            // cover the thing the player is watching.
            _scoreOutline = Outline(_scoreLabel, ScoreOutlineWidth);
            _bestOutline = Outline(_hudBestLabel, BestOutlineWidth);

            var pause = UiBuilder.CreateButton("Pause", screen, "II", () => PausePressed?.Invoke());
            _pauseButton = pause;

            // Fill as a surface, glyph as ink. Tracked as ink it was the one button in the game
            // painted the same colour as its own label: a solid black disc with nothing on it.
            TrackSurface(pause.targetGraphic);
            Track(pause.GetComponentInChildren<Text>());
            _presses.Add(Press(pause));
            var pauseRect = (RectTransform)pause.transform;
            pauseRect.anchorMin = new Vector2(1f, 1f);
            pauseRect.anchorMax = new Vector2(1f, 1f);

            // Pivoted on its own centre and pulled a full radius clear of the corner. Pinned by
            // its top-right corner at an inset of 32 it sat half off the screen on 9x16 — the
            // glyph was sliced down the middle on every captured frame.
            pauseRect.pivot = new Vector2(0.5f, 0.5f);
            pauseRect.sizeDelta = new Vector2(PauseButtonSize, PauseButtonSize);
            pauseRect.anchoredPosition =
                new Vector2(-(PauseButtonSize * 0.5f + PauseInset),
                            -(PauseButtonSize * 0.5f + PauseInset));

            _pauseFill = pause.targetGraphic as Image;

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

            // Anchored low, so the control the screen exists for lands under the thumb that is
            // already holding the phone rather than in the middle, where it is a reach.
            var card = Card("Card", screen, anchorY: 0f, inset: CardInset);
            Track(Label(card.transform, "pause.title", UiBuilder.TitleSize));
            Button(card.transform, "pause.home", () => HomePressed?.Invoke());
            Button(card.transform, "pause.resume", () => ResumePressed?.Invoke(),
                   UiBuilder.TouchTarget * 1.4f, primary: true);
        }

        void BuildGameOver(Transform parent)
        {
            // Spec §10.3: the frozen scene dimmed by forty per cent.
            var screen = UiBuilder.CreatePanel("GameOver", parent, new Color(0f, 0f, 0f, 0.4f));
            _screens[GameState.GameOver] = screen;

            var card = Card("Card", screen, anchorY: 0f, inset: CardInset);

            _overScoreLabel = UiBuilder.CreateLabel("Score", card.transform, "0", UiBuilder.ScoreSize);
            _overBestLabel = UiBuilder.CreateLabel("Best", card.transform, string.Empty, UiBuilder.BodySize);
            _overUnlockLabel = UiBuilder.CreateLabel("Unlocked", card.transform, string.Empty, UiBuilder.BodySize);
            Track(_overScoreLabel); Track(_overBestLabel); Track(_overUnlockLabel);

            // The two ways out share a row above, and Retry is the last thing on the card —
            // which, on a card anchored to the bottom, is the lowest thing on the screen.
            var exits = UiBuilder.CreateRow("Exits", card.transform);
            Button(exits.transform, "over.home", () => HomePressed?.Invoke());
            Button(exits.transform, "over.themes", () => ThemesPressed?.Invoke());

            Button(card.transform, "over.retry", () => RetryPressed?.Invoke(),
                   UiBuilder.TouchTarget * 1.4f, primary: true);
        }

        /// <summary>
        /// A card, registered so that the theme gives it a surface, a shape and a dimmed screen
        /// behind it.
        /// </summary>
        VerticalLayoutGroup Card(string name, Transform parent, float anchorY, float inset)
        {
            var card = UiBuilder.CreateCard(name, parent, CardMargin, anchorY, inset);

            // The plate is paper, not a surface. Given the surface colour it would be the same
            // grey as the buttons standing on it, and a card is only a card while the controls
            // on it are something else. A trace of the frozen scene still shows through, which
            // is what spec 10.3 asks for.
            var plate = card.GetComponent<Image>();
            plate.color = new Color(1f, 1f, 1f, CardOpacity);
            Scrim(plate);
            TrackShape(plate, CardCornerHeight);
            return card;
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
            var carousel = UiBuilder.CreateCarousel("Carousel", column.transform, CardHeight + 20f,
                                                    CarouselSpacing);
            _carousel = carousel.gameObject.AddComponent<CarouselSnapping>();
            _carousel.CardWidth = CardWidth;
            _carousel.Spacing = CarouselSpacing;

            foreach (var theme in ThemePalette.All)
            {
                var captured = theme;
                var card = UiBuilder.CreateButton($"Theme{theme}", carousel.content,
                    string.Empty,
                    () => { Clicked?.Invoke(); ThemeChosen?.Invoke(captured); }, CardHeight);

                var cardRect = (RectTransform)card.transform;
                cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);

                // Shaped and surfaced like every other control. Tracked as ink the card fill
                // was the theme's text colour, which is why the cards were invisible plates
                // with a preview floating on them.
                TrackShape(card.targetGraphic as Image, CardCornerHeight);
                _presses.Add(Press(card));

                var palette = ThemePalette.For(theme);
                var rings = new Color[palette.RingColours.Length];
                for (int i = 0; i < rings.Length; i++)
                    rings[i] = ThemeApplier.ToColor(palette.RingColours[i]);

                // Left untracked on purpose: a preview shows its own theme's colours, not the
                // colours of whichever theme happens to be active.
                var preview = UiBuilder.CreateRingPreview("Preview", card.transform, PreviewSize,
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

            Track(Label(column.transform, "set.title", UiBuilder.TitleSize));

            // Three groups, separated by space rather than by headings. The volume levels are
            // gone: two sliders bought a finer answer to a question almost nobody asks, and
            // cost the rows that made the screen fit on a phone without scrolling. On or off
            // is the distinction a player on a bus actually wants (D-077).
            Toggle(column.transform, "set.sound", _settings.soundEnabled,
                v => { _settings.soundEnabled = v; SettingsChanged?.Invoke(); });
            Toggle(column.transform, "set.music", _settings.musicEnabled,
                v => { _settings.musicEnabled = v; SettingsChanged?.Invoke(); });
            Toggle(column.transform, "set.vibration", _settings.vibrationEnabled,
                v => { _settings.vibrationEnabled = v; SettingsChanged?.Invoke(); });

            UiBuilder.CreateSpacer(column.transform, GroupGap);

            // The one row with a continuous answer, laid out like the switches beside it:
            // name on the left, control on the right.
            Level(column.transform, "set.sensitivity",
                SaveData.MinSensitivity, SaveData.MaxSensitivity, _settings.sensitivity,
                v => { _settings.sensitivity = v; SettingsChanged?.Invoke(); });

            Toggle(column.transform, "set.invert", _settings.invertRotation,
                v => { _settings.invertRotation = v; SettingsChanged?.Invoke(); });

            UiBuilder.CreateSpacer(column.transform, GroupGap);

            Toggle(column.transform, "set.lowfx", _settings.reduceEffects,
                v => { _settings.reduceEffects = v; SettingsChanged?.Invoke(); });

            // A chip that says which language is on. Named one before, it was the only control
            // on the screen that showed a question and not its answer.
            _languageChip = Chip(column.transform, "set.language", CycleLanguage);

            // Filled the moment it exists. Refreshed only on a change, it was blank until the
            // player pressed it — the one state in which it says less than the label it replaced.
            RefreshLanguageChip();

            UiBuilder.CreateSpacer(column.transform, GroupGap);

            // Neither is a setting: one is a page to read and the other is a thing you do once
            // and regret twice. Demoted into a single row of plain words.
            var links = UiBuilder.CreateRow("Links", column.transform);
            Link(links.transform, "set.about", () => { ShowAbout(); AboutPressed?.Invoke(); });
            Link(links.transform, "set.reset", ShowResetConfirm);

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
            RefreshLanguageChip();
            SettingsChanged?.Invoke();
        }

        // ---------------------------------------------------------------- refresh

        /// <summary>
        /// Round, and no smaller than anything else you are asked to hit. The project's floor
        /// is 150 units and a round button has less area than a square one of the same box, so
        /// shaving it was the wrong place to save room.
        /// </summary>
        public const float PauseButtonSize = UiBuilder.TouchTarget;

        /// <summary>Distance from the safe area's edge to the button's edge.</summary>
        public const float PauseInset = 28f;

        /// <summary>Space that separates one group of settings from the next.</summary>
        const float GroupGap = 28f;

        /// <summary>Margin between a row's edge and what sits in it.</summary>
        const float RowInset = 32f;

        /// <summary>Width of the control at the end of a settings row.</summary>
        const float LevelWidth = 420f;

        const float ChipWidth = 320f;

        /// <summary>Side margin of a centred card, so the screen behind it is still a screen.</summary>
        const float CardMargin = 56f;

        /// <summary>How far the lowest control on a card sits above the safe area's edge.</summary>
        const float CardInset = 72f;

        /// <summary>
        /// The height a card's corner is figured from.
        ///
        /// Not the card's own height: a skin states its corner as a share, and half of an
        /// 800-unit card is a capsule. Taken from a button instead, the card's corners match
        /// the corners of the controls standing on it, which is the point of a skin.
        /// </summary>
        const float CardCornerHeight = UiBuilder.TouchTarget;

        /// <summary>A trace of the frozen scene still shows through the plate (spec 10.3).</summary>
        const float CardOpacity = 0.96f;

        /// <summary>
        /// A theme card, and the gap to the next one.
        ///
        /// Narrow enough that both neighbours peek past a centred card on the narrowest screen
        /// the game ships on — the peek is what says there are more than the one in front of you.
        /// </summary>
        const float CardWidth = 600f;

        const float CardHeight = 700f;
        const float CarouselSpacing = 32f;
        const float PreviewSize = 360f;

        Image _pauseFill;
        UnityEngine.UI.Outline _scoreOutline;
        UnityEngine.UI.Outline _bestOutline;

        /// <summary>Thick enough to survive a ring crossing a stroke of the digit.</summary>
        public const float ScoreOutlineWidth = 6f;
        public const float BestOutlineWidth = 4f;

        static UnityEngine.UI.Outline Outline(Graphic graphic, float width)
        {
            if (graphic == null) return null;

            var outline = graphic.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectDistance = new Vector2(width, width);
            outline.useGraphicAlpha = false;
            return outline;
        }

        Button _hardcoreButton;
        Text _hardcoreLockLabel;

        /// <summary>Best in the ordinary game that opens the hardcore mode. Plan [HV-05].</summary>
        public const int HardcoreUnlockBest = 30;

        /// <summary>
        /// Shows or greys the hardcore button.
        ///
        /// Greyed rather than hidden, and told why: a mode the player cannot see is a mode they
        /// never work toward, and "best 30 in the normal game" is a goal where a missing button
        /// is only an absence.
        /// </summary>
        void RefreshHardcore()
        {
            if (_hardcoreButton == null) return;

            bool open = _score != null && _score.Best >= HardcoreUnlockBest;
            _hardcoreButton.interactable = open;

            var label = _hardcoreButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                var colour = label.color;
                colour.a = open ? 1f : 0.45f;
                label.color = colour;
            }

            if (_hardcoreLockLabel == null) return;
            _hardcoreLockLabel.gameObject.SetActive(!open);
            if (!open)
                _hardcoreLockLabel.text = Strings.Format("hardcore.locked", HardcoreUnlockBest);
        }

        void RefreshMenu()
        {
            RefreshHardcore();
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

        /// <summary>
        /// Opens the carousel on the theme that is already on.
        ///
        /// Opening on the first card instead means the player has to find their own theme
        /// before they can see what they are changing from.
        /// </summary>
        void FocusChosenTheme()
        {
            if (_carousel == null || _settings == null) return;

            var all = ThemePalette.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i] == _settings.Theme) { _carousel.Focus(i, immediate: true); return; }
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

            // Painted here rather than through the role lists, because which card is which is
            // not a property of the theme being applied — it is a property of the one chosen.
            // Without it the six cards were identical plates and nothing said which was on.
            var surface = _palette != null ? ThemeApplier.ToColor(_palette.Surface) : Color.grey;
            var accent = _palette != null ? ThemeApplier.ToColor(_palette.Accent) : Color.white;
            var ink = _palette != null ? ThemeApplier.ToColor(_palette.Ui) : Color.black;
            var accentText = _palette != null ? ThemeApplier.ToColor(_palette.AccentText) : Color.white;

            foreach (var theme in ThemePalette.All)
            {
                if (!_themeButtons.TryGetValue(theme, out var button)) continue;

                bool unlocked = _score.IsUnlocked(theme);
                button.interactable = unlocked;

                bool chosen = _settings != null && _settings.Theme == theme;
                if (button.targetGraphic != null)
                    button.targetGraphic.color = chosen ? accent : surface;

                // A locked card reads as locked by its own weight. Greying only the fill leaves
                // a full-strength name on it, which is the one part the eye actually goes to.
                var label = _themeLabels[theme];
                label.text = Strings.ThemeName(theme);
                var colour = chosen ? accentText : ink;
                colour.a = unlocked ? 1f : 0.45f;
                label.color = colour;

                // The condition lives on its own line under the name rather than being glued to
                // it: on a card there is room, and "Earth · Best 50" read as part of the name.
                var padlock = _themeLocks[theme];
                padlock.text = unlocked
                    ? string.Empty
                    : Strings.Format("theme.locked", ScoreKeeper.RequiredBestFor(theme));
                var lockColour = chosen ? accentText : ink;
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

        /// <summary>
        /// A button. <paramref name="primary"/> marks the one action the screen exists for —
        /// Play, Retry — which takes the accent instead of the surface.
        /// </summary>
        Button Button(Transform parent, string key, Action onClick,
                      float height = UiBuilder.TouchTarget, bool primary = false)
        {
            var button = UiBuilder.CreateButton(key, parent, Strings.Get(key),
                () => { Clicked?.Invoke(); onClick?.Invoke(); }, height);
            var label = button.GetComponentInChildren<Text>();
            _stringRefreshers.Add(() => label.text = Strings.Get(key));

            TrackShape(button.targetGraphic as Image, Mathf.Max(height, UiBuilder.TouchTarget));

            _presses.Add(Press(button));

            if (primary)
            {
                TrackAccent(button.targetGraphic, label);
                return button;
            }

            TrackSurface(button.targetGraphic);
            Track(label);
            return button;
        }

        /// <summary>
        /// A named setting with a control at the end of the row.
        ///
        /// The shape every settings row shares: name on the left, the thing you touch on the
        /// right. A label centred above its own slider — which is what this was — reads as a
        /// heading for everything below it rather than as the name of one control.
        /// </summary>
        RectTransform Row(Transform parent, string key, float controlWidth)
        {
            var go = new GameObject(key, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, UiBuilder.TouchTarget);

            var label = UiBuilder.CreateLabel("Label", go.transform, Strings.Get(key),
                                              UiBuilder.BodySize, TextAnchor.MiddleLeft);
            var labelRect = (RectTransform)label.transform;
            UiBuilder.Stretch(labelRect);
            labelRect.offsetMin = new Vector2(RowInset, 0f);
            labelRect.offsetMax = new Vector2(-(controlWidth + RowInset * 2f), 0f);
            _stringRefreshers.Add(() => label.text = Strings.Get(key));
            Track(label);

            return rect;
        }

        /// <summary>Puts a control at the right-hand end of a row, vertically centred.</summary>
        static void PinRight(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-RowInset, 0f);
        }

        /// <summary>A named setting with a slider at the end of it.</summary>
        Slider Level(Transform parent, string key, float min, float max, float value,
                     Action<float> onChanged)
        {
            var row = Row(parent, key, LevelWidth);

            var slider = UiBuilder.CreateSlider("Slider", row, min, max, value, onChanged);

            // Widened by its own end margins and pushed out by the row's, so that the bar ends
            // where a switch's track ends. Pinned like any other control it stopped short of
            // them by exactly one margin, and the right-hand edge of the screen zig-zagged.
            var rect = (RectTransform)slider.transform;
            PinRight(rect, LevelWidth + UiBuilder.SliderEndInset * 2f, UiBuilder.TouchTarget);
            rect.anchoredPosition = new Vector2(-(RowInset - UiBuilder.SliderEndInset), 0f);

            RegisterSlider(slider);
            return slider;
        }

        /// <summary>
        /// A named setting whose value is a word, shown on the button that changes it.
        ///
        /// The label alone said "Language" and nothing else — the only control on the screen
        /// that showed its question and not its answer, on the one screen a player opens
        /// precisely because the answer is currently wrong.
        /// </summary>
        Button Chip(Transform parent, string key, Action onClick)
        {
            var row = Row(parent, key, ChipWidth);

            var chip = UiBuilder.CreateButton("Value", row, string.Empty,
                () => { Clicked?.Invoke(); onClick?.Invoke(); });
            PinRight((RectTransform)chip.transform, ChipWidth, UiBuilder.TouchTarget);

            TrackShape(chip.targetGraphic as Image, UiBuilder.TouchTarget);
            TrackSurface(chip.targetGraphic);
            Track(chip.GetComponentInChildren<Text>());
            _presses.Add(Press(chip));
            return chip;
        }

        /// <summary>
        /// A word you can press, with no button around it.
        ///
        /// For the two things on the settings screen that are not settings. They still carry a
        /// fill, because a transparent one is what the finger lands on; it simply is not drawn.
        /// </summary>
        Button Link(Transform parent, string key, Action onClick)
        {
            var button = UiBuilder.CreateButton(key, parent, Strings.Get(key),
                () => { Clicked?.Invoke(); onClick?.Invoke(); });

            if (button.targetGraphic is Image fill) fill.color = new Color(1f, 1f, 1f, 0f);

            var label = button.GetComponentInChildren<Text>();
            label.fontSize = UiBuilder.BodySize;
            _stringRefreshers.Add(() => label.text = Strings.Get(key));
            Track(label);
            _presses.Add(Press(button));
            return button;
        }

        Toggle Toggle(Transform parent, string key, bool value, Action<bool> onChanged)
        {
            var toggle = UiBuilder.CreateToggle(key, parent, Strings.Get(key), value, onChanged);
            var label = toggle.GetComponentInChildren<Text>();
            _stringRefreshers.Add(() => label.text = Strings.Get(key));
            Track(label);

            // The row's own backing stays faint — it is a hit area, not a control. What the eye
            // is meant to find is the switch at the end of it.
            var visual = toggle.GetComponent<ToggleVisual>();
            if (visual != null) _switches.Add(visual);
            return toggle;
        }

        /// <summary>
        /// Takes a slider apart into the roles its pieces play: a quiet track, a filled part
        /// in the accent, and a knob the finger is aiming for. Painted as one colour before,
        /// which is how the handle ended up a black slab taller than the line it rode on.
        /// </summary>
        void RegisterSlider(Slider slider)
        {
            if (slider == null) return;

            foreach (var part in slider.GetComponentsInChildren<Image>(true))
            {
                if (part.name == "Handle") { _knobs.Add(part); continue; }

                // Shaped like the track it sits in, or a rounded bar ends in a square stub.
                if (part.name == "Fill")
                {
                    _accents.Add(part);
                    TrackShape(part, UiBuilder.SliderTrackHeight);
                    continue;
                }

                _tracks.Add(part);
            }
        }

        /// <summary>Gives a button something to do under a finger.</summary>
        static PressFeedback Press(Button button)
        {
            var press = button.gameObject.AddComponent<PressFeedback>();
            press.Fill = button.targetGraphic;
            return press;
        }

        /// <summary>Text and anything else that should be the theme's ink.</summary>
        void Track(Graphic graphic) { if (graphic != null) _tinted.Add(graphic); }

        /// <summary>The fill of a control: opaque, and distinct from the screen behind it.</summary>
        void TrackSurface(Graphic graphic) { if (graphic != null) _surfaces.Add(graphic); }

        /// <summary>Registers a control whose corners follow the theme's skin.</summary>
        void TrackShape(Image image, float height)
        {
            if (image != null) _shaped.Add((image, height));
        }

        /// <summary>The one action a screen is for. Fill and label move together.</summary>
        void TrackAccent(Graphic fill, Graphic label)
        {
            if (fill != null) _accents.Add(fill);
            if (label != null) _accentTexts.Add(label);
        }

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
