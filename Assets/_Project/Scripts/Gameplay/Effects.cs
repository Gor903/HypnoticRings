using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// The ball's trail, the flash at a passed hole, and the shake on a crush — spec 11.4.
    ///
    /// Everything it can ever draw is built once, in Initialise. The game loop only moves and
    /// recolours what already exists, because CONTRACT.md forbids Instantiate and Destroy while
    /// a run is going, and because an effect that allocates is an effect that stutters on the
    /// exact frame it is meant to celebrate.
    ///
    /// Switched off, it is not merely invisible: nothing ticks, nothing moves, and nothing it
    /// owns touches the simulation at any point. That is what lets "Reduce effects" be proved to
    /// leave the game identical rather than asserted to.
    /// </summary>
    public sealed class Effects : MonoBehaviour
    {
        const int FlashPool = 4;

        GameConfig _config;
        Camera _camera;
        Transform _ball;

        Transform[] _trail;
        Renderer[] _trailRenderers;
        float[] _trailAge;
        int _trailNext;
        float _trailTimer;

        Transform[] _flash;
        Renderer[] _flashRenderers;
        float[] _flashAge;
        Color[] _flashColour;
        int _flashNext;

        Vector3 _cameraHome;
        float _shakeAge = -1f;

        Transform _collapsing;
        Vector3 _collapseHome;
        Vector3 _collapsePivot;
        float _collapseAge = -1f;

        MaterialPropertyBlock _block;
        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColour = Shader.PropertyToID("_Color");

        /// <summary>False when the player has asked for fewer effects.</summary>
        public bool Enabled { get; set; } = true;

        public void Initialise(GameConfig config, Camera camera, Transform ball, Material material)
        {
            _config = config;
            _camera = camera;
            _ball = ball;
            _block = new MaterialPropertyBlock();
            if (_camera != null) _cameraHome = _camera.transform.localPosition;

            _trail = new Transform[Mathf.Max(0, config.trailPoints)];
            _trailRenderers = new Renderer[_trail.Length];
            _trailAge = new float[_trail.Length];

            for (int i = 0; i < _trail.Length; i++)
            {
                // Each point is a little smaller than the last, so the trail tapers rather than
                // reading as a row of identical dots.
                float scale = Mathf.Lerp(0.55f, 0.18f, i / Mathf.Max(1f, _trail.Length - 1f));
                _trail[i] = NewDisc($"Trail{i}", config.ballRadius * scale, material, out _trailRenderers[i]);
                _trailAge[i] = float.MaxValue;
            }

            _flash = new Transform[FlashPool];
            _flashRenderers = new Renderer[FlashPool];
            _flashAge = new float[FlashPool];
            _flashColour = new Color[FlashPool];

            for (int i = 0; i < FlashPool; i++)
            {
                _flash[i] = NewDisc($"Flash{i}", config.ballRadius * 1.6f, material, out _flashRenderers[i]);
                _flashAge[i] = float.MaxValue;
            }
        }

        Transform NewDisc(string name, float radius, Material material, out Renderer renderer)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = TunnelFactory.BuildDiscMesh(radius, 16);

            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return go.transform;
        }

        /// <summary>Hides everything and puts the camera back. Called whenever a run ends.</summary>
        public void Clear()
        {
            for (int i = 0; i < _trail.Length; i++)
            {
                _trailAge[i] = float.MaxValue;
                _trail[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < _flash.Length; i++)
            {
                _flashAge[i] = float.MaxValue;
                _flash[i].gameObject.SetActive(false);
            }

            _shakeAge = -1f;
            _trailTimer = 0f;
            if (_camera != null) _camera.transform.localPosition = _cameraHome;

            if (_collapsing != null)
            {
                _collapsing.localScale = Vector3.one;
                _collapsing.position = _collapseHome;
                _collapsing = null;
            }
            _collapseAge = -1f;
        }

        /// <summary>Drops a flash where a ring was passed, in that ring's own colour.</summary>
        public void Flash(Vector3 position, Color colour)
        {
            if (!Enabled || _flash == null) return;

            int slot = _flashNext;
            _flashNext = (_flashNext + 1) % _flash.Length;

            _flash[slot].position = new Vector3(position.x, position.y, position.z + 0.02f);
            _flashColour[slot] = colour;
            _flashAge[slot] = 0f;
            _flash[slot].gameObject.SetActive(true);
        }

        public void Shake()
        {
            if (!Enabled) return;
            _shakeAge = 0f;
        }

        /// <summary>
        /// Spec 11.4: on a crush the rings draw in around the ball over four tenths of a second.
        ///
        /// Scaling the tunnel about the ball rather than editing each ring's radius, because the
        /// radii are gameplay and this is not — it starts only once the run is already lost, and
        /// the isolation gate would catch it immediately if it ever ran during one.
        /// </summary>
        public void Collapse(Transform tunnel, Vector3 around)
        {
            if (!Enabled || tunnel == null) return;

            _collapsing = tunnel;
            _collapseHome = tunnel.position;
            _collapsePivot = around;
            _collapseAge = 0f;
        }

        /// <summary>
        /// One step of every running effect. Takes dt rather than reading Time, so a test can
        /// drive it to an exact moment instead of waiting for one.
        /// </summary>
        public void Tick(float dt)
        {
            if (!Enabled) return;

            StepTrail(dt);
            StepFlashes(dt);
            StepShake(dt);
            StepCollapse(dt);
        }

        void StepTrail(float dt)
        {
            if (_trail.Length == 0 || _ball == null) return;

            // Points are laid down on a clock, not per frame: otherwise the trail's length is
            // whatever the frame rate happens to be that second.
            float interval = _config.trailFadeSeconds / _trail.Length;
            _trailTimer += dt;

            if (_trailTimer >= interval)
            {
                _trailTimer -= interval;
                _trail[_trailNext].position = _ball.position;
                _trailAge[_trailNext] = 0f;
                _trail[_trailNext].gameObject.SetActive(true);
                _trailNext = (_trailNext + 1) % _trail.Length;
            }

            for (int i = 0; i < _trail.Length; i++)
            {
                if (_trailAge[i] == float.MaxValue) continue;

                _trailAge[i] += dt;
                float life = _trailAge[i] / _config.trailFadeSeconds;

                if (life >= 1f)
                {
                    _trailAge[i] = float.MaxValue;
                    _trail[i].gameObject.SetActive(false);
                    continue;
                }

                Tint(_trailRenderers[i], TrailColour, (float)Easing.Fade(life) * 0.5f);
            }
        }

        void StepFlashes(float dt)
        {
            for (int i = 0; i < _flash.Length; i++)
            {
                if (_flashAge[i] == float.MaxValue) continue;

                _flashAge[i] += dt;
                float life = _flashAge[i] / _config.passFlashSeconds;

                if (life >= 1f)
                {
                    _flashAge[i] = float.MaxValue;
                    _flash[i].gameObject.SetActive(false);
                    continue;
                }

                // Swells and fades at once, so it reads as a soft bloom rather than a dot.
                float grow = 1f + 0.8f * (float)Easing.SmoothStep(life);
                _flash[i].localScale = new Vector3(grow, grow, 1f);
                Tint(_flashRenderers[i], _flashColour[i], (float)Easing.Fade(life));
            }
        }

        void StepCollapse(float dt)
        {
            if (_collapseAge < 0f || _collapsing == null) return;

            _collapseAge += dt;
            float life = Mathf.Clamp01(_collapseAge / _config.crushSeconds);
            float scale = Mathf.Lerp(1f, 0.82f, (float)Easing.SmoothStep(life));

            // Scaling about a pivot: the ball stays put while everything closes in on it.
            _collapsing.localScale = new Vector3(scale, scale, 1f);
            _collapsing.position = _collapsePivot + (_collapseHome - _collapsePivot) * scale;

            if (life < 1f) return;
            _collapseAge = -1f;
        }

        void StepShake(float dt)
        {
            if (_shakeAge < 0f || _camera == null) return;

            _shakeAge += dt;
            float life = _shakeAge / _config.crushSeconds;

            if (life >= 1f)
            {
                _shakeAge = -1f;
                _camera.transform.localPosition = _cameraHome;
                return;
            }

            // Two frequencies that do not share a period, so the camera does not trace a line.
            float amplitude = _config.crushShakeAmplitude;
            float x = (float)Easing.Shake(life, 23.0) * amplitude;
            float y = (float)Easing.Shake(life, 17.0) * amplitude;
            _camera.transform.localPosition = _cameraHome + new Vector3(x, y, 0f);
        }

        /// <summary>Set by the theme so the trail belongs to the palette it is drawn in.</summary>
        public Color TrailColour { get; set; } = Color.white;

        void Tint(Renderer renderer, Color colour, float alpha)
        {
            // Never renderer.material: that clones the material, once per point, every run.
            renderer.GetPropertyBlock(_block);
            var faded = colour;
            faded.a = alpha;
            _block.SetColor(BaseColour, faded);
            _block.SetColor(LegacyColour, faded);
            renderer.SetPropertyBlock(_block);
        }
    }
}
