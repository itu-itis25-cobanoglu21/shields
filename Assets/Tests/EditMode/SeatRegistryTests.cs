using System;
using NUnit.Framework;
using Shields.Session;

namespace Shields.Game.Tests
{
    /// <summary>
    /// Seat ownership rules. These are the parts of TASK 008 that can be proved without a
    /// Relay connection: one seat per participant, no duplicates, and seats that return to
    /// the pool when someone disconnects before the match starts.
    /// </summary>
    public class SeatRegistryTests
    {
        const ulong Host = 0;
        const ulong ClientA = 1;
        const ulong ClientB = 2;

        static SeatRegistry NewRegistry(int seats = 3)
        {
            return new SeatRegistry(seats);
        }

        // ------------------------------------------------------------------ creation

        [Test]
        public void NewRegistry_HasEverySeatEmpty([Values(3, 6, 8)] int seats)
        {
            SeatRegistry registry = NewRegistry(seats);

            Assert.That(registry.SeatCount, Is.EqualTo(seats));
            Assert.That(registry.OccupiedSeatCount, Is.Zero);
            Assert.That(registry.HumanCount, Is.Zero);
            Assert.That(registry.IsFull, Is.False);

            for (int i = 0; i < seats; i++)
                Assert.That(registry.OccupantAt(i).IsEmpty, Is.True, "Seat {0} should start empty.", i);
        }

        [Test]
        public void SeatCountOutsideThreeToEightIsRejected([Values(0, 1, 2, 9, 99, -1)] int seats)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeatRegistry(seats));
        }

        // ------------------------------------------------------------------ assignment

        [Test]
        public void Assign_GivesConnectionOrderSeatsStartingAtZero()
        {
            SeatRegistry registry = NewRegistry();

            Assert.That(registry.AssignSeat(Host), Is.EqualTo(0));
            Assert.That(registry.AssignSeat(ClientA), Is.EqualTo(1));
            Assert.That(registry.AssignSeat(ClientB), Is.EqualTo(2));
        }

        [Test]
        public void Assign_RecordsTheParticipantAsAHumanOccupant()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(ClientA);

            SeatOccupant occupant = registry.OccupantAt(0);

            Assert.That(occupant.IsHuman, Is.True);
            Assert.That(occupant.IsBot, Is.False);
            Assert.That(occupant.ParticipantId, Is.EqualTo(ClientA));
        }

        [Test]
        public void Assign_SameParticipantTwiceReturnsTheSameSeatAndTakesNoSecondSeat()
        {
            SeatRegistry registry = NewRegistry();

            int first = registry.AssignSeat(ClientA);
            int second = registry.AssignSeat(ClientA);

            Assert.That(second, Is.EqualTo(first), "A repeated request must not create a second seat.");
            Assert.That(registry.OccupiedSeatCount, Is.EqualTo(1));
        }

        [Test]
        public void Assign_NeverGivesOneSeatToTwoParticipants()
        {
            SeatRegistry registry = NewRegistry(8);

            for (ulong participant = 0; participant < 8; participant++)
                registry.AssignSeat(participant);

            // Every seat holds a distinct participant.
            for (int i = 0; i < 8; i++)
            {
                for (int j = i + 1; j < 8; j++)
                {
                    Assert.That(registry.OccupantAt(i).ParticipantId,
                        Is.Not.EqualTo(registry.OccupantAt(j).ParticipantId),
                        "Seats {0} and {1} share a participant.", i, j);
                }
            }
        }

        [Test]
        public void Assign_RefusesOnceTheTableIsFull()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);
            registry.AssignSeat(ClientA);
            registry.AssignSeat(ClientB);

            Assert.That(registry.IsFull, Is.True);
            Assert.That(registry.AssignSeat(99), Is.EqualTo(SeatRegistry.NoSeat));
            Assert.That(registry.OccupiedSeatCount, Is.EqualTo(3));
        }

        [Test]
        public void Assign_FillsTheLowestFreeSeatNotTheNextIndex()
        {
            SeatRegistry registry = NewRegistry(6);
            registry.AssignSeat(Host);      // seat 0
            registry.AssignSeat(ClientA);   // seat 1
            registry.AssignSeat(ClientB);   // seat 2

            registry.ReleaseParticipant(ClientA); // frees seat 1

            Assert.That(registry.AssignSeat(50), Is.EqualTo(1), "Seat 1 was free and should be reused first.");
        }

        // ------------------------------------------------------------------ lookup

        [Test]
        public void SeatOf_ReturnsNoSeatForAnUnknownParticipant()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);

            Assert.That(registry.SeatOf(ClientB), Is.EqualTo(SeatRegistry.NoSeat));
        }

        [Test]
        public void OccupantAt_OutOfRangeIsEmptyRatherThanThrowing([Values(-1, 3, 99)] int seatIndex)
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);

            Assert.That(registry.OccupantAt(seatIndex).IsEmpty, Is.True);
        }

        [Test]
        public void IsSeatOwnedBy_IsTrueOnlyForTheActualOwner()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);
            registry.AssignSeat(ClientA);

            Assert.That(registry.IsSeatOwnedBy(0, Host), Is.True);
            Assert.That(registry.IsSeatOwnedBy(0, ClientA), Is.False, "A client must not pass as the owner of seat 0.");
            Assert.That(registry.IsSeatOwnedBy(2, Host), Is.False, "An empty seat is owned by nobody.");
        }

        // ------------------------------------------------------------------ release

        [Test]
        public void Release_FreesTheSeatAndReportsWhichOne()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);
            int clientSeat = registry.AssignSeat(ClientA);

            Assert.That(registry.ReleaseParticipant(ClientA), Is.EqualTo(clientSeat));
            Assert.That(registry.OccupantAt(clientSeat).IsEmpty, Is.True);
            Assert.That(registry.SeatOf(ClientA), Is.EqualTo(SeatRegistry.NoSeat));
            Assert.That(registry.HumanCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_AnUnknownParticipantChangesNothing()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);

            Assert.That(registry.ReleaseParticipant(ClientB), Is.EqualTo(SeatRegistry.NoSeat));
            Assert.That(registry.OccupiedSeatCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_LeavesOtherParticipantsSeatsUntouched()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);
            registry.AssignSeat(ClientA);
            registry.AssignSeat(ClientB);

            registry.ReleaseParticipant(ClientA);

            Assert.That(registry.SeatOf(Host), Is.EqualTo(0));
            Assert.That(registry.SeatOf(ClientB), Is.EqualTo(2), "Releasing one seat must not renumber the others.");
        }

        [Test]
        public void ReleaseThenRejoin_ReusesTheFreedSeat()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);
            registry.AssignSeat(ClientA);

            registry.ReleaseParticipant(ClientA);
            int rejoined = registry.AssignSeat(ClientA);

            Assert.That(rejoined, Is.EqualTo(1));
            Assert.That(registry.HumanCount, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseTwice_IsHarmless()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(ClientA);

            Assert.That(registry.ReleaseParticipant(ClientA), Is.EqualTo(0));
            Assert.That(registry.ReleaseParticipant(ClientA), Is.EqualTo(SeatRegistry.NoSeat));
            Assert.That(registry.OccupiedSeatCount, Is.Zero);
        }

        // ------------------------------------------------------------------ reset

        [Test]
        public void Reset_ResizesTheTableAndClearsEveryAssignment()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);
            registry.AssignSeat(ClientA);

            registry.Reset(8);

            Assert.That(registry.SeatCount, Is.EqualTo(8));
            Assert.That(registry.OccupiedSeatCount, Is.Zero);
            Assert.That(registry.SeatOf(Host), Is.EqualTo(SeatRegistry.NoSeat));
        }

        // ------------------------------------------------------------------ snapshot

        [Test]
        public void Snapshot_CopiesTheTableAndCannotMutateTheRegistry()
        {
            SeatRegistry registry = NewRegistry();
            registry.AssignSeat(Host);

            SeatOccupant[] snapshot = registry.Snapshot();
            snapshot[0] = SeatOccupant.Empty;

            Assert.That(registry.OccupantAt(0).IsHuman, Is.True, "The registry must not share state with its snapshot.");
        }

        [Test]
        public void ApplySnapshot_MakesAClientAgreeWithTheHostExactly()
        {
            SeatRegistry host = NewRegistry(6);
            host.AssignSeat(Host);
            host.AssignSeat(ClientA);
            host.AssignSeat(ClientB);
            host.ReleaseParticipant(ClientA);

            SeatRegistry client = NewRegistry(3);
            client.ApplySnapshot(host.Snapshot());

            Assert.That(client.SeatCount, Is.EqualTo(host.SeatCount));
            Assert.That(client.HumanCount, Is.EqualTo(host.HumanCount));

            for (int i = 0; i < host.SeatCount; i++)
            {
                Assert.That(client.OccupantAt(i).Kind, Is.EqualTo(host.OccupantAt(i).Kind), "Seat {0} kind differs.", i);
                Assert.That(client.OccupantAt(i).ParticipantId, Is.EqualTo(host.OccupantAt(i).ParticipantId),
                    "Seat {0} participant differs.", i);
            }
        }

        [Test]
        public void ApplySnapshot_LetsEachClientResolveItsOwnDifferentSeat()
        {
            SeatRegistry host = NewRegistry();
            host.AssignSeat(Host);
            host.AssignSeat(ClientA);

            SeatRegistry onClient = NewRegistry();
            onClient.ApplySnapshot(host.Snapshot());

            // The same replicated table yields a different local seat per participant,
            // which is the whole point of not assuming local player 0.
            Assert.That(onClient.SeatOf(Host), Is.EqualTo(0));
            Assert.That(onClient.SeatOf(ClientA), Is.EqualTo(1));
        }

        [Test]
        public void ApplySnapshot_RejectsNull()
        {
            SeatRegistry registry = NewRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.ApplySnapshot(null));
        }

        // ------------------------------------------------------------------ occupants

        [Test]
        public void BotSeatsAreNotCountedAsHumansAndOwnNoParticipant()
        {
            SeatOccupant bot = SeatOccupant.Bot();

            Assert.That(bot.IsBot, Is.True);
            Assert.That(bot.IsHuman, Is.False);
            Assert.That(bot.IsEmpty, Is.False);
            Assert.That(bot.ParticipantId, Is.EqualTo(SeatOccupant.NoParticipant));
        }

        [Test]
        public void Participants_ListsHumansInSeatOrder()
        {
            SeatRegistry registry = NewRegistry(6);
            registry.AssignSeat(ClientB);   // seat 0
            registry.AssignSeat(Host);      // seat 1
            registry.AssignSeat(ClientA);   // seat 2

            Assert.That(registry.Participants(), Is.EqualTo(new[] { ClientB, Host, ClientA }));
        }
    }
}
