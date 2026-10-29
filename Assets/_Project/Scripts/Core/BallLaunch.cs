using System;

namespace HR.Core
{
    /// <summary>Where a run throws the ball, and how fast.</summary>
    public readonly struct LaunchPlan
    {
        public readonly double X;
        public readonly double Y;
        public readonly double VelocityX;
        public readonly double VelocityY;

        public LaunchPlan(double x, double y, double velocityX, double velocityY)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public double Distance => Math.Sqrt(X * X + Y * Y);
        public double Speed => Math.Sqrt(VelocityX * VelocityX + VelocityY * VelocityY);
    }

    /// <summary>
    /// Throws the ball into the opening ring.
    ///
    /// Engine-free and seeded: a run that cannot be replayed cannot be told apart from luck, and
    /// "it started badly that one time" is not a bug report anyone can act on. Position and
    /// direction are drawn independently, so a ball can start flying straight at the wall it is
    /// nearest to — which is a legitimate opening, not one worth filtering out.
    /// </summary>
    public static class BallLaunch
    {
        public static LaunchPlan For(int seed, double innerRadius, double outerRadius, double speed)
        {
            if (outerRadius < innerRadius) throw new ArgumentOutOfRangeException(nameof(outerRadius));
            if (speed < 0.0) throw new ArgumentOutOfRangeException(nameof(speed));

            var random = new Random(seed);

            double placeAngle = random.NextDouble() * 2.0 * Math.PI;
            double radius = innerRadius + random.NextDouble() * (outerRadius - innerRadius);
            double headAngle = random.NextDouble() * 2.0 * Math.PI;

            return new LaunchPlan(
                Math.Cos(placeAngle) * radius, Math.Sin(placeAngle) * radius,
                Math.Cos(headAngle) * speed, Math.Sin(headAngle) * speed);
        }

        /// <summary>
        /// Where the ball may start: a thin band just inside the opening ring's wall, touching
        /// nothing. Starting near the wall rather than anywhere in the disc means the first
        /// flight crosses the whole opening, which is what the run should look like; starting
        /// near the centre would have the ball drift over the red circle before it ever moves.
        /// </summary>
        public static (double Inner, double Outer) BandFor(in TunnelSettings settings)
        {
            double outer = settings.TargetRadius - settings.Thickness * 0.5 - settings.BallRadius;
            double inner = outer - settings.BallRadius * 0.5;
            return (inner < 0.0 ? 0.0 : inner, outer);
        }
    }
}
