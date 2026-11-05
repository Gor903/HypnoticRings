using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class CountdownTests
    {
        [Test]
        public void The_digits_come_down_one_a_second()
        {
            var clock = new CountdownClock();
            Assert.AreEqual(3, clock.Digit);

            clock.Advance(0.5);
            Assert.AreEqual(3, clock.Digit, "half a second is still the first digit");

            clock.Advance(0.5);
            Assert.AreEqual(2, clock.Digit);

            clock.Advance(1.0);
            Assert.AreEqual(1, clock.Digit);

            clock.Advance(0.99);
            Assert.AreEqual(1, clock.Digit, "the last digit holds its whole second");
        }

        [Test]
        public void The_ring_dissolves_after_the_digits_and_before_the_run()
        {
            var clock = new CountdownClock();
            clock.Advance(3.0);

            Assert.AreEqual(CountdownPhase.Dissolving, clock.Phase);
            Assert.AreEqual(0, clock.Digit, "no digit is drawn while the ring dissolves");
            Assert.IsFalse(clock.Finished);

            clock.Advance(CountdownClock.DissolveSeconds);
            Assert.AreEqual(CountdownPhase.Done, clock.Phase);
            Assert.IsTrue(clock.Finished);
        }

        [Test]
        public void The_whole_wait_is_three_seconds_and_a_dissolve()
        {
            Assert.AreEqual(3.25, CountdownClock.Total, 1e-9);
        }

        [Test]
        public void The_dissolve_is_crossed_exactly_once()
        {
            var clock = new CountdownClock();
            int crossings = 0;

            for (int i = 0; i < 400; i++)
            {
                const double step = 1.0 / 60.0;
                clock.Advance(step);
                if (clock.JustCrossedDissolve(step)) crossings++;
            }

            Assert.AreEqual(1, crossings, "the solid ring must be told to dissolve once");
        }

        [Test]
        public void Time_never_runs_backwards_or_past_the_end()
        {
            var clock = new CountdownClock();
            clock.Advance(-5.0);
            Assert.AreEqual(0.0, clock.Elapsed, 1e-9);

            clock.Advance(double.NaN);
            Assert.AreEqual(0.0, clock.Elapsed, 1e-9);

            clock.Advance(99.0);
            Assert.AreEqual(CountdownClock.Total, clock.Elapsed, 1e-9);
        }

        [Test]
        public void Restarting_puts_the_first_digit_back()
        {
            var clock = new CountdownClock();
            clock.Advance(99.0);
            clock.Restart();

            Assert.AreEqual(0.0, clock.Elapsed, 1e-9);
            Assert.AreEqual(CountdownClock.FirstDigit, clock.Digit);
            Assert.IsFalse(clock.Finished);
        }

        [Test]
        public void The_countdown_counts_as_being_in_a_run()
        {
            Assert.IsTrue(GameStateMachine.IsInRun(GameState.Countdown),
                "hardcore keeps its look and the record stays put while counting down");
        }

        [Test]
        public void Nothing_but_the_start_gets_out_of_the_countdown()
        {
            foreach (GameTrigger trigger in System.Enum.GetValues(typeof(GameTrigger)))
            {
                var m = new GameStateMachine();
                m.Fire(GameTrigger.Loaded);
                m.Fire(GameTrigger.Play);
                Assert.AreEqual(GameState.Countdown, m.State);

                bool moved = m.Fire(trigger);
                if (trigger == GameTrigger.Started)
                    Assert.IsTrue(moved, "the countdown must be able to end");
                else
                    Assert.IsFalse(moved, $"{trigger} should not interrupt the countdown");
            }
        }

        [Test]
        public void A_digit_lands_hard_and_then_sits_still()
        {
            Assert.AreEqual(CountdownClock.BeatPeak, CountdownClock.ScaleAt(0.0), 1e-9,
                "a digit arrives at its peak");

            Assert.AreEqual(1.0, CountdownClock.ScaleAt(CountdownClock.BeatSeconds), 1e-9,
                "and has settled by the end of the beat");

            Assert.AreEqual(1.0, CountdownClock.ScaleAt(0.9), 1e-9,
                "and stays settled for the rest of the second");
        }

        [Test]
        public void The_beat_only_ever_shrinks()
        {
            double previous = CountdownClock.ScaleAt(0.0);
            for (double t = 0.0; t <= CountdownClock.BeatSeconds; t += 0.01)
            {
                double now = CountdownClock.ScaleAt(t);
                Assert.LessOrEqual(now, previous + 1e-9, $"the digit grew again at {t:F2}s");
                Assert.GreaterOrEqual(now, 1.0 - 1e-9, "the digit shrank past its resting size");
                previous = now;
            }
        }

        [Test]
        public void Every_digit_gets_its_own_beat()
        {
            var clock = new CountdownClock();

            for (int digit = 0; digit < CountdownClock.FirstDigit; digit++)
            {
                Assert.AreEqual(0.0, clock.DigitAge, 1e-9, $"digit {digit} did not start fresh");
                Assert.AreEqual(CountdownClock.BeatPeak, clock.BeatScale, 1e-9);

                clock.Advance(CountdownClock.BeatSeconds);
                Assert.AreEqual(1.0, clock.BeatScale, 1e-9, "the beat outlasted its own length");

                clock.Advance(CountdownClock.DigitSeconds - CountdownClock.BeatSeconds);
            }
        }

        [Test]
        public void Nothing_beats_once_the_digits_are_gone()
        {
            var clock = new CountdownClock();
            clock.Advance(CountdownClock.FirstDigit * CountdownClock.DigitSeconds);

            Assert.AreEqual(CountdownPhase.Dissolving, clock.Phase);
            Assert.AreEqual(0.0, clock.DigitAge, 1e-9);
        }

        [Test]
        public void A_run_allows_exactly_one_continue()
        {
            var snapshot = new RunSnapshot(47, GameMode.Hardcore, 5.0, 0.45, 0);
            Assert.IsTrue(snapshot.CanContinue);

            var spent = snapshot.Spent();
            Assert.IsFalse(spent.CanContinue, "a second continue in one run is not allowed");

            Assert.AreEqual(47, spent.Score, "a continue keeps the score the run died with");
            Assert.AreEqual(GameMode.Hardcore, spent.Mode);
            Assert.AreEqual(5.0, spent.Step, 1e-9, "a continue keeps the tempo");
            Assert.AreEqual(0.45, spent.CompressionSpeed, 1e-9);
        }
    }
}
