using System.Collections;
using System.Collections.Generic;
using System.IO;
using HR.App;
using HR.Core;
using HR.Gameplay;
using HR.Save;
using HR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HR.Tests.PlayMode
{
    public class UiLayoutTests
    {
        static readonly (string Name, int Width, int Height)[] Shapes =
        {
            ("9x16", 1080, 1920),
            ("9x18", 1080, 2160),
            ("9x20", 1080, 2400),
            ("9x21", 1080, 2520),
            ("3x4", 1536, 2048),
        };

        static readonly GameState[] Screens =
        {
            GameState.Menu, GameState.Playing, GameState.Paused,
            GameState.GameOver, GameState.ThemeSelect, GameState.Settings,
        };

        GameConfig _config;
        GameObject _root;
        GameBootstrap _boot;
        Camera _camera;
        string _directory;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _directory = Path.Combine(Path.GetTempPath(), "hr-lay-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            Strings.Use(Language.English);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7.5f;

            _root = new GameObject("Boot");
            cameraGo.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
            _boot = _root.AddComponent<GameBootstrap>();
            _boot.Configure(_config, 11, new SaveService(Path.Combine(_directory, SaveService.FileName)),
                view: _camera);
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
        }

        [UnityTest]
        public IEnumerator Nothing_runs_off_the_edge_on_any_shape()
        {
            int checkedLabels = 0;

            foreach (var shape in Shapes)
            {
                _camera.aspect = (float)shape.Width / shape.Height;
                _boot.Ui.OverrideLayoutShape(_camera, shape.Width, shape.Height);
                yield return null;

                var canvas = (RectTransform)_boot.Ui.Canvas.transform;

                foreach (var state in Screens)
                {
                    _boot.Ui.Show(state);
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    int onThisScreen = 0;
                    foreach (var text in VisibleNow())
                    {
                        checkedLabels++;
                        onThisScreen++;
                        var box = RenderedBox(canvas, text);
                        Assert.IsTrue(canvas.rect.yMin - 1f <= box.yMin && box.yMax <= canvas.rect.yMax + 1f,
                            $"{shape.Name} {state}: '{text.name}' spans y {box.yMin:F0}..{box.yMax:F0} " +
                            $"outside {canvas.rect.yMin:F0}..{canvas.rect.yMax:F0}");
                        Assert.IsTrue(canvas.rect.xMin - 1f <= box.xMin && box.xMax <= canvas.rect.xMax + 1f,
                            $"{shape.Name} {state}: '{text.name}' spans x {box.xMin:F0}..{box.xMax:F0} " +
                            $"outside {canvas.rect.xMin:F0}..{canvas.rect.xMax:F0}");
                    }

                    foreach (var scroll in ScrollsOn(state))
                    {
                        var box = LocalBounds(canvas, (RectTransform)scroll.transform);
                        Assert.IsTrue(canvas.rect.yMin - 1f <= box.yMin && box.yMax <= canvas.rect.yMax + 1f,
                            $"{shape.Name} {state}: the scroll viewport itself overflows");
                        Assert.Greater(scroll.content.rect.height, 0f,
                            $"{shape.Name} {state}: scrolling content has no height");
                        onThisScreen++;
                    }

                    Assert.Greater(onThisScreen, 0,
                        $"{shape.Name} {state}: nothing was checked — the gate is passing vacuously");
                }
            }

            Assert.Greater(checkedLabels, Shapes.Length * Screens.Length,
                $"only {checkedLabels} labels examined across {Shapes.Length} shapes");
            Debug.Log($"[HR] layout gate: {checkedLabels} label placements checked");
        }

        IEnumerable<ScrollRect> ScrollsOn(GameState state)
        {
            var screen = _boot.Ui.ScreenFor(state);
            if (screen == null) yield break;
            foreach (var scroll in screen.GetComponentsInChildren<ScrollRect>(includeInactive: false))
                yield return scroll;
        }

        [UnityTest]
        public IEnumerator No_two_labels_overlap_on_any_shape()
        {
            int overlapChecked = 0;

            foreach (var shape in Shapes)
            {
                _camera.aspect = (float)shape.Width / shape.Height;
                _boot.Ui.OverrideLayoutShape(_camera, shape.Width, shape.Height);
                yield return null;

                var canvas = (RectTransform)_boot.Ui.Canvas.transform;

                foreach (var state in Screens)
                {
                    _boot.Ui.Show(state);
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    var boxes = new List<(string Name, Rect Box)>();
                    foreach (var text in VisibleNow())
                    {
                        if (string.IsNullOrWhiteSpace(text.text)) continue;
                        boxes.Add((text.name, RenderedBox(canvas, text)));
                    }

                    overlapChecked += boxes.Count;
                    for (int i = 0; i < boxes.Count; i++)
                        for (int j = i + 1; j < boxes.Count; j++)
                        {
                            var a = boxes[i];
                            var b = boxes[j];
                            bool hit = a.Box.Overlaps(b.Box);
                            Assert.IsFalse(hit,
                                $"{shape.Name} {state}: '{a.Name}' and '{b.Name}' overlap " +
                                $"({a.Box} vs {b.Box})");
                        }
                }
            }

            Assert.Greater(overlapChecked, Shapes.Length * Screens.Length,
                $"only {overlapChecked} labels compared — the gate is passing vacuously");
            Debug.Log($"[HR] overlap gate: {overlapChecked} labels compared");
        }

        [UnityTest]
        public IEnumerator Nothing_strays_under_a_notch_or_the_gesture_bar()
        {
            int checkedItems = 0;

            foreach (var shape in Shapes)
            {
                _camera.aspect = (float)shape.Width / shape.Height;

                float factor = GameUi.ScaleFactorFor(shape.Width, shape.Height);
                float w = shape.Width / factor;
                float h = shape.Height / factor;

                const float notch = 130f;
                const float bar = 90f;
                var safe = new Rect(0f, bar, w, h - notch - bar);

                _boot.Ui.OverrideLayoutShape(_camera, shape.Width, shape.Height, safe);
                yield return null;

                var canvas = (RectTransform)_boot.Ui.Canvas.transform;

                float top = canvas.rect.yMax - notch;
                float bottom = canvas.rect.yMin + bar;

                foreach (var state in Screens)
                {
                    _boot.Ui.Show(state);
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    foreach (var text in VisibleNow())
                    {
                        var box = RenderedBox(canvas, text);
                        checkedItems++;

                        Assert.LessOrEqual(box.yMax, top + 1f,
                            $"{shape.Name} {state}: '{text.name}' reaches under the cutout " +
                            $"({box.yMax:F0} above {top:F0})");
                        Assert.GreaterOrEqual(box.yMin, bottom - 1f,
                            $"{shape.Name} {state}: '{text.name}' reaches under the gesture bar " +
                            $"({box.yMin:F0} below {bottom:F0})");
                    }
                }
            }

            Assert.Greater(checkedItems, Shapes.Length * Screens.Length,
                $"only {checkedItems} items examined — the gate is passing vacuously");
            Debug.Log($"[HR] safe-area gate: {checkedItems} placements checked");
        }

        IEnumerable<Text> VisibleNow()
        {
            foreach (GameState other in System.Enum.GetValues(typeof(GameState)))
            {
                var screen = _boot.Ui.ScreenFor(other);
                if (screen == null || !screen.gameObject.activeInHierarchy) continue;

                foreach (var text in Labels(screen)) yield return text;
            }
        }

        static IEnumerable<Text> Labels(RectTransform screen)
        {
            foreach (var text in screen.GetComponentsInChildren<Text>(includeInactive: false))
            {
                var scroll = text.GetComponentInParent<ScrollRect>();
                if (scroll != null && (scroll.vertical || scroll.horizontal)) continue;
                yield return text;
            }
        }

        static Rect LocalBounds(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            var min = (Vector2)space.InverseTransformPoint(corners[0]);
            var max = min;
            for (int i = 1; i < 4; i++)
            {
                var p = (Vector2)space.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return new Rect(min, max - min);
        }

        static Rect RenderedBox(RectTransform space, Text text)
        {
            var box = LocalBounds(space, (RectTransform)text.transform);
            var rect = ((RectTransform)text.transform).rect;
            var settings = text.GetGenerationSettings(rect.size);
            var generator = text.cachedTextGenerator;

            generator.PopulateWithErrors(text.text, settings, text.gameObject);

            float width = DrawnWidth(generator) / text.pixelsPerUnit;
            float height = DrawnHeight(generator) / text.pixelsPerUnit;

            if (box.width > 0f) width = Mathf.Min(width, box.width);
            height = Mathf.Max(height, box.height);

            return new Rect(box.center.x - width / 2f, box.center.y - height / 2f, width, height);
        }

        static float DrawnWidth(TextGenerator generator)
        {
            float widest = 0f;
            var characters = generator.characters;
            var lines = generator.lines;

            for (int line = 0; line < lines.Count; line++)
            {
                int from = lines[line].startCharIdx;
                int to = line + 1 < lines.Count ? lines[line + 1].startCharIdx : characters.Count;
                if (to <= from) continue;

                float left = characters[from].cursorPos.x;
                float right = left;
                for (int i = from; i < to; i++)
                    right = Mathf.Max(right, characters[i].cursorPos.x + characters[i].charWidth);

                widest = Mathf.Max(widest, right - left);
            }

            return widest;
        }

        static float DrawnHeight(TextGenerator generator)
        {
            var lines = generator.lines;
            if (lines.Count == 0) return 0f;

            float top = lines[0].topY;
            float bottom = lines[lines.Count - 1].topY - lines[lines.Count - 1].height;
            return top - bottom;
        }
    }
}
