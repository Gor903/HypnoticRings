using HR.Core;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    public sealed class TouchLedgerTests
    {
        TouchLedger _ledger;

        [SetUp]
        public void SetUp()
        {
            _ledger = new TouchLedger();
            _ledger.Watch(ringId: 1);
        }

        bool Step(double t, double radial, bool contact = true) => _ledger.Step(t, radial, contact);

        [Test]
        public void A_reversal_against_the_wall_is_a_touch()
        {
            Step(0.00, 5.0);
            Assert.IsTrue(Step(0.01, -5.0), "flying out then in is exactly what a bounce is");
            Assert.AreEqual(1, _ledger.Touches);
        }

        [Test]
        public void The_first_step_invents_nothing()
        {
            Assert.IsFalse(Step(0.0, -5.0));
            Assert.AreEqual(0, _ledger.Touches);
        }

        [Test]
        public void Travelling_inward_is_not_a_touch_on_every_step()
        {
            Step(0.00, 5.0);
            Assert.IsTrue(Step(0.01, -5.0));

            for (int i = 2; i < 12; i++) Step(i * 0.01, -5.0);

            Assert.AreEqual(1, _ledger.Touches, "a single bounce was counted as many");
        }

        [Test]
        public void A_seam_between_two_colliders_is_one_knock_and_not_two()
        {
            Step(0.000, 5.0);
            Assert.IsTrue(Step(0.001, -5.0), "the knock itself");

            Step(0.002, 5.0);
            Assert.IsFalse(Step(0.003, -5.0), "the same knock, on the far side of a seam");

            Assert.AreEqual(1, _ledger.Touches);
        }

        [Test]
        public void Two_real_bounces_are_never_merged()
        {
            Step(0.00, 5.0);
            Assert.IsTrue(Step(0.01, -5.0));

            Step(0.20, 5.0);
            Assert.IsTrue(Step(0.21, -5.0));

            Assert.AreEqual(2, _ledger.Touches);
        }

        [Test]
        public void Sliding_along_a_wall_is_not_a_touch()
        {
            Step(0.00, 0.01);
            Assert.IsFalse(Step(0.10, -0.01), "noise was counted as a bounce");
            Step(0.20, 0.01);
            Assert.IsFalse(Step(0.30, -0.01));

            Assert.AreEqual(0, _ledger.Touches);
        }

        [Test]
        public void A_reversal_in_open_space_is_not_a_touch()
        {
            Step(0.00, 5.0, contact: true);
            Assert.IsFalse(Step(0.01, -5.0, contact: false));
            Assert.AreEqual(0, _ledger.Touches);
        }

        [Test]
        public void A_graze_on_the_way_out_costs_nothing()
        {
            Step(0.00, 5.0);
            Assert.IsFalse(Step(0.01, 3.0), "still going out, just slower");
            Assert.IsFalse(Step(0.02, 1.0));
            Assert.AreEqual(0, _ledger.Touches);
        }

        [Test]
        public void A_tally_belongs_to_a_ring_and_dies_with_it()
        {
            Step(0.00, 5.0);
            Step(0.01, -5.0);
            Assert.AreEqual(1, _ledger.Touches);

            _ledger.Watch(ringId: 2);
            Assert.AreEqual(0, _ledger.Touches, "the new ring inherited the old one's mistakes");
            Assert.AreEqual(2, _ledger.RingId);
        }

        [Test]
        public void Watching_the_same_ring_again_does_not_forgive_it()
        {
            Step(0.00, 5.0);
            Step(0.01, -5.0);

            _ledger.Watch(ringId: 1);
            Assert.AreEqual(1, _ledger.Touches,
                "re-watching the ring the ball is already in wiped its tally");
        }

        [Test]
        public void Nought_touches_is_better_than_one_and_never_a_failure()
        {
            Assert.IsFalse(_ledger.IsOver(allowed: 1));

            Step(0.00, 5.0);
            Step(0.01, -5.0);
            Assert.IsFalse(_ledger.IsOver(allowed: 1), "one touch is the allowance, not the failure");

            Step(0.20, 5.0);
            Step(0.21, -5.0);
            Assert.IsTrue(_ledger.IsOver(allowed: 1), "the second touch should end it");
        }

        [Test]
        public void A_reset_forgets_the_ring_as_well_as_the_count()
        {
            Step(0.00, 5.0);
            Step(0.01, -5.0);

            _ledger.Reset();

            Assert.AreEqual(0, _ledger.Touches);
            Assert.AreEqual(-1, _ledger.RingId);
            Assert.IsFalse(Step(0.02, -5.0), "a reset ledger counted against no ring at all");
        }
    }
}
