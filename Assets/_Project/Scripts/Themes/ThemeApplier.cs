using System.Collections.Generic;
using HR.Core;
using HR.Gameplay;
using UnityEngine;

namespace HR.Themes
{
    /// <summary>
    /// Paints a theme onto a running game.
    ///
    /// It touches colours and nothing else. CONTRACT.md §3.5 draws that line hard, and it is
    /// not a tidiness rule: if a palette could reach a radius, a thickness or a physics
    /// material, then picking a theme would change how the game plays, and every balance
    /// number would be true only for whichever theme produced it.
    ///
    /// Applies live — no scene reload, so switching a theme mid-run keeps the run.
    /// </summary>
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

        public Theme Current { get; private set; } = Theme.Monochrome;
        public ThemePalette Palette { get; private set; }

        public void Initialise(Camera camera, TunnelController tunnel,
                               Renderer ballRenderer, Renderer ballOutlineRenderer = null)
        {
            _camera = camera;
            _tunnel = tunnel;
            _ballRenderer = ballRenderer;
            _ballOutlineRenderer = ballOutlineRenderer;
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
                _tunnel.SetRingColours(_ringColours);
            }

            Paint(_ballRenderer, ToColor(palette.Ball));
            Paint(_ballOutlineRenderer, ToColor(palette.BallOutline));
        }

        /// <summary>
        /// Written through a property block rather than onto the material: assigning to
        /// renderer.material clones it, and a clone per theme change is a leak that only shows
        /// up after somebody sits on the theme carousel.
        /// </summary>
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
