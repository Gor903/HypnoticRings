using HR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// The two interface effects of spec 11.4: the score swelling on a pass, and the fade
    /// between menu and game.
    ///
    /// Both run on their own clock in unscaled time. A pause sets the timescale to zero, and a
    /// fade that stops halfway through because the game paused leaves the player looking at a
    /// half-dimmed screen with no way to finish it.
    /// </summary>
    public sealed class ScreenJuice : MonoBehaviour
    {
        Text _score;
        Image _curtain;

        float _pulseAge = -1f;
        float _pulseSeconds = 0.15f;
        float _pulseScale = 1.15f;

        float _fadeAge = -1f;
        float _fadeSeconds = 0.35f;
        bool _fadingIn;

        /// <summary>False when the player has asked for fewer effects.</summary>
        public bool Enabled { get; set; } = true;

        public void Initialise(Text score, RectTransform parent, float pulseScale,
                               float pulseSeconds, float fadeSeconds)
        {
            _score = score;
            _pulseScale = pulseScale;
            _pulseSeconds = Mathf.Max(0.01f, pulseSeconds);
            _fadeSeconds = Mathf.Max(0.01f, fadeSeconds);

            // Drawn over everything, and never in the way of a finger.
            var go = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            UiBuilder.Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling();

            _curtain = go.GetComponent<Image>();
            _curtain.color = new Color(0f, 0f, 0f, 0f);
            _curtain.raycastTarget = false;
            go.SetActive(false);
        }

        /// <summary>The colour the curtain fades through — the theme's background, not black.</summary>
        public Color CurtainColour { get; set; } = Color.black;

        public void Pulse()
        {
            if (!Enabled || _score == null) return;
            _pulseAge = 0f;
        }

        /// <summary>Fades the screen out, or back in, over the configured time.</summary>
        public void Fade(bool toClear)
        {
            if (_curtain == null) return;

            if (!Enabled)
            {
                // Switched off, the curtain must not be left sitting on the screen.
                _curtain.gameObject.SetActive(false);
                _fadeAge = -1f;
                return;
            }

            _fadingIn = toClear;
            _fadeAge = 0f;
            _curtain.gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            StepPulse(dt);
            StepFade(dt);
        }

        void StepPulse(float dt)
        {
            if (_pulseAge < 0f || _score == null) return;

            _pulseAge += dt;
            float life = _pulseAge / _pulseSeconds;

            if (life >= 1f)
            {
                _pulseAge = -1f;
                // Set outright rather than decremented: a hundred passes must leave no residue.
                _score.transform.localScale = Vector3.one;
                return;
            }

            float scale = 1f + (_pulseScale - 1f) * (float)Easing.Pulse(life);
            _score.transform.localScale = new Vector3(scale, scale, 1f);
        }

        void StepFade(float dt)
        {
            if (_fadeAge < 0f || _curtain == null) return;

            _fadeAge += dt;
            float life = _fadeAge / _fadeSeconds;
            float ramp = (float)Easing.SmoothStep(Mathf.Clamp01(life));
            float alpha = _fadingIn ? 1f - ramp : ramp;

            var colour = CurtainColour;
            colour.a = alpha;
            _curtain.color = colour;

            if (life < 1f) return;

            _fadeAge = -1f;
            if (_fadingIn) _curtain.gameObject.SetActive(false);
        }

        /// <summary>Puts everything back where it started, for a test or a reset.</summary>
        public void Clear()
        {
            _pulseAge = -1f;
            _fadeAge = -1f;
            if (_score != null) _score.transform.localScale = Vector3.one;
            if (_curtain != null) _curtain.gameObject.SetActive(false);
        }
    }
}
