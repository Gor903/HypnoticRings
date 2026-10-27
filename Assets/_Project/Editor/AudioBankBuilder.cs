using System.IO;
using HR.Audio;
using HR.Core;
using UnityEditor;
using UnityEngine;

namespace HR.Editor
{
    /// <summary>
    /// Points the audio bank at the clips `tools/gen_audio.py` generated, and sets their import
    /// settings for a phone.
    ///
    /// Unlike the game config this is rebuilt every time rather than left alone: nothing here is
    /// hand-tuned, every field is derived from a filename, and a bank holding a stale reference
    /// is a sound that silently stops playing.
    /// </summary>
    public static class AudioBankBuilder
    {
        public const string AudioDir = "Assets/_Project/Audio";
        public const string BankPath = AudioDir + "/AudioBank.asset";

        [MenuItem("Hypnotic Rings/Setup/Rebuild audio bank")]
        public static void Rebuild()
        {
            var bank = AssetDatabase.LoadAssetAtPath<AudioBank>(BankPath);
            if (bank == null)
            {
                Directory.CreateDirectory(AudioDir);
                bank = ScriptableObject.CreateInstance<AudioBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            bank.ambient = Import("ambient", streaming: true);
            bank.knock = Import("knock");
            bank.lose = Import("lose");
            bank.click = Import("click");

            bank.passTones = new AudioClip[ToneLadder.Steps];
            for (int step = 0; step < ToneLadder.Steps; step++)
                bank.passTones[step] = Import($"pass-{step}");

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            if (!bank.IsComplete)
            {
                Debug.LogError("[HR] audio bank is incomplete — run tools/gen_audio.py first");
                return;
            }

            Debug.Log($"[HR] audio bank rebuilt: {BankPath}");
        }

        static AudioClip Import(string name, bool streaming = false)
        {
            string path = $"{AudioDir}/{name}.wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogError($"[HR] audio: {path} is missing");
                return null;
            }

            var settings = importer.defaultSampleSettings;

            // Vorbis for the drone, which is long enough for the saving to matter; the one-shots
            // stay uncompressed, where decode latency costs more than the kilobytes it saves.
            settings.loadType = streaming
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = streaming
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.PCM;
            settings.quality = 0.7f;

            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = streaming;

            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
