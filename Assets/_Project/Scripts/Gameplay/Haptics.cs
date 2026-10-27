using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// The two buzzes of spec 11.4: a short one on a pass, a longer one on a loss.
    ///
    /// The device sits behind a driver so the rule — which buzz, how long, and whether the
    /// player wanted one at all — can be tested without a phone in the room. Without that seam
    /// the only way to check that switching vibration off actually stops it is to hold the
    /// device and feel for nothing, which is not a test.
    /// </summary>
    public sealed class Haptics
    {
        public interface IDriver
        {
            void Buzz(int milliseconds);
        }

        /// <summary>What a test substitutes, and what every non-Android platform gets.</summary>
        public sealed class Silent : IDriver
        {
            public int Calls { get; private set; }
            public int LastMilliseconds { get; private set; } = -1;

            public void Buzz(int milliseconds)
            {
                Calls++;
                LastMilliseconds = milliseconds;
            }
        }

        readonly int _passMs;
        readonly int _loseMs;

        public IDriver Driver { get; set; }

        /// <summary>Mirrors the player's setting; false means the driver is never reached.</summary>
        public bool Enabled { get; set; } = true;

        public Haptics(int passMilliseconds, int loseMilliseconds, IDriver driver = null)
        {
            _passMs = passMilliseconds;
            _loseMs = loseMilliseconds;
            Driver = driver ?? Default();
        }

        public void Pass() => Buzz(_passMs);
        public void Lose() => Buzz(_loseMs);

        void Buzz(int milliseconds)
        {
            if (!Enabled || milliseconds <= 0 || Driver == null) return;
            Driver.Buzz(milliseconds);
        }

        static IDriver Default() =>
            Application.platform == RuntimePlatform.Android ? new Android() : (IDriver)new Silent();

        /// <summary>
        /// The real thing, reached by JNI.
        ///
        /// Unity's own Handheld.Vibrate takes no duration at all — it is a fixed half-second
        /// buzz, which is six times the spec's longest and twenty-five times its shortest. The
        /// durations are the whole point of this effect, so it goes through the platform call.
        /// </summary>
        sealed class Android : IDriver
        {
            AndroidJavaObject _vibrator;
            bool _looked;

            public void Buzz(int milliseconds)
            {
                var vibrator = Resolve();
                if (vibrator == null) return;

                // Below API 26 the one-argument form is all there is; the manifest's minimum is
                // 26, so the modern call is the one that runs, and the old one is the fallback
                // for a device that reports something unexpected.
                using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    var effect = effects.CallStatic<AndroidJavaObject>(
                        "createOneShot", (long)milliseconds, -1);
                    vibrator.Call("vibrate", effect);
                }
            }

            AndroidJavaObject Resolve()
            {
                if (_looked) return _vibrator;
                _looked = true;

                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                return _vibrator;
            }
        }
    }
}
