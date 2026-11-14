using System;

namespace Shields.Social
{
    /// <summary>The hand emotes a seat is holding. Middle Finger uses the left hand; Point and Handshake the right.</summary>
    [Flags]
    public enum EmoteFlags : byte
    {
        None = 0,
        MiddleFinger = 1,
        Point = 2,
        HandshakeOffer = 4
    }

    /// <summary>
    /// What one seat is showing with its hands. Cosmetic only: it never reaches the Shields match.
    /// Reach is how far the pointing hand is pushed out, 0 (normal) to 255 (furthest); Target is
    /// the seat a handshake is offered to, or <see cref="NoTarget"/>.
    /// </summary>
    public readonly struct EmoteState : IEquatable<EmoteState>
    {
        public const int NoTarget = -1;

        public EmoteState(EmoteFlags flags, byte reach, int target)
        {
            Flags = flags;
            Reach = reach;
            Target = target;
        }

        public static EmoteState Idle { get { return new EmoteState(EmoteFlags.None, 0, NoTarget); } }

        public EmoteFlags Flags { get; }
        public byte Reach { get; }
        public int Target { get; }

        public bool IsIdle { get { return Flags == EmoteFlags.None; } }

        public bool Has(EmoteFlags flag) { return (Flags & flag) == flag && flag != EmoteFlags.None; }

        /// <summary>Reach as 0..1.</summary>
        public float ReachFraction { get { return Reach / 255f; } }

        /// <summary>
        /// The state as a seat may actually show it: only known emotes; Point and Handshake share
        /// the right hand, so with both asked for, Point stays; reach only while pointing; a target
        /// only while offering, and only another existing seat.
        /// </summary>
        public EmoteState Sanitized(int ownSeat, int seatCount)
        {
            EmoteFlags flags = Flags & (EmoteFlags.MiddleFinger | EmoteFlags.Point | EmoteFlags.HandshakeOffer);
            if ((flags & EmoteFlags.Point) != 0) flags &= ~EmoteFlags.HandshakeOffer;

            byte reach = (flags & EmoteFlags.Point) != 0 ? Reach : (byte)0;

            int target = NoTarget;
            if ((flags & EmoteFlags.HandshakeOffer) != 0 && Target >= 0 && Target < seatCount && Target != ownSeat)
                target = Target;

            return new EmoteState(flags, reach, target);
        }

        public bool Equals(EmoteState other)
        {
            return Flags == other.Flags && Reach == other.Reach && Target == other.Target;
        }

        public override bool Equals(object obj) { return obj is EmoteState other && Equals(other); }

        public override int GetHashCode() { return ((int)Flags << 16) ^ (Reach << 8) ^ (Target + 1); }

        public static bool operator ==(EmoteState a, EmoteState b) { return a.Equals(b); }
        public static bool operator !=(EmoteState a, EmoteState b) { return !a.Equals(b); }

        public override string ToString()
        {
            return Flags + (Has(EmoteFlags.Point) ? " reach " + Reach : "") + (Target != NoTarget ? " -> seat " + Target : "");
        }
    }

    /// <summary>What the emote authority knows about one seat: may it emote at all.</summary>
    public readonly struct EmoteSeat
    {
        public EmoteSeat(bool present, bool alive, bool isBot)
        {
            Present = present;
            Alive = alive;
            IsBot = isBot;
        }

        /// <summary>A human whose participant is still in the match.</summary>
        public bool Present { get; }
        public bool Alive { get; }
        public bool IsBot { get; }

        /// <summary>Only living, present humans emote. Bots never do.</summary>
        public bool CanEmote { get { return Present && Alive && !IsBot; } }
    }
}
