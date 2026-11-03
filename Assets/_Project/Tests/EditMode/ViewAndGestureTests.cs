using System;
using HR.Core;
using HR.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace HR.Tests.EditMode
{
    public class ViewAndGestureTests
    {
        static readonly (string Name, int Width, int Height)[] Shapes =
        {
            ("9x16", 1080, 1920),
            ("9x20", 1080, 2400),
            ("3x4", 1536, 2048),
        };

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

        double[] LiveRadii()
        {
            var radii = new double[_settings.RingsAhead + 2];
            for (int i = 0; i < radii.Length; i++)
                radii[i] = _settings.TargetRadius + _settings.Step * (i - 1);
            return radii;
        }

        (double Width, double Above, double Below) Frame(int width, int height)
        {
            double halfHeight = _config.orthoSize;
            double halfWidth = halfHeight * width / (double)height;

            double above = halfHeight + _config.CameraOffsetY;
            double below = halfHeight - _config.CameraOffsetY;
            return (halfWidth, above, below);
        }

        [Test]
        public void The_tunnel_centre_sits_above_the_middle_of_the_screen()
        {
            Assert.Greater(_config.centreHeightFraction, 0.5f,
                "the tunnel centre is at or below the middle, which is what it was moved off");

            Assert.Less(_config.CameraOffsetY, 0f, "the camera did not follow the framing");
        }

        [Test]
        public void Every_shape_shows_at_least_six_rings()
        {
            foreach (var shape in Shapes)
            {
                var frame = Frame(shape.Width, shape.Height);

                double reach = Math.Sqrt(frame.Width * frame.Width + frame.Below * frame.Below);

                int onScreen = 0;
                foreach (double radius in LiveRadii())
                    if (radius - _settings.Thickness * 0.5 <= reach) onScreen++;

                Assert.GreaterOrEqual(onScreen, 6,
                    $"{shape.Name} shows only {onScreen} rings");
            }
        }

        [Test]
        public void The_reference_shape_holds_both_rings_the_player_reads()
        {
            var frame = Frame(Shapes[0].Width, Shapes[0].Height);
            double room = Math.Min(frame.Width, Math.Min(frame.Above, frame.Below));

            double ballRing = _settings.TargetRadius;
            double nextRing = ballRing + _settings.Step;

            Assert.LessOrEqual(nextRing + _settings.Thickness * 0.5, room,
                $"the next ring reaches {nextRing + _settings.Thickness * 0.5:F2} and the frame gives {room:F2}");
        }

        [Test]
        public void The_outermost_ring_is_not_left_short_of_the_frame()
        {
            var frame = Frame(Shapes[0].Width, Shapes[0].Height);
            double outermost = _settings.TargetRadius + _settings.Step * _settings.RingsAhead;

            Assert.GreaterOrEqual(outermost, frame.Below,
                $"the tunnel ends at {outermost:F2} but the frame reaches {frame.Below:F2} below the centre");
        }

        static double Sweep(in GestureSettings settings, double degrees, double seconds, int samples)
        {
            var tracker = new GestureTracker(settings);
            double dt = seconds / samples;
            double step = degrees / samples;
            const double radius = 3.0;

            tracker.Begin(radius, 0.0);
            for (int i = 1; i <= samples; i++)
            {
                double radians = step * i * Math.PI / 180.0;
                tracker.Move(Math.Cos(radians) * radius, Math.Sin(radians) * radius, dt);
            }

            return tracker.AngleDeg;
        }

        double FullTurnArcDeg => 360.0 / _config.sensitivity;

        [Test]
        public void A_small_finger_arc_turns_the_rings_a_whole_circle()
        {
            Assert.LessOrEqual(FullTurnArcDeg, 120.0 + 1e-9,
                $"a full turn still costs {FullTurnArcDeg:F0} degrees of finger");

            double turned = Sweep(_config.ToGestureSettings(), FullTurnArcDeg, seconds: 1.0, samples: 24);
            Assert.AreEqual(360.0, turned, 1e-6, "the arc did not come to a full turn");
        }

        [Test]
        public void A_brisk_arc_is_not_clipped_by_the_speed_cap()
        {
            double turned = Sweep(_config.ToGestureSettings(), FullTurnArcDeg, seconds: 0.3, samples: 24);
            Assert.AreEqual(360.0, turned, 1e-6, "the speed cap ate part of the gesture");

            var capped = new GestureSettings(_config.sensitivity, 900.0, _config.inertiaDecay,
                                             _config.deadZoneRadius, false);
            Assert.Less(Sweep(capped, FullTurnArcDeg, 0.3, 24), 360.0 - 1.0,
                        "the old cap no longer clips, so raising it bought nothing");
        }

        [Test]
        public void The_settings_slider_still_scales_the_feel()
        {
            double atOne = Sweep(_config.ToGestureSettings(1f), 90.0, 1.0, 18);
            double atMin = Sweep(_config.ToGestureSettings(SaveData.MinSensitivity), 90.0, 1.0, 18);
            double atMax = Sweep(_config.ToGestureSettings(SaveData.MaxSensitivity), 90.0, 1.0, 18);

            Assert.AreEqual(atOne * SaveData.MinSensitivity, atMin, 1e-6);
            Assert.AreEqual(atOne * SaveData.MaxSensitivity, atMax, 1e-6);
        }
    }
}
