using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    public sealed class UiShimmer : MonoBehaviour
    {
        readonly List<Graphic> _targets = new List<Graphic>();
        readonly List<Color> _home = new List<Color>();

        float _seconds;
        float _depth;
        float _age;

        Color _toward = Color.white;

        public bool Breathing => isActiveAndEnabled && _seconds > 0f && _depth > 0f && _targets.Count > 0;

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

        public void Rest()
        {
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i] != null) _targets[i].color = _home[i];
        }

        void OnDisable() => Rest();

        void Update()
        {
            if (!Breathing) return;

            _age += Time.unscaledDeltaTime;

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
