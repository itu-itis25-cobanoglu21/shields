using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shields.Online;
using Shields.Session;

namespace Shields.Game.Tests
{
    /// <summary>
    /// Fresh random seating for every match (TASK 026): a true permutation of every
    /// participant, identity kept, an exact repeat of the whole previous table rerolled,
    /// and the seat a participant gets never confused with their client id.
    /// </summary>
    public class SeatShufflerTests
    {
        [Test]
        public void RosterHasTheHumansThenNumberedBots()
        {
            MatchParticipant[] roster = SeatShuffler.BuildRoster(new ulong[] { 40, 7 }, 5);

            CollectionAssert.AreEqual(new[]
            {
                MatchParticipant.Human(40), MatchParticipant.Human(7),
                MatchParticipant.Bot(1), MatchParticipant.Bot(2), MatchParticipant.Bot(3)
            }, roster);
        }

        [Test]
        public void SeatingIsAPermutationThatKeepsEveryIdentity([Values(3, 6, 8)] int seats)
        {
            Random random = new Random(seats);
            MatchParticipant[] roster = SeatShuffler.BuildRoster(new ulong[] { 11, 22 }, seats);

            for (int trial = 0; trial < 200; trial++)
            {
                MatchParticipant[] seating = SeatShuffler.Shuffle(roster, null, random);

                Assert.AreEqual(seats, seating.Length, "Every seat is filled exactly once.");
                CollectionAssert.AreEquivalent(roster, seating, "No participant lost, duplicated or invented.");
                Assert.AreEqual(2, seating.Count(p => p.IsHuman), "Humans stay humans.");
                Assert.AreEqual(seats - 2, seating.Count(p => p.IsBot), "Bots stay bots.");
            }
        }

        [Test]
        public void EverySeatIsReachableForTheOfflineHuman([Values(3, 6, 8)] int seats)
        {
            Random random = new Random(99);
            MatchParticipant[] roster = SeatShuffler.BuildRoster(new ulong[] { 0 }, seats);
            HashSet<int> seen = new HashSet<int>();

            for (int trial = 0; trial < 400; trial++)
                seen.Add(SeatShuffler.SeatOf(SeatShuffler.Shuffle(roster, null, random), 0));

            CollectionAssert.AreEquivalent(Enumerable.Range(0, seats), seen, "The human is not stuck in seat 0.");
        }

        [Test]
        public void AnExactRepeatOfTheWholeTableIsRerolled()
        {
            MatchParticipant[] roster = SeatShuffler.BuildRoster(new ulong[] { 1, 2 }, 3);

            // A source that keeps producing the identity permutation would repeat forever.
            Random stubborn = new FixedRandom(0, 0, 0, 0, 0, 0, 0, 0, 1, 0);
            MatchParticipant[] previous = SeatShuffler.Shuffle(roster, null, new FixedRandom(0, 0, 0, 0));
            MatchParticipant[] next = SeatShuffler.Shuffle(roster, previous, stubborn);

            Assert.IsFalse(SeatShuffler.SameSeating(previous, next));
            CollectionAssert.AreEquivalent(roster, next);
        }

        [Test]
        public void ConsecutiveMatchesNeverRepeatTheWholeTable([Values(3, 6, 8)] int seats)
        {
            Random random = new Random(7);
            MatchParticipant[] roster = SeatShuffler.BuildRoster(new ulong[] { 5 }, seats);
            MatchParticipant[] previous = null;
            int humanStayed = 0;

            for (int match = 0; match < 300; match++)
            {
                MatchParticipant[] seating = SeatShuffler.Shuffle(roster, previous, random);
                Assert.IsFalse(SeatShuffler.SameSeating(previous, seating), "Match {0} repeated the whole table.", match);

                if (previous != null && SeatShuffler.SeatOf(previous, 5) == SeatShuffler.SeatOf(seating, 5)) humanStayed++;
                previous = seating;
            }

            // Individual players may still keep their seat by chance; that is a normal shuffle.
            Assert.Greater(humanStayed, 0);
        }

        [Test]
        public void EvenAStuckRandomSourceCannotLoopForever()
        {
            MatchParticipant[] roster = SeatShuffler.BuildRoster(new ulong[] { 1 }, 3);

            // A source of nothing but zeros produces the same permutation every attempt.
            MatchParticipant[] previous = SeatShuffler.Shuffle(roster, null, new FixedRandom());
            MatchParticipant[] next = SeatShuffler.Shuffle(roster, previous, new FixedRandom());

            Assert.IsFalse(SeatShuffler.SameSeating(previous, next), "The bounded reroll falls back to a swap.");
            CollectionAssert.AreEquivalent(roster, next);
        }

        [Test]
        public void DuplicateParticipantsAreRejected()
        {
            MatchParticipant[] roster = { MatchParticipant.Human(1), MatchParticipant.Human(1), MatchParticipant.Bot(1) };

            Assert.Throws<ArgumentException>(() => SeatShuffler.Shuffle(roster, null, new Random(1)));
        }

        // ================================================================ registry and routing

        [Test]
        public void RegistryAppliesTheSeatingAndRoutesBySenderNotClientId()
        {
            const ulong host = 0;
            const ulong client = 1;
            SeatRegistry registry = new SeatRegistry(4);
            registry.AssignSeat(host);
            registry.AssignSeat(client);

            // Client 1 is moved to seat 3 and the host to seat 1: clientId != seat.
            MatchParticipant[] seating = { MatchParticipant.Bot(1), MatchParticipant.Human(host), MatchParticipant.Bot(2), MatchParticipant.Human(client) };

            Assert.IsTrue(registry.ApplyMatchSeating(seating));
            Assert.AreEqual(1, registry.SeatOf(host));
            Assert.AreEqual(3, registry.SeatOf(client));
            Assert.IsTrue(registry.OccupantAt(0).IsBot);
            Assert.IsTrue(registry.OccupantAt(2).IsBot);

            bool[] isBot = PublicStateBuilder.BuildSeatOccupancy(registry, 4);
            CollectionAssert.AreEqual(new[] { true, false, true, false }, isBot, "Human and bot control follow the participant.");
        }

        [Test]
        public void RegistryRefusesSeatingThatDropsOrInventsAHuman()
        {
            SeatRegistry registry = new SeatRegistry(3);
            registry.AssignSeat(10);
            registry.AssignSeat(20);
            SeatOccupant[] before = registry.Snapshot();

            Assert.IsFalse(registry.ApplyMatchSeating(new[] { MatchParticipant.Human(10), MatchParticipant.Bot(1), MatchParticipant.Bot(2) }), "Dropped 20.");
            Assert.IsFalse(registry.ApplyMatchSeating(new[] { MatchParticipant.Human(10), MatchParticipant.Human(20), MatchParticipant.Human(30) }), "Invented 30.");
            Assert.IsFalse(registry.ApplyMatchSeating(new[] { MatchParticipant.Human(10), MatchParticipant.Human(10), MatchParticipant.Human(20) }), "Duplicated 10.");
            Assert.IsFalse(registry.ApplyMatchSeating(new[] { MatchParticipant.Human(10), MatchParticipant.Human(20) }), "Wrong seat count.");

            CollectionAssert.AreEqual(before, registry.Snapshot(), "A refused seating changes nothing.");
        }

        [Test]
        public void ReturningToTheLobbyFreesBotSeatsAndKeepsHumans()
        {
            SeatRegistry registry = new SeatRegistry(4);
            registry.AssignSeat(10);
            registry.ApplyMatchSeating(new[] { MatchParticipant.Bot(1), MatchParticipant.Bot(2), MatchParticipant.Human(10), MatchParticipant.Bot(3) });

            Assert.AreEqual(3, registry.ClearBots());
            Assert.AreEqual(2, registry.SeatOf(10));
            Assert.AreEqual(0, registry.AssignSeat(99), "A joiner can take a freed seat again.");
        }

        [Test]
        public void InitialMatchAndRematchUseTheSameAlgorithm()
        {
            // The host's first match and every rematch both call Shuffle with the last
            // seating (null for the first); same roster, same random stream, same result.
            MatchParticipant[] roster = SeatShuffler.BuildRoster(new ulong[] { 3, 4 }, 6);
            MatchParticipant[] first = SeatShuffler.Shuffle(roster, null, new Random(5));
            MatchParticipant[] again = SeatShuffler.Shuffle(roster, null, new Random(5));

            CollectionAssert.AreEqual(first, again);
        }

        /// <summary>Returns the given values for Next(n), clamped to n - 1, then zeros.</summary>
        sealed class FixedRandom : Random
        {
            readonly Queue<int> _values;

            public FixedRandom(params int[] values)
            {
                _values = new Queue<int>(values);
            }

            public override int Next(int maxValue)
            {
                int value = _values.Count > 0 ? _values.Dequeue() : 0;
                return Math.Min(value, Math.Max(0, maxValue - 1));
            }
        }
    }
}
