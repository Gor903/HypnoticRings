namespace HR.Core
{
    /// <summary>
    /// How visible a ring is for its depth in the tunnel. Pure maths, so the rule can be checked
    /// without rendering anything.
    ///
    /// This is the only depth cue the design allows: CONTRACT.md §2.2 fixes thickness at every
    /// radius, so distance is carried by colour and opacity alone. A ring that faded by getting
    /// thinner would change the physics with it.
    /// </summary>
    public static class DepthShading
    {
        /// <summary>
        /// Opacity for a ring, given where the ball currently is.
        ///
        /// The ball's ring and the one it is aiming for are fully opaque — those are the two the
        /// player is reading. Rings behind fade out as they collapse towards the centre; rings
        /// ahead fade in as they arrive, so nothing ever pops into existence.
        /// </summary>
        public static double AlphaFor(double ringRadius, double ballRingRadius, double vanishRadius,
                                      double step, int ringsAhead)
        {
            if (step <= 0.0) return 1.0;

            double nextRadius = ballRingRadius + step;

            // The two rings that matter right now.
            if (ringRadius <= nextRadius + 1e-9 && ringRadius >= ballRingRadius - 1e-9) return 1.0;

            if (ringRadius < ballRingRadius)
            {
                double span = ballRingRadius - vanishRadius;
                if (span <= 0.0) return 0.0;
                return Clamp01((ringRadius - vanishRadius) / span);
            }

            // Ahead of the next ring: fade in over the last step of its approach.
            double outermost = ballRingRadius + step * (ringsAhead > 0 ? ringsAhead : 1);
            return Clamp01((outermost - ringRadius) / step);
        }

        static double Clamp01(double value) => value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
    }
}
