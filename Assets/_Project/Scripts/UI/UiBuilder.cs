using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// Builds interface elements in code. The whole interface is generated, so it can be
    /// regenerated — a hand-edited scene drifts the moment two sessions touch it.
    ///
    /// Sizes are in reference-canvas units against a 1080×1920 design, which the scaler maps to
    /// the device. TouchTarget is the floor from CONTRACT.md §3: on a 1080-wide phone that is
    /// roughly the 48dp the guidelines ask for, and nothing interactive is built smaller.
    /// </summary>
    public static class UiBuilder
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        /// <summary>Smallest height anything the player taps may have.</summary>
        public const float TouchTarget = 150f;

        public const int TitleSize = 86;
        public const int ScoreSize = 160;
        public const int BodySize = 44;
        public const int ButtonSize = 52;

        static Font _font;
        static Sprite _disc;

        /// <summary>
        /// A filled circle, generated once.
        ///
        /// The interface ships no image files at all, so a round shape has to come from
        /// somewhere; drawing it is cheaper than importing it and cannot go missing. The edge is
        /// antialiased across one pixel, which is the difference between a ring preview and a
        /// staircase.
        /// </summary>
        public static Sprite Disc
        {
            get
            {
                if (_disc != null) return _disc;

                const int size = 128;
                const float radius = size * 0.5f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "HR.Disc",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };

                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float edge = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)(Mathf.Clamp01(edge) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _disc = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                _disc.name = "HR.Disc";
                return _disc;
            }
        }

        /// <summary>
        /// The built-in runtime font. It is a dynamic font, so on a device it falls back to the
        /// system family for glyphs it lacks — which is what gets Cyrillic rendered without
        /// shipping a font asset.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        /// <summary>
        /// Builds the canvas in camera space when given a camera.
        ///
        /// Not a detail: an overlay canvas draws straight to the display and cannot be rendered
        /// into a texture at all, so the interface would be absent from every captured frame
        /// while looking perfectly fine on screen. Camera space also means what is captured is
        /// what ships, rather than two arrangements that can drift apart.
        /// </summary>
        public static Canvas CreateCanvas(string name, Transform parent, Camera camera = null,
                                          int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            // Half and half: a taller phone and a wider tablet both keep the layout readable,
            // where matching width alone blows the type up on a 4:3 tablet.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>
        /// The object that turns a touch into a button press, created once and shared.
        ///
        /// A GraphicRaycaster raycasts for nobody unless an EventSystem is driving it, and a
        /// hierarchy built in code never gets one — the editor only adds it on your behalf when a
        /// UI element is dropped into a scene by hand, which is the one thing this project never
        /// does. Missing, every button, toggle, slider and scroll view in the game is inert on a
        /// device, while a suite that invokes onClick directly stays green throughout.
        ///
        /// The module is the Input System one because the game's input backend is that package;
        /// StandaloneInputModule reads the old Input Manager and would throw on every frame.
        /// </summary>
        public static EventSystem EnsureEventSystem(Transform parent = null)
        {
            // Unity keeps one current EventSystem and logs an error for every extra, so a second
            // interface — a test building its own — joins the existing one rather than fighting it.
            var existing = EventSystem.current != null
                ? EventSystem.current
                : UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return existing;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (parent != null) go.transform.SetParent(parent, false);
            return go.GetComponent<EventSystem>();
        }

        /// <summary>A full-bleed child that stays inside the safe area.</summary>
        public static RectTransform CreateSafeArea(Transform parent)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform CreatePanel(string name, Transform parent, Color? background = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            Stretch(rect);

            if (background.HasValue)
            {
                var image = go.AddComponent<Image>();
                image.color = background.Value;
                image.raycastTarget = true;
            }

            return rect;
        }

        public static Text CreateLabel(string name, Transform parent, string text, int size,
                                       TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.alignment = anchor;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            // A label is as tall as its type. Left at zero a label occupies no space in a layout
            // group while still drawing — so a 160-point score and the record under it were
            // given the same nothing, and only the 28 units of spacing kept them apart. Callers
            // that position a label by hand overwrite this immediately and are unaffected.
            ((RectTransform)go.transform).sizeDelta = new Vector2(0f, Mathf.Ceil(size * 1.45f));

            return label;
        }

        public static Button CreateButton(string name, Transform parent, string text, Action onClick,
                                          float height = TouchTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, Mathf.Max(height, TouchTarget));

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);

            var label = CreateLabel("Label", go.transform, text, ButtonSize);
            Stretch((RectTransform)label.transform);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            return button;
        }

        /// <summary>A labelled on/off row. The whole row is the tap target, not just the box.</summary>
        public static Toggle CreateToggle(string name, Transform parent, string text, bool value,
                                          Action<bool> onChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, TouchTarget);

            var background = go.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.05f);

            var label = CreateLabel("Label", go.transform, text, BodySize, TextAnchor.MiddleLeft);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(32f, 0f);
            labelRect.offsetMax = new Vector2(-180f, 0f);

            var markGo = new GameObject("Mark", typeof(RectTransform), typeof(Image));
            markGo.transform.SetParent(go.transform, false);
            var markRect = (RectTransform)markGo.transform;
            markRect.anchorMin = new Vector2(1f, 0.5f);
            markRect.anchorMax = new Vector2(1f, 0.5f);
            markRect.pivot = new Vector2(1f, 0.5f);
            markRect.anchoredPosition = new Vector2(-32f, 0f);
            markRect.sizeDelta = new Vector2(72f, 72f);

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = markGo.GetComponent<Image>();
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));

            return toggle;
        }

        public static Slider CreateSlider(string name, Transform parent, float min, float max,
                                          float value, Action<float> onChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, TouchTarget);

            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(go.transform, false);
            var track = (RectTransform)trackGo.transform;
            Stretch(track);
            track.offsetMin = new Vector2(32f, TouchTarget * 0.42f);
            track.offsetMax = new Vector2(-32f, -TouchTarget * 0.42f);
            trackGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            Stretch(handleAreaRect);
            float inset = (TouchTarget - 72f) * 0.5f;
            handleAreaRect.offsetMin = new Vector2(32f, inset);
            handleAreaRect.offsetMax = new Vector2(-32f, -inset);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            var handle = (RectTransform)handleGo.transform;
            handle.sizeDelta = new Vector2(72f, 72f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = null;
            slider.handleRect = handle;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));

            return slider;
        }

        /// <summary>Stacks children down the panel with even spacing and a margin.</summary>
        public static VerticalLayoutGroup CreateColumn(string name, Transform parent, float spacing = 28f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(64, 64, 64, 64);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return layout;
        }

        /// <summary>
        /// A column that scrolls when it is taller than the screen it is on.
        ///
        /// The settings screen is the case that forces it: eleven rows at the 150-unit tap floor
        /// come to roughly 1790 units, which fits a tall phone and runs off the bottom of a 4:3
        /// tablet, where the scaler gives the canvas only ~1660. Shrinking the rows is not an
        /// option — the floor is the floor — so the content moves instead.
        /// </summary>
        public static VerticalLayoutGroup CreateScrollColumn(string name, Transform parent,
                                                             float spacing = 28f)
        {
            var viewGo = new GameObject(name, typeof(RectTransform), typeof(ScrollRect),
                                        typeof(RectMask2D), typeof(ScrollCentering));
            viewGo.transform.SetParent(parent, false);
            var viewRect = (RectTransform)viewGo.transform;
            Stretch(viewRect);

            var contentGo = new GameObject("Content", typeof(RectTransform),
                                           typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewGo.transform, false);
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(64, 64, 64, 64);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            contentGo.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            return layout;
        }

        /// <summary>
        /// Puts a backing plate behind a column that hugs its content.
        ///
        /// Spec 10.3 wants the frozen scene showing through at forty per cent, and it does — but
        /// a dimmed tunnel is still a black arc, and a black arc behind dark text is unreadable.
        /// Dimming harder would hide the scene the spec asks to keep, so the plate goes behind
        /// the words only.
        /// </summary>
        public static Image AddCard(VerticalLayoutGroup column, float alpha)
        {
            // On the column's own object, so it renders behind the rows and is sized by the
            // fitter that already measures them.
            var image = column.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A row of cards that scrolls sideways — spec 10.4 asks for a carousel, not a list.
        /// </summary>
        public static ScrollRect CreateCarousel(string name, Transform parent, float height,
                                                float spacing = 24f)
        {
            var viewGo = new GameObject(name, typeof(RectTransform), typeof(ScrollRect),
                                        typeof(RectMask2D));
            viewGo.transform.SetParent(parent, false);
            var viewRect = (RectTransform)viewGo.transform;
            viewRect.sizeDelta = new Vector2(0f, height);

            var contentGo = new GameObject("Content", typeof(RectTransform),
                                           typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewGo.transform, false);
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(48, 48, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            contentGo.GetComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewRect;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            return scroll;
        }

        /// <summary>
        /// Concentric discs in a theme's own colours — the preview on a theme card.
        ///
        /// Deliberately not a live miniature tunnel: that is one more camera and one more mesh
        /// per card, running while the player is only choosing a colour scheme. What the player
        /// is actually comparing is the palette, and the palette is what this shows.
        /// </summary>
        public static RectTransform CreateRingPreview(string name, Transform parent, float size,
                                                      Color background, Color[] rings, Color ball)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size, size);

            // Drawn largest first, alternating ring and background, so each ring reads as a band
            // rather than a disc. Filled circles are cheaper than annuli and look the same here.
            int bands = rings.Length * 2 + 1;
            for (int i = 0; i < bands; i++)
            {
                float scale = 1f - i / (float)bands;
                var disc = new GameObject($"Band{i}", typeof(RectTransform), typeof(Image));
                disc.transform.SetParent(go.transform, false);

                var discRect = (RectTransform)disc.transform;
                discRect.sizeDelta = new Vector2(size * scale, size * scale);

                var image = disc.GetComponent<Image>();
                image.sprite = Disc;
                image.raycastTarget = false;
                image.color = i % 2 == 0 ? background : rings[Mathf.Min(i / 2, rings.Length - 1)];
            }

            var centre = new GameObject("Ball", typeof(RectTransform), typeof(Image));
            centre.transform.SetParent(go.transform, false);
            ((RectTransform)centre.transform).sizeDelta = new Vector2(size * 0.14f, size * 0.14f);
            var ballImage = centre.GetComponent<Image>();
            ballImage.sprite = Disc;
            ballImage.color = ball;
            ballImage.raycastTarget = false;

            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Pins a rect to the top of its parent with a given height.</summary>
        public static void PinTop(RectTransform rect, float height, float inset = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height - inset);
            rect.offsetMax = new Vector2(0f, -inset);
        }
    }
}
