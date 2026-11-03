namespace HR.Core
{
    /// <summary>How a control answers a finger.</summary>
    public enum PressKind
    {
        /// <summary>Settles down and back, as if it had depth.</summary>
        Sink = 0,

        /// <summary>Brightens for a moment. Reads better on dark themes.</summary>
        Flash = 1,

        /// <summary>A circle spreading from where the finger landed, clipped to the control.</summary>
        Ripple = 2,
    }

    /// <summary>Shape of the thing you drag on a slider or a switch.</summary>
    public enum KnobShape
    {
        Square = 0,
        Round = 1,
    }

    /// <summary>
    /// The material an interface is cut from, per theme.
    ///
    /// Separate from <see cref="ThemePalette"/> because it answers a different question. The
    /// palette says what colour a thing is; this says what KIND of thing it is — soft or sharp,
    /// outlined or bare, sinking or flashing under a finger. A theme that only recoloured the
    /// same grey rectangles would be a palette swap, and the point of six themes is that they
    /// feel like six places.
    ///
    /// Engine-free, like the palette, so every value can be checked by a test rather than by
    /// looking at it.
    /// </summary>
    public readonly struct UiSkin
    {
        /// <summary>
        /// Corner rounding as a share of the control's height: 0 is a sharp rectangle, 0.5 is a
        /// capsule. Expressed as a share rather than in units so one number is right for a tall
        /// button and a short chip alike.
        /// </summary>
        public readonly double CornerRadius;

        /// <summary>Width of the edge around a control, in layout units. Zero draws none.</summary>
        public readonly double OutlineWidth;

        public readonly PressKind Press;
        public readonly KnobShape Knob;

        public UiSkin(double cornerRadius, double outlineWidth, PressKind press, KnobShape knob)
        {
            CornerRadius = cornerRadius;
            OutlineWidth = outlineWidth;
            Press = press;
            Knob = knob;
        }

        /// <summary>
        /// What a theme gets if it says nothing: square-ish, outlined, sinking.
        ///
        /// Deliberately the Monochrome skin. A theme with no opinion should look like the one
        /// the whole game was designed against, not like a blank.
        /// </summary>
        public static UiSkin Default => new UiSkin(0.10, 3.0, PressKind.Sink, KnobShape.Square);

        /// <summary>
        /// The skin each theme is cut from.
        ///
        /// The pairing is not arbitrary. A light, airy palette takes soft corners and no edge;
        /// a dark one can afford a hard edge and a flash, because the flash has somewhere to
        /// read against.
        /// </summary>
        public static UiSkin For(Theme theme)
        {
            switch (theme)
            {
                case Theme.Monochrome:
                    return Default;

                case Theme.Midnight:
                    return new UiSkin(0.25, 2.0, PressKind.Flash, KnobShape.Round);

                case Theme.Neon:
                    return new UiSkin(0.30, 4.0, PressKind.Flash, KnobShape.Round);

                case Theme.Pastel:
                    // A capsule, and no edge at all: the softest palette in the set should not
                    // be wearing a hard line around everything.
                    return new UiSkin(0.50, 0.0, PressKind.Sink, KnobShape.Round);

                case Theme.Earth:
                    return new UiSkin(0.20, 2.0, PressKind.Sink, KnobShape.Round);

                case Theme.Aurora:
                    // The one theme that answers a finger where the finger actually is. It can
                    // afford it: the palette is the slowest and the quietest in the set, so a
                    // spreading circle reads as the surface responding rather than as a flicker.
                    return new UiSkin(0.35, 3.0, PressKind.Ripple, KnobShape.Round);

                default:
                    return Default;
            }
        }
    }
}
