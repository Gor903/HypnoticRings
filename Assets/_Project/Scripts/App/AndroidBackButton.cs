using System;
using UnityEngine;
using UnityEngine.Scripting;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace HR.App
{
    public sealed class AndroidBackButton : IDisposable
    {
        public event Action Pressed;

        public bool Active { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR

        const int FirstApiWithDispatcher = 33;

        AndroidJavaObject _dispatcher;
        Callback _callback;

        [Preserve]
        sealed class Callback : AndroidJavaProxy
        {
            readonly Action _invoked;

            public Callback(Action invoked) : base("android.window.OnBackInvokedCallback")
                => _invoked = invoked;

            [Preserve]
            public void onBackInvoked() => _invoked();
        }
#endif

        public AndroidBackButton()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                if (version.GetStatic<int>("SDK_INT") < FirstApiWithDispatcher) return;

            _callback = new Callback(OnInvokedFromAndroid);

            AndroidApplication.InvokeOnUIThread(() =>
            {
                try
                {
                    int priority;
                    using (var dispatcherClass = new AndroidJavaClass("android.window.OnBackInvokedDispatcher"))
                        priority = dispatcherClass.GetStatic<int>("PRIORITY_OVERLAY");

                    var activity = AndroidApplication.currentActivity;
                    _dispatcher = activity.Call<AndroidJavaObject>("getOnBackInvokedDispatcher");
                    _dispatcher.Call("registerOnBackInvokedCallback", priority, _callback);
                    Active = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HR] system back stays on the key-event path: {e.Message}");
                }
            });
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void OnInvokedFromAndroid() =>
            AndroidApplication.InvokeOnUnityMainThread(() => Pressed?.Invoke());
#endif

        public void Raise() => Pressed?.Invoke();

        public void Dispose()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_dispatcher == null) return;

            var dispatcher = _dispatcher;
            var callback = _callback;
            _dispatcher = null;
            _callback = null;
            Active = false;

            AndroidApplication.InvokeOnUIThread(() =>
            {
                try { dispatcher.Call("unregisterOnBackInvokedCallback", callback); }
                catch (Exception e) { Debug.LogWarning($"[HR] system back not released: {e.Message}"); }
                dispatcher.Dispose();
            });
#endif
        }
    }
}
