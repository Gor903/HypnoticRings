using System.Collections;
using HR.Gameplay;
using HR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Proves a real touch reaches the gesture layer. Everything downstream of the tracker is
    /// covered without hardware; this covers the one stretch that is not — reading the
    /// touchscreen, picking the first finger, and converting screen pixels to world units.
    ///
    /// A fault here does not degrade the game, it makes it completely unresponsive on a phone,
    /// and no edit-mode test would notice.
    /// </summary>
    public class RealTouchInputTests : InputTestFixture
    {
        GameObject _root;
        Camera _camera;
        InputController _input;
        GameConfig _config;

        public override void Setup()
        {
            base.Setup();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _root = new GameObject("TouchRoot");

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);
            _camera = cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7.5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(_root.transform, false);
            _input = inputGo.AddComponent<InputController>();
            _input.Initialise(_config.ToGestureSettings(), _camera);
        }

        public override void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
            base.TearDown();
        }

        /// <summary>Screen position for a point on a circle around the tunnel centre.</summary>
        Vector2 ScreenAt(float angleDeg, float worldRadius)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            var world = new Vector3(Mathf.Cos(rad) * worldRadius, Mathf.Sin(rad) * worldRadius, 0f);
            Vector3 screen = _camera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        [UnityTest]
        public IEnumerator A_dragged_finger_turns_the_tunnel()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            yield return null;

            BeginTouch(1, ScreenAt(0f, 3f), screen: touchscreen);
            yield return null;

            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(1, ScreenAt(i * 2f, 3f), screen: touchscreen);
                yield return null;
            }

            Assert.Greater(_input.UserAngleDeg, 0.0, "a real touch drag did not reach the tracker");

            EndTouch(1, ScreenAt(20f, 3f), screen: touchscreen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_second_finger_does_not_hijack_the_gesture()
        {
            // CONTRACT.md §2.10: only the first finger steers. A thumb resting on the screen
            // must not drag the tunnel with it.
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            yield return null;

            BeginTouch(1, ScreenAt(0f, 3f), screen: touchscreen);
            yield return null;
            MoveTouch(1, ScreenAt(10f, 3f), screen: touchscreen);
            yield return null;

            double afterFirstFinger = _input.UserAngleDeg;

            // A second finger appears far away and sweeps hard the other way.
            BeginTouch(2, ScreenAt(180f, 3f), screen: touchscreen);
            yield return null;
            for (int i = 1; i <= 5; i++)
            {
                MoveTouch(2, ScreenAt(180f - i * 10f, 3f), screen: touchscreen);
                yield return null;
            }

            Assert.GreaterOrEqual(_input.UserAngleDeg, afterFirstFinger - 1e-3,
                "a second finger dragged the tunnel backwards");

            EndTouch(1, ScreenAt(10f, 3f), screen: touchscreen);
            EndTouch(2, ScreenAt(130f, 3f), screen: touchscreen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_touch_on_the_tunnel_centre_is_ignored()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            yield return null;

            // Inside the 0.6u dead zone, where a pixel of jitter would be a large angle.
            BeginTouch(1, ScreenAt(0f, 0.1f), screen: touchscreen);
            yield return null;
            MoveTouch(1, ScreenAt(90f, 0.1f), screen: touchscreen);
            yield return null;

            Assert.AreEqual(0.0, _input.UserAngleDeg, 1e-6, "the dead zone let a centre touch through");

            EndTouch(1, ScreenAt(90f, 0.1f), screen: touchscreen);
            yield return null;
        }
    }
}
