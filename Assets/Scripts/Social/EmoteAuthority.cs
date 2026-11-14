using System.Collections.Generic;

namespace Shields.Social
{
    public enum EmoteSubmitResult : byte
    {
        Accepted = 0,
        /// <summary>Valid, but identical to what the seat already shows: nothing to broadcast.</summary>
        Unchanged = 1,
        StaleGeneration = 2,
        InvalidSeat = 3,
        /// <summary>The seat may not emote (dead, left, bot).</summary>
        NotAllowed = 4
    }

    /// <summary>
    /// The authority's view of every seat's hand emotes for one match generation: the offline
    /// machine, or the host online. It validates what each seat asks to show, pairs mutual
    /// handshake offers through a <see cref="HandshakeMatcher"/>, and on every tick drops the
    /// emotes and handshakes of seats that may no longer emote (dead, left). Cosmetic only;
    /// nothing here ever reaches the Shields match. Pure state: the caller supplies time and
    /// the roster, and broadcasts what changed.
    /// </summary>
    public sealed class EmoteAuthority
    {
        readonly EmoteState[] _states;
        readonly HandshakeMatcher _handshakes;

        public EmoteAuthority(int seatCount, int generation, double handshakeSeconds = HandshakeTiming.Total)
        {
            _states = new EmoteState[seatCount < 0 ? 0 : seatCount];
            for (int i = 0; i < _states.Length; i++) _states[i] = EmoteState.Idle;

            _handshakes = new HandshakeMatcher(_states.Length, handshakeSeconds);
            Generation = generation;
        }

        public int Generation { get; }

        public int SeatCount { get { return _states.Length; } }

        public HandshakeMatcher Handshakes { get { return _handshakes; } }

        public EmoteState StateOf(int seat) { return Valid(seat) ? _states[seat] : EmoteState.Idle; }

        /// <summary>
        /// A seat asks to show <paramref name="requested"/>. The seat comes from the authority's
        /// own participant map, never from the request. Accepted means the stored state changed
        /// and <paramref name="applied"/> must be broadcast.
        /// </summary>
        public EmoteSubmitResult Submit(int seat, EmoteState requested, int generation, IReadOnlyList<EmoteSeat> roster,
            out EmoteState applied)
        {
            applied = StateOf(seat);

            if (generation != Generation) return EmoteSubmitResult.StaleGeneration;
            if (!Valid(seat)) return EmoteSubmitResult.InvalidSeat;
            if (roster == null || seat >= roster.Count || !roster[seat].CanEmote) return EmoteSubmitResult.NotAllowed;

            EmoteState state = requested.Sanitized(seat, _states.Length);
            bool offering = state.Has(EmoteFlags.HandshakeOffer);
            _handshakes.SetOffer(seat, offering, state.Target);

            if (state == _states[seat]) return EmoteSubmitResult.Unchanged;

            _states[seat] = state;
            applied = state;
            return EmoteSubmitResult.Accepted;
        }

        /// <summary>The seat's own machine saw gameplay take its right hand: cancel its handshake for both.</summary>
        public bool Interrupt(int seat, int generation, List<HandshakeEnd> ended)
        {
            if (generation != Generation || !Valid(seat)) return false;

            return _handshakes.Interrupt(seat, ended);
        }

        /// <summary>A participant left: their emotes stop and their handshake is cancelled.</summary>
        public void RemoveSeat(int seat, HandshakeEndReason reason, List<int> clearedSeats, List<HandshakeEnd> ended)
        {
            if (!Valid(seat)) return;

            _handshakes.RemoveSeat(seat, reason, ended);
            if (_states[seat].IsIdle) return;

            _states[seat] = EmoteState.Idle;
            if (clearedSeats != null) clearedSeats.Add(seat);
        }

        /// <summary>
        /// Drops the emotes of seats that may no longer emote, pairs mutual offers, and ends
        /// handshakes that have played out. Everything that changed is added to the lists.
        /// </summary>
        public void Tick(double now, IReadOnlyList<EmoteSeat> roster, List<int> clearedSeats, List<HandshakePair> started,
            List<HandshakeEnd> ended)
        {
            for (int seat = 0; seat < _states.Length; seat++)
            {
                EmoteSeat info = roster != null && seat < roster.Count ? roster[seat] : default;
                if (info.CanEmote) continue;

                HandshakeEndReason reason = !info.Present ? HandshakeEndReason.Left : HandshakeEndReason.Died;
                RemoveSeat(seat, reason, clearedSeats, ended);
            }

            _handshakes.Tick(now, ended);
            _handshakes.Match(now, s => roster != null && s < roster.Count && roster[s].CanEmote, started);
        }

        /// <summary>Drops everything: match end, rematch, teardown.</summary>
        public void Clear(List<int> clearedSeats, List<HandshakeEnd> ended)
        {
            _handshakes.Clear(ended);
            for (int seat = 0; seat < _states.Length; seat++)
            {
                if (_states[seat].IsIdle) continue;

                _states[seat] = EmoteState.Idle;
                if (clearedSeats != null) clearedSeats.Add(seat);
            }
        }

        bool Valid(int seat) { return seat >= 0 && seat < _states.Length; }
    }
}
