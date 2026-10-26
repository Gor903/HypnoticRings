using System;

namespace HR.Core
{
    /// <summary>
    /// What the gesture needs to know, engine-free. Exists so HR.Input can stay independent of
    /// HR.Gameplay: the input layer reads a struct, not the config asset (CONTRACT.md §6).
    /// </summary>
    public readonly struct GestureSettings
    {
        public readonly double Sensitivity;
        public readonly double MaxSpeedDeg;
        public readonly double InertiaDecay;
        public readonly double DeadZoneRadius;
        public readonly bool Inverted;

        public GestureSettings(double sensitivity, double maxSpeedDeg, double inertiaDecay,
                               double deadZoneRadius, bool inverted)
        {
            Sensitivity = sensitivity;
            MaxSpeedDeg = maxSpeedDeg;
            InertiaDecay = inertiaDecay;
            DeadZoneRadius = deadZoneRadius;
            Inverted = inverted;
        }

        public static GestureSettings Default => new GestureSettings(1.0, 540.0, 5.0, 0.6, false);

        public GestureSettings WithInverted(bool inverted) =>
            new GestureSettings(Sensitivity, MaxSpeedDeg, InertiaDecay, DeadZoneRadius, inverted);
    }

    /// <summary>
    /// Turns finger positions into the shared rotation angle every ring follows.
    ///
    /// Engine-free, and positions arrive in world units relative to the tunnel centre, so the
    /// awkward parts — the wrap at ±180, the singularity at the centre, what a released finger
    /// leaves behind — are testable without input hardware or a scene.
    ///
    /// CONTRACT.md §2.10 for the parameters, §6.3 for the cases that must not break.
    /// </summary>
    public sealed class GestureTracker
    {
        readonly double _sensitivity;
        readonly double _maxSpeedDeg;
        readonly double _inertiaDecay;
        readonly double _deadZoneRadius;
        readonly bool _inverted;

        bool _tracking;
        double _lastPointerAngleDeg;

        public GestureTracker(in GestureSettings settings)
        {
            if (settings.Sensitivity <= 0.0) throw new ArgumentOutOfRangeException(nameof(settings));
            if (settings.MaxSpeedDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(settings));
            if (settings.InertiaDecay < 0.0) throw new ArgumentOutOfRangeException(nameof(settings));
            if (settings.DeadZoneRadius < 0.0) throw new ArgumentOutOfRangeException(nameof(settings));

            _sensitivity = settings.Sensitivity;
            _maxSpeedDeg = settings.MaxSpeedDeg;
            _inertiaDecay = settings.InertiaDecay;
            _deadZoneRadius = settings.DeadZoneRadius;
            _inverted = settings.Inverted;
        }

        /// <summary>The angle every ring adds to its own spin.</summary>
        public double AngleDeg { get; private set; }

        /// <summary>Current angular speed, in degrees per second. Survives a release as inertia.</summary>
        public double VelocityDeg { get; private set; }

        /// <summary>True while a finger is down and outside the dead zone.</summary>
        public bool IsTracking => _tracking;

        /// <summary>
        /// A finger went down. Inside the dead zone it is ignored rather than tracked: near the
        /// centre a pixel of movement is a huge change of angle, and the ring would snap.
        /// </summary>
        public void Begin(double x, double y)
        {
            if (InsideDeadZone(x, y)) { _tracking = false; return; }
            _tracking = true;
            _lastPointerAngleDeg = AngleOf(x, y);
        }

        /// <summary>
        /// The finger moved. Returns the angle applied this step.
        ///
        /// A sample that strays into the dead zone is dropped and tracking is suspended rather
        /// than re-anchored, so dragging through the centre and out the other side does not
        /// produce a jump — it resumes from wherever the finger reappears.
        /// </summary>
        public double Move(double x, double y, double dt)
        {
            if (dt <= 0.0) return 0.0;

            if (InsideDeadZone(x, y))
            {
                _tracking = false;
                return 0.0;
            }

            double pointerAngle = AngleOf(x, y);

            if (!_tracking)
            {
                // Re-anchor without turning anything: this sample only establishes where we are.
                _tracking = true;
                _lastPointerAngleDeg = pointerAngle;
                return 0.0;
            }

            // Shortest way round, so a flick across the ±180 seam reads as a small step forward
            // rather than most of a turn backwards.
            double delta = TunnelMath.ShortestDeltaDeg(_lastPointerAngleDeg, pointerAngle);
            _lastPointerAngleDeg = pointerAngle;

            delta *= _sensitivity;
            if (_inverted) delta = -delta;

            double maxDelta = _maxSpeedDeg * dt;
            if (delta > maxDelta) delta = maxDelta;
            else if (delta < -maxDelta) delta = -maxDelta;

            AngleDeg += delta;
            VelocityDeg = delta / dt;
            return delta;
        }

        /// <summary>The finger lifted, or left the screen. Whatever it was doing carries on and decays.</summary>
        public void End()
        {
            _tracking = false;
        }

        /// <summary>
        /// The gesture is void — paused, backgrounded, focus lost. Unlike End this throws the
        /// momentum away: coming back to a tunnel still spinning from a gesture made before the
        /// phone call is not a gesture the player made.
        /// </summary>
        public void Cancel()
        {
            _tracking = false;
            VelocityDeg = 0.0;
        }

        /// <summary>
        /// Advances inertia. Call every step; it does nothing while a finger is tracking, because
        /// then the finger is the authority.
        /// </summary>
        public void Tick(double dt)
        {
            if (_tracking || dt <= 0.0) return;
            if (VelocityDeg == 0.0) return;

            AngleDeg += VelocityDeg * dt;
            VelocityDeg *= Math.Exp(-_inertiaDecay * dt);

            // Below a fraction of a degree per second there is nothing left to see, and letting
            // it run forever keeps the tunnel imperceptibly drifting for the rest of the session.
            if (Math.Abs(VelocityDeg) < 0.01) VelocityDeg = 0.0;
        }

        /// <summary>Back to a standing start — a new run, not a new gesture.</summary>
        public void Reset()
        {
            AngleDeg = 0.0;
            VelocityDeg = 0.0;
            _tracking = false;
            _lastPointerAngleDeg = 0.0;
        }

        bool InsideDeadZone(double x, double y) =>
            x * x + y * y < _deadZoneRadius * _deadZoneRadius;

        static double AngleOf(double x, double y) => Math.Atan2(y, x) * TunnelMath.DegPerRad;
    }
}
