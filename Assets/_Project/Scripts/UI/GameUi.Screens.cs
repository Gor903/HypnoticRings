using System;
using HR.Core;
using HR.Themes;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    public sealed partial class GameUi
    {
        void BuildMenu(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Menu", parent, new Color(1f, 1f, 1f, 0.88f));
            _screens[GameState.Menu] = screen;
            Scrim(screen);

            var column = UiBuilder.CreateColumn("Column", screen);

            var title = Label(column.transform, "app.title", UiBuilder.TitleSize, FontRole.Display);
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

            _scoreLabel = UiBuilder.CreateLabel("Score", screen, "0", UiBuilder.ScoreSize,
                                                role: FontRole.Display);
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

            _hintLabel = UiBuilder.CreateLabel("Hint", screen, Strings.Get(SwipeHintKey), UiBuilder.BodySize);
            var hintRect = (RectTransform)_hintLabel.transform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.offsetMin = new Vector2(0f, 180f);
            hintRect.offsetMax = new Vector2(0f, 260f);
            _stringRefreshers.Add(() => _hintLabel.text = Strings.Get(_hintKey));
            Track(_hintLabel);

            _countdownLabel = UiBuilder.CreateLabel("Countdown", screen, string.Empty,
                                                    UiBuilder.CountdownSize, role: FontRole.Display);
            var digitRect = (RectTransform)_countdownLabel.transform;
            digitRect.anchorMin = new Vector2(0.5f, 1f);
            digitRect.anchorMax = new Vector2(0.5f, 1f);
            digitRect.pivot = new Vector2(0.5f, 0.5f);
            digitRect.sizeDelta = new Vector2(UiBuilder.ReferenceWidth, CountdownBand);
            digitRect.anchoredPosition = new Vector2(0f, -TunnelCentreY);
            Track(_countdownLabel);
            Outline(_countdownLabel, 6f);
            _countdownLabel.gameObject.SetActive(false);
        }

        public const float TunnelCentreY = 538f;

        public static readonly float CountdownBand =
            Mathf.Ceil(UiBuilder.CountdownSize * 1.45f);

        void BuildPause(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Pause", parent, Color.white);
            _screens[GameState.Paused] = screen;
            Scrim(screen.GetComponent<Image>());

            _pauseScoreLabel = UiBuilder.CreateLabel("Score", screen, "0", UiBuilder.ScoreSize,
                                                     role: FontRole.Display);
            UiBuilder.PinTop((RectTransform)_pauseScoreLabel.transform,
                             (float)AdLayout.AdScoreBand, (float)AdLayout.TopInset);
            Track(_pauseScoreLabel);

            _pauseMrecLabel = AdLabel("MrecLabel", screen);

            _pauseCard = Card("Card", screen, anchorY: 0f, inset: CardInset);
            _pauseCard.padding = new RectOffset(40, 40, 24, 24);
            Track(Label(_pauseCard.transform, "pause.title", UiBuilder.TitleSize, FontRole.Display));
            Button(_pauseCard.transform, "pause.home", () => HomePressed?.Invoke());
            Button(_pauseCard.transform, "pause.resume", () => ResumePressed?.Invoke(),
                   UiBuilder.TouchTarget * 1.4f, primary: true);

            _pauseBannerLabel = AdLabel("BannerLabel", screen);
        }

        Text AdLabel(string name, Transform parent)
        {
            var label = UiBuilder.CreateLabel(name, parent, Strings.Get(AdLabelKey),
                                              (int)AdLayout.AdLabelSize, TextAnchor.MiddleLeft);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, (float)AdLayout.AdLabelBand);
            rect.offsetMin = new Vector2(AdSideInset, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-AdSideInset, rect.offsetMax.y);

            _stringRefreshers.Add(() => label.text = Strings.Get(AdLabelKey));
            Track(label);
            label.gameObject.SetActive(false);
            return label;
        }

        public const string AdLabelKey = "ad.label";

        public const float AdSideInset = 64f;

        void BuildGameOver(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("GameOver", parent, Color.white);
            _screens[GameState.GameOver] = screen;
            Scrim(screen.GetComponent<Image>());

            _overScoreLabel = UiBuilder.CreateLabel("Score", screen, "0", UiBuilder.ScoreSize,
                                                    role: FontRole.Display);
            UiBuilder.PinTop((RectTransform)_overScoreLabel.transform,
                             (float)AdLayout.AdScoreBand, (float)AdLayout.TopInset);
            Track(_overScoreLabel);

            _overBestLabel = UiBuilder.CreateLabel("Best", screen, string.Empty, UiBuilder.BodySize);
            UiBuilder.PinTop((RectTransform)_overBestLabel.transform, (float)AdLayout.RecordBand,
                             (float)(AdLayout.TopInset + AdLayout.AdScoreBand + AdLayout.RecordGap));
            Track(_overBestLabel);

            _overMrecLabel = AdLabel("MrecLabel", screen);

            _overCard = Card("Card", screen, anchorY: 0f, inset: CardInset);
            _overCard.padding = new RectOffset(40, 40, 8, 8);
            _overCard.spacing = 16f;

            _overRewardButton = Button(_overCard.transform, "over.continue",
                                       () => RewardPressed?.Invoke());
            _overRewardButton.gameObject.SetActive(false);

            _overUnlockLabel = UiBuilder.CreateLabel("Unlocked", _overCard.transform, string.Empty,
                                                     UiBuilder.BodySize);
            Track(_overUnlockLabel);

            var exits = UiBuilder.CreateRow("Exits", _overCard.transform, spacing: 16f);
            Button(exits.transform, "over.home", () => HomePressed?.Invoke());
            Button(exits.transform, "over.themes", () => ThemesPressed?.Invoke());

            Button(_overCard.transform, "over.retry", () => RetryPressed?.Invoke(),
                   UiBuilder.TouchTarget, primary: true);

            _overBannerLabel = AdLabel("BannerLabel", screen);
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

            var title = Label(column.transform, "themes.title", UiBuilder.TitleSize, FontRole.Display);
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

                TrackShadow(card.targetGraphic as Image, CardCornerHeight);
                _presses.Add(Press(card));

                var palette = ThemePalette.For(theme);
                var rings = RingColoursOf(palette);

                bool split = theme == Theme.MinimalLight;
                var preview = split
                    ? SplitPreview(card.transform, palette, rings)
                    : UiBuilder.CreateRingPreview("Preview", card.transform, PreviewSize,
                        ThemeApplier.ToColor(palette.Background), rings,
                        ThemeApplier.ToColor(palette.Ball));

                if (split) BuildVariantHalves(card);

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

        static Color[] RingColoursOf(ThemePalette palette)
        {
            var rings = new Color[palette.RingColours.Length];
            for (int i = 0; i < rings.Length; i++)
                rings[i] = ThemeApplier.ToColor(palette.RingColours[i]);

            return rings;
        }

        static RectTransform SplitPreview(Transform parent, ThemePalette light, Color[] lightRings)
        {
            var dark = ThemePalette.For(Theme.MinimalDark);

            return UiBuilder.CreateSplitRingPreview("Preview", parent, PreviewSize,
                ThemeApplier.ToColor(light.Background), lightRings,
                ThemeApplier.ToColor(light.Ball),
                ThemeApplier.ToColor(dark.Background), RingColoursOf(dark),
                ThemeApplier.ToColor(dark.Ball));
        }

        void BuildVariantHalves(Button card)
        {
            Half("PickLight", 0f, 0.5f, Theme.MinimalLight);
            Half("PickDark", 0.5f, 1f, Theme.MinimalDark);

            void Half(string name, float from, float to, Theme variant)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                                        typeof(VariantPicker));
                go.transform.SetParent(card.transform, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(from, 0f);
                rect.anchorMax = new Vector2(to, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var image = go.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);

                var picker = go.GetComponent<VariantPicker>();
                picker.Picked += () =>
                {
                    Clicked?.Invoke();
                    ThemeChosen?.Invoke(variant);
                };

                _variantPickers.Add(picker);
            }
        }

        void BuildSettings(Transform parent)
        {
            var screen = UiBuilder.CreatePanel("Settings", parent, new Color(1f, 1f, 1f, 0.95f));
            _screens[GameState.Settings] = screen;
            Scrim(screen);

            var column = UiBuilder.CreateScrollColumn("Column", screen, 16f);

            Track(Label(column.transform, "set.title", UiBuilder.TitleSize, FontRole.Display));

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

            _privacyLink = Link(column.transform, "set.privacy", () => PrivacyPressed?.Invoke());
            _privacyLink.gameObject.SetActive(false);

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
            Track(Label(column.transform, "app.title", UiBuilder.TitleSize, FontRole.Display));

            var version = UiBuilder.CreateLabel("Version", column.transform,
                Application.version, UiBuilder.BodySize);
            Track(version);
            _stringRefreshers.Add(() => version.text = Application.version);

            var body = Label(column.transform, "about.body", UiBuilder.BodySize, wrap: true);
            ((RectTransform)body.transform).sizeDelta = new Vector2(0f, 340f);
            Track(body);
            Track(Label(column.transform, "about.privacy", UiBuilder.BodySize));
            Track(Label(column.transform, "about.fonts", UiBuilder.BodySize));

            Button(column.transform, "common.back", () => _aboutPanel.gameObject.SetActive(false));

            _aboutPanel.gameObject.SetActive(false);
        }
    }
}
