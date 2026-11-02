using System.Collections.Generic;
using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>One row of the difficulty table: what the game asks for once this many rings are cleared.</summary>
    [System.Serializable]
    public struct DifficultyAnchor
    {
        [Tooltip("Rings cleared at which this row applies.")]
        public int rings;
        [Tooltip("Corridor width between neighbouring rings. Tightens as a run goes on.")]
        public float clearance;
        [Tooltip("Seconds a ring is meant to last. The shrink speed is derived from it and the step.")]
        public float ringInterval;
        [Tooltip("Ceiling on the retraction after a pass. Separate from the timer on purpose.")]
        public float maxCompressionSpeed;
        [Tooltip("Clear opening in world units, measured at the target radius. The angle is held from there.")]
        public float gapArcWidth;
        [Tooltip("How fast a ring turns itself.")]
        public float autoSpinDeg;
    }

    /// <summary>
    /// The balance table, as an asset.
    ///
    /// It used to be a static array in DifficultyCurve, which made it the one set of balance
    /// numbers in the project that could not be tuned without a recompile — against the rule in
    /// CLAUDE.md that numbers live in a ScriptableObject. The curve still owns the shape between
    /// rows; this owns the rows.
    /// </summary>
    [CreateAssetMenu(menuName = "Hypnotic Rings/Difficulty Table", fileName = "DifficultyTable")]
    public sealed class DifficultyTable : ScriptableObject
    {
        [Tooltip("In ascending order of rings. The last row is the ceiling: past it nothing gets harder.")]
        public DifficultyAnchor[] anchors =
        {
            new DifficultyAnchor { rings = 0,  clearance = 1.10f, ringInterval = 3.00f, maxCompressionSpeed = 2.0f, gapArcWidth = 4.32f, autoSpinDeg = 0f },
            new DifficultyAnchor { rings = 10, clearance = 1.05f, ringInterval = 2.45f, maxCompressionSpeed = 3.0f, gapArcWidth = 3.90f, autoSpinDeg = 15f },
            new DifficultyAnchor { rings = 30, clearance = 0.96f, ringInterval = 1.93f, maxCompressionSpeed = 4.0f, gapArcWidth = 3.48f, autoSpinDeg = 40f },
            new DifficultyAnchor { rings = 80, clearance = 0.72f, ringInterval = 1.59f, maxCompressionSpeed = 5.0f, gapArcWidth = 2.80f, autoSpinDeg = 70f },
        };

        /// <summary>The rows in the engine-free shape the curve reads.</summary>
        public List<CurveAnchor> ToAnchors()
        {
            var rows = new List<CurveAnchor>(anchors.Length);
            for (int i = 0; i < anchors.Length; i++)
                rows.Add(new CurveAnchor(anchors[i].rings, anchors[i].clearance,
                                         anchors[i].ringInterval, anchors[i].maxCompressionSpeed,
                                         anchors[i].gapArcWidth, anchors[i].autoSpinDeg));
            return rows;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var problems = DifficultyCurve.Validate(ToAnchors());
            if (problems.Count > 0)
                Debug.LogWarning($"[HR] {name} is not a usable table:\n  " + string.Join("\n  ", problems), this);
        }
#endif
    }
}
