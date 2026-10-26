using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using HR.Core;
using HR.Gameplay;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HR.Editor
{
    /// <summary>
    /// Measures the three ways a ring can follow its shrinking radius (plan, phase 1). Reports
    /// cost *and* geometric error, because the cheapest option is only interesting if the tunnel
    /// it produces is the one the contract describes.
    ///
    /// Writes a markdown table to .artifacts/ so the numbers in DECISIONS come from a file
    /// rather than from memory.
    /// </summary>
    public static class CompressionBenchmark
    {
        const int Rings = 6;
        const int Steps = 2000;
        const float Dt = 1f / 90f;
        const string ReportPath = "docs/benchmarks/compression-benchmark.md";

        struct Result
        {
            public CompressionMode Mode;
            public double MicrosecondsPerStep;
            public double ThicknessError;
            public double OpeningError;
            public double MaxRadiusLag;
            public double FinalRadius;
        }

        [MenuItem("Hypnotic Rings/Benchmarks/Compression")]
        public static void RunFromMenu() => Run();

        public static void CIRun()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError($"[HR] benchmark failed: {e}"); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            var config = ConfigBuilder.Load();
            var settings = config.ToSettings();

            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;

            var results = new List<Result>();
            try
            {
                foreach (CompressionMode mode in Enum.GetValues(typeof(CompressionMode)))
                    results.Add(Measure(config, settings, mode));
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
            }

            string report = Format(results, settings);
            Directory.CreateDirectory("docs/benchmarks");
            File.WriteAllText(ReportPath, report);
            Debug.Log("[HR] compression benchmark\n" + report);
        }

        static Result Measure(GameConfig config, in TunnelSettings settings, CompressionMode mode)
        {
            var original = config.compressionMode;
            config.compressionMode = mode;

            var root = new GameObject($"Bench_{mode}");
            var rings = new RingController[Rings];
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);

            try
            {
                for (int i = 0; i < Rings; i++)
                {
                    rings[i] = TunnelFactory.CreateRing(config, root.transform, material, i);
                    rings[i].Configure(settings.TargetRadius + settings.Step * i,
                                       270.0, settings.MinGapWidth, 20.0);
                }

                // Shrink from the starting radius down towards the crush radius, which is the
                // whole range the game ever uses.
                double span = settings.TargetRadius - settings.CrushRadius;
                double perStep = span / Steps;

                double maxLag = 0.0;
                var watch = Stopwatch.StartNew();
                for (int step = 0; step < Steps; step++)
                {
                    double delta = perStep * (step + 1);
                    for (int i = 0; i < Rings; i++)
                    {
                        double target = settings.TargetRadius + settings.Step * i - delta;
                        if (target > settings.Thickness) rings[i].SetRadius(target);
                        rings[i].StepRotation(0.0, Dt);
                        if (rings[i].RadiusLag > maxLag) maxLag = rings[i].RadiusLag;
                    }
                    Physics2D.Simulate(Dt);
                }
                watch.Stop();

                // Error is read from the built colliders, not computed from the mode.
                double thickness = rings[0].MeasureWorldThickness();
                double opening = rings[0].MeasureWorldGapArcLength();

                return new Result
                {
                    Mode = mode,
                    MicrosecondsPerStep = watch.Elapsed.TotalMilliseconds * 1000.0 / Steps,
                    ThicknessError = Math.Abs(thickness - settings.Thickness),
                    OpeningError = Math.Abs(opening - settings.MinGapWidth),
                    MaxRadiusLag = maxLag,
                    FinalRadius = rings[0].Radius,
                };
            }
            finally
            {
                config.compressionMode = original;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        static string Format(List<Result> results, in TunnelSettings settings)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;

            sb.AppendLine("# Бенчмарк способов сжатия колец");
            sb.AppendLine();
            sb.AppendLine($"Условия: {Rings} колец, {Steps} шагов по {Dt:F5} с, радиус едет от "
                          + $"{settings.TargetRadius.ToString("F2", ci)} до "
                          + $"{settings.CrushRadius.ToString("F2", ci)} u. Ошибки измерены с "
                          + "построенного коллайдера, а не посчитаны из режима.");
            sb.AppendLine();
            sb.AppendLine("| Режим | мкс/шаг | Ошибка толщины, u | Ошибка просвета, u | Отставание радиуса, u |");
            sb.AppendLine("|---|---:|---:|---:|---:|");

            foreach (var r in results)
                sb.AppendLine($"| `{r.Mode}` | {r.MicrosecondsPerStep.ToString("F1", ci)} | "
                              + $"{r.ThicknessError.ToString("F4", ci)} | "
                              + $"{r.OpeningError.ToString("F4", ci)} | "
                              + $"{r.MaxRadiusLag.ToString("F4", ci)} |");

            sb.AppendLine();
            sb.AppendLine($"Допуск: толщина {settings.Thickness.ToString("F2", ci)} u, "
                          + $"просвет {settings.MinGapWidth.ToString("F2", ci)} u, "
                          + $"запас коридора {(settings.Clearance - settings.BallRadius * 2).ToString("F2", ci)} u. "
                          + "Ошибка в доли u — это уже заметная доля шарика диаметром 1 u.");
            sb.AppendLine();
            sb.AppendLine("Два первых столбца ошибок читаются с **построенной** геометрии, поэтому "
                          + "у режимов с перестроением они нулевые по определению. Настоящая ошибка "
                          + "порогового режима — последний столбец: насколько коллайдер отстал от "
                          + "радиуса, на котором кольцо должно быть прямо сейчас.");
            return sb.ToString();
        }
    }
}
