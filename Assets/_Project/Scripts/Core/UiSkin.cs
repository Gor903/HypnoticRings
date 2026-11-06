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

        public readonly double Chamfer;

        public readonly double HardShadow;

        public UiSkin(double cornerRadius, double outlineWidth, PressKind press, KnobShape knob,
                      double chamfer = 0.0, double hardShadow = 0.0)
        {
            CornerRadius = cornerRadius;
            OutlineWidth = outlineWidth;
            Press = press;
            Knob = knob;
            Chamfer = chamfer;
            HardShadow = hardShadow;
        }

        public static UiSkin Default => new UiSkin(0.10, 3.0, PressKind.Sink, KnobShape.Square);

        public static UiSkin For(Theme theme)
        {
            switch (theme)
            {
                case Theme.MinimalLight:
                    return new UiSkin(0.20, 2.0, PressKind.Sink, KnobShape.Round);

                case Theme.MinimalDark:
                    return new UiSkin(0.20, 2.0, PressKind.Flash, KnobShape.Round);

                case Theme.Neon:
                    return new UiSkin(0.30, 4.0, PressKind.Flash, KnobShape.Round);

                case Theme.Deco:

                    return new UiSkin(0.0, 3.0, PressKind.Flash, KnobShape.Square, chamfer: 0.18);

                case Theme.Sumie:
                    return Default;

                case Theme.Ocean:

                    return new UiSkin(0.37, 2.0, PressKind.Ripple, KnobShape.Round);

                case Theme.Bauhaus:

                    return new UiSkin(0.0, 3.0, PressKind.Sink, KnobShape.Square, hardShadow: 6.0);

                case Theme.Crt:
                    return new UiSkin(0.06, 3.0, PressKind.Flash, KnobShape.Square);

                case Theme.Ember:

                    return new UiSkin(0.15, 3.0, PressKind.Flash, KnobShape.Round);

                default:
                    return Default;
            }
        }
    }
}
