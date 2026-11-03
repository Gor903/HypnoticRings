using System;
using System.Collections.Generic;
using System.IO;
using HR.Core;
using UnityEngine;

namespace HR.Save
{
    public sealed class SaveService
    {
        public const string FileName = "save.json";

        readonly string _path;

        public SaveService(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            _path = path;
        }

        public static SaveService Default() =>
            new SaveService(Path.Combine(Application.persistentDataPath, FileName));

        public string FilePath => _path;
        public bool Exists => File.Exists(_path);

        public IReadOnlyList<string> LastReport => _report;
        readonly List<string> _report = new List<string>();

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
            catch {  }
        }
    }
}
