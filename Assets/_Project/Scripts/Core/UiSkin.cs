namespace HR.Core
{
    public enum PressKind
    {
        Sink = 0,

        Flash = 1,

        Ripple = 2,
    }

    public enum KnobShape
    {
        Square = 0,
        Round = 1,
    }

    public readonly struct UiSkin
    {
        public readonly double CornerRadius;

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

        public static UiSkin Default => new UiSkin(0.10, 3.0, PressKind.Sink, KnobShape.Square);

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

                    return new UiSkin(0.50, 0.0, PressKind.Sink, KnobShape.Round);

                case Theme.Earth:
                    return new UiSkin(0.20, 2.0, PressKind.Sink, KnobShape.Round);

                case Theme.Aurora:

                    return new UiSkin(0.35, 3.0, PressKind.Ripple, KnobShape.Round);

                case Theme.Ember:

                    return new UiSkin(0.15, 3.0, PressKind.Flash, KnobShape.Round);

                default:
                    return Default;
            }
        }
    }
}
