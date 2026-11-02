using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Measures the window a hardcore player actually gets, before any of its numbers are tuned.
    ///
    /// Plan [H-17] asks for this first and for a reason: the whole mode rests on an estimate
    /// made on paper — that between leaving one ring and being turned back twice by the next
    /// there is something like 0.35 to 1.0 seconds. If that is wrong the mode is either trivial
    /// or impossible, and no amount of adjusting the opening angles in [H-10] would tell us
    /// which.
    ///
    /// Writes what it finds to docs/benchmarks/ so the numbers outlive the run that produced
    /// them. The assertions are deliberately loose: this is an instrument, and an instrument
    /// that fails when the thing it measures changes is not an instrument.
    /// </summary>
    [Category("Endurance")]
    public sealed class HardcoreWindowTests
    {
        const float Dt = 1f / 90f;
        const int Runs = 60;
        const int StepsPerRun = 9000;   // 100 seconds of play each

        GameConfig _config;
        SimulationMode2D _simulationMode;
        GameObject _root;
        BallController _ball;
        TunnelController _tunnel;
        RingPool _pool;

        readonly List<double> _toFirstTouch = new List<double>();
        readonly List<double> _betweenTouches = new List<double>();
        readonly List<double> _angleToNearestGap = new List<double>();

        // Paired on purpose. Separate medians answer "how far" and "how long" but not the only
        // question that matters: whether the SAME ring gave enough of both at once.
        readonly List<double> _demandedSpin = new List<double>();

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _simulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -14f);
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _simulationMode;
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        void Build(int seed)
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);

            _root = new GameObject("HardcoreRoot");
            var material = TunnelFactory.CreateUnlitMaterial(Color.white);
            var ringPhysics = TunnelFactory.CreatePhysicsMaterial("Ring", _config.ringFriction, _config.ringBounce);
            var ballPhysics = TunnelFactory.CreatePhysicsMaterial("Ball", _config.ballFriction, _config.ballBounce);
            var settings = _config.ToSettings();

            _ball = TunnelFactory.CreateBall(_config, _root.transform, ballPhysics, material, settings);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(_root.transform, false);
            _tunnel = tunnelGo.AddComponent<TunnelController>();
            _pool = new RingPool(_config, tunnelGo.transform, material);
            _tunnel.Initialise(_config, _ball, _pool);
            _tunnel.AutoStep = false;
            _tunnel.BeginRun(seed);

            foreach (var ring in _tunnel.Rings)
                for (int i = 0; i < ring.ActiveSegmentCount; i++)
                    ring.SegmentAt(i).Collider.sharedMaterial = ringPhysics;
        }

        [UnityTest]
        [Timeout(1_800_000)]
        public IEnumerator The_window_a_hardcore_player_gets_is_measured_before_it_is_tuned()
        {
            _toFirstTouch.Clear();
            _betweenTouches.Clear();
            _angleToNearestGap.Clear();
            _demandedSpin.Clear();

            for (int run = 0; run < Runs; run++)
            {
                Build(seed: run + 1);

                int lastRing = -1;
                double clock = 0.0;
                double leftRingAt = 0.0;
                double firstTouchAt = -1.0;
                double angleAtFirstTouch = -1.0;
                int touchesHere = 0;

                _tunnel.BallTouchedRing += count =>
                {
                    touchesHere = count;
                    if (count == 1)
                    {
                        firstTouchAt = clock;
                        _toFirstTouch.Add(clock - leftRingAt);

                        // Where the nearest way out was at the moment of the first touch. This
                        // is the number the mode lives or dies by: how far the player has to
                        // turn, in the time they have, to put an opening where the ball is
                        // about to arrive.
                        var ring = _tunnel.ResolveBallRing();
                        if (ring != null)
                        {
                            angleAtFirstTouch = Math.Abs(TunnelMath.ShortestDeltaDeg(
                                ring.NearestGapWorldDeg(_ball.AngleDeg), _ball.AngleDeg));
                            _angleToNearestGap.Add(angleAtFirstTouch);
                        }
                    }
                    else if (count == 2 && firstTouchAt >= 0.0)
                    {
                        double window = clock - firstTouchAt;
                        _betweenTouches.Add(window);

                        if (window > 1e-6 && angleAtFirstTouch >= 0.0)
                            _demandedSpin.Add(angleAtFirstTouch / window);
                    }
                };

                for (int step = 0; step < StepsPerRun; step++)
                {
                    var ring = _tunnel.ResolveBallRing();
                    if (ring == null) break;

                    if (ring.SpawnIndex != lastRing)
                    {
                        lastRing = ring.SpawnIndex;
                        leftRingAt = clock;
                        firstTouchAt = -1.0;
                        angleAtFirstTouch = -1.0;
                        touchesHere = 0;
                    }

                    _tunnel.Step(Dt);
                    _ball.HoldCruisingSpeed(Dt);
                    _ball.ClampSpeed();
                    Physics2D.Simulate(Dt);
                    clock += Dt;

                    if (_tunnel.HasRingClosedOnCentre()) break;
                }

                if (run % 10 == 0) yield return null;
            }

            Report();

            // An instrument, not a gate: it has to have measured something, and the something
            // has to be physically sane. Anything tighter would fail the first time the mode
            // was tuned, which is exactly when the instrument is needed.
            Assert.Greater(_toFirstTouch.Count, 50, "almost no first touches were seen at all");
            Assert.Greater(_betweenTouches.Count, 20, "almost no second touches were seen");
            Assert.Greater(Median(_betweenTouches), 0.0, "the window between touches is not positive");
        }

        static double Median(List<double> values)
        {
            if (values.Count == 0) return 0.0;
            values.Sort();
            return values[values.Count / 2];
        }

        static double Percentile(List<double> values, double share)
        {
            if (values.Count == 0) return 0.0;
            values.Sort();
            int index = (int)(values.Count * share);
            return values[Mathf.Clamp(index, 0, values.Count - 1)];
        }

        void Report()
        {
            var text = new StringBuilder();
            text.AppendLine("# Окно игрока в хардкоре — замер");
            text.AppendLine();
            text.AppendLine($"Снято {Runs} прогонами по {StepsPerRun} шагов на настоящей физике, ");
            text.AppendLine("с геометрией v2 и без каких-либо правок под хардкор. Плана [H-17].");
            text.AppendLine();
            text.AppendLine("| Величина | Наблюдений | 10-й проц. | Медиана | 90-й проц. |");
            text.AppendLine("|---|---:|---:|---:|---:|");
            Row(text, "Выход → первое касание, с", _toFirstTouch);
            Row(text, "Первое → второе касание, с", _betweenTouches);
            Row(text, "Угол до ближайшего выхода при первом касании, °", _angleToNearestGap);
            Row(text, "**Требуемая скорость поворота, °/с**", _demandedSpin);
            text.AppendLine();

            // The one number the mode stands on: how often the same ring demanded more than a
            // thumb can give. Computed from PAIRS, because a ring that gave a wide angle and a
            // long window is not the same ring as one that gave neither.
            int impossible = 0;
            for (int i = 0; i < _demandedSpin.Count; i++)
                if (_demandedSpin[i] > _config.comfortSpinDeg) impossible++;

            double share = _demandedSpin.Count > 0 ? impossible * 100.0 / _demandedSpin.Count : 0.0;
            text.AppendLine($"**Непроходимых колец: {impossible} из {_demandedSpin.Count} "
                            + $"({share:F1}%)** — тех, где требуемая скорость поворота выше "
                            + $"комфортных {_config.comfortSpinDeg:F0} °/с.");
            text.AppendLine();
            text.AppendLine("**Как это читать.** Четвёртая строка — главная: это угол, делённый ");
            text.AppendLine("на окно, то есть скорость, которую кольцо потребовало от пальца. ");
            text.AppendLine("Всё, что выше комфортных 300 °/с, игрок физически не успевает.");
            text.AppendLine();
            text.AppendLine("Доля непроходимых считается по **парам**, а не по двум медианам: ");
            text.AppendLine("кольцо, давшее широкий угол и длинное окно, — это не то же кольцо, ");
            text.AppendLine("что не дало ни того, ни другого, и перемножать их распределения ");
            text.AppendLine("значило бы придумать себе более ровный мир, чем есть.");
            text.AppendLine();
            text.AppendLine("Замер снят **без** хардкорных правок [H-10]: отверстия здесь обычные. ");
            text.AppendLine("Широкое отверстие уменьшает требуемый угол примерно на свою половину, ");
            text.AppendLine("потому что довернуть надо кромку, а не центр.");

            var path = Path.Combine(Application.dataPath, "..", "docs", "benchmarks",
                                    "hardcore-window.md");
            File.WriteAllText(Path.GetFullPath(path), text.ToString());
            Debug.Log($"[HR] hardcore window: between touches median {Median(_betweenTouches):F3}s, "
                      + $"angle median {Median(_angleToNearestGap):F1} deg");
        }

        static void Row(StringBuilder text, string label, List<double> values)
        {
            var c = CultureInfo.InvariantCulture;
            text.AppendLine($"| {label} | {values.Count} | "
                            + $"{Percentile(values, 0.1).ToString("F2", c)} | "
                            + $"{Median(values).ToString("F2", c)} | "
                            + $"{Percentile(values, 0.9).ToString("F2", c)} |");
        }
    }
}
