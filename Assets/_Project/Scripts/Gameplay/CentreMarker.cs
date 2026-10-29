using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// The red circle at the centre of the tunnel: the line a ring must not cross.
    ///
    /// Drawn at exactly the radius the losing rule tests, so the player watches the boundary
    /// close instead of being told afterwards that something touched something. It carries no
    /// collider — the ball flies over it freely, and the circle has nothing to do with the ball.
    ///
    /// Its translucency is mixed into an opaque colour rather than drawn with alpha, which is
    /// what the rest of the game does: the unlit shader the tunnel uses ignores alpha entirely,
    /// and there is never anything behind this circle except the background.
    /// </summary>
    public sealed class CentreMarker : MonoBehaviour
    {
        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        static readonly int Colour = Shader.PropertyToID("_Color");

        /// <summary>Sits behind the rings and the ball, which are at zero, so depth sorts it.</summary>
        public const float Depth = 0.2f;

        /// <summary>Enough sides that the boundary does not read as a polygon at this size.</summary>
        const int Sides = 48;

        MaterialPropertyBlock _block;
        Renderer _renderer;
        Color _red;
        float _opacity;

        public void Initialise(GameConfig config, Transform parent, Material material)
        {
            _block = new MaterialPropertyBlock();
            _red = config.redColour;
            _opacity = config.redOpacity;

            transform.SetParent(parent, false);
            transform.localPosition = new Vector3(0f, 0f, Depth);

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = TunnelFactory.BuildDiscMesh(config.redRadius, Sides);

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;

            Apply(Color.black);
        }

        /// <summary>
        /// Repaints the circle over a background. Called on every theme change, because the
        /// mixed colour only means anything against the background it was mixed with.
        /// </summary>
        public void Apply(Color background)
        {
            if (_renderer == null) return;

            var mixed = Color.Lerp(background, _red, _opacity);
            mixed.a = 1f;

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColour, mixed);
            _block.SetColor(Colour, mixed);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>What the circle is actually painted, so a contrast check can read it.</summary>
        public static Color MixedColour(Color background, Color red, float opacity)
        {
            var mixed = Color.Lerp(background, red, opacity);
            mixed.a = 1f;
            return mixed;
        }
    }
}
