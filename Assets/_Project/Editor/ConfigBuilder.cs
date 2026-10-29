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
        public const string DifficultyTablePath = ConfigDir + "/DifficultyTable.asset";

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
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(existing, GameConfigPath);
                Debug.Log($"[HR] Created {GameConfigPath} with contract defaults");
            }

            // Linked here rather than by hand: a config without a table silently falls back to
            // the curve's built-in rows, which is the one failure that looks like nothing.
            if (existing.difficulty == null)
            {
                existing.difficulty = EnsureDifficultyTable();
                EditorUtility.SetDirty(existing);
            }

            return existing;
        }

        public static DifficultyTable EnsureDifficultyTable()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DifficultyTable>(DifficultyTablePath);
            if (existing != null) return existing;

            var table = ScriptableObject.CreateInstance<DifficultyTable>();
            AssetDatabase.CreateAsset(table, DifficultyTablePath);
            Debug.Log($"[HR] Created {DifficultyTablePath} with the balance rows");
            return table;
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
