using HR.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HR.UI
{
    public sealed class ToggleVisual : MonoBehaviour
    {
        public Image Track;
        public Image Knob;

        Color _off = Color.grey;
        Color _on = Color.white;
        Color _knob = Color.white;
        bool _isOn;

        const float KnobTravel = 1f;

        public void SetColours(Color trackOff, Color trackOn, Color knob)
        {
            _off = trackOff;
            _on = trackOn;
            _knob = knob;
            Apply(_isOn);
        }

        public void Apply(bool isOn)
        {
            _isOn = isOn;

            if (Track != null) Track.color = isOn ? _on : _off;
            if (Knob == null) return;

            Knob.color = _knob;

            var rect = (RectTransform)Knob.transform;
            rect.anchorMin = new Vector2(isOn ? KnobTravel : 0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(isOn ? 1f : 0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    public sealed class UiChrome : MonoBehaviour
    {
        Outline _edge;
        Outline _halo;

        public bool Glowing => _halo != null && _halo.enabled;

        public void SetEdge(double width, Color colour)
        {
            if (width <= 0.0)
            {
                if (_edge != null) _edge.enabled = false;
                return;
            }

            if (_edge == null) _edge = Add();
            _edge.enabled = true;
            _edge.effectColor = colour;
            _edge.effectDistance = new Vector2((float)width, (float)width);
        }

        public void SetHalo(double glow, Color colour)
        {
            if (glow <= 0.0)
            {
                if (_halo != null) _halo.enabled = false;
                return;
            }

            if (_halo == null) _halo = Add();
            _halo.enabled = true;

            colour.a = (float)glow * UiPainter.HaloAlpha;
            _halo.effectColor = colour;

            float reach = (float)glow * UiPainter.HaloReach;
            _halo.effectDistance = new Vector2(reach, reach);
        }

        Outline Add()
        {
            var outline = gameObject.AddComponent<Outline>();

            outline.useGraphicAlpha = false;
            return outline;
        }
    }

    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public const float Recovery = 0.10f;

        public const float SinkDistance = 7f;

        public const float FlashAmount = 0.35f;

        public Graphic Fill;
        PressKind _kind = PressKind.Sink;

        Vector2 _home;
        Color _resting;
        bool _held;

        float _age = Recovery;

        Image _ripple;
        float _rippleReach;

        public Image Ripple => _ripple;

        public const float RippleAlpha = 0.30f;

        public void Configure(in UiSkin skin, Graphic fill, Color ripple)
        {
            _kind = skin.Press;
            Fill = fill;

            if (_kind != PressKind.Ripple)
            {
                if (_ripple != null) _ripple.gameObject.SetActive(false);
                return;
            }

            if (_ripple == null) _ripple = BuildRipple();
            ripple.a = 0f;
            _ripple.color = ripple;
            _rippleInk = ripple;
            _ripple.gameObject.SetActive(true);
        }

        Color _rippleInk = Color.white;

        Image BuildRipple()
        {
            var area = new GameObject("RippleArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(transform, false);
            UiBuilder.Stretch((RectTransform)area.transform);

            var go = new GameObject("Ripple", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(area.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = UiBuilder.Disc;
            image.raycastTarget = false;
            return image;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_age >= Recovery)
            {
                _home = ((RectTransform)transform).anchoredPosition;
                if (Fill != null) _resting = Fill.color;
            }

            if (_kind == PressKind.Ripple) StartRipple(eventData);

            _held = true;
            _age = 0f;
            ApplyPressed(1f);
        }

        void StartRipple(PointerEventData eventData)
        {
            if (_ripple == null) return;

            var rect = (RectTransform)transform;
            Vector2 local = Vector2.zero;
            if (eventData != null)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out local);

            _ripple.rectTransform.anchoredPosition = local;

            var size = rect.rect.size;
            float reach = Mathf.Max(
                new Vector2(size.x * 0.5f + Mathf.Abs(local.x), size.y * 0.5f + Mathf.Abs(local.y))
                    .magnitude, 1f) * 2f;
            _rippleReach = reach;
        }

        public void OnPointerUp(PointerEventData eventData) => _held = false;

        void Update()
        {
            if (_age >= Recovery) return;

            if (_held && _kind != PressKind.Ripple) return;

            _age += Time.unscaledDeltaTime;
            ApplyPressed(Mathf.Clamp01(1f - _age / Recovery));
        }

        void ApplyPressed(float amount)
        {
            if (_kind == PressKind.Sink)
            {
                ((RectTransform)transform).anchoredPosition =
                    _home + new Vector2(0f, -SinkDistance * amount);
                return;
            }

            if (_kind == PressKind.Ripple)
            {
                if (_ripple == null) return;

                float spread = 1f - amount;
                _ripple.rectTransform.sizeDelta = new Vector2(_rippleReach * spread, _rippleReach * spread);

                var ink = _rippleInk;
                ink.a = RippleAlpha * amount;
                _ripple.color = ink;
                return;
            }

            if (Fill != null) Fill.color = Color.Lerp(_resting, Color.white, FlashAmount * amount);
        }
    }
}
