using System;

namespace HR.Core
{
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

        public double AngleDeg { get; private set; }

        public double VelocityDeg { get; private set; }

        public bool IsTracking => _tracking;

        public void Begin(double x, double y)
        {
            if (InsideDeadZone(x, y)) { _tracking = false; return; }
            _tracking = true;
            _lastPointerAngleDeg = AngleOf(x, y);
        }

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
                _tracking = true;
                _lastPointerAngleDeg = pointerAngle;
                return 0.0;
            }

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

        public void End()
        {
            _tracking = false;
        }

        public void Cancel()
        {
            _tracking = false;
            VelocityDeg = 0.0;
        }

        public void Tick(double dt)
        {
            if (_tracking || dt <= 0.0) return;
            if (VelocityDeg == 0.0) return;

            AngleDeg += VelocityDeg * dt;
            VelocityDeg *= Math.Exp(-_inertiaDecay * dt);

            if (Math.Abs(VelocityDeg) < 0.01) VelocityDeg = 0.0;
        }

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
