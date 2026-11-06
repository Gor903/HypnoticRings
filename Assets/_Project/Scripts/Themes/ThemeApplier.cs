using System.Collections.Generic;
using HR.Core;
using HR.Gameplay;
using UnityEngine;

namespace HR.Themes
{
    public sealed class ThemeApplier : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly List<Color> _ringColours = new List<Color>();
        MaterialPropertyBlock _block;

        Camera _camera;
        TunnelController _tunnel;
        Renderer _ballRenderer;
        Renderer _ballOutlineRenderer;

        public Theme Current { get; private set; } = Theme.MinimalLight;
        public ThemePalette Palette { get; private set; }

        BackdropRenderer _backdrop;

        public BackdropRenderer Backdrop => _backdrop;

        bool _effectsWanted = true;

        public void Initialise(Camera camera, TunnelController tunnel,
                               Renderer ballRenderer, Renderer ballOutlineRenderer = null)
        {
            _camera = camera;
            _tunnel = tunnel;
            _ballRenderer = ballRenderer;
            _ballOutlineRenderer = ballOutlineRenderer;

            var go = new GameObject("Backdrop");
            go.transform.SetParent(transform, false);
            _backdrop = go.AddComponent<BackdropRenderer>();
            _backdrop.Initialise(camera);
        }

        public void SetEffectsWanted(bool wanted)
        {
            _effectsWanted = wanted;
            if (_backdrop != null) _backdrop.SetEffectsWanted(wanted);
        }

        public void Apply(Theme theme) => Apply(ThemePalette.For(theme));

        public void Apply(ThemePalette palette)
        {
            if (palette == null) return;

            Palette = palette;
            Current = palette.Theme;

            var background = ToColor(palette.Background);
            if (_camera != null) _camera.backgroundColor = background;

            if (_tunnel != null)
            {
                _ringColours.Clear();
                foreach (var colour in palette.RingColours) _ringColours.Add(ToColor(colour));
                _tunnel.SetFadeColour(background);
                _tunnel.SetDepthAndGlow(palette.DepthFade, palette.Glow);
                _tunnel.SetRingColours(_ringColours);
            }

            Paint(_ballRenderer, ToColor(palette.Ball));
            Paint(_ballOutlineRenderer, ToColor(palette.BallOutline));

            if (_backdrop != null) _backdrop.Show(palette.Theme, _effectsWanted);

            _shimmerClock = 0.0;
            _shimmerTarget = ShimmerTargetFor(palette.Background);
        }

        double _shimmerClock;
        Color _shimmerTarget = Color.white;

        static Color ShimmerTargetFor(in Rgb background) =>
            Contrast.Luminance(background) < 0.5 ? Color.white : Color.black;

        public void Tick(double dt)
        {
            if (Palette == null || _tunnel == null) return;
            if (Palette.ShimmerSeconds <= 0.0 || Palette.ShimmerDepth <= 0.0) return;
            if (dt <= 0.0) return;

            double period = System.Math.Max(ThemePalette.MinShimmerSeconds, Palette.ShimmerSeconds);
            _shimmerClock = (_shimmerClock + dt) % period;

            _ringColours.Clear();
            for (int i = 0; i < Palette.RingColours.Length; i++)
            {
                double phase = _shimmerClock / period + i * 0.25;
                double swing = (System.Math.Sin(phase * System.Math.PI * 2.0) + 1.0) * 0.5;
                float amount = (float)(swing * Palette.ShimmerDepth);

                _ringColours.Add(Color.Lerp(ToColor(Palette.RingColours[i]), _shimmerTarget, amount));
            }

            _tunnel.SetRingColours(_ringColours);
        }

        void Paint(Renderer renderer, Color colour)
        {
            if (renderer == null) return;

            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, colour);
            _block.SetColor(ColorId, colour);
            renderer.SetPropertyBlock(_block);
        }

        public static Color ToColor(in Rgb colour) =>
            new Color(colour.R / 255f, colour.G / 255f, colour.B / 255f, 1f);
    }
}
