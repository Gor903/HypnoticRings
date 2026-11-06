using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using HR.Themes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HR.Tests.PlayMode
{
    public sealed class LongPlayTests
    {
        const int Runs = 10;
        const int SettleFrames = 4;

        GameConfig _config;
        GameObject _root;
        GameBootstrap _boot;
        Camera _camera;
        string _directory;
        float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _directory = Path.Combine(Path.GetTempPath(), "hr-long-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);
            _timeScale = Time.timeScale;

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _config.orthoSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.transform.position = new Vector3(0f, _config.CameraOffsetY, -10f);
            if (cameraGo.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGo.AddComponent<UniversalAdditionalCameraData>();

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, runSeed: 20261103,
                new SaveService(Path.Combine(_directory, SaveService.FileName)),
                view: _camera);
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
        }

        struct Census
        {
            public int Transforms, Graphics, Renderers, Colliders, Behaviours;
            public int Meshes, Materials, Textures;
            public long Heap;
        }

        Census Take()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            return new Census
            {
                Transforms = _root.GetComponentsInChildren<Transform>(true).Length,
                Graphics   = _root.GetComponentsInChildren<Graphic>(true).Length,
                Renderers  = _root.GetComponentsInChildren<Renderer>(true).Length,
                Colliders  = _root.GetComponentsInChildren<Collider2D>(true).Length,
                Behaviours = _root.GetComponentsInChildren<MonoBehaviour>(true).Length,
                Meshes     = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                Materials  = Resources.FindObjectsOfTypeAll<Material>().Length,
                Textures   = Resources.FindObjectsOfTypeAll<Texture>().Length,
                Heap       = System.GC.GetTotalMemory(true),
            };
        }

        void Press(GameState screen, string button)
        {
            foreach (var candidate in _boot.Ui.ScreenFor(screen)
                         .GetComponentsInChildren<Button>(includeInactive: true))
                if (candidate.name == button) { candidate.onClick.Invoke(); return; }

            Assert.Fail($"no button '{button}' on {screen}");
        }

        const float FingerRadius = 3.0f;
        double _fingerAngle;

        static Vector2 FingerAt(double angleDeg)
        {
            double rad = angleDeg * System.Math.PI / 180.0;
            return new Vector2((float)(System.Math.Cos(rad) * FingerRadius),
                               (float)(System.Math.Sin(rad) * FingerRadius));
        }

        void SteerTowardTheGap(float dt)
        {
            var ring = _boot.Tunnel.ResolveBallRing();
            if (ring == null) return;

            double want = TunnelMath.ShortestDeltaDeg(
                ring.NearestGapWorldDeg(_boot.Tunnel.BallAt(0).AngleDeg),
                _boot.Tunnel.BallAt(0).AngleDeg);
            double most = _config.comfortSpinDeg * dt;

            _fingerAngle += Mathf.Clamp((float)want, (float)-most, (float)most) / _config.sensitivity;
            _boot.Input.FeedPointer(true, FingerAt(_fingerAngle), dt);
        }

        void SealBallRing()
        {
            var ring = _boot.Tunnel.ResolveBallRing();
            if (ring == null) return;
            ring.ConfigureSolid(ring.Radius, autoSpinDeg: 0.0);
        }

        IEnumerator PlayScoring(int frames)
        {
            _boot.Input.ExternalDriver = true;
            for (int frame = 0; frame < frames; frame++)
            {
                if (_boot.Game.State != GameState.Playing) yield break;
                SteerTowardTheGap(Time.deltaTime);
                yield return null;
            }
        }

        IEnumerator DieNow(int frameCap)
        {
            _boot.Input.ExternalDriver = true;
            _boot.Input.Cancel();

            SealBallRing();
            for (int frame = 0; frame < frameCap; frame++)
            {
                if (_boot.Game.State != GameState.Playing) yield break;
                if (frame % 30 == 0) SealBallRing();
                yield return null;
            }

            Assert.Fail($"the run did not end within {frameCap} frames");
        }

        IEnumerator LeaveCountdown()
        {
            double waited = 0.0;
            int frames = 0;

            while (_boot.Game.State == GameState.Countdown
                   && waited < CountdownClock.Total * 4.0
                   && frames++ < 200000)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.AreNotEqual(GameState.Countdown, _boot.Game.State,
                $"the countdown never ended: {waited:F2}s of game time over {frames} frames");
        }

        IEnumerator Run()
        {
            yield return LeaveCountdown();
            yield return PlayScoring(600);
            yield return DieNow(3000);
            for (int i = 0; i < SettleFrames; i++) yield return null;
        }

        [UnityTest]
        [Category("Endurance")]
        [Timeout(1_200_000)]
        public IEnumerator Ten_runs_in_a_row_leave_the_game_the_same_size()
        {
            Time.timeScale = 8f;
            yield return null;

            Press(GameState.Menu, "menu.play");
            yield return Run();

            var first = Take();
            var samples = new List<Census> { first };
            int scored = _boot.Game.Score;

            for (int run = 1; run < Runs; run++)
            {
                Press(GameState.GameOver, "over.retry");
                yield return null;
                yield return Run();
                scored += _boot.Game.Score;
                samples.Add(Take());
            }

            Assert.Greater(scored, Runs,
                $"{Runs} runs scored {scored} rings between them — the harness never played");

            var last = samples[samples.Count - 1];
            Report(samples);

            Assert.LessOrEqual(last.Transforms, first.Transforms,
                $"transforms grew from {first.Transforms} to {last.Transforms} over {Runs} runs");
            Assert.LessOrEqual(last.Graphics, first.Graphics,
                $"interface graphics grew from {first.Graphics} to {last.Graphics}");
            Assert.LessOrEqual(last.Renderers, first.Renderers,
                $"renderers grew from {first.Renderers} to {last.Renderers}");
            Assert.LessOrEqual(last.Colliders, first.Colliders,
                $"colliders grew from {first.Colliders} to {last.Colliders}");
            Assert.LessOrEqual(last.Behaviours, first.Behaviours,
                $"components grew from {first.Behaviours} to {last.Behaviours}");
            Assert.LessOrEqual(last.Materials - first.Materials, 0,
                $"materials grew by {last.Materials - first.Materials}");
            Assert.LessOrEqual(last.Meshes - first.Meshes, 0,
                $"meshes grew by {last.Meshes - first.Meshes}");
            Assert.Less(last.Heap - first.Heap, 2L * 1024 * 1024,
                $"managed heap grew by {(last.Heap - first.Heap) / 1024} KB over {Runs} runs");
        }

        static double TimeCanvasRebuild(RectTransform screen, int passes)
        {
            var graphics = screen.GetComponentsInChildren<Graphic>(true);
            Canvas.ForceUpdateCanvases();

            var clock = new System.Diagnostics.Stopwatch();
            for (int pass = 0; pass < passes; pass++)
            {
                foreach (var g in graphics) g.SetAllDirty();
                clock.Start();
                Canvas.ForceUpdateCanvases();
                clock.Stop();
            }

            return clock.Elapsed.TotalMilliseconds * 1000.0 / passes;
        }

        [UnityTest]
        [Category("Endurance")]
        [Timeout(1_200_000)]
        public IEnumerator Report_what_a_canvas_rebuild_costs()
        {
            const int Passes = 60;
            var screens = new[]
            {
                GameState.Playing, GameState.Menu, GameState.Settings,
                GameState.ThemeSelect, GameState.GameOver,
            };

            var report = new StringBuilder("[HR] canvas rebuild, microseconds per full rebuild\n");
            report.AppendLine("  theme        screen        graphics   with chrome   without   chrome costs");

            foreach (var theme in new[] { Theme.MinimalLight, Theme.Neon })
            {
                _boot.ChooseTheme(theme);
                yield return null;

                foreach (var state in screens)
                {
                    _boot.Ui.Show(state);
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    var screen = _boot.Ui.ScreenFor(state);
                    int count = screen.GetComponentsInChildren<Graphic>(true).Length;

                    double with = TimeCanvasRebuild(screen, Passes);

                    var outlines = screen.GetComponentsInChildren<Outline>(true);
                    foreach (var o in outlines) o.enabled = false;
                    double without = TimeCanvasRebuild(screen, Passes);
                    foreach (var o in outlines) o.enabled = true;

                    report.AppendLine($"  {theme,-12} {state,-13} {count,8}   {with,11:F0}   "
                                      + $"{without,7:F0}   {with - without,+8:F0}  ({outlines.Length} outlines)");
                    yield return null;
                }
            }

            Debug.Log(report.ToString());
            Assert.Pass();
        }

        [UnityTest]
        [Category("Endurance")]
        [Timeout(1_800_000)]
        public IEnumerator Report_whether_a_run_gets_more_expensive_as_the_score_climbs()
        {
            var buckets = new[] { 10, 25, 50, 100, 200 };
            var cost = new double[buckets.Length];
            var sampled = new int[buckets.Length];

            Time.timeScale = 4f;
            yield return null;
            Press(GameState.Menu, "menu.play");
            _boot.Input.ExternalDriver = true;

            var clock = new System.Diagnostics.Stopwatch();
            int bucket = 0;
            int guard = 0;

            while (bucket < buckets.Length && guard++ < 200000)
            {
                if (_boot.Game.State == GameState.Countdown)
                {
                    yield return null;
                    continue;
                }

                if (_boot.Game.State != GameState.Playing)
                {
                    Press(GameState.GameOver, "over.retry");
                    yield return null;
                    continue;
                }

                while (bucket < buckets.Length && _boot.Game.Score > buckets[bucket]) bucket++;
                if (bucket >= buckets.Length) break;

                clock.Restart();
                SteerTowardTheGap(Time.deltaTime);
                Canvas.ForceUpdateCanvases();
                clock.Stop();

                cost[bucket] += clock.Elapsed.TotalMilliseconds * 1000.0;
                sampled[bucket]++;
                yield return null;
            }

            var report = new StringBuilder("[HR] cost by score, microseconds per frame of steer + canvas\n");
            report.AppendLine("  up to score   frames   mean us   live rings");
            for (int i = 0; i < buckets.Length; i++)
            {
                if (sampled[i] == 0) continue;
                report.AppendLine($"  {buckets[i],11}   {sampled[i],6}   {cost[i] / sampled[i],7:F1}   "
                                  + $"{_boot.Tunnel.ActiveRingCount,10}");
            }

            Debug.Log(report.ToString());
            Assert.Greater(sampled[0], 0, "never reached the first bucket");
        }

        static void Report(List<Census> samples)
        {
            var report = new StringBuilder("[HR] long play: census after each run\n");
            report.AppendLine("  run  transforms  graphics  renderers  colliders  behaviours  "
                              + "meshes  materials  textures  heap KB");

            for (int i = 0; i < samples.Count; i++)
            {
                var c = samples[i];
                report.AppendLine($"  {i + 1,3}  {c.Transforms,10}  {c.Graphics,8}  {c.Renderers,9}  "
                                  + $"{c.Colliders,9}  {c.Behaviours,10}  {c.Meshes,6}  "
                                  + $"{c.Materials,9}  {c.Textures,8}  {c.Heap / 1024,7}");
            }

            var f = samples[0];
            var l = samples[samples.Count - 1];
            report.AppendLine($"  delta {l.Transforms - f.Transforms,+8}  {l.Graphics - f.Graphics,+8}  "
                              + $"{l.Renderers - f.Renderers,+9}  {l.Colliders - f.Colliders,+9}  "
                              + $"{l.Behaviours - f.Behaviours,+10}  {l.Meshes - f.Meshes,+6}  "
                              + $"{l.Materials - f.Materials,+9}  {l.Textures - f.Textures,+8}  "
                              + $"{(l.Heap - f.Heap) / 1024,+7}");

            Debug.Log(report.ToString());
        }
    }
}
