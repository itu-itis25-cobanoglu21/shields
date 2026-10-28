using System;
using System.Collections.Generic;

namespace Shields.Session
{
    /// <summary>
    /// Somebody taking part in one match: a human participant (by network participant
    /// id, never by seat) or a numbered bot. Identity follows the participant from match
    /// to match; only the seat they are given changes.
    /// </summary>
    public readonly struct MatchParticipant : IEquatable<MatchParticipant>
    {
        MatchParticipant(SeatOccupantKind kind, ulong participantId, int botNumber)
        {
            Kind = kind;
            ParticipantId = participantId;
            BotNumber = botNumber;
        }

        public static MatchParticipant Human(ulong participantId)
        {
            return new MatchParticipant(SeatOccupantKind.Human, participantId, 0);
        }

        /// <summary>Bots are numbered from 1 so two bots are distinguishable participants.</summary>
        public static MatchParticipant Bot(int botNumber)
        {
            return new MatchParticipant(SeatOccupantKind.Bot, SeatOccupant.NoParticipant, botNumber);
        }

        public SeatOccupantKind Kind { get; }

        public ulong ParticipantId { get; }

        public int BotNumber { get; }

        public bool IsHuman { get { return Kind == SeatOccupantKind.Human; } }

        public bool IsBot { get { return Kind == SeatOccupantKind.Bot; } }

        public SeatOccupant ToOccupant()
        {
            return IsHuman ? SeatOccupant.Human(ParticipantId) : SeatOccupant.Bot();
        }

        public bool Equals(MatchParticipant other)
        {
            return Kind == other.Kind && ParticipantId == other.ParticipantId && BotNumber == other.BotNumber;
        }

        public override bool Equals(object obj)
        {
            return obj is MatchParticipant other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Kind * 397) ^ ParticipantId.GetHashCode() ^ (BotNumber * 7919);
        }

        public override string ToString()
        {
            return IsHuman ? "human " + ParticipantId : "bot " + BotNumber;
        }
    }

    /// <summary>
    /// Fresh random seating for every match, offline and online, first match and rematch
    /// alike. Every participant, human or bot, is shuffled over every seat with a
    /// Fisher-Yates permutation. If the whole table comes out exactly as last match's,
    /// it is rerolled; individual participants may still keep their seat by chance.
    ///
    /// Plain C# with an injected random source, so it is fully unit testable.
    /// </summary>
    public static class SeatShuffler
    {
        const int MaxRerolls = 64;

        /// <summary>
        /// The participants of a match: the given humans in order, then bots numbered
        /// 1, 2, ... for every remaining seat.
        /// </summary>
        public static MatchParticipant[] BuildRoster(IReadOnlyList<ulong> humanParticipants, int seatCount)
        {
            if (seatCount < 1) throw new ArgumentOutOfRangeException(nameof(seatCount));

            int humans = humanParticipants != null ? humanParticipants.Count : 0;
            if (humans > seatCount) throw new ArgumentException("More humans than seats.", nameof(humanParticipants));

            MatchParticipant[] roster = new MatchParticipant[seatCount];
            for (int i = 0; i < humans; i++) roster[i] = MatchParticipant.Human(humanParticipants[i]);
            for (int i = humans; i < seatCount; i++) roster[i] = MatchParticipant.Bot(i - humans + 1);

            return roster;
        }

        /// <summary>
        /// Returns the seating, indexed by seat: who sits where. Contains every
        /// participant exactly once. <paramref name="previousSeating"/> is last match's
        /// result, or null for the first match.
        /// </summary>
        public static MatchParticipant[] Shuffle(IReadOnlyList<MatchParticipant> participants,
            IReadOnlyList<MatchParticipant> previousSeating, Random random)
        {
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (random == null) throw new ArgumentNullException(nameof(random));

            HashSet<MatchParticipant> distinct = new HashSet<MatchParticipant>(participants);
            if (distinct.Count != participants.Count) throw new ArgumentException("A participant is listed twice.", nameof(participants));

            MatchParticipant[] seating = new MatchParticipant[participants.Count];

            for (int attempt = 0; ; attempt++)
            {
                for (int i = 0; i < seating.Length; i++) seating[i] = participants[i];

                for (int i = seating.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    MatchParticipant swap = seating[i];
                    seating[i] = seating[j];
                    seating[j] = swap;
                }

                if (!SameSeating(seating, previousSeating)) return seating;

                // Astronomically unlikely with a working random source; never loop forever.
                if (attempt >= MaxRerolls)
                {
                    MatchParticipant first = seating[0];
                    seating[0] = seating[1];
                    seating[1] = first;
                    return seating;
                }
            }
        }

        /// <summary>True when every seat holds the same participant in both seatings.</summary>
        public static bool SameSeating(IReadOnlyList<MatchParticipant> a, IReadOnlyList<MatchParticipant> b)
        {
            if (a == null || b == null || a.Count != b.Count || a.Count < 2) return false;

            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i])) return false;

            return true;
        }

        /// <summary>The seat a human participant was given, or <see cref="SeatRegistry.NoSeat"/>.</summary>
        public static int SeatOf(IReadOnlyList<MatchParticipant> seating, ulong participantId)
        {
            if (seating == null) return SeatRegistry.NoSeat;

            for (int i = 0; i < seating.Count; i++)
                if (seating[i].IsHuman && seating[i].ParticipantId == participantId) return i;

            return SeatRegistry.NoSeat;
        }
    }
}
