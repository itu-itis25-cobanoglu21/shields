namespace Shields.Online
{
    /// <summary>The independent revision streams of one match.</summary>
    public enum SnapshotChannel
    {
        Setup = 0,
        Match = 1
    }

    /// <summary>What the gate decided about one incoming message.</summary>
    public enum GateDecision
    {
        /// <summary>Older generation, or an old revision in the current one: drop it.</summary>
        Stale = 0,

        /// <summary>Same generation, newer revision.</summary>
        Accepted = 1,

        /// <summary>A newer generation: per-match state was reset, and the message is accepted.</summary>
        AcceptedNewGeneration = 2
    }

    /// <summary>
    /// Orders public match messages across consecutive matches in one session.
    ///
    /// The host numbers every match with a generation (1, 2, 3 ...) and restarts its
    /// snapshot revisions at 1 for each one. A revision on its own therefore cannot say
    /// whether it is old: revision 2 of match 2 is newer than revision 5 of match 1.
    /// The gate compares generation first. A newer generation resets every channel's
    /// applied revision; within a generation revisions must increase; anything from an
    /// older generation is stale, so a delayed message from a finished match can never
    /// overwrite the new one.
    ///
    /// Plain C#, so the whole policy is unit tested without a network.
    /// </summary>
    public sealed class MatchGenerationGate
    {
        readonly uint[] _applied = new uint[2];

        /// <summary>The newest generation seen; 0 before the first match.</summary>
        public int Generation { get; private set; }

        public uint AppliedRevision(SnapshotChannel channel)
        {
            return _applied[(int)channel];
        }

        /// <summary>Decides on a revisioned snapshot, and records it when accepted.</summary>
        public GateDecision AcceptSnapshot(int generation, uint revision, SnapshotChannel channel)
        {
            GateDecision decision = AdvanceTo(generation);
            if (decision == GateDecision.Stale) return GateDecision.Stale;

            int index = (int)channel;
            if (decision == GateDecision.Accepted && revision <= _applied[index]) return GateDecision.Stale;

            _applied[index] = revision;
            return decision;
        }

        /// <summary>
        /// Decides on an unrevisioned event of a match (an action result, a waiter
        /// broadcast): accepted for the current or a newer generation, stale for an older one.
        /// </summary>
        public GateDecision AcceptEvent(int generation)
        {
            return AdvanceTo(generation);
        }

        /// <summary>Forgets everything. A fresh network session starts again from generation 0.</summary>
        public void Reset()
        {
            Generation = 0;
            for (int i = 0; i < _applied.Length; i++) _applied[i] = 0;
        }

        GateDecision AdvanceTo(int generation)
        {
            if (generation < Generation) return GateDecision.Stale;
            if (generation == Generation) return GateDecision.Accepted;

            Generation = generation;
            for (int i = 0; i < _applied.Length; i++) _applied[i] = 0;
            return GateDecision.AcceptedNewGeneration;
        }
    }
}
