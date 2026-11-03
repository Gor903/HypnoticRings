#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using UnityEngine;

namespace HR.Gameplay
{
    public sealed class SandboxBootstrap : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] bool compress = true;
        [SerializeField] bool rotate = true;
        [Tooltip("Same seed replays the same tunnel — for reproducing a sighting.")]
        [SerializeField] int seed = 0;

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

            Tunnel = TunnelFactory.Build(config, transform, out BallController ball, seed);
            Ball = ball;
            Tunnel.CompressionEnabled = compress;
            Tunnel.RotationEnabled = rotate;
        }
    }
}
#endif
