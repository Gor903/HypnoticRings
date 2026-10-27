using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// Centres a scrolling column while its content still fits, and lets it scroll once it does
    /// not.
    ///
    /// One alignment cannot serve both screens that use the same column: the theme list is five
    /// rows and belongs in the middle of the screen like every other menu, while settings is
    /// eleven rows and has to start at the top or the first one is unreachable.
    /// </summary>
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
