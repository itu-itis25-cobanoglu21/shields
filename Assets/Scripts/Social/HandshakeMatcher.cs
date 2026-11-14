using System;
using System.Collections.Generic;

namespace Shields.Social
{
    /// <summary>
    /// Timing of one matched handshake, shared by the authority (which frees the pair after
    /// <see cref="Total"/>) and every machine's presentation (which plays it locally from the
    /// moment the pair arrives), so nobody streams the hands.
    /// </summary>
    public static class HandshakeTiming
    {
        /// <summary>Both right hands travel to the table centre.</summary>
        public const double Approach = 0.40;

        /// <summary>Fingers close around the other hand.</summary>
        public const double Grip = 0.12;

        /// <summary>The quick up/down shakes.</summary>
        public const double Shake = 0.75;

        /// <summary>Fingers open again before the hands separate.</summary>
        public const double Release = 0.15;

        public const double Total = Approach + Grip + Shake + Release;
    }

    public enum HandshakeEndReason : byte
    {
        Completed = 0,
        /// <summary>Gameplay took one of the right hands.</summary>
        Interrupted = 1,
        /// <summary>A participant left the match.</summary>
        Left = 2,
        Died = 3,
        /// <summary>The match ended, a rematch began, or the session went away.</summary>
        Reset = 4
    }

    /// <summary>One matched handshake between two seats. SeatA is always the lower seat.</summary>
    public readonly struct HandshakePair : IEquatable<HandshakePair>
    {
        public HandshakePair(int id, int seatA, int seatB, double startTime)
        {
            Id = id;
            SeatA = Math.Min(seatA, seatB);
            SeatB = Math.Max(seatA, seatB);
            StartTime = startTime;
        }

        public int Id { get; }
        public int SeatA { get; }
        public int SeatB { get; }
        public double StartTime { get; }

        public bool Involves(int seat) { return seat == SeatA || seat == SeatB; }

        /// <summary>The other participant, or -1 when the seat is not in this pair.</summary>
        public int PartnerOf(int seat) { return seat == SeatA ? SeatB : seat == SeatB ? SeatA : -1; }

        public bool Equals(HandshakePair other) { return Id == other.Id && SeatA == other.SeatA && SeatB == other.SeatB; }
        public override bool Equals(object obj) { return obj is HandshakePair other && Equals(other); }
        public override int GetHashCode() { return (Id * 397) ^ (SeatA << 4) ^ SeatB; }

        public override string ToString() { return "handshake #" + Id + " (seat " + SeatA + " + seat " + SeatB + ")"; }
    }

    public readonly struct HandshakeEnd
    {
        public HandshakeEnd(HandshakePair pair, HandshakeEndReason reason)
        {
            Pair = pair;
            Reason = reason;
        }

        public HandshakePair Pair { get; }
        public HandshakeEndReason Reason { get; }

        public override string ToString() { return Pair + " ended: " + Reason; }
    }

    /// <summary>
    /// Pairs mutual handshake offers. A seat offers to one target seat while it holds the
    /// handshake input; when A offers to B and B offers to A, and both are allowed to shake,
    /// exactly one pair is made. A seat is in at most one pair. After a handshake both seats
    /// must let go of the input before they can be paired again, so holding it never loops.
    /// Pure state: the caller supplies time and who may shake.
    /// </summary>
    public sealed class HandshakeMatcher
    {
        readonly int[] _target;
        readonly bool[] _offering;
        readonly bool[] _needsRearm;
        readonly List<HandshakePair> _pairs = new List<HandshakePair>();
        readonly double _duration;
        int _nextId = 1;

        public HandshakeMatcher(int seatCount, double durationSeconds = HandshakeTiming.Total)
        {
            if (seatCount < 0) throw new ArgumentOutOfRangeException(nameof(seatCount));

            _target = new int[seatCount];
            _offering = new bool[seatCount];
            _needsRearm = new bool[seatCount];
            for (int i = 0; i < seatCount; i++) _target[i] = EmoteState.NoTarget;

            _duration = Math.Max(0.0, durationSeconds);
        }

        public int SeatCount { get { return _target.Length; } }

        public double Duration { get { return _duration; } }

        public IReadOnlyList<HandshakePair> Pairs { get { return _pairs; } }

        public bool IsOffering(int seat) { return Valid(seat) && _offering[seat]; }

        public int TargetOf(int seat) { return Valid(seat) ? _target[seat] : EmoteState.NoTarget; }

        /// <summary>True when the seat shook hands and has not let go of the input since.</summary>
        public bool NeedsRearm(int seat) { return Valid(seat) && _needsRearm[seat]; }

        public bool TryGetPair(int seat, out HandshakePair pair)
        {
            for (int i = 0; i < _pairs.Count; i++)
                if (_pairs[i].Involves(seat)) { pair = _pairs[i]; return true; }

            pair = default;
            return false;
        }

        public bool InPair(int seat) { return TryGetPair(seat, out _); }

        /// <summary>
        /// The seat holds (or let go of) the handshake input, offered to a target seat. Letting
        /// go withdraws the offer and re-arms the seat; it never cancels a handshake already
        /// playing, which finishes on its own.
        /// </summary>
        public void SetOffer(int seat, bool offering, int target)
        {
            if (!Valid(seat)) return;

            if (!offering)
            {
                _offering[seat] = false;
                _target[seat] = EmoteState.NoTarget;
                _needsRearm[seat] = false;
                return;
            }

            _offering[seat] = true;
            _target[seat] = Valid(target) && target != seat ? target : EmoteState.NoTarget;
        }

        /// <summary>
        /// Makes a pair for every mutual offer whose seats may shake now. Each new pair is added
        /// to <paramref name="started"/> exactly once. Returns how many were made.
        /// </summary>
        public int Match(double now, Func<int, bool> canShake, List<HandshakePair> started)
        {
            int made = 0;

            for (int a = 0; a < _target.Length; a++)
            {
                if (!Ready(a, canShake)) continue;

                int b = _target[a];
                if (b <= a || !Ready(b, canShake) || _target[b] != a) continue;

                HandshakePair pair = new HandshakePair(_nextId++, a, b, now);
                _pairs.Add(pair);
                _needsRearm[a] = true;
                _needsRearm[b] = true;

                if (started != null) started.Add(pair);
                made++;
            }

            return made;
        }

        /// <summary>Ends every pair whose handshake has fully played.</summary>
        public void Tick(double now, List<HandshakeEnd> ended)
        {
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                if (now - _pairs[i].StartTime < _duration) continue;

                HandshakePair pair = _pairs[i];
                _pairs.RemoveAt(i);
                if (ended != null) ended.Add(new HandshakeEnd(pair, HandshakeEndReason.Completed));
            }
        }

        /// <summary>Gameplay took this seat's right hand: its handshake, if any, is cancelled for both.</summary>
        public bool Interrupt(int seat, List<HandshakeEnd> ended)
        {
            return EndPairOf(seat, HandshakeEndReason.Interrupted, ended);
        }

        /// <summary>The seat died or left: its offer is gone and its handshake, if any, is cancelled.</summary>
        public void RemoveSeat(int seat, HandshakeEndReason reason, List<HandshakeEnd> ended)
        {
            if (!Valid(seat)) return;

            _offering[seat] = false;
            _target[seat] = EmoteState.NoTarget;
            _needsRearm[seat] = false;
            EndPairOf(seat, reason, ended);
        }

        /// <summary>Drops every offer and pair (match end, rematch, teardown).</summary>
        public void Clear(List<HandshakeEnd> ended)
        {
            for (int i = 0; i < _pairs.Count; i++)
                if (ended != null) ended.Add(new HandshakeEnd(_pairs[i], HandshakeEndReason.Reset));

            _pairs.Clear();
            for (int i = 0; i < _target.Length; i++)
            {
                _offering[i] = false;
                _target[i] = EmoteState.NoTarget;
                _needsRearm[i] = false;
            }
        }

        bool EndPairOf(int seat, HandshakeEndReason reason, List<HandshakeEnd> ended)
        {
            for (int i = 0; i < _pairs.Count; i++)
            {
                if (!_pairs[i].Involves(seat)) continue;

                HandshakePair pair = _pairs[i];
                _pairs.RemoveAt(i);
                if (ended != null) ended.Add(new HandshakeEnd(pair, reason));
                return true;
            }

            return false;
        }

        bool Ready(int seat, Func<int, bool> canShake)
        {
            return Valid(seat) && _offering[seat] && !_needsRearm[seat] && _target[seat] != EmoteState.NoTarget &&
                !InPair(seat) && (canShake == null || canShake(seat));
        }

        bool Valid(int seat) { return seat >= 0 && seat < _target.Length; }
    }
}
