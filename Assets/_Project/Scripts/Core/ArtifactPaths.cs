using System;
using System.IO;

namespace HR.Core
{
    /// <summary>
    /// Where everything the project produces for the outside world goes — and it is not the
    /// project.
    ///
    /// Builds, captured frames and store art are outputs, not source. Kept inside the
    /// repository they grow it without bound: seventy-six screenshots rewritten by every
    /// capture had taken the git directory past sixty megabytes for files regenerable in two
    /// minutes by one command. Worse, a diff of a slice drowned in binary noise, so the one
    /// place a reviewer looks stopped being readable.
    ///
    /// One answer, in one place, because the people asking are three: a shell script, an editor
    /// build tool, and a play-mode test. Each computing its own relative path is how two of them
    /// quietly disagree.
    ///
    /// Engine-free, like the rest of this assembly: nothing here needs a renderer or a scene,
    /// and the editor tooling must be able to ask before a project is even open.
    /// </summary>
    public static class ArtifactPaths
    {
        /// <summary>Overrides everything, so a build machine can put outputs where it likes.</summary>
        public const string OverrideVariable = "HR_ARTIFACTS";

        /// <summary>The folder every game's outputs sit beside each other in.</summary>
        public const string FolderName = "game-builds";

        /// <summary>This game's folder inside it.</summary>
        public const string GameName = "HypnoticRings";

        /// <summary>
        /// The root of this game's outputs.
        ///
        /// Default is <c>~/Desktop/game-builds/&lt;game&gt;</c> — visible, next to the other
        /// games, and nowhere near a working copy. A path that needs explaining is a path
        /// somebody will second-guess at the worst moment.
        /// </summary>
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

        /// <summary>Signed artifacts: the bundle for the store, the APK for a hand.</summary>
        public static string Builds => Path.Combine(Root, "builds");

        /// <summary>Captured frames — every one of them regenerable by one command.</summary>
        public static string Screenshots => Path.Combine(Root, "screenshots");

        /// <summary>Icon, feature graphic and the store's own screenshots.</summary>
        public static string StoreArt => Path.Combine(Root, "store-art");

        /// <summary>Creates a folder under the root and hands back its path.</summary>
        public static string Ensure(params string[] parts)
        {
            string path = Root;
            for (int i = 0; i < parts.Length; i++) path = Path.Combine(path, parts[i]);

            Directory.CreateDirectory(path);
            return path;
        }
    }
}
