namespace HR.Editor
{
    public enum ReleaseOrientation
    {
        Portrait,
        Landscape,
        Auto
    }

    public static class ReleaseSettings
    {
        public const string ApplicationId = ProjectSetup.ApplicationId;
        public const string ProductName = ProjectSetup.ProductName;
        public const string CompanyName = ProjectSetup.CompanyName;

        public const string VersionName = ProjectSetup.VersionName;

        public const int MinSdk = ProjectSetup.MinSdk;
        public const int TargetSdk = ProjectSetup.TargetSdk;

        public const ReleaseOrientation Orientation = ReleaseOrientation.Portrait;

        public static readonly string[] ScenePaths =
        {
            SceneBuilder.BootScene,
            SceneBuilder.MainScene,
        };

        public const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        public const string ActivityClass = "com.unity3d.player.UnityPlayerGameActivity";

        public const string IconPath = "Assets/Art/Generated/icon_1024.png";
        public const string AdaptiveForegroundPath = "Assets/Art/Generated/icon_fg.png";
        public const string AdaptiveBackgroundPath = "Assets/Art/Generated/icon_bg.png";

        public static string OutputDirectory => HR.Core.ArtifactPaths.Builds;
        public const string BundleName = "hypnoticrings.aab";

        public const string EnvKeystore = "HYPNOTICRINGS_KEYSTORE";
        public const string EnvKeystorePass = "HYPNOTICRINGS_KEYSTORE_PASS";
        public const string EnvKeyAlias = "HYPNOTICRINGS_KEY_ALIAS";
        public const string EnvKeyPass = "HYPNOTICRINGS_KEY_PASS";

        public const bool RequiresInternet = true;
    }
}
