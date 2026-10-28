using NUnit.Framework;
using Shields.Session;

namespace Shields.Game.Tests
{
    /// <summary>
    /// Host side rematch voting (TASK 026): connected humans only, unanimity, one start,
    /// disconnects stop counting, and every match collects a fresh vote.
    /// </summary>
    public class RematchVoteTests
    {
        const ulong A = 0;
        const ulong B = 5;
        const ulong C = 9;

        [Test]
        public void NothingStartsBeforeEveryHumanAgrees()
        {
            RematchVote vote = new RematchVote();
            ulong[] humans = { A, B, C };

            Assert.IsTrue(vote.SetVote(A, true, humans));
            Assert.IsFalse(vote.TryLockForRestart(humans));
            Assert.IsTrue(vote.SetVote(B, true, humans));
            Assert.IsFalse(vote.TryLockForRestart(humans));
            Assert.AreEqual(2, vote.YesCount);
            Assert.IsFalse(vote.Locked);
        }

        [Test]
        public void UnanimityStartsExactlyOnce()
        {
            RematchVote vote = new RematchVote();
            ulong[] humans = { A, B };
            vote.SetVote(A, true, humans);
            vote.SetVote(B, true, humans);

            Assert.IsTrue(vote.TryLockForRestart(humans), "First caller starts the rematch.");
            Assert.IsFalse(vote.TryLockForRestart(humans), "A second near-simultaneous vote cannot start it again.");
            Assert.IsFalse(vote.SetVote(A, false, humans), "Votes are locked once the restart begins.");
            Assert.IsTrue(vote.Locked);
        }

        [Test]
        public void BotsNeverCountBecauseOnlyHumansAreVoters()
        {
            // 3 humans + 3 bots: only the humans are ever passed as the voter set.
            RematchVote vote = new RematchVote();
            ulong[] humans = { A, B, C };
            foreach (ulong human in humans) vote.SetVote(human, true, humans);

            Assert.AreEqual(3, vote.YesCount);
            Assert.IsTrue(vote.TryLockForRestart(humans));
        }

        [Test]
        public void SomebodyWithoutASeatCannotVote()
        {
            RematchVote vote = new RematchVote();

            Assert.IsFalse(vote.SetVote(77, true, new[] { A }));
            Assert.AreEqual(0, vote.YesCount);
        }

        [Test]
        public void DuplicateYesCountsOnce()
        {
            RematchVote vote = new RematchVote();
            ulong[] humans = { A, B };
            vote.SetVote(A, true, humans);
            vote.SetVote(A, true, humans);
            vote.SetVote(A, true, humans);

            Assert.AreEqual(1, vote.YesCount);
            Assert.IsFalse(vote.TryLockForRestart(humans));
        }

        [Test]
        public void AVoteCanBeWithdrawnBeforeTheStart()
        {
            RematchVote vote = new RematchVote();
            ulong[] humans = { A, B };
            vote.SetVote(A, true, humans);
            vote.SetVote(A, false, humans);
            vote.SetVote(B, true, humans);

            Assert.IsFalse(vote.HasVoted(A));
            Assert.IsFalse(vote.TryLockForRestart(humans));
        }

        [Test]
        public void ADisconnectedVoterNoLongerBlocksTheTable()
        {
            RematchVote vote = new RematchVote();
            ulong[] all = { A, B, C };
            vote.SetVote(A, true, all);
            vote.SetVote(B, true, all);
            Assert.IsFalse(vote.TryLockForRestart(all), "C has not voted.");

            ulong[] afterCLeft = { A, B };
            Assert.IsTrue(vote.TryLockForRestart(afterCLeft), "A + B are now everyone.");
        }

        [Test]
        public void ADisconnectedYesVoterIsRemovedFromTheTally()
        {
            RematchVote vote = new RematchVote();
            ulong[] all = { A, B, C };
            vote.SetVote(C, true, all);

            Assert.IsTrue(vote.Prune(new[] { A, B }));
            Assert.AreEqual(0, vote.YesCount);
            Assert.IsFalse(vote.HasVoted(C));
        }

        [Test]
        public void NobodyConnectedMeansNoRestart()
        {
            RematchVote vote = new RematchVote();

            Assert.IsFalse(vote.TryLockForRestart(new ulong[0]));
        }

        [Test]
        public void EachNewMatchCollectsAFreshVote()
        {
            RematchVote vote = new RematchVote();
            ulong[] humans = { A, B };

            // Match 1 ends, both vote, Match 2 starts.
            vote.SetVote(A, true, humans);
            vote.SetVote(B, true, humans);
            Assert.IsTrue(vote.TryLockForRestart(humans));
            vote.Reset();

            Assert.AreEqual(0, vote.YesCount);
            Assert.IsFalse(vote.Locked);

            // Match 2 ends: a brand new vote, which again needs everyone.
            vote.SetVote(B, true, humans);
            Assert.IsFalse(vote.TryLockForRestart(humans));
            vote.SetVote(A, true, humans);
            Assert.IsTrue(vote.TryLockForRestart(humans));
        }
    }
}
