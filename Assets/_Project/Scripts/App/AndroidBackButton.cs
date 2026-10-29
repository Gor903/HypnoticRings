using System;
using UnityEngine;
using UnityEngine.Scripting;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace HR.App
{
    /// <summary>
    /// Delivers Android's system back to the game.
    ///
    /// Unity's native layer turns a back <em>key event</em> into Escape, which is all the game
    /// ever needed — until Android 13 put predictive back behind
    /// <c>OnBackInvokedCallback</c>. From then on the gesture is routed to that callback
    /// instead of being sent as a key, and because androidx's activity registers one of its own,
    /// the press is swallowed: nothing reaches Unity and nothing closes the screen. The manifest
    /// opt-out is not a way out either — it is on the generated launcher manifest, which wins the
    /// merge against this project's own (D-053), and Android is retiring it besides.
    ///
    /// So the callback is registered here, at default priority, which puts it ahead of androidx's.
    /// Registering one also means the activity no longer finishes itself on back, and the game
    /// decides what back means — including leaving the menu, which it already did for Escape.
    /// </summary>
    public sealed class AndroidBackButton : IDisposable
    {
        /// <summary>Raised on the Unity main thread, never on Android's.</summary>
        public event Action Pressed;

        /// <summary>
        /// True once Android is delivering back here rather than as a key. Worth having on the
        /// object itself: which of the two paths a given phone takes is the first thing anyone
        /// debugging this will want, and it is not visible from anywhere else.
        /// </summary>
        public bool Active { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        // The dispatcher arrived in Android 13; before that the key event still comes through.
        const int FirstApiWithDispatcher = 33;

        AndroidJavaObject _dispatcher;
        Callback _callback;

        // Preserved because nothing in managed code ever calls onBackInvoked: it is reached by
        // name from the JNI bridge, which the High stripping this project builds with cannot see.
        [Preserve]
        sealed class Callback : AndroidJavaProxy
        {
            readonly Action _invoked;

            public Callback(Action invoked) : base("android.window.OnBackInvokedCallback")
                => _invoked = invoked;

            // Named for Java, called by Android on the UI thread.
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

            // Registration is only legal on the UI thread, and this runs from Awake on Unity's.
            AndroidApplication.InvokeOnUIThread(() =>
            {
                try
                {
                    // Overlay priority, not default. Unity registers a callback of its own, and
                    // among equals the dispatcher prefers whichever registered last — which on a
                    // device was Unity's. It takes the press and the game never hears about it:
                    // dumpsys listed this callback present but Unity's on top. Overlay sits
                    // strictly above the default band, so registration order stops deciding.
                    int priority;
                    using (var dispatcherClass = new AndroidJavaClass("android.window.OnBackInvokedDispatcher"))
                        priority = dispatcherClass.GetStatic<int>("PRIORITY_OVERLAY");

                    // Not disposed: this is the player's own activity, not a handle to own.
                    var activity = AndroidApplication.currentActivity;
                    _dispatcher = activity.Call<AndroidJavaObject>("getOnBackInvokedDispatcher");
                    _dispatcher.Call("registerOnBackInvokedCallback", priority, _callback);
                    Active = true;
                }
                catch (Exception e)
                {
                    // A device that refuses the registration keeps the key-event path, which is
                    // the behaviour on everything below Android 13 anyway.
                    Debug.LogWarning($"[HR] system back stays on the key-event path: {e.Message}");
                }
            });
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void OnInvokedFromAndroid() =>
            AndroidApplication.InvokeOnUnityMainThread(() => Pressed?.Invoke());
#endif

        /// <summary>The seam a test presses, standing in for the platform.</summary>
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
