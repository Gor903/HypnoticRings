using HR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    public static class UiPainter
    {
        public static void Shape(Image image, float heightUnits, in UiSkin skin)
        {
            if (image == null) return;

            bool chamfered = skin.Chamfer > 0.0;
            double corner = (chamfered ? skin.Chamfer : skin.CornerRadius) * heightUnits;

            if (corner < 1.0)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                return;
            }

            corner = System.Math.Min(corner, heightUnits * 0.5);

            image.sprite = chamfered ? UiBuilder.Chamfer : UiBuilder.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = (float)(UiBuilder.RoundedBorder / corner);
        }

        public static void Edge(Graphic graphic, in UiSkin skin, Color edge, double glow,
                                Color halo, bool effectsWanted)
        {
            if (graphic == null) return;

            var chrome = graphic.GetComponent<UiChrome>();
            if (chrome == null) chrome = graphic.gameObject.AddComponent<UiChrome>();

            chrome.SetEdge(skin.OutlineWidth, edge);
            chrome.SetHalo(effectsWanted ? glow : 0.0, halo);
        }

        public const float HaloReach = 9f;

        public const float HaloAlpha = 0.38f;

        public static void Shadow(Graphic graphic, in UiSkin skin, Color ink)
        {
            if (graphic == null) return;

            var chrome = graphic.GetComponent<UiChrome>();
            if (chrome == null) chrome = graphic.gameObject.AddComponent<UiChrome>();

            chrome.SetShadow(skin.HardShadow, ink);
        }

        public static void ShapeKnob(Image image, in UiSkin skin)
        {
            if (image == null) return;

            if (skin.Knob == KnobShape.Round)
            {
                image.sprite = UiBuilder.Disc;
                image.type = Image.Type.Simple;
                return;
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
        }
    }
}
