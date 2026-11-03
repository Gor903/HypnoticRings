using System;
using System.IO;

namespace HR.Core
{
    public static class ArtifactPaths
    {
        public const string OverrideVariable = "HR_ARTIFACTS";

        public const string FolderName = "game-builds";

        public const string GameName = "HypnoticRings";

        public static string Root
        {
            get
            {
                string over = Environment.GetEnvironmentVariable(OverrideVariable);
                if (!string.IsNullOrEmpty(over)) return over;

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(home)) home = Environment.GetEnvironmentVariable("HOME") ?? ".";

                return Path.Combine(home, "Desktop", FolderName, GameName);
            }
        }

        public static string Builds => Path.Combine(Root, "builds");

        public static string Screenshots => Path.Combine(Root, "screenshots");

        public static string StoreArt => Path.Combine(Root, "store-art");

        public static string Ensure(params string[] parts)
        {
            string path = Root;
            for (int i = 0; i < parts.Length; i++) path = Path.Combine(path, parts[i]);

            Directory.CreateDirectory(path);
            return path;
        }
    }
}
