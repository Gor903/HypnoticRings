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
                _tunnel.SetSuperColour(ToColor(palette.Super));
                _tunnel.SetFadeColour(background);
                _tunnel.SetDepthAndGlow(palette.DepthFade, palette.Glow);
                _tunnel.SetRingColours(_ringColours);
            }

            Paint(_ballRenderer, ToColor(palette.Ball));
            Paint(_ballOutlineRenderer, ToColor(palette.BallOutline));

            _shimmerClock = 0.0;
            _shimmerTarget = ShimmerTargetFor(palette.Background);
        }

        double _shimmerClock;
        Color _shimmerTarget = Color.white;

        /// <summary>
        /// Which way a shimmer moves: whichever extreme is further from the background.
        ///
        /// Stated as a rule rather than hard-coded to white, because the two themes that
        /// shimmer today happen to be dark and the next one might not be. On a pale background
        /// brightening would dim the rings into it, which is the failure this is here to avoid.
        /// </summary>
        static Color ShimmerTargetFor(in Rgb background) =>
            Contrast.Luminance(background) < 0.5 ? Color.white : Color.black;

        /// <summary>
        /// Moves a shimmering palette along by a step.
        ///
        /// Driven rather than self-ticking, so a test can take it to an exact moment and so a
        /// paused game does not keep breathing. The period is floored well above the
        /// photosensitivity limit from plan [G-34] and [X-14]: the player stares straight at
        /// this tunnel, and a palette that pulses quickly stops being a style and becomes a
        /// health question.
        /// </summary>
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
                // Each colour is a quarter-cycle behind the one before it, so the tunnel reads
                // as a wave travelling outwards rather than as every ring blinking together.
                double phase = _shimmerClock / period + i * 0.25;
                double swing = (System.Math.Sin(phase * System.Math.PI * 2.0) + 1.0) * 0.5;
                float amount = (float)(swing * Palette.ShimmerDepth);

                // Always AWAY from the background, never toward it. Swinging both ways was the
                // obvious reading of "shimmer" and it cost Neon's purple ring its legibility at
                // the bottom of every cycle — contrast 2.92 against a floor of 3.0, for a
                // tenth of a second, over and over. A palette may not buy movement with
                // readability, so the dim half of the cycle is gone.
                _ringColours.Add(Color.Lerp(ToColor(Palette.RingColours[i]), _shimmerTarget, amount));
            }

            _tunnel.SetRingColours(_ringColours);
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
