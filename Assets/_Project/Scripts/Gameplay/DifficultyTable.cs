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
        [Tooltip("Shrink speed in equilibrium. This is the timer: budget = (target - T/2 - red) / it.")]
        public float compressionSpeed;
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
            new DifficultyAnchor { rings = 0,  compressionSpeed = 0.45f, maxCompressionSpeed = 2.0f, gapArcWidth = 4.10f, autoSpinDeg = 0f },
            new DifficultyAnchor { rings = 10, compressionSpeed = 0.55f, maxCompressionSpeed = 3.0f, gapArcWidth = 3.70f, autoSpinDeg = 15f },
            new DifficultyAnchor { rings = 30, compressionSpeed = 0.70f, maxCompressionSpeed = 4.0f, gapArcWidth = 3.27f, autoSpinDeg = 40f },
            new DifficultyAnchor { rings = 80, compressionSpeed = 0.85f, maxCompressionSpeed = 5.0f, gapArcWidth = 2.60f, autoSpinDeg = 70f },
        };

        /// <summary>The rows in the engine-free shape the curve reads.</summary>
        public List<CurveAnchor> ToAnchors()
        {
            var rows = new List<CurveAnchor>(anchors.Length);
            for (int i = 0; i < anchors.Length; i++)
                rows.Add(new CurveAnchor(anchors[i].rings, anchors[i].compressionSpeed,
                                         anchors[i].maxCompressionSpeed, anchors[i].gapArcWidth,
                                         anchors[i].autoSpinDeg));
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
