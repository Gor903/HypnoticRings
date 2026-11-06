using System;
using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public class DifficultyCurveTests
    {
        static readonly TunnelSettings Settings = TunnelSettings.Default;
        static DifficultyCurve Curve => new DifficultyCurve(Settings);

        static void AssertSmooth(Func<int, double> sample, int upTo, string what)
        {
            double min = double.MaxValue, max = double.MinValue;
            for (int n = 0; n <= upTo; n++)
            {
                double v = sample(n);
                if (v < min) min = v;
                if (v > max) max = v;
            }

            double allowed = 0.15 * (max - min);
            for (int n = 0; n < upTo; n++)
            {
                double step = Math.Abs(sample(n + 1) - sample(n));
                Assert.LessOrEqual(step, allowed,
                    $"{what} jumps {step:F4} between ring {n} and {n + 1}; range {min:F3}..{max:F3} allows {allowed:F4}");
            }
        }

        [Test]
        public void Compression_matches_the_contract_at_every_anchor()
        {
            var c = Curve;
            Assert.AreEqual(1.28 / 3.00, c.CompressionSpeedAt(0), 1e-9);
            Assert.AreEqual(1.23 / 2.45, c.CompressionSpeedAt(10), 1e-9);
            Assert.AreEqual(1.14 / 1.93, c.CompressionSpeedAt(30), 1e-9);
            Assert.AreEqual(0.90 / 1.59, c.CompressionSpeedAt(80), 1e-9);
        }

        [Test]
        public void Retraction_matches_the_contract_at_every_anchor()
        {
            var c = Curve;
            Assert.AreEqual(2.0, c.MaxCompressionSpeedAt(0), 1e-9);
            Assert.AreEqual(3.0, c.MaxCompressionSpeedAt(10), 1e-9);
            Assert.AreEqual(4.0, c.MaxCompressionSpeedAt(30), 1e-9);
            Assert.AreEqual(5.0, c.MaxCompressionSpeedAt(80), 1e-9);
        }

        [Test]
        public void Gap_width_matches_the_contract_at_every_anchor()
        {
            var c = Curve;

            Assert.AreEqual(4.32, c.GapArcWidthAt(0), 1e-9);
            Assert.AreEqual(3.90, c.GapArcWidthAt(10), 1e-9);
            Assert.AreEqual(3.48, c.GapArcWidthAt(30), 1e-9);
            Assert.AreEqual(2.80, c.GapArcWidthAt(80), 1e-9);
        }

        [Test]
        public void Spin_matches_the_contract_at_every_anchor_past_the_tutorial()
        {
            var c = Curve;
            Assert.AreEqual(15.0, c.AutoSpinAt(10), 1e-9);
            Assert.AreEqual(40.0, c.AutoSpinAt(30), 1e-9);
            Assert.AreEqual(70.0, c.AutoSpinAt(80), 1e-9);
        }

        [Test]
        public void The_first_rings_teach_rather_than_test()
        {
            var c = Curve;
            for (int n = 0; n < DifficultyCurve.TutorialRings; n++)
                Assert.AreEqual(0.0, c.AutoSpinAt(n), $"ring {n} spins during the tutorial");
        }

        [Test]
        public void Rings_only_ever_arrive_sooner()
        {
            var c = Curve;
            for (int n = 0; n < 200; n++)
                Assert.LessOrEqual(c.RingIntervalAt(n + 1), c.RingIntervalAt(n) + 1e-12,
                    $"rings got slower between {n} and {n + 1}");
        }

        [Test]
        public void The_corridor_only_ever_tightens_and_never_past_the_floor()
        {
            var c = Curve;
            var settings = TunnelSettings.Default;
            double floor = settings.BallRadius * 2.0 * TunnelSettings.CorridorFloorInDiameters;

            for (int n = 0; n < 200; n++)
            {
                Assert.LessOrEqual(c.ClearanceAt(n + 1), c.ClearanceAt(n) + 1e-12,
                    $"the corridor widened between {n} and {n + 1}");
                Assert.GreaterOrEqual(c.ClearanceAt(n), floor - 1e-12,
                    $"the corridor at {n} is under the floor a ball can use");
            }
        }

        [Test]
        public void Openings_only_ever_narrow()
        {
            var c = Curve;
            for (int n = 0; n < 200; n++)
                Assert.GreaterOrEqual(c.GapArcWidthAt(n), c.GapArcWidthAt(n + 1) - 1e-12,
                    $"the opening widened between ring {n} and {n + 1}");
        }

        [Test]
        public void Spin_only_ever_increases_after_the_tutorial()
        {
            var c = Curve;
            for (int n = DifficultyCurve.TutorialRings; n < 200; n++)
                Assert.LessOrEqual(c.AutoSpinAt(n), c.AutoSpinAt(n + 1) + 1e-12,
                    $"spin slowed between ring {n} and {n + 1}");
        }

        [Test]
        public void Nothing_lurches_between_neighbouring_rings()
        {
            var c = Curve;
            AssertSmooth(c.CompressionSpeedAt, 200, "compression");
            AssertSmooth(c.GapArcWidthAt, 200, "gap width");
            AssertSmooth(n => c.AutoSpinAt(n), 200, "spin");
        }

        [Test]
        public void Difficulty_stops_climbing_at_the_ceiling()
        {
            var c = Curve;
            Assert.AreEqual(c.CompressionSpeedAt(c.Ceiling), c.CompressionSpeedAt(5000), 1e-12);
            Assert.AreEqual(c.GapArcWidthAt(c.Ceiling), c.GapArcWidthAt(5000), 1e-12);
            Assert.AreEqual(c.AutoSpinAt(c.Ceiling), c.AutoSpinAt(5000), 1e-12);
        }

        [Test]
        public void The_opening_never_drops_below_what_the_ball_can_fit_through()
        {
            var c = Curve;
            for (int n = 0; n <= 500; n++)
                Assert.GreaterOrEqual(c.GapArcWidthAt(n), Settings.MinGapWidth - 1e-12,
                    $"ring {n} would be narrower than the floor");
        }

        [Test]
        public void The_time_budget_shrinks_but_never_vanishes()
        {
            var c = Curve;
            for (int n = 0; n <= 500; n++)
                Assert.Greater(c.TimeBudgetAt(n), 1.0, $"ring {n} leaves under a second to react");

            Assert.Less(c.TimeBudgetAt(80), c.TimeBudgetAt(0), "the game never gets more urgent");
        }

        [Test]
        public void Every_ring_the_curve_produces_is_catchable()
        {
            var c = Curve;
            for (int n = 0; n <= 500; n++)
            {
                var snapshot = c.SnapshotAt(n);
                double ceiling = TunnelMath.MaxReachableSpinDeg(snapshot.ComfortSpinDeg, snapshot.TimeBudget);
                Assert.LessOrEqual(snapshot.AutoSpinDeg, ceiling,
                    $"ring {n} asks for {snapshot.AutoSpinDeg:F1} deg/s, catchable limit is {ceiling:F1}");
            }
        }

        [Test]
        public void Neighbour_directions_follow_the_stages_the_contract_describes()
        {
            var c = Curve;
            Assert.IsFalse(c.SnapshotAt(0).AlternateSpinDirection, "tutorial rings should move together");
            Assert.IsTrue(c.SnapshotAt(10).AlternateSpinDirection, "rings should alternate from ten");
            Assert.IsTrue(c.SnapshotAt(20).AlternateSpinDirection);
            Assert.IsFalse(c.SnapshotAt(30).AlternateSpinDirection, "directions should go random from thirty");
        }
    }

    public class ReachabilityTests
    {
        [Test]
        public void The_spin_ceiling_is_exactly_where_the_rule_stops_holding()
        {
            const double comfort = 200.0, budget = 4.0;
            double ceiling = TunnelMath.MaxReachableSpinDeg(comfort, budget);

            Assert.IsTrue(TunnelMath.IsReachable(180.0, ceiling - 0.01, comfort, budget));
            Assert.IsFalse(TunnelMath.IsReachable(180.0, ceiling + 0.01, comfort, budget));
        }

        [Test]
        public void A_ring_under_the_ceiling_is_catchable_from_anywhere()
        {
            const double comfort = 200.0, budget = 4.0;
            double ceiling = TunnelMath.MaxReachableSpinDeg(comfort, budget);

            for (double delta = -180.0; delta <= 180.0; delta += 5.0)
                Assert.IsTrue(TunnelMath.IsReachable(delta, ceiling, comfort, budget),
                    $"a gap {delta} degrees away was not catchable at the ceiling spin");
        }

        [Test]
        public void An_impossible_budget_forbids_spin_rather_than_returning_nonsense()
        {
            Assert.AreEqual(0.0, TunnelMath.MaxReachableSpinDeg(200.0, 0.0));
            Assert.AreEqual(0.0, TunnelMath.MaxReachableSpinDeg(200.0, -1.0));
            Assert.AreEqual(0.0, TunnelMath.MaxReachableSpinDeg(10.0, 1.0));
        }

        [Test]
        public void The_generator_refuses_to_emit_a_ring_nobody_could_catch()
        {
            var difficulty = new DifficultySnapshot(0.6, 4.0, 1.6, 400.0, false, 3.83, 200.0, 1.10, 1.28);
            var generator = new RingGenerator(5);
            double ceiling = TunnelMath.MaxReachableSpinDeg(200.0, 3.83);

            double previous = 0.0;
            for (int i = 0; i < 200; i++)
            {
                double spin = generator.Next(difficulty, previous).AutoSpinDeg;
                Assert.LessOrEqual(Math.Abs(spin), ceiling + 1e-9,
                    $"ring {i} spins at {spin:F1}, past the catchable limit {ceiling:F1}");
                previous = spin;
            }

            Assert.Greater(generator.Clamped, 0, "nothing was clamped, so the guard was never exercised");
        }

        [Test]
        public void Clamping_is_reported_rather_than_silent()
        {
            var gentle = new DifficultySnapshot(0.15, 1.0, 3.2, 20.0, false, 15.33, 200.0, 1.10, 1.28);
            var generator = new RingGenerator(5);
            double previous = 0.0;
            for (int i = 0; i < 200; i++) previous = generator.Next(gentle, previous).AutoSpinDeg;

            Assert.AreEqual(0, generator.Clamped, "a comfortable curve should never need clamping");
        }
    }

    public class GameStateMachineTests
    {
        [Test]
        public void A_run_follows_the_flow_the_contract_draws()
        {
            var m = new GameStateMachine();
            Assert.AreEqual(GameState.Boot, m.State);

            Assert.IsTrue(m.Fire(GameTrigger.Loaded));
            Assert.AreEqual(GameState.Menu, m.State);

            Assert.IsTrue(m.Fire(GameTrigger.Play));
            Assert.AreEqual(GameState.Countdown, m.State);
            Assert.IsFalse(m.TunnelRuns, "the tunnel must stand still while counting down");

            Assert.IsTrue(m.Fire(GameTrigger.Started));
            Assert.AreEqual(GameState.Playing, m.State);

            Assert.IsTrue(m.Fire(GameTrigger.Crushed));
            Assert.AreEqual(GameState.GameOver, m.State);

            Assert.IsTrue(m.Fire(GameTrigger.Retry));
            Assert.AreEqual(GameState.Countdown, m.State);

            Assert.IsTrue(m.Fire(GameTrigger.Started));
            Assert.AreEqual(GameState.Playing, m.State);
        }

        [Test]
        public void Pause_and_resume_return_to_exactly_where_they_left()
        {
            var m = new GameStateMachine();
            m.Fire(GameTrigger.Loaded);
            m.Fire(GameTrigger.Play);
            m.Fire(GameTrigger.Started);

            Assert.IsTrue(m.Fire(GameTrigger.Pause));
            Assert.AreEqual(GameState.Paused, m.State);
            Assert.IsFalse(m.TunnelRuns, "the tunnel must not advance while paused");

            Assert.IsTrue(m.Fire(GameTrigger.Resume));
            Assert.AreEqual(GameState.Playing, m.State);
            Assert.IsTrue(m.TunnelRuns);
        }

        [Test]
        public void A_second_pause_while_paused_is_ignored_rather_than_breaking_anything()
        {
            var m = new GameStateMachine();
            m.Fire(GameTrigger.Loaded);
            m.Fire(GameTrigger.Play);
            m.Fire(GameTrigger.Started);
            m.Fire(GameTrigger.Pause);

            Assert.IsFalse(m.Fire(GameTrigger.Pause), "a repeated pause should be refused");
            Assert.AreEqual(GameState.Paused, m.State);
        }

        [Test]
        public void A_crush_arriving_after_the_run_ended_changes_nothing()
        {
            var m = new GameStateMachine();
            m.Fire(GameTrigger.Loaded);
            m.Fire(GameTrigger.Play);
            m.Fire(GameTrigger.Started);
            m.Fire(GameTrigger.Crushed);

            Assert.IsFalse(m.Fire(GameTrigger.Crushed));
            Assert.AreEqual(GameState.GameOver, m.State);
        }

        [Test]
        public void Back_means_pause_in_play_and_resume_when_paused()
        {
            var m = new GameStateMachine();
            m.Fire(GameTrigger.Loaded);
            m.Fire(GameTrigger.Play);
            m.Fire(GameTrigger.Started);

            m.Fire(GameTrigger.Back);
            Assert.AreEqual(GameState.Paused, m.State);

            m.Fire(GameTrigger.Back);
            Assert.AreEqual(GameState.Playing, m.State);
        }

        [Test]
        public void Themes_return_to_whichever_screen_opened_them()
        {
            var m = new GameStateMachine();
            m.Fire(GameTrigger.Loaded);

            m.Fire(GameTrigger.OpenThemes);
            m.Fire(GameTrigger.Back);
            Assert.AreEqual(GameState.Menu, m.State, "themes opened from the menu should go back to it");

            m.Fire(GameTrigger.Play);
            m.Fire(GameTrigger.Started);
            m.Fire(GameTrigger.Crushed);
            m.Fire(GameTrigger.OpenThemes);
            m.Fire(GameTrigger.Back);
            Assert.AreEqual(GameState.GameOver, m.State, "themes opened from game over should go back to it");
        }

        [Test]
        public void Play_cannot_be_started_from_the_middle_of_a_run()
        {
            var m = new GameStateMachine();
            m.Fire(GameTrigger.Loaded);
            m.Fire(GameTrigger.Play);
            Assert.IsFalse(m.Fire(GameTrigger.Play));
        }

        [Test]
        public void Every_transition_is_announced()
        {
            var m = new GameStateMachine();
            int count = 0;
            GameState lastFrom = GameState.Boot, lastTo = GameState.Boot;
            m.Changed += (from, to) => { count++; lastFrom = from; lastTo = to; };

            m.Fire(GameTrigger.Loaded);
            m.Fire(GameTrigger.Play);
            m.Fire(GameTrigger.Started);
            m.Fire(GameTrigger.Pause);

            Assert.AreEqual(4, count);
            Assert.AreEqual(GameState.Playing, lastFrom);
            Assert.AreEqual(GameState.Paused, lastTo);
        }

        [Test]
        public void A_refused_trigger_announces_nothing()
        {
            var m = new GameStateMachine();
            int count = 0;
            m.Changed += (_, __) => count++;

            Assert.IsFalse(m.Fire(GameTrigger.Play));
            Assert.AreEqual(0, count);
        }
    }

    public class ScoreKeeperTests
    {
        [Test]
        public void A_ring_is_worth_one_point_and_nothing_else()
        {
            var s = new ScoreKeeper();
            s.BeginRun();
            for (int i = 0; i < 7; i++) s.AddRing();
            Assert.AreEqual(7, s.Score);
        }

        [Test]
        public void The_best_shown_during_a_run_is_the_one_the_run_started_with()
        {
            var s = new ScoreKeeper(best: 10);
            s.BeginRun();
            for (int i = 0; i < 15; i++) s.AddRing();

            Assert.AreEqual(10, s.DisplayedBest);
            Assert.AreEqual(15, s.Best);
            Assert.IsTrue(s.IsNewBest);
        }

        [Test]
        public void A_run_that_falls_short_is_not_a_new_best()
        {
            var s = new ScoreKeeper(best: 20);
            s.BeginRun();
            for (int i = 0; i < 5; i++) s.AddRing();
            Assert.IsFalse(s.IsNewBest);
            Assert.AreEqual(20, s.Best);
        }

        [Test]
        public void The_next_run_shows_the_record_the_last_one_set()
        {
            var s = new ScoreKeeper(best: 10);
            s.BeginRun();
            for (int i = 0; i < 15; i++) s.AddRing();

            s.BeginRun();
            Assert.AreEqual(15, s.DisplayedBest);
            Assert.AreEqual(0, s.Score);
            Assert.IsFalse(s.IsNewBest);
        }

        [Test]
        public void Three_themes_are_free_so_three_can_always_be_switched()
        {
            var s = new ScoreKeeper();
            Assert.IsTrue(s.IsUnlocked(Theme.MinimalLight));
            Assert.IsTrue(s.IsUnlocked(Theme.MinimalDark));
            Assert.IsTrue(s.IsUnlocked(Theme.Neon));
            Assert.IsFalse(s.IsUnlocked(Theme.Deco));
            Assert.IsFalse(s.IsUnlocked(Theme.Sumie));
        }

        [Test]
        public void Themes_unlock_at_the_scores_the_contract_names()
        {
            var s = new ScoreKeeper(best: 25);
            Assert.IsTrue(s.IsUnlocked(Theme.Deco));
            Assert.IsFalse(s.IsUnlocked(Theme.Sumie));

            s.RestoreBest(50);
            Assert.IsTrue(s.IsUnlocked(Theme.Sumie));
            Assert.IsFalse(s.IsUnlocked(Theme.Ocean));

            s.RestoreBest(75);
            Assert.IsTrue(s.IsUnlocked(Theme.Ocean));
            Assert.IsFalse(s.IsUnlocked(Theme.Bauhaus));

            s.RestoreBest(100);
            Assert.IsTrue(s.IsUnlocked(Theme.Bauhaus));
            Assert.IsFalse(s.IsUnlocked(Theme.Crt));

            s.RestoreBest(150);
            Assert.IsTrue(s.IsUnlocked(Theme.Crt));
        }

        [Test]
        public void An_unlock_is_announced_in_the_run_that_earned_it_and_not_after()
        {
            var s = new ScoreKeeper(best: 24);
            s.BeginRun();
            for (int i = 0; i < 25; i++) s.AddRing();

            Assert.IsTrue(s.UnlockedThisRun(Theme.Deco));
            Assert.IsFalse(s.UnlockedThisRun(Theme.Sumie));

            s.BeginRun();
            Assert.IsFalse(s.UnlockedThisRun(Theme.Deco), "the unlock was announced a second time");
        }

        [Test]
        public void A_corrupt_save_cannot_poison_the_best()
        {
            var s = new ScoreKeeper(best: -5);
            Assert.AreEqual(0, s.Best);

            s.RestoreBest(-100);
            Assert.AreEqual(0, s.Best);
        }

        [Test]
        public void Resetting_progress_relocks_what_it_should()
        {
            var s = new ScoreKeeper(best: 60);
            Assert.IsTrue(s.IsUnlocked(Theme.Sumie));

            s.ResetProgress();
            Assert.AreEqual(0, s.Best);
            Assert.IsFalse(s.IsUnlocked(Theme.Sumie));
            Assert.IsTrue(s.IsUnlocked(Theme.Neon), "the free themes must stay free");
        }
    }
}
