using System.Collections;
using HR.Audio;
using HR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HR.Tests.PlayMode
{
    /// <summary>
    /// Phase 7's gates: the two switches are genuinely independent, the voice pool is reused
    /// rather than grown, and a loss ends the streak.
    /// </summary>
    public class AudioTests
    {
        GameObject _root;
        AudioManager _audio;
        AudioBank _bank;
        SaveData _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = SaveData.Defaults();
            _bank = ScriptableObject.CreateInstance<AudioBank>();

            // Synthesised in the test rather than loaded: what is under test is the manager's
            // routing, and a missing asset would fail this for the wrong reason.
            _bank.ambient = Silence("ambient", 1f);
            _bank.knock = Silence("knock", 0.1f);
            _bank.lose = Silence("lose", 0.5f);
            _bank.click = Silence("click", 0.05f);
            _bank.passTones = new AudioClip[ToneLadder.Steps];
            for (int i = 0; i < ToneLadder.Steps; i++) _bank.passTones[i] = Silence($"pass{i}", 0.3f);

            _root = new GameObject("Audio");
            _audio = _root.AddComponent<AudioManager>();
            _audio.Initialise(_bank, _settings);
        }

        [TearDown]
        public void TearDown()
        {
            // Global state: left paused it would silence every test that runs after this one.
            AudioListener.pause = false;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_bank != null) Object.DestroyImmediate(_bank);
        }

        static AudioClip Silence(string name, float seconds)
        {
            int samples = Mathf.Max(1, (int)(44100 * seconds));
            var clip = AudioClip.Create(name, samples, 1, 44100, false);
            clip.SetData(new float[samples], 0);
            return clip;
        }

        [UnityTest]
        public IEnumerator A_complete_bank_starts_the_manager()
        {
            Assert.IsTrue(_bank.IsComplete);
            Assert.IsTrue(_audio.Ready);
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_missing_clip_runs_silent_instead_of_throwing()
        {
            // Sound is the one subsystem whose failure must never cost a player their run.
            var broken = ScriptableObject.CreateInstance<AudioBank>();
            var go = new GameObject("Broken");
            var manager = go.AddComponent<AudioManager>();

            LogAssert.Expect(LogType.Warning, "[HR] audio: the bank is missing clips, running silent");
            Assert.DoesNotThrow(() => manager.Initialise(broken, _settings));
            Assert.IsFalse(manager.Ready);

            Assert.DoesNotThrow(() => manager.PlayKnock());
            Assert.DoesNotThrow(() => manager.PlayLose());
            Assert.DoesNotThrow(() => manager.PlayPass(0.0));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(broken);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Muting_the_music_leaves_the_effects_alone()
        {
            _settings.musicEnabled = false;
            _audio.ApplySettings();
            yield return null;

            var sources = _root.GetComponents<AudioSource>();
            Assert.IsFalse(sources[0].isPlaying, "the drone should have stopped");

            // The effect still sounds: that is the whole reason the switches are separate.
            _audio.PlayKnock();
            yield return null;

            bool anyEffect = false;
            for (int i = 1; i < sources.Length; i++) anyEffect |= sources[i].isPlaying;
            Assert.IsTrue(anyEffect, "muting the music silenced the effects too");
        }

        [UnityTest]
        public IEnumerator Muting_the_effects_leaves_the_music_alone()
        {
            _settings.soundEnabled = false;
            _audio.ApplySettings();
            _audio.PlayKnock();
            yield return null;

            var sources = _root.GetComponents<AudioSource>();
            Assert.IsTrue(sources[0].isPlaying, "muting the effects stopped the drone too");

            for (int i = 1; i < sources.Length; i++)
                Assert.IsFalse(sources[i].isPlaying, $"voice {i} sounded while effects were off");
        }

        [UnityTest]
        public IEnumerator The_voice_pool_is_reused_and_never_grows()
        {
            int before = _root.GetComponents<AudioSource>().Length;

            for (int i = 0; i < AudioManager.VoiceCount * 4; i++)
            {
                _audio.PlayKnock();
                yield return null;
            }

            Assert.AreEqual(before, _root.GetComponents<AudioSource>().Length,
                "effects allocated new sources instead of reusing the pool");
            Assert.LessOrEqual(_audio.PeakVoicesInUse, AudioManager.VoiceCount);
        }

        [UnityTest]
        public IEnumerator Losing_focus_hands_the_output_back_and_regaining_it_takes_it_again()
        {
            // Android does not do this for us. Without it the drone keeps playing over whatever
            // the player switched to, which is the one-star review a small game actually gets.
            Assert.IsFalse(AudioListener.pause);

            _root.SendMessage("OnApplicationFocus", false);
            Assert.IsTrue(AudioListener.pause, "the game kept the output after losing focus");

            _root.SendMessage("OnApplicationFocus", true);
            Assert.IsFalse(AudioListener.pause, "the game never took the output back");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Backgrounding_also_ends_the_streak()
        {
            _audio.PlayPass(0.0);
            _audio.PlayPass(0.5);
            Assert.AreEqual(1, _audio.StreakStep);

            _root.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(0, _audio.StreakStep,
                "a streak survived the game being sent to the background");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_streak_climbs_and_a_loss_puts_it_back()
        {
            Assert.AreEqual(0, _audio.PlayPass(0.0));
            Assert.AreEqual(1, _audio.PlayPass(1.0));
            Assert.AreEqual(2, _audio.PlayPass(2.0));

            _audio.PlayLose();
            Assert.AreEqual(0, _audio.StreakStep, "a loss should end the streak");
            Assert.AreEqual(0, _audio.PlayPass(2.5), "the next pass starts at the bottom again");
            yield return null;
        }
    }
}
