using System;

namespace HR.Core
{
    /// <summary>
    /// Counts how many times a ball has been turned back by a ring.
    ///
    /// The hardcore rule is "one touch per ring, a second one ends the run", and the whole
    /// difficulty of it is deciding what a touch is. The obvious answer — a collision event —
    /// is wrong here, and not marginally: a ring is cut into collider segments of up to 120°
    /// with rounded ends, so a single knock on the seam between two of them raises two events.
    /// A player would die for one mistake they made once, and nothing on screen would explain
    /// it. See plan [H-04].
    ///
    /// So a touch is defined by what the ball DID, not by what the physics engine reported: the
    /// moment its radial velocity flips from outward to inward. That has a second property
    /// worth having — a graze along the edge of an opening on the way out is not a touch at all,
    /// because the ball never turned back. Only a graze that actually sent it inward counts.
    ///
    /// Engine-free so the rule can be driven to an exact moment instead of waited for.
    /// </summary>
    public sealed class TouchLedger
    {
        /// <summary>
        /// Shortest gap between two touches of the same ring, in seconds.
        ///
        /// Guards the seam between two collider segments, where one physical knock can produce
        /// two reversals a millisecond apart. Long enough to swallow that, short enough that two
        /// real bounces off opposite walls of a corridor are never merged.
        /// </summary>
        public const double DebounceSeconds = 0.060;

        /// <summary>
        /// Radial speed below which a reversal is not a touch, in units per second.
        ///
        /// A ball sliding along a wall crosses zero radial velocity constantly without ever
        /// being turned back by anything. Without a floor the ledger would count the noise.
        /// </summary>
        public const double MinReversal = 0.05;

        int _ringId = -1;
        int _touches;
        double _lastTouchTime = double.NegativeInfinity;
        double _previousRadialVelocity;
        bool _seenAnything;

        /// <summary>Touches recorded against the ring currently being watched.</summary>
        public int Touches => _touches;

        /// <summary>Which ring this ledger is counting for, or -1.</summary>
        public int RingId => _ringId;

        /// <summary>
        /// Starts counting for a ring. Counts live on the RING, not on the run: the ring the
        /// ball leaves dissolves, and its tally goes with it (plan [H-05]).
        /// </summary>
        public void Watch(int ringId)
        {
            if (ringId == _ringId) return;

            _ringId = ringId;
            _touches = 0;
            _lastTouchTime = double.NegativeInfinity;
            _seenAnything = false;
        }

        public void Reset()
        {
            _ringId = -1;
            _touches = 0;
            _lastTouchTime = double.NegativeInfinity;
            _seenAnything = false;
            _previousRadialVelocity = 0.0;
        }

        /// <summary>
        /// One step of watching. Returns true on the step a touch is recorded.
        ///
        /// <paramref name="radialVelocity"/> is positive outward. <paramref name="inContact"/>
        /// says whether the ball is against this ring's collider — a reversal in open space is
        /// gravity or the speed regulator, not a wall.
        /// </summary>
        public bool Step(double time, double radialVelocity, bool inContact)
        {
            double previous = _previousRadialVelocity;
            _previousRadialVelocity = radialVelocity;

            // The first sample has nothing to compare against; treating zero as "was moving
            // outward" would invent a touch on the step the ledger started.
            if (!_seenAnything)
            {
                _seenAnything = true;
                return false;
            }

            if (!inContact) return false;
            if (_ringId < 0) return false;

            // Outward, then inward. Not "is moving inward": a ball that spends ten steps
            // travelling inward would otherwise be touching on every one of them.
            bool reversed = previous > 0.0 && radialVelocity < 0.0;
            if (!reversed) return false;

            // How hard it was turned, not how fast it is going: a long slide along a wall
            // crosses zero constantly and is turned back by nothing.
            double strength = Math.Abs(previous) + Math.Abs(radialVelocity);
            if (strength < MinReversal) return false;

            if (time - _lastTouchTime < DebounceSeconds) return false;

            _lastTouchTime = time;
            _touches++;
            return true;
        }

        /// <summary>
        /// Whether the ring has been touched more than the rule allows.
        ///
        /// Reading "not more than one" rather than "exactly one" — plan [H-02]. A ball that
        /// leaves through an opening without touching anything is a clean pass and the best
        /// thing that can happen, not a failure. Punishing it would mean punishing geometry.
        /// </summary>
        public bool IsOver(int allowed) => _touches > allowed;
    }
}
