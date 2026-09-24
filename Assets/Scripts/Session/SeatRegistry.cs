using System;
using System.Collections.Generic;

namespace Shields.Session
{
    /// <summary>
    /// The authoritative map from network participant to seat index. Only the host owns
    /// an instance of this; clients read a replicated copy of its snapshot.
    ///
    /// Plain C# on purpose. It has no Unity and no networking dependency, so the seating
    /// rules that actually matter, one seat per participant, no duplicates, and seats that
    /// come back when someone leaves, can be unit tested without a Relay connection.
    ///
    /// Seats are handed out lowest free index first, which for the prototype means the
    /// host takes seat 0 and joiners take 1, 2, ... in connection order (TASK 008 section 7).
    /// That ordering is a policy of this class, not an assumption baked into presentation.
    /// </summary>
    public sealed class SeatRegistry
    {
        /// <summary>Returned when no seat could be assigned or a participant has none.</summary>
        public const int NoSeat = -1;

        public const int MinSeats = 3;
        public const int MaxSeats = 8;

        SeatOccupant[] _seats;

        public SeatRegistry(int seatCount)
        {
            _seats = CreateSeats(seatCount);
        }

        public int SeatCount { get { return _seats.Length; } }

        public int OccupiedSeatCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _seats.Length; i++)
                    if (!_seats[i].IsEmpty) count++;

                return count;
            }
        }

        public int HumanCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _seats.Length; i++)
                    if (_seats[i].IsHuman) count++;

                return count;
            }
        }

        public bool IsFull { get { return OccupiedSeatCount >= _seats.Length; } }

        /// <summary>
        /// Resizes the table and clears every assignment. Called by the host when the
        /// target player count changes before the network test starts.
        /// </summary>
        public void Reset(int seatCount)
        {
            _seats = CreateSeats(seatCount);
        }

        /// <summary>
        /// Gives <paramref name="participantId"/> the lowest free seat and returns it.
        ///
        /// Idempotent: a participant that already holds a seat gets that same seat back
        /// rather than a second one, so a repeated or replayed assignment request can
        /// never give one participant two seats. Returns <see cref="NoSeat"/> when the
        /// table is full.
        /// </summary>
        public int AssignSeat(ulong participantId)
        {
            int existing = SeatOf(participantId);
            if (existing != NoSeat) return existing;

            for (int i = 0; i < _seats.Length; i++)
            {
                if (!_seats[i].IsEmpty) continue;

                _seats[i] = SeatOccupant.Human(participantId);
                return i;
            }

            return NoSeat;
        }

        /// <summary>
        /// Frees whatever seat a participant held. Returns the seat that was released, or
        /// <see cref="NoSeat"/> if they held none. The seat becomes immediately reusable,
        /// which is the pre-match disconnect behaviour TASK 008 section 15 asks for.
        /// </summary>
        public int ReleaseParticipant(ulong participantId)
        {
            int seat = SeatOf(participantId);
            if (seat == NoSeat) return NoSeat;

            _seats[seat] = SeatOccupant.Empty;
            return seat;
        }

        /// <summary>The seat a participant holds, or <see cref="NoSeat"/>.</summary>
        public int SeatOf(ulong participantId)
        {
            for (int i = 0; i < _seats.Length; i++)
                if (_seats[i].IsHuman && _seats[i].ParticipantId == participantId) return i;

            return NoSeat;
        }

        public SeatOccupant OccupantAt(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _seats.Length) return SeatOccupant.Empty;

            return _seats[seatIndex];
        }

        public bool IsSeatOwnedBy(int seatIndex, ulong participantId)
        {
            SeatOccupant occupant = OccupantAt(seatIndex);
            return occupant.IsHuman && occupant.ParticipantId == participantId;
        }

        /// <summary>
        /// A copy of the whole seating table, safe to replicate. Callers cannot mutate the
        /// registry through it.
        /// </summary>
        public SeatOccupant[] Snapshot()
        {
            SeatOccupant[] copy = new SeatOccupant[_seats.Length];
            Array.Copy(_seats, copy, _seats.Length);
            return copy;
        }

        /// <summary>
        /// Replaces the whole table from a replicated snapshot. Used by clients, which
        /// never assign seats themselves and only mirror what the host sent.
        /// </summary>
        public void ApplySnapshot(IReadOnlyList<SeatOccupant> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            SeatOccupant[] seats = new SeatOccupant[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++) seats[i] = snapshot[i];

            _seats = seats;
        }

        /// <summary>Every participant currently holding a seat, ordered by seat index.</summary>
        public IReadOnlyList<ulong> Participants()
        {
            List<ulong> participants = new List<ulong>();
            for (int i = 0; i < _seats.Length; i++)
                if (_seats[i].IsHuman) participants.Add(_seats[i].ParticipantId);

            return participants;
        }

        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("{0} seats, {1} human(s)", _seats.Length, HumanCount);

            for (int i = 0; i < _seats.Length; i++)
                sb.AppendFormat("\n  seat {0}: {1}", i, _seats[i]);

            return sb.ToString();
        }

        static SeatOccupant[] CreateSeats(int seatCount)
        {
            if (seatCount < MinSeats || seatCount > MaxSeats)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount,
                    string.Format("Shields seats {0} to {1} players.", MinSeats, MaxSeats));
            }

            SeatOccupant[] seats = new SeatOccupant[seatCount];
            for (int i = 0; i < seats.Length; i++) seats[i] = SeatOccupant.Empty;

            return seats;
        }
    }
}
