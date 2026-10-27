using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using HR.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HR.Editor
{
    /// <summary>
    /// Runs the difficulty sweep the plan asks for: thousands of runs per skill level, seeded so
    /// the whole thing replays. Writes raw results; tools/plot_balance.py turns them into the
    /// report and the charts.
    ///
    /// Splitting it that way is deliberate — the simulation has to be the game's own code, and
    /// the plotting has to be able to change without re-running an hour of simulation.
    /// </summary>
    public static class BalanceReport
    {
        public const int RunsPerLevel = 5000;
        const int MaxRings = 300;
        const string RawPath = "docs/benchmarks/balance-raw.csv";
        const string CurvePath = "docs/benchmarks/balance-curve.csv";

        [MenuItem("Hypnotic Rings/Benchmarks/Balance sweep")]
        public static void RunFromMenu() => Run();

        /// <summary>Per-ring detail for one run. For understanding a result, not for reporting it.</summary>
        public static void CIDiagnose()
        {
            try
            {
                var config = ConfigBuilder.Load();
                var settings = config.ToSettings();
                var curve = new DifficultyCurve(settings);
                var simulator = new BalanceSimulator(settings, curve);
                var bot = BotBrain.Strong(1);

                Debug.Log($"[DIAG] fall needs {simulator.FallSeconds:F3}s of steady alignment");
                Debug.Log("[DIAG] ring | gapW | tolDeg | spin | budget | spinCeil | outcome");

                double userAngle = 0.0, previousSpin = 0.0;
                var generator = new RingGenerator(1);
                for (int n = 0; n < 60; n++)
                {
                    var d = curve.SnapshotAt(n);
                    var spec = generator.Next(d, previousSpin);
                    previousSpin = spec.AutoSpinDeg;

                    double before = userAngle;
                    bool ok = Probe(simulator, bot, spec, d, ref userAngle, out double bestError, out double drift);
                    double ceiling = TunnelMath.MaxReachableSpinDeg(d.ComfortSpinDeg, d.TimeBudget);

                    if (n < 8 || !ok || n % 10 == 0)
                        Debug.Log($"[DIAG] {n,4} | {spec.GapArcWidth,5:F2} | {simulator.ToleranceDeg(spec.GapArcWidth),6:F2} "
                                  + $"| {spec.AutoSpinDeg,6:F1} | {d.TimeBudget,6:F2} | {ceiling,8:F1} "
                                  + $"| {(ok ? "pass" : "FAIL")} bestErr={bestError:F2} drift={drift:F1}");

                    if (!ok)
                    {
                        Debug.Log($"[DIAG] died at ring {n}; tracing that ring");
                        Trace(simulator, BotBrain.Strong(1), spec, d, before);
                        break;
                    }
                }

                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogError($"[HR] diagnose failed: {e}"); EditorApplication.Exit(1); }
        }

        static bool Probe(BalanceSimulator sim, BotBrain bot, in RingSpec spec,
                          in DifficultySnapshot d, ref double userAngle,
                          out double bestError, out double drift)
        {
            bot.Reset();
            const double dt = 1.0 / 90.0;
            double tolerance = sim.ToleranceDeg(spec.GapArcWidth);
            double ringSpin = 0.0, aligned = 0.0;
            bestError = double.MaxValue;

            for (double t = 0.0; t < d.TimeBudget; t += dt)
            {
                ringSpin += spec.AutoSpinDeg * dt;
                double gapWorld = spec.GapCentreDeg + ringSpin + userAngle;
                userAngle += bot.Decide(TunnelMath.BottomAngleDeg, gapWorld, dt);

                double error = Math.Abs(TunnelMath.ShortestDeltaDeg(
                    spec.GapCentreDeg + ringSpin + userAngle, TunnelMath.BottomAngleDeg));
                if (error < bestError) bestError = error;

                if (error <= tolerance)
                {
                    aligned += dt;
                    if (aligned >= sim.FallSeconds) { drift = bot.DriftEstimateDeg; return true; }
                }
                else aligned = 0.0;
            }

            drift = bot.DriftEstimateDeg;
            return false;
        }

        /// <summary>Error against time for one ring, to see the shape rather than the summary.</summary>
        static void Trace(BalanceSimulator sim, BotBrain bot, in RingSpec spec,
                          in DifficultySnapshot d, double userAngle)
        {
            bot.Reset();
            const double dt = 1.0 / 90.0;
            double tolerance = sim.ToleranceDeg(spec.GapArcWidth);
            double ringSpin = 0.0, aligned = 0.0, longest = 0.0;
            int step = 0;

            for (double t = 0.0; t < d.TimeBudget; t += dt, step++)
            {
                ringSpin += spec.AutoSpinDeg * dt;
                double gapWorld = spec.GapCentreDeg + ringSpin + userAngle;
                double applied = bot.Decide(TunnelMath.BottomAngleDeg, gapWorld, dt);
                userAngle += applied;

                double error = TunnelMath.ShortestDeltaDeg(
                    spec.GapCentreDeg + ringSpin + userAngle, TunnelMath.BottomAngleDeg);

                if (Math.Abs(error) <= tolerance) aligned += dt; else aligned = 0.0;
                if (aligned > longest) longest = aligned;

                if (step % 15 == 0 && step < 450)
                    Debug.Log($"[TRACE] t={t:F2} err={error,8:F2} tol={tolerance:F2} "
                              + $"step={applied * 90.0,7:F1}deg/s drift={bot.DriftEstimateDeg,7:F1} aligned={aligned:F3}");
            }

            Debug.Log($"[TRACE] longest hold {longest:F3}s, needed {sim.FallSeconds:F3}s");
        }

        public static void CIRun()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError($"[HR] balance sweep failed: {e}"); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            var config = ConfigBuilder.Load();
            var settings = config.ToSettings();
            var curve = new DifficultyCurve(settings);
            var simulator = new BalanceSimulator(settings, curve);

            Directory.CreateDirectory("docs/benchmarks");
            WriteCurve(curve, settings);

            var ci = CultureInfo.InvariantCulture;
            var raw = new StringBuilder();
            raw.AppendLine("level,seed,score,clamped,ended_on_time");

            var watch = Stopwatch.StartNew();
            foreach (string level in new[] { "weak", "medium", "strong" })
            {
                int total = 0, min = int.MaxValue, max = 0, clampedRuns = 0;

                for (int i = 0; i < RunsPerLevel; i++)
                {
                    var bot = MakeBot(level, i);
                    var result = simulator.Run(bot, seed: i, maxRings: MaxRings);

                    raw.AppendLine($"{level},{result.Seed},{result.Score},{result.ClampedRings},"
                                   + $"{(result.EndedOnTime ? 1 : 0)}");

                    total += result.Score;
                    if (result.Score < min) min = result.Score;
                    if (result.Score > max) max = result.Score;
                    if (result.ClampedRings > 0) clampedRuns++;
                }

                Debug.Log($"[HR] balance {level}: mean {(double)total / RunsPerLevel:F1}, "
                          + $"min {min}, max {max}, runs needing a clamp {clampedRuns}");
            }
            watch.Stop();

            File.WriteAllText(RawPath, raw.ToString());
            Debug.Log($"[HR] balance sweep: {RunsPerLevel} runs x 3 levels in "
                      + $"{watch.Elapsed.TotalSeconds.ToString("F1", ci)}s -> {RawPath}");
        }

        static BotBrain MakeBot(string level, int seed)
        {
            switch (level)
            {
                case "weak": return BotBrain.Weak(seed);
                case "medium": return BotBrain.Medium(seed);
                case "strong": return BotBrain.Strong(seed);
                default: throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        /// <summary>The curve itself, so the report can show what the numbers were produced from.</summary>
        static void WriteCurve(DifficultyCurve curve, in TunnelSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ring,compression,gap_width,auto_spin,time_budget,spin_ceiling");

            var ci = CultureInfo.InvariantCulture;
            for (int n = 0; n <= 120; n++)
            {
                double budget = curve.TimeBudgetAt(n);
                double ceiling = TunnelMath.MaxReachableSpinDeg(settings.ComfortSpinDeg, budget);
                sb.AppendLine(string.Join(",",
                    n.ToString(ci),
                    curve.CompressionSpeedAt(n).ToString("F4", ci),
                    curve.GapArcWidthAt(n).ToString("F4", ci),
                    curve.AutoSpinAt(n).ToString("F4", ci),
                    budget.ToString("F4", ci),
                    ceiling.ToString("F4", ci)));
            }

            File.WriteAllText(CurvePath, sb.ToString());
        }
    }
}
