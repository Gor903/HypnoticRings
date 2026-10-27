namespace HR.Editor
{
    /// <summary>How the game handles rotation. Decided before the UI is built.</summary>
    public enum ReleaseOrientation
    {
        Portrait,
        Landscape,
        Auto        // both ways; the UI has to work at every aspect ratio
    }

    /// <summary>
    /// The single source of truth for everything the release build depends on.
    /// These are the values Apply writes and Verify checks, so the two can never
    /// drift apart.
    /// </summary>
    public static class ReleaseSettings
    {
        // Irreversible once published — confirm with a human before first upload.
        // Deliberately not literals. ProjectSetup already declares these and applies them to the
        // player settings; a second copy here would be a second truth, and it arrived already
        // disagreeing — the template's ProductName was "HypnoticRings" against the project's
        // "Hypnotic Rings", which is the launcher label a player reads.
        public const string ApplicationId = ProjectSetup.ApplicationId;
        public const string ProductName = ProjectSetup.ProductName;
        public const string CompanyName = ProjectSetup.CompanyName;

        /// <summary>Shown to users. The version code is the one the store orders by.</summary>
        public const string VersionName = ProjectSetup.VersionName;

        /// <summary>
        /// Android 8.0. Not a preference: Unity 6000.6's AndroidSdkVersions enum
        /// starts at 26, so a lower value is silently clamped rather than
        /// rejected. Adaptive icons are native from here.
        /// </summary>
        public const int MinSdk = ProjectSetup.MinSdk;
        public const int TargetSdk = ProjectSetup.TargetSdk;

        /// <summary>
        /// Reversible on paper, expensive in practice: it decides the whole UI
        /// layout and the aspect ratio every store screenshot is taken at.
        /// </summary>
        public const ReleaseOrientation Orientation = ReleaseOrientation.Portrait;

        /// <summary>
        /// Exactly the scenes that ship, in build order — the first one loads at
        /// launch. A single-scene game lists one; a level-based game lists all of
        /// them. Verify fails on any difference in either direction, so a sample
        /// scene that sneaks into the list blocks the build instead of shipping.
        /// </summary>
        public static readonly string[] ScenePaths =
        {
            // From SceneBuilder, not retyped: it is the thing that creates them, and a second
            // copy of a path is a second thing to forget when one of them moves.
            SceneBuilder.BootScene,
            SceneBuilder.MainScene,
        };

        /// <summary>
        /// Lives in Assets/Plugins/Android. Its absence is a build-blocking
        /// problem, not a warning: deleting it silently changes the permission
        /// set the store shows users.
        /// </summary>
        public const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        /// <summary>
        /// The activity the manifest must declare. Paired with the GameActivity
        /// application entry; switching that player setting changes this too.
        /// </summary>
        public const string ActivityClass = "com.unity3d.player.UnityPlayerGameActivity";

        public const string IconPath = "Assets/Art/Generated/icon_1024.png";
        public const string AdaptiveForegroundPath = "Assets/Art/Generated/icon_fg.png";
        public const string AdaptiveBackgroundPath = "Assets/Art/Generated/icon_bg.png";

        public const string OutputDirectory = "Build";
        public const string BundleName = "hypnoticrings.aab";

        // Signing comes from the environment; these are only the variable names.
        public const string EnvKeystore = "HYPNOTICRINGS_KEYSTORE";
        public const string EnvKeystorePass = "HYPNOTICRINGS_KEYSTORE_PASS";
        public const string EnvKeyAlias = "HYPNOTICRINGS_KEY_ALIAS";
        public const string EnvKeyPass = "HYPNOTICRINGS_KEY_PASS";

        /// <summary>
        /// False while the shipping build contains no ad SDK. A game that opens
        /// no socket must not declare INTERNET: it would contradict the privacy
        /// policy and the data-safety form, and users would see a permission the
        /// game never uses.
        ///
        /// Adding an ad SDK flips three things together, and they must move
        /// together or the build is dishonest: this constant, the
        /// <c>tools:node="remove"</c> entry in the custom manifest, and the
        /// data-safety answers in the submission pack.
        /// </summary>
        public const bool RequiresInternet = false;
    }
}
