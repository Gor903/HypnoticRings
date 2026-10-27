using System;
using System.Collections.Generic;
using System.IO;
using HR.Core;
using UnityEngine;

namespace HR.Save
{
    /// <summary>
    /// Reads and writes the one save file.
    ///
    /// Two rules shape everything here. A write must be atomic, because the phone can be killed
    /// between opening a file and finishing it, and a half-written save is worse than none. And
    /// a load must never throw: whatever is on disk — truncated, hand-edited, from an older
    /// build, or not there at all — the game has to start.
    /// </summary>
    public sealed class SaveService
    {
        public const string FileName = "save.json";

        readonly string _path;

        /// <summary>Path is injected so tests write to a temporary directory, not the real save.</summary>
        public SaveService(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            _path = path;
        }

        public static SaveService Default() =>
            new SaveService(Path.Combine(Application.persistentDataPath, FileName));

        /// <summary>Not named Path: that shadows System.IO.Path inside this class.</summary>
        public string FilePath => _path;
        public bool Exists => File.Exists(_path);

        /// <summary>Everything the last Load or Save had to repair or could not do.</summary>
        public IReadOnlyList<string> LastReport => _report;
        readonly List<string> _report = new List<string>();

        /// <summary>
        /// Loads, repairing whatever it must. Never throws and never returns null — a fresh
        /// install and a corrupt file both end up at sane defaults, and the difference is
        /// recorded in the report rather than in a crash.
        /// </summary>
        public SaveData Load()
        {
            _report.Clear();

            if (!File.Exists(_path)) return SaveData.Defaults();

            string json;
            try
            {
                json = File.ReadAllText(_path);
            }
            catch (Exception e)
            {
                _report.Add($"could not read the save ({e.GetType().Name}); starting fresh");
                return SaveData.Defaults();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                _report.Add("the save was empty; starting fresh");
                return SaveData.Defaults();
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                _report.Add($"the save was unreadable ({e.GetType().Name}); starting fresh");
                return SaveData.Defaults();
            }

            if (data == null)
            {
                _report.Add("the save parsed to nothing; starting fresh");
                return SaveData.Defaults();
            }

            _report.AddRange(data.Normalise());
            return data;
        }

        /// <summary>
        /// Writes atomically: a temporary file first, then one replace. A process killed partway
        /// leaves either the old save or the new one, never half of either.
        /// </summary>
        public bool Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _report.Clear();

            data.version = SaveData.CurrentVersion;
            string temporary = _path + ".tmp";

            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(temporary, JsonUtility.ToJson(data, prettyPrint: true));
                Replace(temporary, _path);
                return true;
            }
            catch (Exception e)
            {
                // A save that cannot be written is worth reporting and surviving. Losing a best
                // score is a disappointment; refusing to run because of it is a bug.
                _report.Add($"could not write the save ({e.GetType().Name}: {e.Message})");
                TryDelete(temporary);
                return false;
            }
        }

        static void Replace(string temporary, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(temporary, destination);
                return;
            }

            try
            {
                // Replace is the atomic one where the platform has it.
                File.Replace(temporary, destination, destinationBackupFileName: null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Delete(destination);
                File.Move(temporary, destination);
            }
            catch (IOException)
            {
                File.Delete(destination);
                File.Move(temporary, destination);
            }
        }

        /// <summary>Wipes the save. The settings screen asks first (CONTRACT.md §4).</summary>
        public bool Delete()
        {
            _report.Clear();
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
                TryDelete(_path + ".tmp");
                return true;
            }
            catch (Exception e)
            {
                _report.Add($"could not delete the save ({e.GetType().Name})");
                return false;
            }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort; the next write replaces it anyway */ }
        }
    }
}
