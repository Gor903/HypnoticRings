using UnityEngine;

namespace HR.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _applied;
        Vector2Int _appliedResolution;
        bool _pinned;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (_pinned) return;
            if (Screen.safeArea != _applied
                || Screen.width != _appliedResolution.x
                || Screen.height != _appliedResolution.y)
                Apply();
        }

        public void Pin(Rect area, Vector2Int resolution)
        {
            _pinned = true;
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (resolution.x <= 0 || resolution.y <= 0) return;

            _applied = area;
            _appliedResolution = resolution;
            Fit(area, resolution.x, resolution.y);
        }

        public void Apply()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var area = Screen.safeArea;
            _applied = area;
            _appliedResolution = new Vector2Int(Screen.width, Screen.height);
            Fit(area, Screen.width, Screen.height);
        }

        void Fit(Rect area, int width, int height)
        {
            var min = area.position;
            var max = area.position + area.size;
            min.x /= width;
            min.y /= height;
            max.x /= width;
            max.y /= height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
