namespace HR.Core
{
    /// <summary>What happens when a ring closes onto the red circle.</summary>
    public enum CrushOutcome
    {
        /// <summary>The run is over.</summary>
        Lost = 0,

        /// <summary>The ring bursts and takes the ball inside it; the other ball plays on.</summary>
        BallSacrificed = 1,
    }

    /// <summary>
    /// The one place that decides whether a crush ends the run.
    ///
    /// Kept here, engine-free, because it is a rule rather than a behaviour: it depends only on
    /// how many balls are in play and how many of them the closing ring has inside it. Deciding
    /// it inside the tunnel would mean the rule could only ever be checked by crushing a real
    /// ring in a real scene, which is the slowest possible way to be sure of something this
    /// short.
    ///
    /// Plan [G-29].
    /// </summary>
    public static class SpareLife
    {
        /// <summary>
        /// <paramref name="ballsInside"/> counts the balls whose innermost enclosing ring is the
        /// one that just reached the red circle. A ball further out is not inside it and is not
        /// in danger from it.
        /// </summary>
        public static CrushOutcome Resolve(int ballsAlive, int ballsInside)
        {
            // One ball is the whole game: there is nothing to carry on with.
            if (ballsAlive <= 1) return CrushOutcome.Lost;

            // Every ball in play was inside the ring that burst, so nothing survived it. This is
            // the case that makes a second ball a reprieve rather than immunity — two balls in
            // the same ring are one mistake, not two chances.
            if (ballsInside >= ballsAlive) return CrushOutcome.Lost;

            return CrushOutcome.BallSacrificed;
        }
    }
}
