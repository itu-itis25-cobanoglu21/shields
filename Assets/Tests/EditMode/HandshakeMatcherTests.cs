using System.Collections.Generic;
using NUnit.Framework;
using Shields.Social;

namespace Shields.Game.Tests
{
    /// <summary>
    /// Hand emotes (TASK 033): mutual handshake pairing, re-arming, completion and every way a
    /// handshake is cancelled, plus the emote authority's validation and generation safety.
    /// Plain C# with an explicit clock; no transforms or animation.
    /// </summary>
    public class HandshakeMatcherTests
    {
        const int Gen = 5;

        static readonly System.Func<int, bool> Everyone = s => true;

        static EmoteSeat[] Roster(int seats, int botsFrom = int.MaxValue)
        {
            EmoteSeat[] roster = new EmoteSeat[seats];
            for (int i = 0; i < seats; i++) roster[i] = new EmoteSeat(true, true, i >= botsFrom);
            return roster;
        }

        static EmoteState Offer(int target) { return new EmoteState(EmoteFlags.HandshakeOffer, 0, target); }

        static List<HandshakePair> Match(HandshakeMatcher m, double now = 0.0)
        {
            var started = new List<HandshakePair>();
            m.Match(now, Everyone, started);
            return started;
        }

        // ================================================================ matching

        [Test]
        public void OneSidedOfferDoesNotMatch()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);

            Assert.That(Match(m), Is.Empty);
            Assert.That(m.InPair(0), Is.False);
        }

        [Test]
        public void MutualOffersMatchOnce()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);

            List<HandshakePair> started = Match(m, 2.0);

            Assert.That(started.Count, Is.EqualTo(1));
            Assert.That(started[0].SeatA, Is.EqualTo(0));
            Assert.That(started[0].SeatB, Is.EqualTo(1));
            Assert.That(started[0].StartTime, Is.EqualTo(2.0));
            Assert.That(m.InPair(0) && m.InPair(1), Is.True);
        }

        [Test]
        public void OfferToSomeoneElseDoesNotMatch()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 2);

            Assert.That(Match(m), Is.Empty);
        }

        [Test]
        public void DuplicateIntentsDoNotDuplicateTheHandshake()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);

            Assert.That(Match(m).Count, Is.EqualTo(1));

            // The same offers sent again while the pair plays change nothing.
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            Assert.That(Match(m, 0.1), Is.Empty);
            Assert.That(m.Pairs.Count, Is.EqualTo(1));
        }

        [Test]
        public void MatchedHandshakeStartsOnlyOnce()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(2, true, 3);
            m.SetOffer(3, true, 2);

            Assert.That(Match(m, 0.0).Count, Is.EqualTo(1));
            for (int i = 1; i < 10; i++) Assert.That(Match(m, i * 0.1), Is.Empty);
        }

        [Test]
        public void ASeatIsNeverInTwoHandshakes()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            Match(m);

            // Seat 2 and seat 1 now want each other, but seat 1 is busy shaking with seat 0.
            m.SetOffer(2, true, 1);
            m.SetOffer(1, true, 2);
            Assert.That(Match(m, 0.1), Is.Empty);
            Assert.That(m.Pairs.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseBeforeMatchRemovesTheIntent()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);
            m.SetOffer(0, false, EmoteState.NoTarget);
            m.SetOffer(1, true, 0);

            Assert.That(m.IsOffering(0), Is.False);
            Assert.That(Match(m), Is.Empty);
        }

        [Test]
        public void ReleaseDuringTheHandshakeLetsItFinish()
        {
            var m = new HandshakeMatcher(4, 1.0);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            Match(m);

            m.SetOffer(0, false, EmoteState.NoTarget);
            m.SetOffer(1, false, EmoteState.NoTarget);

            Assert.That(m.InPair(0), Is.True);
        }

        [Test]
        public void CompletionClearsThePair()
        {
            var m = new HandshakeMatcher(4, 1.0);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            Match(m, 10.0);

            var ended = new List<HandshakeEnd>();
            m.Tick(10.5, ended);
            Assert.That(ended, Is.Empty);

            m.Tick(11.0, ended);
            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(ended[0].Reason, Is.EqualTo(HandshakeEndReason.Completed));
            Assert.That(m.Pairs, Is.Empty);
        }

        [Test]
        public void BothMustReArmBeforeShakingAgain()
        {
            var m = new HandshakeMatcher(4, 1.0);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            Match(m, 0.0);
            m.Tick(2.0, null);

            // Still holding: no endless loop.
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            Assert.That(Match(m, 2.0), Is.Empty);
            Assert.That(m.NeedsRearm(0) && m.NeedsRearm(1), Is.True);

            // Only one of them lets go and grabs again: still no.
            m.SetOffer(0, false, EmoteState.NoTarget);
            m.SetOffer(0, true, 1);
            Assert.That(Match(m, 2.1), Is.Empty);

            // Both re-armed: they may shake again.
            m.SetOffer(1, false, EmoteState.NoTarget);
            m.SetOffer(1, true, 0);
            Assert.That(Match(m, 2.2).Count, Is.EqualTo(1));
        }

        [Test]
        public void InterruptCancelsForBoth()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);
            Match(m);

            var ended = new List<HandshakeEnd>();
            Assert.That(m.Interrupt(1, ended), Is.True);
            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(ended[0].Reason, Is.EqualTo(HandshakeEndReason.Interrupted));
            Assert.That(m.InPair(0) || m.InPair(1), Is.False);
        }

        [Test]
        public void SeatsThatMayNotShakeNeverMatch()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 1);
            m.SetOffer(1, true, 0);

            var started = new List<HandshakePair>();
            m.Match(0.0, s => s != 1, started);
            Assert.That(started, Is.Empty);
        }

        [Test]
        public void SelfOfferIsIgnored()
        {
            var m = new HandshakeMatcher(4);
            m.SetOffer(0, true, 0);

            Assert.That(m.TargetOf(0), Is.EqualTo(EmoteState.NoTarget));
            Assert.That(Match(m), Is.Empty);
        }

        // ================================================================ authority: cleanup

        [Test]
        public void DisconnectClearsThePair()
        {
            var a = new EmoteAuthority(4, Gen);
            var roster = Roster(4);
            a.Submit(0, Offer(1), Gen, roster, out _);
            a.Submit(1, Offer(0), Gen, roster, out _);

            var started = new List<HandshakePair>();
            a.Tick(0.0, roster, null, started, null);
            Assert.That(started.Count, Is.EqualTo(1));

            // Seat 1's participant leaves.
            roster[1] = new EmoteSeat(false, true, false);
            var cleared = new List<int>();
            var ended = new List<HandshakeEnd>();
            a.Tick(0.2, roster, cleared, null, ended);

            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(ended[0].Reason, Is.EqualTo(HandshakeEndReason.Left));
            Assert.That(cleared, Does.Contain(1));
            Assert.That(a.Handshakes.Pairs, Is.Empty);
        }

        [Test]
        public void RemovingASeatDirectlyClearsThePair()
        {
            var a = new EmoteAuthority(4, Gen);
            var roster = Roster(4);
            a.Submit(2, Offer(3), Gen, roster, out _);
            a.Submit(3, Offer(2), Gen, roster, out _);
            a.Tick(0.0, roster, null, null, null);

            var ended = new List<HandshakeEnd>();
            var cleared = new List<int>();
            a.RemoveSeat(3, HandshakeEndReason.Left, cleared, ended);

            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(cleared, Is.EquivalentTo(new[] { 3 }));
            Assert.That(a.StateOf(3).IsIdle, Is.True);
        }

        [Test]
        public void DeathClearsThePairAndTheEmotes()
        {
            var a = new EmoteAuthority(4, Gen);
            var roster = Roster(4);
            a.Submit(0, new EmoteState(EmoteFlags.HandshakeOffer | EmoteFlags.MiddleFinger, 0, 1), Gen, roster, out _);
            a.Submit(1, Offer(0), Gen, roster, out _);
            a.Tick(0.0, roster, null, null, null);

            roster[0] = new EmoteSeat(true, false, false);
            var cleared = new List<int>();
            var ended = new List<HandshakeEnd>();
            a.Tick(0.3, roster, cleared, null, ended);

            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(ended[0].Reason, Is.EqualTo(HandshakeEndReason.Died));
            Assert.That(cleared, Is.EquivalentTo(new[] { 0 }));
            Assert.That(a.StateOf(0).IsIdle, Is.True);
        }

        [Test]
        public void GenerationChangeClearsEverything()
        {
            var a = new EmoteAuthority(4, Gen);
            var roster = Roster(4);
            a.Submit(0, Offer(1), Gen, roster, out _);
            a.Submit(1, Offer(0), Gen, roster, out _);
            a.Tick(0.0, roster, null, null, null);

            var ended = new List<HandshakeEnd>();
            var cleared = new List<int>();
            a.Clear(cleared, ended);

            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(ended[0].Reason, Is.EqualTo(HandshakeEndReason.Reset));
            Assert.That(cleared, Is.EquivalentTo(new[] { 0, 1 }));

            // The next generation's authority starts empty and refuses the previous one's requests.
            var next = new EmoteAuthority(4, Gen + 1);
            Assert.That(next.Submit(0, Offer(1), Gen, roster, out _), Is.EqualTo(EmoteSubmitResult.StaleGeneration));
            Assert.That(next.Handshakes.Pairs, Is.Empty);
            Assert.That(next.StateOf(0).IsIdle, Is.True);
        }

        [Test]
        public void StaleGenerationInterruptIsIgnored()
        {
            var a = new EmoteAuthority(4, Gen);
            var roster = Roster(4);
            a.Submit(0, Offer(1), Gen, roster, out _);
            a.Submit(1, Offer(0), Gen, roster, out _);
            a.Tick(0.0, roster, null, null, null);

            Assert.That(a.Interrupt(0, Gen - 1, null), Is.False);
            Assert.That(a.Handshakes.Pairs.Count, Is.EqualTo(1));
            Assert.That(a.Interrupt(0, Gen, null), Is.True);
        }

        // ================================================================ authority: validation

        [Test]
        public void BotsAndTheDeadCannotEmote()
        {
            var a = new EmoteAuthority(4, Gen);
            var roster = Roster(4, 2);
            roster[1] = new EmoteSeat(true, false, false);

            Assert.That(a.Submit(2, new EmoteState(EmoteFlags.MiddleFinger, 0, -1), Gen, roster, out _),
                Is.EqualTo(EmoteSubmitResult.NotAllowed));
            Assert.That(a.Submit(1, new EmoteState(EmoteFlags.Point, 10, -1), Gen, roster, out _),
                Is.EqualTo(EmoteSubmitResult.NotAllowed));
            Assert.That(a.Submit(0, new EmoteState(EmoteFlags.MiddleFinger, 0, -1), Gen, roster, out _),
                Is.EqualTo(EmoteSubmitResult.Accepted));
        }

        [Test]
        public void ABotTargetNeverShakes()
        {
            var a = new EmoteAuthority(4, Gen);
            var roster = Roster(4, 2);
            a.Submit(0, Offer(2), Gen, roster, out _);

            var started = new List<HandshakePair>();
            a.Tick(0.0, roster, null, started, null);
            Assert.That(started, Is.Empty);
        }

        [Test]
        public void InvalidSeatIsRefused()
        {
            var a = new EmoteAuthority(4, Gen);
            Assert.That(a.Submit(7, Offer(0), Gen, Roster(4), out _), Is.EqualTo(EmoteSubmitResult.InvalidSeat));
            Assert.That(a.Submit(-1, Offer(0), Gen, Roster(4), out _), Is.EqualTo(EmoteSubmitResult.InvalidSeat));
        }

        [Test]
        public void SameStateTwiceIsUnchanged()
        {
            var a = new EmoteAuthority(4, Gen);
            var s = new EmoteState(EmoteFlags.Point, 40, -1);

            Assert.That(a.Submit(0, s, Gen, Roster(4), out _), Is.EqualTo(EmoteSubmitResult.Accepted));
            Assert.That(a.Submit(0, s, Gen, Roster(4), out _), Is.EqualTo(EmoteSubmitResult.Unchanged));
        }

        [Test]
        public void PointAndHandshakeShareTheRightHand()
        {
            EmoteState both = new EmoteState(EmoteFlags.Point | EmoteFlags.HandshakeOffer | EmoteFlags.MiddleFinger, 90, 2)
                .Sanitized(0, 4);

            Assert.That(both.Has(EmoteFlags.Point), Is.True);
            Assert.That(both.Has(EmoteFlags.HandshakeOffer), Is.False);
            Assert.That(both.Has(EmoteFlags.MiddleFinger), Is.True);
            Assert.That(both.Target, Is.EqualTo(EmoteState.NoTarget));
            Assert.That(both.Reach, Is.EqualTo(90));
        }

        [Test]
        public void SanitizeDropsReachWithoutPointAndBadTargets()
        {
            EmoteState s = new EmoteState(EmoteFlags.HandshakeOffer, 200, 9).Sanitized(1, 4);
            Assert.That(s.Reach, Is.EqualTo(0));
            Assert.That(s.Target, Is.EqualTo(EmoteState.NoTarget));

            EmoteState self = new EmoteState(EmoteFlags.HandshakeOffer, 0, 1).Sanitized(1, 4);
            Assert.That(self.Target, Is.EqualTo(EmoteState.NoTarget));
        }

        [Test]
        public void AuthorityPairsThroughSubmittedOffers()
        {
            var a = new EmoteAuthority(6, Gen);
            var roster = Roster(6);
            a.Submit(1, Offer(4), Gen, roster, out _);
            a.Submit(4, Offer(1), Gen, roster, out _);

            var started = new List<HandshakePair>();
            var ended = new List<HandshakeEnd>();
            a.Tick(3.0, roster, null, started, ended);
            Assert.That(started.Count, Is.EqualTo(1));
            Assert.That(started[0].PartnerOf(1), Is.EqualTo(4));

            // Releasing the offer during the shake does not end it; time does.
            a.Submit(1, EmoteState.Idle, Gen, roster, out _);
            a.Tick(3.0 + HandshakeTiming.Total * 0.5, roster, null, started, ended);
            Assert.That(ended, Is.Empty);

            a.Tick(3.0 + HandshakeTiming.Total + 0.01, roster, null, started, ended);
            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(ended[0].Reason, Is.EqualTo(HandshakeEndReason.Completed));
        }
    }
}
