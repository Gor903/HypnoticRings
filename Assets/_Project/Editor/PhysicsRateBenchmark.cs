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
    /// Settles the fixed physics step (DECISIONS D-007). The spec starts at 1/90 s and asks for
    /// 1/60 and 1/120 to be compared; this measures all three on the thing that actually matters:
    /// how still a resting ball sits, how far it sinks into an arc, and what it costs.
    /// </summary>
    public static class PhysicsRateBenchmark
    {
        const float Seconds = 6f;
        const string ReportPath = "docs/benchmarks/physics-rate-benchmark.md";

        struct Result
        {
            public int Hz;
            public double JitterMicroUnits;
            public double MaxPenetration;
            public double MillisecondsPerSimSecond;
            public double TravelPerStep;
        }

        [MenuItem("Hypnotic Rings/Benchmarks/Physics rate")]
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

            var previous = Physics2D.simulationMode;
            var previousGravity = Physics2D.gravity;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, ProjectSetup.Gravity);

            var results = new List<Result>();
            try
            {
                foreach (int hz in new[] { 60, 90, 120 })
                    results.Add(Measure(config, settings, hz));
            }
            finally
            {
                Physics2D.simulationMode = previous;
                Physics2D.gravity = previousGravity;
            }

            string report = Format(results, settings);
            Directory.CreateDirectory("docs/benchmarks");
            File.WriteAllText(ReportPath, report);
            Debug.Log("[HR] physics rate benchmark\n" + report);
        }

        static Result Measure(GameConfig config, in TunnelSettings settings, int hz)
        {
            float dt = 1f / hz;
            var root = new GameObject($"Rate_{hz}");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", config.ringFriction, config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", config.ballFriction, config.ballBounce);

            try
            {
                var ball = TunnelFactory.CreateBall(config, root.transform, ballPhysics, material, settings);
                var ring = TunnelFactory.CreateRing(config, root.transform, material, 0);
                ring.ConfigureSolid(settings.TargetRadius, 0.0);
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;

                int steps = Mathf.RoundToInt(Seconds / dt);
                int settle = steps / 3;   // let it come to rest before measuring stillness

                double restDistance = settings.TargetRadius - settings.Thickness * 0.5 - settings.BallRadius;
                double sum = 0.0, sumSq = 0.0, maxPenetration = 0.0;
                int samples = 0;

                var watch = Stopwatch.StartNew();
                for (int step = 0; step < steps; step++)
                {
                    ball.ClampSpeed();
                    Physics2D.Simulate(dt);

                    double d = ball.DistanceToCentre;
                    double penetration = restDistance - d;
                    if (penetration > maxPenetration) maxPenetration = penetration;

                    if (step >= settle)
                    {
                        sum += d; sumSq += d * d; samples++;
                    }
                }
                watch.Stop();

                double mean = sum / samples;
                double variance = Math.Max(0.0, sumSq / samples - mean * mean);

                return new Result
                {
                    Hz = hz,
                    JitterMicroUnits = Math.Sqrt(variance) * 1e6,
                    MaxPenetration = maxPenetration,
                    MillisecondsPerSimSecond = watch.Elapsed.TotalMilliseconds / Seconds,
                    TravelPerStep = settings.MaxBallSpeed * dt,
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        static string Format(List<Result> results, in TunnelSettings settings)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;

            sb.AppendLine("# Бенчмарк фиксированного шага физики");
            sb.AppendLine();
            sb.AppendLine($"Шарик лежит на сплошном кольце радиуса "
                          + $"{settings.TargetRadius.ToString("F2", ci)} u, {Seconds.ToString("F0", ci)} с "
                          + "симуляции, первая треть отброшена на устаканивание.");
            sb.AppendLine();
            sb.AppendLine("| Шаг | Дрожание покоя, мк·u | Макс. продавливание, u | мс на 1 с симуляции | Путь за шаг, u |");
            sb.AppendLine("|---|---:|---:|---:|---:|");

            foreach (var r in results)
                sb.AppendLine($"| 1/{r.Hz} | {r.JitterMicroUnits.ToString("F1", ci)} | "
                              + $"{r.MaxPenetration.ToString("F4", ci)} | "
                              + $"{r.MillisecondsPerSimSecond.ToString("F2", ci)} | "
                              + $"{r.TravelPerStep.ToString("F3", ci)} |");

            sb.AppendLine();
            sb.AppendLine($"Толщина кольца {settings.Thickness.ToString("F2", ci)} u — путь за шаг должен "
                          + "оставаться заметно меньше неё, иначе дискретная коллизия может промахнуться.");
            return sb.ToString();
        }
    }
}
