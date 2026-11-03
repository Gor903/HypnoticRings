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

        /// <summary>Takes the skin's kind of press. Colour and position are read when pressed.</summary>
        public void Configure(in UiSkin skin, Graphic fill)
        {
            _kind = skin.Press;
            Fill = fill;
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

            _held = true;
            _age = 0f;
            ApplyPressed(1f);
        }

        public void OnPointerUp(PointerEventData eventData) => _held = false;

        void Update()
        {
            if (_held || _age >= Recovery) return;

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

            if (Fill != null) Fill.color = Color.Lerp(_resting, Color.white, FlashAmount * amount);
        }
    }
}
