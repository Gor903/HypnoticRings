using HR.Core;
using UnityEngine;

namespace HR.Audio
{
    /// <summary>
    /// Plays the game's sounds, with music and effects answering to separate switches.
    ///
    /// Separate is the point: a player who mutes the drone usually still wants to hear the note
    /// a pass makes, and one switch for both makes that impossible. The two volumes are also
    /// different by default — the drone sits under the effects, never over them.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        /// <summary>Enough voices for overlapping effects, few enough to stay honest on a phone.</summary>
        public const int VoiceCount = 6;

        AudioBank _bank;
        SaveData _settings;
        AudioSource _music;
        AudioSource[] _voices;
        readonly ToneLadder _ladder = new ToneLadder();
        int _next;

        public bool Ready { get; private set; }

        /// <summary>Highest voice count ever in use at once, so the pool size can be judged.</summary>
        public int PeakVoicesInUse { get; private set; }

        public void Initialise(AudioBank bank, SaveData settings)
        {
            _settings = settings;

            // A missing bank silences the game rather than throwing. Sound is the one subsystem
            // whose absence must never cost a player their run.
            if (bank == null || !bank.IsComplete)
            {
                Debug.LogWarning("[HR] audio: the bank is missing clips, running silent");
                Ready = false;
                return;
            }

            _bank = bank;

            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = bank.ambient;
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.spatialBlend = 0f;
                _voices[i] = voice;
            }

            Ready = true;
            ApplySettings();
        }

        /// <summary>Re-reads the switches. Called whenever the settings screen changes one.</summary>
        public void ApplySettings()
        {
            if (!Ready) return;

            _music.volume = _settings.musicVolume;
            foreach (var voice in _voices) voice.volume = _settings.soundVolume;

            bool wantMusic = _settings.musicEnabled && _settings.musicVolume > 0f;
            if (wantMusic && !_music.isPlaying) _music.Play();
            else if (!wantMusic && _music.isPlaying) _music.Stop();
        }

        /// <summary>A pass, sounded at whatever step of the ladder the streak has reached.</summary>
        public int PlayPass(double time)
        {
            int step = _ladder.Pass(time);
            if (Ready) Play(_bank.passTones[Mathf.Clamp(step, 0, _bank.passTones.Length - 1)]);
            return step;
        }

        public void PlayKnock() { if (Ready) Play(_bank.knock); }
        public void PlayClick() { if (Ready) Play(_bank.click); }

        /// <summary>A loss. Also ends the streak, per CONTRACT.md G-13.</summary>
        public void PlayLose()
        {
            _ladder.Reset();
            if (Ready) Play(_bank.lose);
        }

        /// <summary>A pause breaks the streak without making a sound.</summary>
        public void ResetStreak() => _ladder.Reset();

        public int StreakStep => _ladder.Step;

        void Play(AudioClip clip)
        {
            if (clip == null || !_settings.soundEnabled || _settings.soundVolume <= 0f) return;

            // Round robin rather than first-free: an effect that arrives when every voice is busy
            // should steal the oldest one, not be dropped. A silent pass reads as a missed pass.
            var voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;

            voice.clip = clip;
            voice.volume = _settings.soundVolume;
            voice.Play();

            int busy = 0;
            foreach (var other in _voices) if (other.isPlaying) busy++;
            if (busy > PeakVoicesInUse) PeakVoicesInUse = busy;
        }

        /// <summary>
        /// Hands the output back when the game loses focus.
        ///
        /// Android does not do this for us. Without it the drone keeps playing over whatever the
        /// player switched to, which is the single most common one-star review a small game gets.
        /// </summary>
        void OnApplicationPause(bool paused) => SetFocus(!paused);

        void OnApplicationFocus(bool focused) => SetFocus(focused);

        void SetFocus(bool active)
        {
            if (!Ready) return;

            AudioListener.pause = !active;
            if (!active) _ladder.Reset();
        }
    }
}
