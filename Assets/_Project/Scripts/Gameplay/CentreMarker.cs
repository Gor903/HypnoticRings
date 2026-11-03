using UnityEngine;

namespace HR.Gameplay
{
    public sealed class CentreMarker : MonoBehaviour
    {
        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        static readonly int Colour = Shader.PropertyToID("_Color");

        public const float Depth = 0.2f;

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

        public static Color MixedColour(Color background, Color red, float opacity)
        {
            var mixed = Color.Lerp(background, red, opacity);
            mixed.a = 1f;
            return mixed;
        }
    }
}
