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
        [Range(0f, 1f)]
        [Tooltip("Chance a ring is cut with more than one opening. The openings share less angle than one would get.")]
        public float multiGapChance;
        [Range(0f, 1f)]
        [Tooltip("Of the multi-gap rings, the share cut with three openings rather than two.")]
        public float tripleShare;
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
            new DifficultyAnchor { rings = 0,  clearance = 1.10f, ringInterval = 3.00f, maxCompressionSpeed = 2.0f, gapArcWidth = 4.32f, autoSpinDeg = 0f,  multiGapChance = 0.00f, tripleShare = 0.00f },
            new DifficultyAnchor { rings = 10, clearance = 1.05f, ringInterval = 2.45f, maxCompressionSpeed = 3.0f, gapArcWidth = 3.90f, autoSpinDeg = 15f, multiGapChance = 0.25f, tripleShare = 0.00f },
            new DifficultyAnchor { rings = 30, clearance = 0.96f, ringInterval = 1.93f, maxCompressionSpeed = 4.0f, gapArcWidth = 3.48f, autoSpinDeg = 40f, multiGapChance = 0.36f, tripleShare = 0.25f },
            new DifficultyAnchor { rings = 80, clearance = 0.72f, ringInterval = 1.59f, maxCompressionSpeed = 5.0f, gapArcWidth = 2.80f, autoSpinDeg = 70f, multiGapChance = 0.40f, tripleShare = 0.25f },
        };

        /// <summary>
        /// The hardcore table, built rather than authored as a second asset.
        ///
        /// Its rows are not a variation on the ordinary ones: compression is off entirely, the
        /// openings are far wider, and multi-gap rings arrive early and often. An asset would
        /// invite someone to tune one table and forget the other; a function cannot drift from
        /// the reasoning that produced it, which is written right here. Plan [H-10].
        /// </summary>
        public static List<CurveAnchor> HardcoreAnchors()
        {
            // Compression is zero, so a ring never closes on the centre by itself: the clock a
            // player races in hardcore is the ball's flight, not the tunnel's squeeze ([H-08]).
            // The interval is still what the curve divides by, so it is set long enough that
            // the derived speed is a rounding error rather than a squeeze.
            const float Still = 600f;

            return new List<CurveAnchor>
            {
                //           n    c     Δt      v_max  w      spin  multi  triple
                new CurveAnchor(0,  1.10, Still, 1.5, 6.60, 0.0, 0.00, 0.00),
                new CurveAnchor(4,  1.10, Still, 1.5, 6.00, 0.0, 0.00, 0.00),
                new CurveAnchor(10, 1.10, Still, 1.5, 5.10, 0.0, 0.30, 0.00),
                new CurveAnchor(30, 1.10, Still, 1.5, 4.20, 20.0, 0.35, 0.25),
                new CurveAnchor(80, 1.10, Still, 1.5, 3.30, 40.0, 0.35, 0.25),
            };
        }

        /// <summary>The rows in the engine-free shape the curve reads.</summary>
        public List<CurveAnchor> ToAnchors()
        {
            var rows = new List<CurveAnchor>(anchors.Length);
            for (int i = 0; i < anchors.Length; i++)
                rows.Add(new CurveAnchor(anchors[i].rings, anchors[i].clearance,
                                         anchors[i].ringInterval, anchors[i].maxCompressionSpeed,
                                         anchors[i].gapArcWidth, anchors[i].autoSpinDeg,
                                         anchors[i].multiGapChance, anchors[i].tripleShare));
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
