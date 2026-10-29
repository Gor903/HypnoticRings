using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using HR.Core;
using HR.Gameplay;
using HR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// What one step of the game actually costs, measured on the real tunnel with the effects
    /// running — not on a stripped-down rig that flatters it.
    ///
    /// This measures a desktop, and a phone is not a desktop. It is still worth having: it says
    /// which part of the step is expensive and gives the device run in phase 13 a number to be
    /// compared against rather than a shrug. What it cannot do is prove 60 FPS on hardware, and
    /// the report says so in those words.
    /// </summary>
    public class FrameBudgetBenchmark
    {
        const float Dt = 1f / 90f;
        const string OutputPath = "docs/benchmarks/frame-budget.md";

        GameConfig _config;
        TunnelSettings _settings;
        SimulationMode2D _simulationMode;

        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        InputController _input;
        Effects _effects;
        double _fingerAngle;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _settings = _config.ToSettings();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -14f);
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        void Build(bool withEffects)
        {
            _root = new GameObject("BudgetRoot");

            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, _settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _tunnel.Initialise(_config, _ball, new RingPool(_config, tunnelGo.transform, material));
            _tunnel.AutoStep = false;
            _tunnel.BeginRun(31);

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(_root.transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(_config.ToGestureSettings(), null);
            _input.ExternalDriver = true;

            var effectsGo = new GameObject("Effects");
            effectsGo.transform.SetParent(_root.transform, false);
            _effects = effectsGo.AddComponent<Effects>();
            _effects.Initialise(_config, cameraGo.GetComponent<Camera>(), _ball.transform, material);
            _effects.Enabled = withEffects;

            _fingerAngle = 0.0;
        }

        /// <summary>Microseconds per step, averaged over a run long enough to settle.</summary>
        (double tunnel, double physics, double effects) Measure(bool withEffects, int steps)
        {
            Build(withEffects);
            var bot = BotBrain.Strong(5);

            // Warm up: the first steps build geometry that later steps reuse.
            for (int i = 0; i < 400; i++) StepOnce(bot, null, null, null);

            var tunnelClock = new Stopwatch();
            var physicsClock = new Stopwatch();
            var effectsClock = new Stopwatch();

            for (int i = 0; i < steps; i++) StepOnce(bot, tunnelClock, physicsClock, effectsClock);

            double micros(Stopwatch c) => c.Elapsed.TotalMilliseconds * 1000.0 / steps;
            var result = (micros(tunnelClock), micros(physicsClock), micros(effectsClock));

            Object.DestroyImmediate(_root);
            _root = null;
            return result;
        }

        void StepOnce(BotBrain bot, Stopwatch tunnelClock, Stopwatch physicsClock, Stopwatch effectsClock)
        {
            var ring = _tunnel.ResolveBallRing();
            if (ring != null)
            {
                // The bot decides a ring angle; the finger moves that divided by the gain.
                _fingerAngle += bot.Decide(TunnelMath.BottomAngleDeg, ring.GapCentreWorldDeg, Dt)
                                / _config.sensitivity;
                _input.FeedPointer(true, FingerAt(_fingerAngle), Dt);
                _tunnel.UserAngleDeg = _input.UserAngleDeg;
            }

            tunnelClock?.Start();
            _tunnel.Step(Dt);
            _ball.ClampSpeed();
            tunnelClock?.Stop();

            physicsClock?.Start();
            Physics2D.Simulate(Dt);
            physicsClock?.Stop();

            effectsClock?.Start();
            _effects.Tick(Dt);
            effectsClock?.Stop();
        }

        Vector2 FingerAt(double degrees)
        {
            float radians = (float)(degrees * Mathf.Deg2Rad);
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 3f;
        }

        [UnityTest]
        [Category("Benchmark")]
        public IEnumerator Measure_what_one_step_costs()
        {
            const int steps = 20000;

            var on = Measure(withEffects: true, steps: steps);
            yield return null;
            var off = Measure(withEffects: false, steps: steps);

            double budget = Dt * 1000.0 * 1000.0;   // microseconds available per step at 90 Hz
            double totalOn = on.tunnel + on.physics + on.effects;
            double totalOff = off.tunnel + off.physics + off.effects;

            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# Бюджет шага");
            sb.AppendLine();
            sb.AppendLine($"Замер на desktop, {steps} шагов после прогрева, шаг {Dt:F5} с " +
                          $"(бюджет {budget:F0} мкс).");
            sb.AppendLine("**Это не измерение на устройстве** — телефон медленнее, и числа ниже " +
                          "годятся как ориентир и как то, с чем сравнивать в фазе 13.");
            sb.AppendLine();
            sb.AppendLine("| Часть шага | С эффектами, мкс | Без эффектов, мкс |");
            sb.AppendLine("|---|---:|---:|");
            sb.AppendLine($"| Тоннель (сжатие, вращение, геометрия) | {on.tunnel.ToString("F2", ci)} | {off.tunnel.ToString("F2", ci)} |");
            sb.AppendLine($"| `Physics2D.Simulate` | {on.physics.ToString("F2", ci)} | {off.physics.ToString("F2", ci)} |");
            sb.AppendLine($"| Эффекты | {on.effects.ToString("F2", ci)} | {off.effects.ToString("F2", ci)} |");
            sb.AppendLine($"| **Итого** | **{totalOn.ToString("F2", ci)}** | **{totalOff.ToString("F2", ci)}** |");
            sb.AppendLine($"| Доля бюджета шага | {(100.0 * totalOn / budget).ToString("F1", ci)}% | {(100.0 * totalOff / budget).ToString("F1", ci)}% |");
            sb.AppendLine();
            sb.AppendLine($"Эффекты стоят {(on.effects).ToString("F2", ci)} мкс — это цена, которую " +
                          "снимает флаг «Снизить эффекты».");

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, sb.ToString());

            Debug.Log($"[HR] frame budget: {totalOn:F2} us/step with effects, {totalOff:F2} without, " +
                      $"budget {budget:F0} us ({100.0 * totalOn / budget:F1}% used)");

            // Not a device claim — a sanity bound. A step costing more than the whole budget on
            // a desktop would mean the phone has no chance at all.
            Assert.Less(totalOn, budget * 0.5,
                $"one step costs {totalOn:F0} us against a {budget:F0} us budget");
        }
    }
}
