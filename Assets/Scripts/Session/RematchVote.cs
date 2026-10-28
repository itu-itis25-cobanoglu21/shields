using System.Collections.Generic;

namespace Shields.Session
{
    /// <summary>
    /// Host side rematch voting after a match is over. Only connected humans vote; bots
    /// are always ready. The rematch starts when every currently connected human has
    /// voted yes, and <see cref="TryLockForRestart"/> lets that happen exactly once.
    ///
    /// The voter set is always the current one passed in, so a human who disconnects
    /// stops counting the moment they are gone and can never block the table.
    /// Plain C#, keyed by participant id, never by seat.
    /// </summary>
    public sealed class RematchVote
    {
        readonly HashSet<ulong> _yes = new HashSet<ulong>();
        readonly List<ulong> _ordered = new List<ulong>();

        /// <summary>True once a rematch has started from this vote; nothing changes it any more.</summary>
        public bool Locked { get; private set; }

        public int YesCount { get { return _yes.Count; } }

        /// <summary>Yes voters in the order they voted.</summary>
        public IReadOnlyList<ulong> YesVoters { get { return _ordered; } }

        public bool HasVoted(ulong participantId)
        {
            return _yes.Contains(participantId);
        }

        /// <summary>
        /// Records one participant's vote. Refused when locked or when the participant is
        /// not a currently connected human. Voting yes twice counts once.
        /// </summary>
        public bool SetVote(ulong participantId, bool yes, IReadOnlyCollection<ulong> connectedHumans)
        {
            if (Locked || connectedHumans == null || !Contains(connectedHumans, participantId)) return false;

            if (yes)
            {
                if (_yes.Add(participantId)) _ordered.Add(participantId);
            }
            else if (_yes.Remove(participantId))
            {
                _ordered.Remove(participantId);
            }

            return true;
        }

        /// <summary>Drops votes of anybody no longer connected. Returns true when something changed.</summary>
        public bool Prune(IReadOnlyCollection<ulong> connectedHumans)
        {
            bool changed = false;

            for (int i = _ordered.Count - 1; i >= 0; i--)
            {
                ulong voter = _ordered[i];
                if (connectedHumans != null && Contains(connectedHumans, voter)) continue;

                _ordered.RemoveAt(i);
                _yes.Remove(voter);
                changed = true;
            }

            return changed;
        }

        /// <summary>Every connected human has voted yes, and there is at least one of them.</summary>
        public bool IsUnanimous(IReadOnlyCollection<ulong> connectedHumans)
        {
            if (connectedHumans == null || connectedHumans.Count == 0) return false;

            foreach (ulong human in connectedHumans)
                if (!_yes.Contains(human)) return false;

            return true;
        }

        /// <summary>
        /// The start-once guard: prunes, and if the vote is unanimous and not yet locked,
        /// locks it and returns true. Every later call returns false.
        /// </summary>
        public bool TryLockForRestart(IReadOnlyCollection<ulong> connectedHumans)
        {
            if (Locked) return false;

            Prune(connectedHumans);
            if (!IsUnanimous(connectedHumans)) return false;

            Locked = true;
            return true;
        }

        /// <summary>A new match began: a fresh, unlocked vote.</summary>
        public void Reset()
        {
            _yes.Clear();
            _ordered.Clear();
            Locked = false;
        }

        static bool Contains(IReadOnlyCollection<ulong> collection, ulong value)
        {
            foreach (ulong item in collection)
                if (item == value) return true;

            return false;
        }
    }
}
