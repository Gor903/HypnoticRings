using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HR.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public sealed class CarouselSnapping : MonoBehaviour, IEndDragHandler, IBeginDragHandler
    {
        public float CardWidth = 600f;
        public float Spacing = 32f;

        public const float Ease = 16f;

        ScrollRect _scroll;
        HorizontalLayoutGroup _layout;
        float _target = float.NaN;
        float _paddedFor = float.NaN;
        bool _dragging;

        public int Focused { get; private set; }

        float Step => CardWidth + Spacing;

        void Awake()
        {
            _scroll = GetComponent<ScrollRect>();
            if (_scroll.content != null) _layout = _scroll.content.GetComponent<HorizontalLayoutGroup>();
        }

        public void Focus(int index, bool immediate)
        {
            Focused = Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
            _target = -Focused * Step;

            if (!immediate || _scroll == null || _scroll.content == null) return;
            _scroll.velocity = Vector2.zero;
            var position = _scroll.content.anchoredPosition;
            _scroll.content.anchoredPosition = new Vector2(_target, position.y);
        }

        int Count => _scroll != null && _scroll.content != null ? _scroll.content.childCount : 0;

        public void OnBeginDrag(PointerEventData eventData) => _dragging = true;

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (_scroll == null || _scroll.content == null) return;

            _scroll.velocity = Vector2.zero;
            Focus(Mathf.RoundToInt(-_scroll.content.anchoredPosition.x / Step), immediate: false);
        }

        void LateUpdate()
        {
            if (_scroll == null || _scroll.content == null || _scroll.viewport == null) return;

            Pad(_scroll.viewport.rect.width);
            if (_dragging || float.IsNaN(_target)) return;

            var position = _scroll.content.anchoredPosition;
            if (Mathf.Abs(position.x - _target) < 0.5f)
            {
                if (position.x != _target)
                    _scroll.content.anchoredPosition = new Vector2(_target, position.y);
                return;
            }

            _scroll.velocity = Vector2.zero;
            float eased = Mathf.Lerp(position.x, _target, 1f - Mathf.Exp(-Ease * Time.unscaledDeltaTime));
            _scroll.content.anchoredPosition = new Vector2(eased, position.y);
        }

        void Pad(float viewportWidth)
        {
            if (_layout == null || viewportWidth <= 0f) return;
            if (Mathf.Approximately(viewportWidth, _paddedFor)) return;

            _paddedFor = viewportWidth;
            int side = Mathf.Max(0, Mathf.RoundToInt((viewportWidth - CardWidth) * 0.5f));
            _layout.padding = new RectOffset(side, side, 0, 0);
            LayoutRebuilder.MarkLayoutForRebuild(_scroll.content);
        }
    }
}
