using System;
using System.Collections.Generic;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// The rules that came in with the flying ball: how a run throws it, what the red circle
    /// means, and the balance table now that it is an asset. DECISIONS D-058.
    /// </summary>
    public class FlightAndTableTests
    {
        GameConfig _config;
        TunnelSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _settings = _config.ToSettings();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void A_launch_starts_inside_the_opening_ring_and_touches_nothing()
        {
            var band = BallLaunch.BandFor(_settings);
            double wall = _settings.TargetRadius - _settings.Thickness * 0.5;

            for (int seed = 1; seed <= 200; seed++)
            {
                var plan = BallLaunch.For(seed, band.Inner, band.Outer, _config.cruiseSpeed);

                Assert.LessOrEqual(plan.Distance + _settings.BallRadius, wall + 1e-9,
                    $"seed {seed} starts the ball overlapping its ring");
                Assert.GreaterOrEqual(plan.Distance, band.Inner - 1e-9,
                    $"seed {seed} starts the ball off the band");
                Assert.AreEqual(_config.cruiseSpeed, plan.Speed, 1e-6,
                    $"seed {seed} starts at the wrong speed");
            }
        }

        [Test]
        public void A_launch_never_overlaps_the_red_circle()
        {
            var band = BallLaunch.BandFor(_settings);
            for (int seed = 1; seed <= 200; seed++)
            {
                var plan = BallLaunch.For(seed, band.Inner, band.Outer, _config.cruiseSpeed);
                Assert.Greater(plan.Distance - _settings.BallRadius, _settings.RedRadius,
                    $"seed {seed} starts the ball on the losing circle");
            }
        }

        [Test]
        public void The_same_seed_throws_the_ball_the_same_way_and_different_seeds_do_not()
        {
            var band = BallLaunch.BandFor(_settings);
            var a = BallLaunch.For(7, band.Inner, band.Outer, 5.0);
            var again = BallLaunch.For(7, band.Inner, band.Outer, 5.0);

            Assert.AreEqual(a.X, again.X, 1e-12);
            Assert.AreEqual(a.VelocityY, again.VelocityY, 1e-12);

            // Spread, not merely difference: a launch that always headed the same way would
            // still pass a two-seed comparison while making every run open identically.
            var headings = new HashSet<int>();
            for (int seed = 1; seed <= 200; seed++)
            {
                var plan = BallLaunch.For(seed, band.Inner, band.Outer, 5.0);
                double deg = Math.Atan2(plan.VelocityY, plan.VelocityX) * TunnelMath.DegPerRad;
                headings.Add((int)Math.Floor((deg + 360.0) % 360.0 / 45.0));
            }

            Assert.AreEqual(8, headings.Count, "the launch never uses some eighths of the circle");
        }

        [Test]
        public void The_losing_radius_is_the_circle_the_player_can_see()
        {
            // The rule and the drawing have to be the same number, or the player is watching a
            // boundary that is not the one being tested.
            Assert.AreEqual(_config.redRadius + _config.thickness * 0.5, _settings.CrushRadius, 1e-9);

            Assert.IsTrue(TunnelMath.HasReachedRed(_settings.CrushRadius, _settings.Thickness,
                                                   _settings.RedRadius));
            Assert.IsFalse(TunnelMath.HasReachedRed(_settings.CrushRadius + 1e-4, _settings.Thickness,
                                                    _settings.RedRadius));
        }

        [Test]
        public void The_red_circle_reads_on_every_theme()
        {
            // Not a text contrast — this is a disc, and it only has to be unmistakably there.
            const double visible = 1.5;

            foreach (var theme in ThemePalette.All)
            {
                var palette = ThemePalette.For(theme);
                var background = HR.Themes.ThemeApplier.ToColor(palette.Background);

                var drawn = CentreMarker.MixedColour(background, _config.redColour, _config.redOpacity);
                double ratio = Contrast.Ratio(ToRgb(drawn), palette.Background);

                Assert.Greater(ratio, visible, $"the circle is {ratio:F2} against {theme}");
            }
        }

        static Rgb ToRgb(Color colour) => new Rgb(
            (byte)Mathf.Clamp(Mathf.RoundToInt(colour.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(colour.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(colour.b * 255f), 0, 255));

        [Test]
        public void The_table_keeps_the_timer_and_the_retraction_apart()
        {
            var curve = new DifficultyCurve(_settings);

            // The opening is the point of the whole change: the timer barely moves while the
            // snap-back after a pass is allowed to grow into it.
            Assert.Less(curve.MaxCompressionSpeedAt(0), curve.MaxCompressionSpeedAt(80));
            Assert.Less(curve.CompressionSpeedAt(0), curve.CompressionSpeedAt(80));

            // D-059 cut both by four: ten seconds a ring is a screensaver, and the floor that
            // matters is not a round number but the hand — the weakest one notices after 0.30s
            // and needs 0.37s of lined-up opening, so a budget is only honest at several of those.
            Assert.GreaterOrEqual(curve.TimeBudgetAt(0), 3.5, "the opening kills too fast");
            Assert.GreaterOrEqual(curve.TimeBudgetAt(80), 2.0, "the ceiling kills too fast");

            for (int rings = 0; rings <= 120; rings++)
                Assert.GreaterOrEqual(curve.MaxCompressionSpeedAt(rings), curve.CompressionSpeedAt(rings),
                    $"at {rings} rings the ceiling is below the speed it caps");
        }

        [Test]
        public void The_asset_and_the_built_in_rows_say_the_same_thing()
        {
            var table = ScriptableObject.CreateInstance<DifficultyTable>();
            try
            {
                var rows = table.ToAnchors();
                Assert.AreEqual(DifficultyCurve.DefaultAnchors.Count, rows.Count);

                for (int i = 0; i < rows.Count; i++)
                {
                    var expected = DifficultyCurve.DefaultAnchors[i];
                    Assert.AreEqual(expected.Rings, rows[i].Rings, $"row {i} rings");
                    Assert.AreEqual(expected.CompressionSpeed, rows[i].CompressionSpeed, 1e-6, $"row {i} speed");
                    Assert.AreEqual(expected.MaxCompressionSpeed, rows[i].MaxCompressionSpeed, 1e-6, $"row {i} ceiling");
                    Assert.AreEqual(expected.GapArcWidth, rows[i].GapArcWidth, 1e-6, $"row {i} opening");
                    Assert.AreEqual(expected.AutoSpinDeg, rows[i].AutoSpinDeg, 1e-6, $"row {i} spin");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void A_mistyped_table_is_refused_rather_than_quietly_used()
        {
            var backwards = new List<CurveAnchor>
            {
                new CurveAnchor(10, 0.15, 1.0, 3.2, 0.0),
                new CurveAnchor(0, 0.18, 1.6, 2.8, 12.0),
            };
            Assert.IsNotEmpty(DifficultyCurve.Validate(backwards));
            Assert.Throws<ArgumentException>(() => new DifficultyCurve(_settings, backwards));

            var ceilingUnderSpeed = new List<CurveAnchor> { new CurveAnchor(0, 0.5, 0.2, 3.2, 0.0) };
            Assert.IsNotEmpty(DifficultyCurve.Validate(ceilingUnderSpeed));
        }

        [Test]
        public void The_narrowest_opening_is_a_floor_a_table_cannot_go_under()
        {
            // The floor used to be unreachable, which read as dead. It is load-bearing now that
            // the rows live in an asset a balance pass can edit by hand.
            var tooNarrow = new List<CurveAnchor> { new CurveAnchor(0, 0.15, 1.0, 0.1, 0.0) };
            var curve = new DifficultyCurve(_settings, tooNarrow);

            Assert.AreEqual(_settings.MinGapWidth, curve.GapArcWidthAt(0), 1e-9,
                "a row narrower than the floor was used as written");
            Assert.GreaterOrEqual(_settings.MinGapWidth, _settings.BallRadius * 2.0,
                "the floor itself does not admit the ball");
        }
    }
}
