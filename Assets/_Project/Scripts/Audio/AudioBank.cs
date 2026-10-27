using UnityEngine;

namespace HR.Audio
{
    /// <summary>
    /// Every clip the game can play, in one asset.
    ///
    /// Built by a script like everything else here, rather than loaded from a Resources folder:
    /// Resources goes into the build whole, cannot be stripped by what actually references it,
    /// and gives no error when a name is wrong — only silence at the moment the sound was
    /// supposed to play.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioBank", menuName = "Hypnotic Rings/Audio Bank")]
    public sealed class AudioBank : ScriptableObject
    {
        public AudioClip ambient;
        public AudioClip knock;
        public AudioClip lose;
        public AudioClip click;

        /// <summary>The pentatonic ladder, lowest step first. Index comes from ToneLadder.</summary>
        public AudioClip[] passTones;

        /// <summary>Everything that has to be present before the manager is worth starting.</summary>
        public bool IsComplete =>
            ambient != null && knock != null && lose != null && click != null
            && passTones != null && passTones.Length >= HR.Core.ToneLadder.Steps
            && System.Array.TrueForAll(passTones, clip => clip != null);
    }
}
