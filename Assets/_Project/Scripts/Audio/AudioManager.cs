using HR.Core;
using UnityEngine;

namespace HR.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        public const int VoiceCount = 6;

        AudioBank _bank;
        SaveData _settings;
        AudioSource _music;
        AudioSource[] _voices;
        readonly ToneLadder _ladder = new ToneLadder();
        int _next;

        public bool Ready { get; private set; }

        public int PeakVoicesInUse { get; private set; }

        public void Initialise(AudioBank bank, SaveData settings)
        {
            _settings = settings;

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

        public void ApplySettings()
        {
            if (!Ready) return;

            _music.volume = _settings.musicVolume;
            foreach (var voice in _voices) voice.volume = _settings.soundVolume;

            bool wantMusic = _settings.musicEnabled && _settings.musicVolume > 0f;
            if (wantMusic && !_music.isPlaying) _music.Play();
            else if (!wantMusic && _music.isPlaying) _music.Stop();
        }

        public int PlayPass(double time)
        {
            int step = _ladder.Pass(time);
            if (Ready) Play(_bank.passTones[Mathf.Clamp(step, 0, _bank.passTones.Length - 1)]);
            return step;
        }

        public void PlayKnock() { if (Ready) Play(_bank.knock); }
        public void PlayClick() { if (Ready) Play(_bank.click); }

        public void PlayLose()
        {
            _ladder.Reset();
            if (Ready) Play(_bank.lose);
        }

        public void ResetStreak() => _ladder.Reset();

        public int StreakStep => _ladder.Step;

        void Play(AudioClip clip)
        {
            if (clip == null || !_settings.soundEnabled || _settings.soundVolume <= 0f) return;

            var voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;

            voice.clip = clip;
            voice.volume = _settings.soundVolume;
            voice.Play();

            int busy = 0;
            foreach (var other in _voices) if (other.isPlaying) busy++;
            if (busy > PeakVoicesInUse) PeakVoicesInUse = busy;
        }

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
