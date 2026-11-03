using UnityEngine;

namespace HR.Gameplay
{
    public sealed class Haptics
    {
        public interface IDriver
        {
            void Buzz(int milliseconds);
        }

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

        sealed class Android : IDriver
        {
            AndroidJavaObject _vibrator;
            bool _looked;

            public void Buzz(int milliseconds)
            {
                var vibrator = Resolve();
                if (vibrator == null) return;

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
