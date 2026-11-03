using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// How wide a way out actually is, in balls.
    ///
    /// Reported from the phone: a ring with more than one opening is almost impossible to leave.
    /// Everything about the split was expressed in degrees and shares of a single gap's angle,
    /// which says nothing about whether a ball fits — the only unit that answers that is the
    /// ball's own diameter, and nothing in the project was measuring in it.
    /// </summary>
    public sealed class GapWidthTests
    {
        /// <summary>
        /// Narrowest way out worth cutting, measured in ball diameters.
        ///
        /// Below this the ball can only pass dead-on. It is not a theoretical floor — at 1.0 the
        /// opening is exactly the ball, which admits it only on a path through the centre of the
        /// gap with no angle and no spin at all.
        /// </summary>
        public const double MinClearBalls = GapSplit.MinClearBalls;

        static GameConfig Config() => ScriptableObject.CreateInstance<GameConfig>();

        static double Floor(in DifficultySnapshot difficulty) =>
            GapSplit.MinGapDegFor(difficulty.BallRadius, difficulty.Thickness,
                                  difficulty.ReferenceRadius);

        /// <summary>The clear width of each opening on a ring of this many gaps, in balls.</summary>
        static double[] ClearancesAt(GameConfig config, int score, int gapCount)
        {
            var difficulty = new DifficultyCurve(config.ToSettings()).SnapshotAt(score);

            double single = GapSplit.AngleFor(difficulty.GapArcWidth,
                                              difficulty.Thickness, difficulty.ReferenceRadius);
            var angles = new double[RingGaps.MaxGaps];
            GapSplit.Split(single, gapCount, angles, Floor(difficulty));

            var clear = new double[gapCount];
            for (int i = 0; i < gapCount; i++)
            {
                double opening = GapSplit.OpeningFor(angles[i], difficulty.Thickness,
                                                     difficulty.ReferenceRadius);
                clear[i] = opening / (config.ballRadius * 2.0);
            }

            return clear;
        }

        [Test]
        public void Report_how_wide_every_kind_of_ring_opens()
        {
            // A measurement rather than an assertion, kept because the numbers are the argument:
            // the shares were chosen in degrees, and this is the only place they are stated in
            // the unit the player experiences.
            var config = Config();
            try
            {
                var report = new System.Text.StringBuilder("[HR] gap widths, in ball diameters\n");
                foreach (int score in new[] { 0, 10, 25, 50, 100, 200 })
                {
                    report.Append($"  score {score,3}: ");
                    for (int count = 1; count <= 3; count++)
                    {
                        var clear = ClearancesAt(config, score, count);
                        report.Append($"{count}gap[");
                        for (int i = 0; i < clear.Length; i++)
                            report.Append(i == 0 ? $"{clear[i]:F2}" : $" {clear[i]:F2}");
                        report.Append("]  ");
                    }
                    report.Append('\n');
                }

                Debug.Log(report.ToString());
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void Report_what_each_candidate_floor_would_allow()
        {
            // The decision table. A floor in ball diameters decides two things at once — how
            // wide the narrowest way out is, and how many ways out a ring can afford at all —
            // and the second one is the part that is easy to choose by accident.
            var config = Config();
            try
            {
                var report = new System.Text.StringBuilder(
                    "[HR] what each floor allows (floor in balls -> widest gap count per score)\n");

                foreach (double floorBalls in new[] { 1.61, 1.8, 2.0, 2.2, 2.4 })
                {
                    report.Append($"  floor {floorBalls:F2}: ");
                    foreach (int score in new[] { 0, 25, 50, 100, 200 })
                    {
                        var difficulty = new DifficultyCurve(config.ToSettings()).SnapshotAt(score);
                        double single = GapSplit.AngleFor(difficulty.GapArcWidth,
                            difficulty.Thickness, difficulty.ReferenceRadius);

                        int best = 1;
                        double narrowest = 0.0;
                        for (int count = 3; count >= 1; count--)
                        {
                            var angles = new double[RingGaps.MaxGaps];
                            GapSplit.Split(single, count, angles, Floor(difficulty));

                            double worst = double.MaxValue;
                            for (int i = 0; i < count; i++)
                            {
                                double balls = GapSplit.OpeningFor(angles[i], difficulty.Thickness,
                                    difficulty.ReferenceRadius) / (config.ballRadius * 2.0);
                                if (balls < worst) worst = balls;
                            }

                            if (worst >= floorBalls) { best = count; narrowest = worst; break; }
                        }

                        report.Append($"s{score}={best}gap@{narrowest:F2}  ");
                    }
                    report.Append('\n');
                }

                Debug.Log(report.ToString());
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void Report_what_the_generator_actually_emits()
        {
            // The split maths says what a ring COULD be cut into; the generator decides what it
            // is. Those are different questions and only the second one is the game — a floor
            // that widens the narrowest opening also makes the generator refuse that shape, and
            // reading the first number alone would miss it entirely.
            var config = Config();
            try
            {
                var report = new System.Text.StringBuilder(
                    "[HR] what the generator emits, 600 rings per score\n");

                foreach (int score in new[] { 0, 10, 25, 50, 100, 200 })
                {
                    var difficulty = new DifficultyCurve(config.ToSettings()).SnapshotAt(score);
                    var generator = new RingGenerator(seed: 20261103 + score);

                    var counts = new int[4];
                    double narrowest = double.MaxValue;
                    for (int i = 0; i < 600; i++)
                    {
                        var spec = generator.Next(difficulty, 0.0);
                        counts[spec.Gaps.Count]++;

                        for (int g = 0; g < spec.Gaps.Count; g++)
                        {
                            double balls = spec.Gaps[g].Opening / (config.ballRadius * 2.0);
                            if (balls < narrowest) narrowest = balls;
                        }
                    }

                    report.Append($"  score {score,3}: 1gap {counts[1],3}  2gap {counts[2],3}  "
                                  + $"3gap {counts[3],3}   narrowest {narrowest:F2} balls\n");
                }

                Debug.Log(report.ToString());
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void Every_opening_admits_the_ball_with_room_to_aim()
        {
            // The gate the report above exists to defend. Checked on every ring shape at every
            // point of the curve, because the complaint was not about one score — it was about
            // every ring that had more than one way out.
            var config = Config();
            try
            {
                for (int score = 0; score <= 200; score += 5)
                for (int count = 1; count <= 3; count++)
                {
                    var clear = ClearancesAt(config, score, count);
                    for (int i = 0; i < clear.Length; i++)
                        Assert.GreaterOrEqual(clear[i], MinClearBalls,
                            $"score {score}, {count} gaps: opening {i} is {clear[i]:F2} balls wide");
                }
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}
