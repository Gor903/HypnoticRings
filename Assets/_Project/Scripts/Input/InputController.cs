using HR.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace HR.Input
{
    /// <summary>
    /// Reads the finger and hands the gesture to a GestureTracker. Thin on purpose: all the
    /// behaviour that can be got wrong lives in the tracker, where it is tested without
    /// hardware (CONTRACT.md §2.10, §6.3).
    ///
    /// The player may touch anywhere on screen, not only on a ring — the gesture is about the
    /// angle around the tunnel centre, not about hitting anything.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        GestureTracker _tracker;
        Camera _camera;
        Vector2 _centreWorld;

        bool _pointerWasDown;
        int _activeTouchId = -1;

        /// <summary>
        /// Set while a test or the bot supplies pointer samples directly. Polling is skipped so
        /// the two sources cannot fight over the same gesture.
        /// </summary>
        public bool ExternalDriver { get; set; }

        public GestureTracker Tracker => _tracker;
        public double UserAngleDeg => _tracker?.AngleDeg ?? 0.0;
        public double VelocityDeg => _tracker?.VelocityDeg ?? 0.0;

        public void Initialise(in GestureSettings settings, Camera camera, Vector2 centreWorld = default)
        {
            _camera = camera;
            _centreWorld = centreWorld;
            _tracker = new GestureTracker(settings);
        }

        void OnEnable()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        void Update()
        {
            if (_tracker == null || ExternalDriver) return;

            float dt = Time.deltaTime;
            Poll(dt);
            _tracker.Tick(dt);
        }

        /// <summary>
        /// Supplies one pointer sample, in world units. The seam the bot and the gesture tests
        /// use — the same path a real touch takes, minus the hardware.
        /// </summary>
        public void FeedPointer(bool pressed, Vector2 worldPosition, float dt)
        {
            if (_tracker == null) return;
            ApplyPointer(pressed, worldPosition, dt);
            _tracker.Tick(dt);
        }

        /// <summary>Paused, backgrounded, or focus lost: the gesture is void and its momentum with it.</summary>
        public void Cancel()
        {
            _pointerWasDown = false;
            _activeTouchId = -1;
            _tracker?.Cancel();
        }

        public void ResetAngle() => _tracker?.Reset();

        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        void OnApplicationPause(bool paused) { if (paused) Cancel(); }

        void Poll(float dt)
        {
            if (TryReadTouch(out Vector2 screenPosition, out bool pressed)
                || TryReadMouse(out screenPosition, out pressed))
            {
                ApplyPointer(pressed, ToWorld(screenPosition), dt);
                return;
            }

            ApplyPointer(false, Vector2.zero, dt);
        }

        bool TryReadTouch(out Vector2 screenPosition, out bool pressed)
        {
            screenPosition = default;
            pressed = false;

            var touches = Touch.activeTouches;
            if (touches.Count == 0)
            {
                _activeTouchId = -1;
                return false;
            }

            // Only the first finger steers. A second finger landing mid-gesture must not take
            // the ring with it, and lifting it must not end the gesture either.
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (_activeTouchId == -1 || touch.touchId == _activeTouchId)
                {
                    _activeTouchId = touch.touchId;
                    screenPosition = touch.screenPosition;
                    pressed = touch.phase != UnityEngine.InputSystem.TouchPhase.Ended
                              && touch.phase != UnityEngine.InputSystem.TouchPhase.Canceled;
                    if (!pressed) _activeTouchId = -1;
                    return true;
                }
            }

            return false;
        }

        bool TryReadMouse(out Vector2 screenPosition, out bool pressed)
        {
            screenPosition = default;
            pressed = false;

            var mouse = Mouse.current;
            if (mouse == null) return false;

            screenPosition = mouse.position.ReadValue();
            pressed = mouse.leftButton.isPressed;
            return true;
        }

        void ApplyPointer(bool pressed, Vector2 worldPosition, float dt)
        {
            Vector2 relative = worldPosition - _centreWorld;

            if (pressed && !_pointerWasDown) _tracker.Begin(relative.x, relative.y);
            else if (pressed) _tracker.Move(relative.x, relative.y, dt);
            else if (_pointerWasDown) _tracker.End();

            _pointerWasDown = pressed;
        }

        Vector2 ToWorld(Vector2 screenPosition)
        {
            if (_camera == null) return Vector2.zero;
            Vector3 world = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));
            return new Vector2(world.x, world.y);
        }
    }
}
