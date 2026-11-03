using UnityEngine;

namespace HR.Audio
{
    [CreateAssetMenu(fileName = "AudioBank", menuName = "Hypnotic Rings/Audio Bank")]
    public sealed class AudioBank : ScriptableObject
    {
        public AudioClip ambient;
        public AudioClip knock;
        public AudioClip lose;
        public AudioClip click;

        public AudioClip[] passTones;

        public bool IsComplete =>
            ambient != null && knock != null && lose != null && click != null
            && passTones != null && passTones.Length >= HR.Core.ToneLadder.Steps
            && System.Array.TrueForAll(passTones, clip => clip != null);
    }
}
