using System;
using System.Collections.Generic;
using HR.Core;
using HR.Themes;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    public sealed class GameUi : MonoBehaviour
    {
        public event Action PlayPressed;

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

        public event Action Clicked;

        readonly Dictionary<GameState, RectTransform> _screens = new Dictionary<GameState, RectTransform>();
        readonly List<Action> _stringRefreshers = new List<Action>();

        readonly List<Graphic> _tinted = new List<Graphic>();
        readonly List<Graphic> _surfaces = new List<Graphic>();
        readonly List<Graphic> _accents = new List<Graphic>();
        readonly List<Graphic> _accentTexts = new List<Graphic>();
        readonly List<Image> _scrims = new List<Image>();

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
        UiShimmer _shimmer;

        public UiShimmer Shimmer => _shimmer;

        ThemePalette _palette;
        RectTransform _confirmPanel;
        RectTransform _aboutPanel;

        Canvas _canvas;
        SafeAreaFitter _safeArea;

        public Canvas Canvas => _canvas;

        public Text ScoreLabel => _scoreLabel;

        public GameState Current { get; private set; } = GameState.Boot;

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

            float factor = ScaleFactorFor(width, height);
            float units = height / factor;

            var rect = (RectTransform)_canvas.transform;
            rect.sizeDelta = new Vector2(width / factor, units);

            float unit = 2f * camera.orthographicSize / units;
            rect.localScale = new Vector3(unit, unit, unit);
            rect.rotation = camera.transform.rotation;
            rect.position = camera.transform.position + camera.transform.forward;

            var size = rect.sizeDelta;
            var area = safeArea ?? new Rect(0f, 0f, size.x, size.y);
            _safeArea?.Pin(area, new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y)));

            Canvas.ForceUpdateCanvases();
        }

        public void Initialise(SaveData settings, ScoreKeeper score, Camera camera = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _score = score ?? throw new ArgumentNullException(nameof(score));

            UiBuilder.EnsureEventSystem(transform);

            _canvas = UiBuilder.CreateCanvas("UI", transform, camera);
            _shimmer = gameObject.AddComponent<UiShimmer>();
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

        public void Show(GameState state)
        {
            Current = state;

            var wanted = state == GameState.Settings ? GameState.Settings : state;

            foreach (var pair in _screens)
                pair.Value.gameObject.SetActive(pair.Key == wanted);

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

        public void ApplyTheme(ThemePalette palette)
        {
            if (palette == null) return;
            _palette = palette;

            if (_shimmer != null) _shimmer.Rest();
            var ink = new Color(palette.Ui.R / 255f, palette.Ui.G / 255f, palette.Ui.B / 255f, 1f);
            var paper = new Color(palette.Background.R / 255f, palette.Background.G / 255f,
                                  palette.Background.B / 255f, 1f);

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

            Paint(_surfaces, ThemeApplier.ToColor(palette.Surface));
            Paint(_accents, ThemeApplier.ToColor(palette.Accent));
            Paint(_accentTexts, ThemeApplier.ToColor(palette.AccentText));

            var skin = UiSkin.For(palette.Theme);

            bool effects = _settings == null || !_settings.reduceEffects;
            var edge = ThemeApplier.ToColor(palette.Outline);
            var halo = ThemeApplier.ToColor(palette.Accent);

            foreach (var (image, height) in _shaped)
            {
                UiPainter.Shape(image, height, skin);
                double glow = effects && _accents.Contains(image) ? palette.Glow : 0.0;
                UiPainter.Edge(image, skin, edge, glow, halo, effects);
            }

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

            var rippleInk = ThemeApplier.ToColor(palette.Ui);
            foreach (var press in _presses)
            {
                if (press == null) continue;
                press.Configure(skin, press.Fill, rippleInk);
            }

            if (_pauseFill != null)
            {
                _pauseFill.sprite = UiBuilder.Disc;
                _pauseFill.type = Image.Type.Simple;
            }

            if (_scoreOutline != null) _scoreOutline.effectColor = paper;
            if (_bestOutline != null) _bestOutline.effectColor = paper;

            RefreshThemes();

            if (_shimmer != null)
                _shimmer.Configure(effects ? palette.ShimmerSeconds : 0.0, palette.ShimmerDepth,
                                   paper, _accents);
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

        void BuildMenu(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Menu", parent, new Color(1f, 1f, 1f, 0.88f));
            _screens[GameState.Menu] = screen;
            Scrim(screen);

            var column = UiBuilder.CreateColumn("Column", screen);

            var title = Label(column.transform, "app.title", UiBuilder.TitleSize);
            _menuBestLabel = UiBuilder.CreateLabel("Best", column.transform, string.Empty, UiBuilder.BodySize);
            Track(_menuBestLabel);

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

            _hudBestLabel.color = new Color(1f, 1f, 1f, 0.6f);
            Track(_hudBestLabel);

            _scoreOutline = Outline(_scoreLabel, ScoreOutlineWidth);
            _bestOutline = Outline(_hudBestLabel, BestOutlineWidth);

            var pause = UiBuilder.CreateButton("Pause", screen, "II", () => PausePressed?.Invoke());
            _pauseButton = pause;

            TrackSurface(pause.targetGraphic);
            Track(pause.GetComponentInChildren<Text>());
            _presses.Add(Press(pause));
            var pauseRect = (RectTransform)pause.transform;
            pauseRect.anchorMin = new Vector2(1f, 1f);
            pauseRect.anchorMax = new Vector2(1f, 1f);

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

            var card = Card("Card", screen, anchorY: 0f, inset: CardInset);
            Track(Label(card.transform, "pause.title", UiBuilder.TitleSize));
            Button(card.transform, "pause.home", () => HomePressed?.Invoke());
            Button(card.transform, "pause.resume", () => ResumePressed?.Invoke(),
                   UiBuilder.TouchTarget * 1.4f, primary: true);
        }

        void BuildGameOver(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("GameOver", parent, new Color(0f, 0f, 0f, 0.4f));
            _screens[GameState.GameOver] = screen;

            var card = Card("Card", screen, anchorY: 0f, inset: CardInset);

            _overScoreLabel = UiBuilder.CreateLabel("Score", card.transform, "0", UiBuilder.ScoreSize);
            _overBestLabel = UiBuilder.CreateLabel("Best", card.transform, string.Empty, UiBuilder.BodySize);
            _overUnlockLabel = UiBuilder.CreateLabel("Unlocked", card.transform, string.Empty, UiBuilder.BodySize);
            Track(_overScoreLabel); Track(_overBestLabel); Track(_overUnlockLabel);

            var exits = UiBuilder.CreateRow("Exits", card.transform);
            Button(exits.transform, "over.home", () => HomePressed?.Invoke());
            Button(exits.transform, "over.themes", () => ThemesPressed?.Invoke());

            Button(card.transform, "over.retry", () => RetryPressed?.Invoke(),
                   UiBuilder.TouchTarget * 1.4f, primary: true);
        }

        VerticalLayoutGroup Card(string name, Transform parent, float anchorY, float inset)
        {
            var card = UiBuilder.CreateCard(name, parent, CardMargin, anchorY, inset);

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

                TrackShape(card.targetGraphic as Image, CardCornerHeight);
                _presses.Add(Press(card));

                var palette = ThemePalette.For(theme);
                var rings = new Color[palette.RingColours.Length];
                for (int i = 0; i < rings.Length; i++)
                    rings[i] = ThemeApplier.ToColor(palette.RingColours[i]);

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

            RefreshThemes();
        }

        void BuildSettings(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Settings", parent, new Color(1f, 1f, 1f, 0.95f));
            _screens[GameState.Settings] = screen;
            Scrim(screen);

            var column = UiBuilder.CreateScrollColumn("Column", screen, 16f);

            Track(Label(column.transform, "set.title", UiBuilder.TitleSize));

            Toggle(column.transform, "set.sound", _settings.soundEnabled,
                v => { _settings.soundEnabled = v; SettingsChanged?.Invoke(); });
            Toggle(column.transform, "set.music", _settings.musicEnabled,
                v => { _settings.musicEnabled = v; SettingsChanged?.Invoke(); });
            Toggle(column.transform, "set.vibration", _settings.vibrationEnabled,
                v => { _settings.vibrationEnabled = v; SettingsChanged?.Invoke(); });

            UiBuilder.CreateSpacer(column.transform, GroupGap);

            Level(column.transform, "set.sensitivity",
                SaveData.MinSensitivity, SaveData.MaxSensitivity, _settings.sensitivity,
                v => { _settings.sensitivity = v; SettingsChanged?.Invoke(); });

            Toggle(column.transform, "set.invert", _settings.invertRotation,
                v => { _settings.invertRotation = v; SettingsChanged?.Invoke(); });

            UiBuilder.CreateSpacer(column.transform, GroupGap);

            Toggle(column.transform, "set.lowfx", _settings.reduceEffects,
                v => { _settings.reduceEffects = v; SettingsChanged?.Invoke(); });

            _languageChip = Chip(column.transform, "set.language", CycleLanguage);

            RefreshLanguageChip();

            UiBuilder.CreateSpacer(column.transform, GroupGap);

            var links = UiBuilder.CreateRow("Links", column.transform);
            Link(links.transform, "set.about", () => { ShowAbout(); AboutPressed?.Invoke(); });
            Link(links.transform, "set.reset", ShowResetConfirm);

            Button(column.transform, "common.back", () => BackPressed?.Invoke());

            BuildResetConfirm(screen);
            BuildAbout(screen);
        }

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

            var version = UiBuilder.CreateLabel("Version", column.transform,
                Application.version, UiBuilder.BodySize);
            Track(version);
            _stringRefreshers.Add(() => version.text = Application.version);

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

        public const float PauseButtonSize = UiBuilder.TouchTarget;

        public const float PauseInset = 28f;

        const float GroupGap = 28f;

        const float RowInset = 32f;

        const float LevelWidth = 420f;

        const float ChipWidth = 320f;

        const float CardMargin = 56f;

        const float CardInset = 72f;

        const float CardCornerHeight = UiBuilder.TouchTarget;

        const float CardOpacity = 0.96f;

        const float CardWidth = 600f;

        const float CardHeight = 700f;
        const float CarouselSpacing = 32f;
        const float PreviewSize = 360f;

        Image _pauseFill;
        UnityEngine.UI.Outline _scoreOutline;
        UnityEngine.UI.Outline _bestOutline;

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

        public const int HardcoreUnlockBest = 30;

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

                var label = _themeLabels[theme];
                label.text = Strings.ThemeName(theme);
                var colour = chosen ? accentText : ink;
                colour.a = unlocked ? 1f : 0.45f;
                label.color = colour;

                var padlock = _themeLocks[theme];
                padlock.text = unlocked
                    ? string.Empty
                    : Strings.Format("theme.locked", ScoreKeeper.RequiredBestFor(theme));
                var lockColour = chosen ? accentText : ink;
                lockColour.a = unlocked ? 0f : 0.7f;
                padlock.color = lockColour;
            }
        }

        Text Label(Transform parent, string key, int size)
        {
            var label = UiBuilder.CreateLabel(key, parent, Strings.Get(key), size);
            _stringRefreshers.Add(() => label.text = Strings.Get(key));
            return label;
        }

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

        static void PinRight(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-RowInset, 0f);
        }

        Slider Level(Transform parent, string key, float min, float max, float value,
                     Action<float> onChanged)
        {
            var row = Row(parent, key, LevelWidth);

            var slider = UiBuilder.CreateSlider("Slider", row, min, max, value, onChanged);

            var rect = (RectTransform)slider.transform;
            PinRight(rect, LevelWidth + UiBuilder.SliderEndInset * 2f, UiBuilder.TouchTarget);
            rect.anchoredPosition = new Vector2(-(RowInset - UiBuilder.SliderEndInset), 0f);

            RegisterSlider(slider);
            return slider;
        }

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

            var visual = toggle.GetComponent<ToggleVisual>();
            if (visual != null) _switches.Add(visual);
            return toggle;
        }

        void RegisterSlider(Slider slider)
        {
            if (slider == null) return;

            foreach (var part in slider.GetComponentsInChildren<Image>(true))
            {
                if (part.name == "Handle") { _knobs.Add(part); continue; }

                if (part.name == "Fill")
                {
                    _accents.Add(part);
                    TrackShape(part, UiBuilder.SliderTrackHeight);
                    continue;
                }

                _tracks.Add(part);
            }
        }

        static PressFeedback Press(Button button)
        {
            var press = button.gameObject.AddComponent<PressFeedback>();
            press.Fill = button.targetGraphic;
            return press;
        }

        void Track(Graphic graphic) { if (graphic != null) _tinted.Add(graphic); }

        void TrackSurface(Graphic graphic) { if (graphic != null) _surfaces.Add(graphic); }

        void TrackShape(Image image, float height)
        {
            if (image != null) _shaped.Add((image, height));
        }

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

        public IEnumerable<RectTransform> InteractiveElements()
        {
            foreach (var selectable in GetComponentsInChildren<Selectable>(includeInactive: true))
                yield return (RectTransform)selectable.transform;
        }

        public RectTransform ScreenFor(GameState state) =>
            _screens.TryGetValue(state, out var screen) ? screen : null;
    }
}
