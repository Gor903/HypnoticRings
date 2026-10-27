using System.Collections.Generic;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// Reuses rings rather than creating and destroying them. An endless game creates a ring
    /// every few seconds forever, and Instantiate/Destroy in that loop is both the frame spike
    /// and the memory growth the phase 3 gate is looking for.
    ///
    /// Each ring keeps its segment children and their buffers for the life of the pool, so
    /// renting one costs an activation and a Configure, not an allocation.
    /// </summary>
    public sealed class RingPool
    {
        readonly Stack<RingController> _free = new Stack<RingController>();
        readonly List<RingController> _all = new List<RingController>();
        readonly GameConfig _config;
        readonly Transform _parent;
        readonly Material _material;

        public RingPool(GameConfig config, Transform parent, Material material)
        {
            _config = config;
            _parent = parent;
            _material = material;
        }

        /// <summary>Rings ever created. Should settle at a small number and never climb again.</summary>
        public int Created => _all.Count;
        public int InUse => _all.Count - _free.Count;
        public int Available => _free.Count;

        public RingController Rent()
        {
            if (_free.Count > 0)
            {
                var reused = _free.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }

            var ring = TunnelFactory.CreateRing(_config, _parent, _material, _all.Count);
            _all.Add(ring);
            return ring;
        }

        public void Return(RingController ring)
        {
            if (ring == null) return;
            ring.gameObject.SetActive(false);
            _free.Push(ring);
        }

        /// <summary>Warms the pool so the first seconds of a run do not allocate.</summary>
        public void Prewarm(int count)
        {
            var rented = new RingController[count];
            for (int i = 0; i < count; i++) rented[i] = Rent();
            for (int i = 0; i < count; i++) Return(rented[i]);
        }
    }
}
