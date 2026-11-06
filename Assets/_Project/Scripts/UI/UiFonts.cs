using HR.Core;
using UnityEngine;

namespace HR.UI
{
    public static class UiFonts
    {
        public const string DisplayPath = "Fonts/Unbounded-Bold";

        public const string TextPath = "Fonts/Manrope-Medium";

        public const float BestFitFloor = 0.75f;

        static Font _display;
        static Font _text;
        static Font _fallback;
        static bool _warned;

        public static Font For(FontRole role) =>
            role == FontRole.Display
                ? (_display != null ? _display : _display = Load(DisplayPath))
                : (_text != null ? _text : _text = Load(TextPath));

        public static int MinSizeFor(int size) => Mathf.RoundToInt(size * BestFitFloor);

        public static void Forget()
        {
            _display = null;
            _text = null;
            _warned = false;
        }

        static Font Load(string path)
        {
            var font = Resources.Load<Font>(path);
            if (font != null) return font;

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning($"UiFonts: {path} is missing, falling back to the built-in font");
            }

            return Fallback;
        }

        static Font Fallback =>
            _fallback != null
                ? _fallback
                : _fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                              ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
