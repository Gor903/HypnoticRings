using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// Keeps a sideways carousel resting on a whole card.
    ///
    /// A scroll view left to itself stops wherever the finger let go, which on a row of cards
    /// means one card and two slices of its neighbours — and a card cut off by the edge of the
    /// screen reads as the layout having broken rather than as a hint that there is more. Here
    /// the row always settles with one card centred and its neighbours peeking symmetrically,
    /// which says the same thing on purpose.
    ///
    /// The end padding is worked out rather than authored: it has to be half the spare width so
    /// that the first and the last card can reach the middle, and the spare width depends on the
    /// screen, which is not known until the layout has run.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class CarouselSnapping : MonoBehaviour, IEndDragHandler, IBeginDragHandler
    {
        public float CardWidth = 600f;
        public float Spacing = 32f;

        /// <summary>How quickly it settles. High enough to feel like a snap, not a journey.</summary>
        public const float Ease = 16f;

        ScrollRect _scroll;
        HorizontalLayoutGroup _layout;
        float _target = float.NaN;
        float _paddedFor = float.NaN;
        bool _dragging;

        /// <summary>The card the row is resting on, or heading for.</summary>
        public int Focused { get; private set; }

        float Step => CardWidth + Spacing;

        void Awake()
        {
            _scroll = GetComponent<ScrollRect>();
            if (_scroll.content != null) _layout = _scroll.content.GetComponent<HorizontalLayoutGroup>();
        }

        /// <summary>Puts a card in the middle. Without a glide, when the screen is opening.</summary>
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

        /// <summary>Half the spare width at each end, so an end card can still reach the middle.</summary>
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
