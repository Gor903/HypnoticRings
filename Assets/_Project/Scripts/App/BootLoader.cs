using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HR.App
{
    /// <summary>
    /// The only job of the boot scene: hand over to the game.
    ///
    /// Boot is registered first in the build settings, so whatever it does is what a player sees
    /// when the icon is tapped. Left empty — which it was — it is a black screen that never ends,
    /// and nothing in the editor reports it, because the editor opens whichever scene was saved
    /// last and never goes through the build list at all.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] string nextScene = "Main";

        /// <summary>The scene this hands over to, so a test can check the link without playing it.</summary>
        public string NextScene => nextScene;

        IEnumerator Start()
        {
            var load = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError($"[HR] boot: '{nextScene}' is not in the build settings");
                yield break;
            }

            while (!load.isDone) yield return null;
        }
    }
}
