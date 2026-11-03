using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ScrollCentering : MonoBehaviour
    {
        ScrollRect _scroll;
        float _applied = float.NaN;

        void Awake() => _scroll = GetComponent<ScrollRect>();

        void LateUpdate()
        {
            if (_scroll == null) _scroll = GetComponent<ScrollRect>();
            if (_scroll.content == null || _scroll.viewport == null) return;

            float slack = _scroll.viewport.rect.height - _scroll.content.rect.height;
            float offset = slack > 0f ? -slack * 0.5f : 0f;
            _scroll.vertical = slack <= 0f;

            if (slack <= 0f || Mathf.Approximately(offset, _applied)) return;

            _applied = offset;
            var position = _scroll.content.anchoredPosition;
            _scroll.content.anchoredPosition = new Vector2(position.x, offset);
        }
    }
}
