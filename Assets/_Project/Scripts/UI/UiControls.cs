using HR.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// The two halves of a switch, kept visible in both states.
    ///
    /// uGUI's own answer is to hide the tick when the toggle is off, which leaves a settings row
    /// that is a line of text with nothing beside it — the player cannot tell that "Invert
    /// rotation" is a control at all, let alone which way it is set. Here the track never goes
    /// away and the knob slides across it, so off is a state rather than an absence.
    /// </summary>
    public sealed class ToggleVisual : MonoBehaviour
    {
        public Image Track;
        public Image Knob;

        Color _off = Color.grey;
        Color _on = Color.white;
        Color _knob = Color.white;
        bool _isOn;

        /// <summary>How far along the track the knob sits, as a share of the spare room.</summary>
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

    /// <summary>
    /// The edge and the halo a control wears, kept together so neither is looked up by type.
    ///
    /// Both are <see cref="Outline"/> components on the same object, and two of those cannot be
    /// told apart by <c>GetComponent</c>. Holding the references is the whole job.
    /// </summary>
    public sealed class UiChrome : MonoBehaviour
    {
        Outline _edge;
        Outline _halo;

        /// <summary>True while a halo is actually being drawn, for the effects test.</summary>
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

            // The copies keep their own colour outright. Multiplied by the graphic's alpha they
            // would inherit whatever transparency the control had, and a halo on a label at
            // sixty per cent is a halo nobody specified.
            outline.useGraphicAlpha = false;
            return outline;
        }
    }

    /// <summary>
    /// What a button does under a finger.
    ///
    /// Without it a press is silent: the sound plays, the screen changes, and nothing about the
    /// control itself acknowledges having been touched. On a phone that reads as a dropped tap,
    /// and the player presses again.
    ///
    /// Runs on unscaled time because the most important button in the game is Resume, and a
    /// paused game has no other kind.
    /// </summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>Seconds to settle back. Short enough to feel like a surface, not a toy.</summary>
        public const float Recovery = 0.10f;

        /// <summary>How far a sinking control moves, in layout units.</summary>
        public const float SinkDistance = 7f;

        /// <summary>How much a flashing control brightens, 0 to 1.</summary>
        public const float FlashAmount = 0.35f;

        public Graphic Fill;
        PressKind _kind = PressKind.Sink;

        Vector2 _home;
        Color _resting;
        bool _held;

        // Starts spent, not fresh. At zero the recovery animation is mid-flight the moment the
        // component exists, so every button sank by itself after a theme change — a press
        // nobody made, on every control at once.
        float _age = Recovery;

        Image _ripple;
        float _rippleReach;

        /// <summary>The ripple's colour, so the test can see it is the one the theme asked for.</summary>
        public Image Ripple => _ripple;

        /// <summary>Strongest the ripple is drawn, at the instant of the touch.</summary>
        public const float RippleAlpha = 0.30f;

        /// <summary>
        /// Takes the skin's kind of press. Colour and position are read when pressed.
        ///
        /// A ripple needs a disc and a mask to clip it, and both are built the first time a
        /// theme actually asks for one — five of the six never do, and a disc per button on
        /// every screen is five screens of objects drawing nothing.
        /// </summary>
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
            // The mask goes on a container of its own, never on the button. A RectMask2D clips
            // everything under its rect, and the button's own halo is drawn as part of the
            // button's mesh — masked there, the one theme with both a ripple and a glow would
            // lose the glow to the ripple.
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
            // Where the control rests is read here rather than when it was built. These sit in
            // layout groups that have not run yet while the interface is being assembled, so a
            // position remembered then is the one the button had before anything placed it —
            // and the first press of the game would move it there. The colour likewise: a flash
            // returns to what it remembered, and a theme may have changed since.
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

        /// <summary>
        /// Puts the ripple where the finger landed.
        ///
        /// A ripple that always starts at the middle is an animation; one that starts under the
        /// thumb is an answer. The point arrives in screen space and has to be brought into the
        /// control's own, which is the only reason this needs the event at all.
        /// </summary>
        void StartRipple(PointerEventData eventData)
        {
            if (_ripple == null) return;

            var rect = (RectTransform)transform;
            Vector2 local = Vector2.zero;
            if (eventData != null)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out local);

            _ripple.rectTransform.anchoredPosition = local;

            // Big enough to cover the control from wherever it started: the far corner is the
            // worst case, and anything smaller stops spreading while the eye is still following.
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

            // A sink and a flash hold while the finger is down — that is what "pressed" looks
            // like. A ripple does not: it is the record of the touch landing, and one frozen
            // mid-spread under a held thumb reads as a stuck animation.
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

                // Spreads as it fades: the circle is largest exactly when it is faintest, which
                // is what makes it read as a surface absorbing the touch rather than as a disc
                // being drawn on top of the button.
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
