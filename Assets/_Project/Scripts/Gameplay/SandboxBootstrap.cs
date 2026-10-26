#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// Stands up a tunnel in the sandbox scene. The scene file holds a camera and this component
    /// and nothing else — everything visible is built by code, so the scene can be regenerated.
    ///
    /// Compiled out of player builds: it is a workbench, the sandbox scene is deliberately absent
    /// from the build list, and a class nothing can instantiate has no business shipping.
    /// </summary>
    public sealed class SandboxBootstrap : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] bool compress = true;
        [SerializeField] bool rotate = true;

        public TunnelController Tunnel { get; private set; }
        public BallController Ball { get; private set; }

        void Start()
        {
            if (config == null)
            {
                Debug.LogError("[HR] Sandbox has no GameConfig assigned", this);
                enabled = false;
                return;
            }

            Tunnel = TunnelFactory.Build(config, transform, out BallController ball);
            Ball = ball;
            Tunnel.CompressionEnabled = compress;
            Tunnel.RotationEnabled = rotate;
        }
    }
}
#endif
