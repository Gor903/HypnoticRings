using HR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HR.UI
{
    /// <summary>
    /// Puts a skin on a control.
    ///
    /// The single place that knows what a skin means in pixels. Everything else — the builder,
    /// the screens — says what a thing IS (a button, a panel, a switch) and leaves what it
    /// LOOKS like to here. Spread that knowledge around instead and a new skin property has to
    /// be remembered in a dozen places, which is how half the themes end up half-applied.
    /// </summary>
    public static class UiPainter
    {
        /// <summary>
        /// Rounds a control's corners to the skin's taste.
        ///
        /// The radius arrives as a share of the control's height, so one number suits a tall
        /// button and a short chip. The renderer does the work: a single capsule sprite is
        /// sliced nine ways and its border scaled down to the corner this control wants, which
        /// is why there is one texture in the game rather than one per radius per height.
        /// </summary>
        public static void Shape(Image image, float heightUnits, in UiSkin skin)
        {
            if (image == null) return;

            double corner = skin.CornerRadius * heightUnits;

            // A sharp rectangle is the absence of a sprite, not a sprite with no rounding:
            // slicing a capsule down to a zero-width border leaves seams where the pieces meet.
            if (corner < 1.0)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                return;
            }

            // Never more than half the height, or the two corners of one side overlap and the
            // renderer starts shrinking the whole sprite to compensate.
            corner = System.Math.Min(corner, heightUnits * 0.5);

            image.sprite = UiBuilder.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = (float)(UiBuilder.RoundedBorder / corner);
        }

        /// <summary>
        /// Rounds a square control — a knob, a pause button — to the skin's shape.
        ///
        /// Separate from <see cref="Shape"/> because a knob is not a short button: the skin
        /// says round or square outright rather than by degree, and a half-rounded knob reads
        /// as a rendering mistake.
        /// </summary>
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
