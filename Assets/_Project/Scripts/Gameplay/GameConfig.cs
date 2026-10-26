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
        [Header("Tunnel geometry — 1 unit is the ball's diameter")]
        [Tooltip("Ring thickness. Physical equals visual, in every theme, always.")]
        public float thickness = 0.30f;
        [Tooltip("Corridor clearance between neighbouring rings.")]
        public float clearance = 1.25f;
        [Tooltip("Radius the ball's ring settles at between passes.")]
        public float targetRadius = 3.0f;
        [Tooltip("Rings without the ball inside fade out below this radius.")]
        public float vanishRadius = 0.9f;
        [Tooltip("Rings kept outside the ball's ring.")]
        public int ringsAhead = 3;

        [Header("Ball")]
        public float ballRadius = 0.5f;
        public float maxBallSpeed = 12f;
        [Range(0f, 1f)] public float ballFriction = 0.25f;
        [Range(0f, 1f)] public float ballBounce = 0.2f;
        [Range(0f, 1f)] public float ringFriction = 0.25f;
        [Range(0f, 1f)] public float ringBounce = 0f;

        [Header("Pass and crush")]
        [Tooltip("Extra distance past a ring's outer edge before it counts as cleared.")]
        public float passMargin = 0.3f;
        [Tooltip("Slack before the closing cavity counts as a crush.")]
        public float crushMargin = 0.05f;

        [Header("Compression")]
        [Tooltip("Speed the whole tunnel closes at in equilibrium.")]
        public float baseCompressionSpeed = 0.15f;
        [Tooltip("Pull that retracts the tunnel after a pass.")]
        public float pullToCentre = 3.0f;
        public float maxCompressionSpeed = 4.0f;

        [Header("Gaps")]
        [Tooltip("Narrowest opening any difficulty curve may produce.")]
        public float minGapWidth = 1.5f;
        [Tooltip("Angular speed a player can comfortably sustain.")]
        public float comfortSpinDeg = 200f;

        [Header("Tessellation")]
        [Tooltip("Finest angular step used to approximate an arc.")]
        public float maxArcStepDeg = 7.5f;
        [Tooltip("Widest arc a single collider may span.")]
        public float maxSegmentSweepDeg = 120f;
        [Tooltip("Points per rounded end cap. Zero gives square ends the ball can catch on.")]
        public int capPoints = 4;

        [Header("Compression strategy — chosen by the phase 1 benchmark")]
        public CompressionMode compressionMode = CompressionMode.RebuildOnThreshold;
        [Tooltip("Radius change that triggers a rebuild in RebuildOnThreshold mode.")]
        public float rebuildThreshold = 0.02f;

        public TunnelSettings ToSettings() => new TunnelSettings(
            thickness, clearance, targetRadius, vanishRadius, ballRadius, crushMargin,
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
