using HR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
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

        public bool Enabled { get; set; } = true;

        public void Initialise(Text score, RectTransform parent, float pulseScale,
                               float pulseSeconds, float fadeSeconds)
        {
            _score = score;
            _pulseScale = pulseScale;
            _pulseSeconds = Mathf.Max(0.01f, pulseSeconds);
            _fadeSeconds = Mathf.Max(0.01f, fadeSeconds);

            var go = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            UiBuilder.Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling();

            _curtain = go.GetComponent<Image>();
            _curtain.color = new Color(0f, 0f, 0f, 0f);
            _curtain.raycastTarget = false;
            go.SetActive(false);
        }

        public Color CurtainColour { get; set; } = Color.black;

        public void Pulse()
        {
            if (!Enabled || _score == null) return;
            _pulseAge = 0f;
        }

        public void Fade(bool toClear)
        {
            if (_curtain == null) return;

            if (!Enabled)
            {
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

        public void Clear()
        {
            _pulseAge = -1f;
            _fadeAge = -1f;
            if (_score != null) _score.transform.localScale = Vector3.one;
            if (_curtain != null) _curtain.gameObject.SetActive(false);
        }
    }
}
