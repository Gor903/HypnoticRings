using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// The slow breath a glowing theme gives its interface.
    ///
    /// The tunnel already does this: Neon and Aurora move their ring colours a little, on a
    /// cycle floored well above the photosensitivity limit the plan sets. The interface was the
    /// only part of those themes standing perfectly still, which made the buttons look pasted
    /// onto someone else's screen.
    ///
    /// Everything it animates is read back from the controls at the moment the theme is applied,
    /// so it never needs to know what colour anything is — only how far to move it and how
    /// slowly. Switched off, it restores exactly what it found.
    /// </summary>
    public sealed class UiShimmer : MonoBehaviour
    {
        readonly List<Graphic> _targets = new List<Graphic>();
        readonly List<Color> _home = new List<Color>();

        float _seconds;
        float _depth;
        float _age;

        /// <summary>
        /// Where a breath goes. Toward the paper rather than toward white, because on a light
        /// theme white is the background and the control would breathe itself invisible.
        /// </summary>
        Color _toward = Color.white;

        /// <summary>True while anything is actually moving. The effects test reads this.</summary>
        public bool Breathing => isActiveAndEnabled && _seconds > 0f && _depth > 0f && _targets.Count > 0;

        /// <summary>
        /// Takes the theme's cycle and the controls to move.
        ///
        /// Called on every repaint, and it has to be: the resting colours are what a breath
        /// returns to, and a set remembered under the previous theme would drag that theme's
        /// colours into this one the first time anything exhaled.
        ///
        /// What it does NOT do is put the old colours back — <see cref="Rest"/> has to run
        /// before the repaint, not after. Resting here undid the painting that had just
        /// happened: the new theme was applied, and then every accent was handed back the
        /// brightness it had been caught at under the old one.
        /// </summary>
        public void Configure(double seconds, double depth, Color toward, IEnumerable<Graphic> targets)
        {
            _targets.Clear();
            _home.Clear();

            _seconds = (float)seconds;
            _depth = (float)depth;
            _toward = toward;
            _age = 0f;

            if (_seconds <= 0f || _depth <= 0f || targets == null) return;

            foreach (var graphic in targets)
            {
                if (graphic == null) continue;
                _targets.Add(graphic);
                _home.Add(graphic.color);
            }
        }

        /// <summary>Puts everything back where it was found. Idempotent on purpose.</summary>
        public void Rest()
        {
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i] != null) _targets[i].color = _home[i];
        }

        void OnDisable() => Rest();

        void Update()
        {
            if (!Breathing) return;

            // Unscaled, like every other animation in the interface: the one screen a player
            // sees most of while nothing else is running is the paused one.
            _age += Time.unscaledDeltaTime;

            // A full cycle out and back, so there is no instant where the colour jumps. Starting
            // at nought means a theme change does not begin mid-breath.
            float phase = Mathf.Sin(_age / _seconds * 2f * Mathf.PI) * 0.5f + 0.5f;
            float amount = _depth * phase;

            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] == null) continue;

                var breathed = Color.Lerp(_home[i], _toward, amount);
                breathed.a = _home[i].a;
                _targets[i].color = breathed;
            }
        }
    }
}
