using UnityEngine;

namespace HR.UI
{
    /// <summary>
    /// Keeps a RectTransform inside the screen's safe area — clear of the camera cutout at the
    /// top and the gesture bar at the bottom.
    ///
    /// Re-applies when the safe area changes rather than once at startup: it changes on
    /// rotation, on a foldable opening, and on some devices a frame or two after launch, and a
    /// one-shot fit leaves the pause button under the notch on exactly those.
    /// </summary>
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

        /// <summary>
        /// Fits to an explicit area and stops tracking the device's. The screenshot rig needs it:
        /// the real safe area belongs to the batch window, not to the shape being captured.
        /// </summary>
        public void Pin(Rect area, Vector2Int resolution)
        {
            _pinned = true;
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (resolution.x <= 0 || resolution.y <= 0) return;

            _applied = area;
            _appliedResolution = resolution;
            Fit(area, resolution.x, resolution.y);
        }

        /// <summary>Public so a test or a screenshot rig can force a fit without waiting a frame.</summary>
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
