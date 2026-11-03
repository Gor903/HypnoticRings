using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HR.UI
{
    public static class UiBuilder
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        public const float TouchTarget = 150f;

        public const int TitleSize = 86;
        public const int ScoreSize = 160;
        public const int BodySize = 44;
        public const int ButtonSize = 52;

        public const float TrackWidth = 132f;
        public const float TrackHeight = 72f;

        public const float KnobInset = 8f;

        public const float SliderEndInset = 32f;

        public const float SliderTrackHeight = 36f;

        public const float SliderKnobSize = 60f;

        static Font _font;
        static Sprite _disc;

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

        static Sprite _rounded;

        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;

                const int size = 128;
                const float radius = size * 0.5f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "HR.Rounded",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };

                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float corner = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)(Mathf.Clamp01(corner) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _rounded = Sprite.Create(texture, new Rect(0, 0, size, size),
                                         new Vector2(0.5f, 0.5f), pixelsPerUnit: 100f, extrude: 0,
                                         meshType: SpriteMeshType.FullRect,
                                         border: new Vector4(radius, radius, radius, radius));
                _rounded.name = "HR.Rounded";
                return _rounded;
            }
        }

        public const float RoundedBorder = 64f;

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

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static EventSystem EnsureEventSystem(Transform parent = null)
        {
            var existing = EventSystem.current != null
                ? EventSystem.current
                : UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return existing;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (parent != null) go.transform.SetParent(parent, false);
            return go.GetComponent<EventSystem>();
        }

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
            image.color = Color.white;

            var label = CreateLabel("Label", go.transform, text, ButtonSize);
            Stretch((RectTransform)label.transform);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            return button;
        }

        public static Toggle CreateToggle(string name, Transform parent, string text, bool value,
                                          Action<bool> onChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, TouchTarget);

            var background = go.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0f);

            var label = CreateLabel("Label", go.transform, text, BodySize, TextAnchor.MiddleLeft);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(32f, 0f);
            labelRect.offsetMax = new Vector2(-180f, 0f);

            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(go.transform, false);
            var trackRect = (RectTransform)trackGo.transform;
            trackRect.anchorMin = new Vector2(1f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.anchoredPosition = new Vector2(-32f, 0f);
            trackRect.sizeDelta = new Vector2(TrackWidth, TrackHeight);

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(Image));
            knobGo.transform.SetParent(trackGo.transform, false);
            var knobRect = (RectTransform)knobGo.transform;
            knobRect.sizeDelta = new Vector2(TrackHeight - KnobInset * 2f,
                                             TrackHeight - KnobInset * 2f);

            var visual = go.AddComponent<ToggleVisual>();
            visual.Track = trackGo.GetComponent<Image>();
            visual.Knob = knobGo.GetComponent<Image>();

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = background;

            toggle.graphic = null;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(visual.Apply);
            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));
            visual.Apply(value);

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
            track.offsetMin = new Vector2(SliderEndInset, (TouchTarget - SliderTrackHeight) * 0.5f);
            track.offsetMax = new Vector2(-SliderEndInset, -(TouchTarget - SliderTrackHeight) * 0.5f);
            trackGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
            trackGo.name = "Track";

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRect = (RectTransform)fillArea.transform;
            Stretch(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(SliderEndInset, (TouchTarget - SliderTrackHeight) * 0.5f);
            fillAreaRect.offsetMax = new Vector2(-SliderEndInset, -(TouchTarget - SliderTrackHeight) * 0.5f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            Stretch(fillRect);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            Stretch(handleAreaRect);
            float inset = (TouchTarget - SliderKnobSize) * 0.5f;
            handleAreaRect.offsetMin = new Vector2(SliderEndInset + SliderKnobSize * 0.5f, inset);
            handleAreaRect.offsetMax = new Vector2(-(SliderEndInset + SliderKnobSize * 0.5f), -inset);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            var handle = (RectTransform)handleGo.transform;
            handle.sizeDelta = new Vector2(SliderKnobSize, SliderKnobSize);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handle;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));

            return slider;
        }

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

        public static Image AddCard(VerticalLayoutGroup column, float alpha)
        {
            var image = column.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            return image;
        }

        public static HorizontalLayoutGroup CreateRow(string name, Transform parent,
                                                      float height = TouchTarget,
                                                      float spacing = 24f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(0f, height);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            return layout;
        }

        public static VerticalLayoutGroup CreateCard(string name, Transform parent,
                                                     float sideMargin, float anchorY = 0.5f,
                                                     float inset = 0f, float spacing = 24f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(1f, anchorY);
            rect.pivot = new Vector2(0.5f, anchorY);
            rect.offsetMin = new Vector2(sideMargin, 0f);
            rect.offsetMax = new Vector2(-sideMargin, 0f);
            rect.anchoredPosition = new Vector2(0f, inset);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(40, 40, 48, 48);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.GetComponent<Image>().color = Color.white;

            return layout;
        }

        public static RectTransform CreateSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, height);
            return rect;
        }

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

        public static RectTransform CreateRingPreview(string name, Transform parent, float size,
                                                      Color background, Color[] rings, Color ball)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size, size);

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
