using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HR.App
{
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] string nextScene = "Main";

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
