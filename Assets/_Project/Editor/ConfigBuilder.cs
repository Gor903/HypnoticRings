using System.IO;
using HR.Gameplay;
using UnityEditor;
using UnityEngine;

namespace HR.Editor
{
    /// <summary>
    /// Creates the config assets the game reads. Idempotent: an existing asset is left alone,
    /// because its numbers are what the balance pass tunes and regenerating would undo that.
    /// </summary>
    public static class ConfigBuilder
    {
        public const string ConfigDir = "Assets/_Project/Config";
        public const string GameConfigPath = ConfigDir + "/GameConfig.asset";

        [MenuItem("Hypnotic Rings/Setup/Create missing config assets")]
        public static void EnsureAll()
        {
            Directory.CreateDirectory(ConfigDir);
            EnsureGameConfig();
            AssetDatabase.SaveAssets();
        }

        public static GameConfig EnsureGameConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (existing != null) return existing;

            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, GameConfigPath);
            Debug.Log($"[HR] Created {GameConfigPath} with contract defaults");
            return config;
        }

        public static GameConfig Load()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
                throw new FileNotFoundException($"{GameConfigPath} is missing; run Create missing config assets");
            return config;
        }
    }
}
