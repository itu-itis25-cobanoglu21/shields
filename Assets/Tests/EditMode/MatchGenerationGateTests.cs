using NUnit.Framework;
using Shields.Online;

namespace Shields.Game.Tests
{
    /// <summary>
    /// Snapshot ordering across consecutive matches (TASK 026). Root cause of the old bug:
    /// the host restarts revisions at 1 for every match while clients kept the previous
    /// match's applied revision, so the new match looked stale. Generations fix that.
    /// </summary>
    public class MatchGenerationGateTests
    {
        [Test]
        public void TheOldBugIsReproducedByRevisionsAlone()
        {
            // Without a generation, revision 1 of match 2 cannot beat revision 5 of match 1.
            MatchGenerationGate gate = new MatchGenerationGate();
            gate.AcceptSnapshot(1, 5, SnapshotChannel.Setup);

            Assert.AreEqual(GateDecision.Stale, gate.AcceptSnapshot(1, 1, SnapshotChannel.Setup),
                "Same generation: an old revision is stale, as it should be.");
            Assert.AreEqual(GateDecision.AcceptedNewGeneration, gate.AcceptSnapshot(2, 1, SnapshotChannel.Setup),
                "New generation: revision 1 is accepted.");
        }

        [Test]
        public void GenerationOneOrdersRevisionsNormally()
        {
            MatchGenerationGate gate = new MatchGenerationGate();

            Assert.AreEqual(GateDecision.AcceptedNewGeneration, gate.AcceptSnapshot(1, 1, SnapshotChannel.Setup));
            Assert.AreEqual(GateDecision.Accepted, gate.AcceptSnapshot(1, 2, SnapshotChannel.Setup));
            Assert.AreEqual(GateDecision.Stale, gate.AcceptSnapshot(1, 2, SnapshotChannel.Setup), "Duplicate.");
            Assert.AreEqual(GateDecision.Stale, gate.AcceptSnapshot(1, 1, SnapshotChannel.Setup), "Older.");
            Assert.AreEqual(GateDecision.Accepted, gate.AcceptSnapshot(1, 3, SnapshotChannel.Match));
            Assert.AreEqual(3u, gate.AppliedRevision(SnapshotChannel.Match));
        }

        [Test]
        public void ANewGenerationResetsEveryChannel()
        {
            MatchGenerationGate gate = new MatchGenerationGate();
            gate.AcceptSnapshot(1, 2, SnapshotChannel.Setup);
            gate.AcceptSnapshot(1, 40, SnapshotChannel.Match);

            Assert.AreEqual(GateDecision.AcceptedNewGeneration, gate.AcceptSnapshot(2, 1, SnapshotChannel.Setup));
            Assert.AreEqual(2, gate.Generation);
            Assert.AreEqual(0u, gate.AppliedRevision(SnapshotChannel.Match), "The match channel restarts too.");
            Assert.AreEqual(GateDecision.Accepted, gate.AcceptSnapshot(2, 3, SnapshotChannel.Match),
                "A low match revision of the new generation is accepted.");
        }

        [Test]
        public void StaleMessagesFromAnEarlierMatchAreRejected()
        {
            MatchGenerationGate gate = new MatchGenerationGate();
            gate.AcceptSnapshot(1, 9, SnapshotChannel.Match);
            gate.AcceptSnapshot(2, 1, SnapshotChannel.Setup);

            Assert.AreEqual(GateDecision.Stale, gate.AcceptSnapshot(1, 10, SnapshotChannel.Match), "A late match 1 snapshot.");
            Assert.AreEqual(GateDecision.Stale, gate.AcceptEvent(1), "A late match 1 action result.");
            Assert.AreEqual(2, gate.Generation);
        }

        [Test]
        public void ThreeAndFourMatchesInARowAllWork()
        {
            MatchGenerationGate gate = new MatchGenerationGate();

            for (int generation = 1; generation <= 4; generation++)
            {
                Assert.AreEqual(GateDecision.AcceptedNewGeneration, gate.AcceptSnapshot(generation, 1, SnapshotChannel.Setup), "Gen {0} deal.", generation);
                Assert.AreEqual(GateDecision.Accepted, gate.AcceptSnapshot(generation, 2, SnapshotChannel.Setup), "Gen {0} Shields.", generation);
                Assert.AreEqual(GateDecision.Accepted, gate.AcceptSnapshot(generation, 3, SnapshotChannel.Match), "Gen {0} play.", generation);
                Assert.AreEqual(GateDecision.Accepted, gate.AcceptEvent(generation), "Gen {0} result.", generation);
                Assert.AreEqual(GateDecision.Accepted, gate.AcceptSnapshot(generation, 4, SnapshotChannel.Match), "Gen {0} turn.", generation);

                if (generation > 1)
                    Assert.AreEqual(GateDecision.Stale, gate.AcceptSnapshot(generation - 1, 99, SnapshotChannel.Match), "Gen {0} rejects gen {1}.", generation, generation - 1);
            }
        }

        [Test]
        public void AnEventOfANewerGenerationAdvancesIt()
        {
            MatchGenerationGate gate = new MatchGenerationGate();
            gate.AcceptSnapshot(1, 5, SnapshotChannel.Match);

            Assert.AreEqual(GateDecision.AcceptedNewGeneration, gate.AcceptEvent(2));
            Assert.AreEqual(GateDecision.Accepted, gate.AcceptSnapshot(2, 1, SnapshotChannel.Match));
        }

        [Test]
        public void ResetStartsAFreshSession()
        {
            MatchGenerationGate gate = new MatchGenerationGate();
            gate.AcceptSnapshot(3, 7, SnapshotChannel.Setup);
            gate.Reset();

            Assert.AreEqual(0, gate.Generation);
            Assert.AreEqual(GateDecision.AcceptedNewGeneration, gate.AcceptSnapshot(1, 1, SnapshotChannel.Setup));
        }
    }
}
