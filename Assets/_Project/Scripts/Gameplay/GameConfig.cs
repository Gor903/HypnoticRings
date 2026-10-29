using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>How a ring's geometry follows its shrinking radius. Measured in phase 1.</summary>
    public enum CompressionMode
    {
        /// <summary>Scale the transform. Cheapest, and it scales thickness too — see the benchmark.</summary>
        TransformScale = 0,
        /// <summary>Rebuild mesh and collider every physics step. Exact, and the most work.</summary>
        RebuildEveryStep = 1,
        /// <summary>Rebuild once the radius has moved past a threshold. Cheap, slightly stale.</summary>
        RebuildOnThreshold = 2,
    }

    /// <summary>
    /// The editable source for every number the game depends on. Nothing in the gameplay code
    /// carries a literal — CLAUDE.md forbids it, and bot simulation is supposed to move these.
    /// Values are CONTRACT.md §2 starting values.
    /// </summary>
    [CreateAssetMenu(menuName = "Hypnotic Rings/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Tunnel geometry — world units; the ball is 0.54 of one (D-059)")]
        [Tooltip("Ring thickness. Physical equals visual, in every theme, always.")]
        public float thickness = 0.40f;
        [Tooltip("Corridor clearance between neighbouring rings. Its floor is the ball's diameter.")]
        public float clearance = 0.95f;
        [Tooltip("Radius the ball's ring settles at between passes. Gap widths are read here.")]
        public float targetRadius = 3.0f;
        [Tooltip("Rings without the ball inside fade out below this radius.")]
        public float vanishRadius = 1.05f;
        [Tooltip("Rings kept outside the ball's ring. Also how many of them the frame can show.")]
        public int ringsAhead = 9;

        [Header("View — CONTRACT.md G-10: the camera is static and frames the tunnel")]
        [Tooltip("Half the height the camera sees, in world units. Larger shows more rings.")]
        [Min(1f)] public float orthoSize = 10.0f;
        [Tooltip("Height of the tunnel centre on screen: 0 is the bottom edge, 1 is the top.")]
        [Range(0.35f, 0.85f)] public float centreHeightFraction = 0.72f;

        [Header("Ball — it flies, it does not roll")]
        public float ballRadius = 0.27f;
        [Tooltip("Safety ceiling, not a feel knob: a step longer than a ring is thick could pass through one.")]
        public float maxBallSpeed = 16f;
        [Tooltip("Speed the regulator holds the ball at, feeding back whatever a bounce took or gave.")]
        public float cruiseSpeed = 10f;
        [Tooltip("How hard the regulator pulls, in units per second squared.")]
        public float cruiseAccel = 5f;
        [Tooltip("Share of world gravity the ball feels. Bends the flight; must not drive it.")]
        [Range(0f, 1f)] public float ballGravityScale = 0.15f;
        [Tooltip("Zero friction and full bounce, or the flight decays into a roll.")]
        [Range(0f, 1f)] public float ballFriction = 0f;
        [Range(0f, 1f)] public float ballBounce = 1f;
        [Range(0f, 1f)] public float ringFriction = 0f;
        [Range(0f, 1f)] public float ringBounce = 1f;

        [Header("Pass and the red circle")]
        [Tooltip("Extra distance past a ring's outer edge before it counts as cleared.")]
        public float passMargin = 0.22f;
        [Tooltip("The red circle at the centre. A ring whose inner edge touches it ends the run.")]
        public float redRadius = 0.75f;
        [Tooltip("How strongly the circle is mixed over the theme's background.")]
        [Range(0.1f, 0.9f)] public float redOpacity = 0.45f;
        [Tooltip("The circle's own colour, before it is mixed over the background.")]
        public Color redColour = new Color(1f, 0.231f, 0.188f, 1f);
        [Tooltip("Seconds a ring takes to dissolve once the ball has left it.")]
        public float dissolveSeconds = 0.25f;

        [Header("Balance table")]
        [Tooltip("The difficulty rows. Without one the curve falls back to its built-in defaults.")]
        public DifficultyTable difficulty;

        [Header("Compression")]
        [Tooltip("Only the opening value. Once a run scores, the difficulty table owns this.")]
        public float baseCompressionSpeed = 0.45f;
        [Tooltip("Pull that retracts the tunnel after a pass.")]
        public float pullToCentre = 3.0f;
        [Tooltip("Highest retraction any table row may ask for. The row itself sets the live ceiling.")]
        public float maxCompressionSpeed = 5.0f;

        [Header("Gaps")]
        [Tooltip("Narrowest clear opening that still admits the ball. A shrinking ring never cuts less.")]
        public float minGapWidth = 0.80f;
        [Tooltip("Angular speed a player can comfortably sustain.")]
        public float comfortSpinDeg = 300f;

        [Header("Gesture")]
        [Tooltip("Finger arc times this is the ring arc. At 3.0 a 120 degree arc is a full turn.")]
        [Range(0.6f, 4.0f)] public float sensitivity = 3.0f;
        [Tooltip("Ceiling on how fast a gesture may turn the tunnel.")]
        public float maxUserAngularSpeedDeg = 1500f;
        [Tooltip("Exponential decay of leftover spin once the finger lifts.")]
        public float inertiaDecay = 5.0f;
        [Tooltip("Touches closer to the centre than this are ignored — there a pixel is a huge angle.")]
        public float deadZoneRadius = 0.6f;

        [Header("Tessellation")]
        [Tooltip("Finest angular step used to approximate an arc.")]
        public float maxArcStepDeg = 7.5f;
        [Tooltip("Widest arc a single collider may span.")]
        public float maxSegmentSweepDeg = 120f;
        [Tooltip("Points per rounded end cap. Zero gives square ends the ball can catch on.")]
        public int capPoints = 4;

        [Header("Effects — spec 11.4, every one off when Reduce effects is on")]
        [Tooltip("Points in the ball's trail; the spec asks for six to eight")]
        [Range(0, 12)] public int trailPoints = 8;
        [Tooltip("Seconds a trail point takes to fade out")]
        public float trailFadeSeconds = 0.22f;
        [Tooltip("Flash at the hole when a ring is passed")]
        public float passFlashSeconds = 0.12f;
        [Tooltip("Score pulse: 1.0 to this and back")]
        public float scorePulseScale = 1.15f;
        public float scorePulseSeconds = 0.15f;
        [Tooltip("Camera shake on a crush, in world units")]
        public float crushShakeAmplitude = 0.08f;
        public float crushSeconds = 0.4f;
        [Tooltip("Fade between menu and game")]
        public float transitionSeconds = 0.35f;

        [Header("Vibration — spec 11.4")]
        public int vibratePassMs = 20;
        public int vibrateLoseMs = 60;

        [Header("Compression strategy — chosen by the phase 1 benchmark")]
        public CompressionMode compressionMode = CompressionMode.RebuildOnThreshold;
        [Tooltip("Radius change that triggers a rebuild in RebuildOnThreshold mode.")]
        public float rebuildThreshold = 0.02f;

        /// <summary>Where the camera sits so the tunnel centre lands at its share of the height.</summary>
        public float CameraOffsetY => (0.5f - centreHeightFraction) * 2f * orthoSize;

        /// <summary>
        /// The player's slider scales the base gain rather than replacing it, so the setting
        /// keeps the spec's 0.6–1.6 range while the feel of 1.0 is ours to choose.
        /// </summary>
        public GestureSettings ToGestureSettings(float playerSensitivity = 1f, bool inverted = false) =>
            new GestureSettings(sensitivity * playerSensitivity, maxUserAngularSpeedDeg,
                                inertiaDecay, deadZoneRadius, inverted);

        public TunnelSettings ToSettings() => new TunnelSettings(
            thickness, clearance, targetRadius, vanishRadius, ballRadius, redRadius,
            passMargin, minGapWidth, pullToCentre, maxCompressionSpeed, comfortSpinDeg,
            maxBallSpeed, maxArcStepDeg, maxSegmentSweepDeg, ringsAhead, capPoints);

#if UNITY_EDITOR
        // OnValidate only ever runs in the editor, so it does not belong in a player build at
        // all — and leaving it there is what makes a logging check fire on shipping code.
        void OnValidate()
        {
            var problems = ToSettings().Validate();
            if (problems.Count > 0)
                Debug.LogWarning($"[HR] {name} is not playable:\n  " + string.Join("\n  ", problems), this);
        }
#endif
    }
}
