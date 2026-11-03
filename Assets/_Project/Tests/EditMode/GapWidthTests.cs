using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace HR.Tests.EditMode
{
    public sealed class GapWidthTests
    {
        public const double MinClearBalls = GapSplit.MinClearBalls;

        static GameConfig Config() => ScriptableObject.CreateInstance<GameConfig>();

        static double Floor(in DifficultySnapshot difficulty) =>
            GapSplit.MinGapDegFor(difficulty.BallRadius, difficulty.Thickness,
                                  difficulty.ReferenceRadius);

        [Test]
        public void Three_openings_are_asked_for_and_never_granted()
        {
            var config = Config();
            try
            {
                int threes = 0, twos = 0;
                for (int score = 0; score <= 200; score += 10)
                {
                    var difficulty = new DifficultyCurve(config.ToSettings()).SnapshotAt(score);
                    var generator = new RingGenerator(seed: 4096 + score);

                    for (int i = 0; i < 400; i++)
                    {
                        int count = generator.Next(difficulty, 0.0).Gaps.Count;
                        if (count == 3) threes++;
                        if (count == 2) twos++;
                    }
                }

                Assert.Greater(twos, 0, "two openings never came out either — the sweep is broken");
                Assert.AreEqual(0, threes,
                    $"{threes} three-gap rings came out; the floor used to refuse all of them");
            }
            finally { Object.DestroyImmediate(config); }
        }

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
