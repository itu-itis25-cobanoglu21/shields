namespace Shields.Session
{
    /// <summary>
    /// What kind of participant owns a seat. This is authoritative session state, never
    /// inferred from prefabs, components, GameObject names or the absence of local input.
    ///
    /// Human and Bot are deliberately separate from "is this seat mine". Whether a human
    /// seat is the local one is answered by comparing its participant against the local
    /// participant, not by a flag stored here, because the same registry is replicated to
    /// every client and must read identically on all of them.
    /// </summary>
    public enum SeatOccupantKind
    {
        /// <summary>Nobody owns this seat yet.</summary>
        Empty = 0,

        /// <summary>A connected network participant owns this seat.</summary>
        Human = 1,

        /// <summary>
        /// Reserved for seats the host fills with bots when a match starts. TASK 008 never
        /// produces this value; it exists so human and bot stay explicitly different
        /// concepts rather than "human = whoever is connected, bot = everyone else".
        /// </summary>
        Bot = 2
    }

    /// <summary>
    /// One seat's ownership record. A value type so a snapshot of the registry can be
    /// copied and replicated without any shared mutable state.
    /// </summary>
    public readonly struct SeatOccupant
    {
        /// <summary>The participant id used for a seat that no human owns.</summary>
        public const ulong NoParticipant = ulong.MaxValue;

        public SeatOccupant(SeatOccupantKind kind, ulong participantId)
        {
            Kind = kind;
            ParticipantId = participantId;
        }

        public static SeatOccupant Empty
        {
            get { return new SeatOccupant(SeatOccupantKind.Empty, NoParticipant); }
        }

        public static SeatOccupant Human(ulong participantId)
        {
            return new SeatOccupant(SeatOccupantKind.Human, participantId);
        }

        public static SeatOccupant Bot()
        {
            return new SeatOccupant(SeatOccupantKind.Bot, NoParticipant);
        }

        public SeatOccupantKind Kind { get; }

        /// <summary>
        /// The owning network participant, or <see cref="NoParticipant"/> for an empty or
        /// bot seat.
        /// </summary>
        public ulong ParticipantId { get; }

        public bool IsEmpty { get { return Kind == SeatOccupantKind.Empty; } }

        public bool IsHuman { get { return Kind == SeatOccupantKind.Human; } }

        public bool IsBot { get { return Kind == SeatOccupantKind.Bot; } }

        public override string ToString()
        {
            switch (Kind)
            {
                case SeatOccupantKind.Human: return "human " + ParticipantId;
                case SeatOccupantKind.Bot: return "bot";
                default: return "empty";
            }
        }
    }
}
