using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Shields.Bots;
using Shields.Cards;
using Shields.Game;

namespace Shields.Game.Tests
{
    /// <summary>
    /// EditMode tests for the temporary random bot. Every test uses a seeded random
    /// source, so results are deterministic.
    /// </summary>
    public class RandomBotPolicyTests
    {
        const int Draws = 600;

        /// <summary>Five seats; seat 3 is eliminated. Card ids are made up and unique.</summary>
        static List<MatchPlayerState> Table(bool botHasCharge, int chargeCardId = 900, CardRank chargeRank = CardRank.King)
        {
            List<MatchPlayerState> players = new List<MatchPlayerState>();
            int nextId = 0;

            for (int i = 0; i < 5; i++)
            {
                List<PlayingCard> dealt = new List<PlayingCard>
                {
                    new PlayingCard(nextId++, 0, CardSuit.Clubs, CardRank.Five),
                    new PlayingCard(nextId++, 0, CardSuit.Hearts, CardRank.Six),
                    new PlayingCard(nextId++, 0, CardSuit.Spades, CardRank.Seven)
                };

                PlayerSetupState setup = new PlayerSetupState(i, i != 0, dealt);
                setup.SelectShieldAt(2);
                players.Add(new MatchPlayerState(setup));
            }

            players[3].IsAlive = false;

            if (botHasCharge)
                players[1].ChargeCard = new PlayingCard(chargeCardId, 0, CardSuit.Diamonds, chargeRank);

            return players;
        }

        static List<TurnActionRequest> Choose(List<MatchPlayerState> players, int botIndex, int seed)
        {
            RandomBotPolicy policy = new RandomBotPolicy(new Random(seed));
            List<TurnActionRequest> requests = new List<TurnActionRequest>();

            for (int i = 0; i < Draws; i++) requests.Add(policy.ChooseAction(players, botIndex));

            return requests;
        }

        [Test]
        public void ChoosesOnlyTheThreeActionsWithRoughlyEqualChance()
        {
            List<TurnActionRequest> requests = Choose(Table(false), 1, 17);

            foreach (TurnActionRequest request in requests)
            {
                Assert.AreEqual(1, request.PlayerIndex);
                Assert.That(request.ActionType,
                    Is.EqualTo(TurnActionType.Attack).Or.EqualTo(TurnActionType.ReplaceShield).Or.EqualTo(TurnActionType.Charge));
            }

            foreach (TurnActionType type in new[] { TurnActionType.Attack, TurnActionType.ReplaceShield, TurnActionType.Charge })
            {
                int count = requests.Count(r => r.ActionType == type);
                Assert.That(count, Is.InRange(Draws / 3 - 60, Draws / 3 + 60), "{0} chosen {1} times.", type, count);
            }
        }

        [Test]
        public void AttackTargetsALivingOpponentOnly()
        {
            List<MatchPlayerState> players = Table(false);
            List<TurnActionRequest> attacks = Choose(players, 1, 23).Where(r => r.ActionType == TurnActionType.Attack).ToList();

            Assert.IsNotEmpty(attacks);
            foreach (TurnActionRequest attack in attacks)
            {
                Assert.AreNotEqual(1, attack.TargetIndex, "A bot never attacks itself.");
                Assert.IsTrue(players[attack.TargetIndex].IsAlive, "A bot never attacks a dead player.");
            }

            CollectionAssert.AreEquivalent(new[] { 0, 2, 4 }, attacks.Select(a => a.TargetIndex).Distinct());
        }

        [Test]
        public void ReplaceShieldTargetsAnyLivingPlayerIncludingSelf()
        {
            List<MatchPlayerState> players = Table(false);
            List<TurnActionRequest> replaces = Choose(players, 1, 29).Where(r => r.ActionType == TurnActionType.ReplaceShield).ToList();

            foreach (TurnActionRequest replace in replaces)
                Assert.IsTrue(players[replace.TargetIndex].IsAlive);

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 4 }, replaces.Select(r => r.TargetIndex).Distinct(),
                "Self is a legal Shield target; the dead seat is not.");
        }

        [Test]
        public void FirstChargeHasNoTarget()
        {
            List<TurnActionRequest> charges = Choose(Table(false), 1, 31).Where(r => r.ActionType == TurnActionType.Charge).ToList();

            Assert.IsNotEmpty(charges);
            foreach (TurnActionRequest charge in charges)
                Assert.AreEqual(TurnActionRequest.NoTarget, charge.TargetIndex);
        }

        [Test]
        public void ChargedChargeTargetsALivingOpponentOnly()
        {
            List<MatchPlayerState> players = Table(true);
            List<TurnActionRequest> charges = Choose(players, 1, 37).Where(r => r.ActionType == TurnActionType.Charge).ToList();

            Assert.IsNotEmpty(charges);
            foreach (TurnActionRequest charge in charges)
            {
                Assert.AreNotEqual(TurnActionRequest.NoTarget, charge.TargetIndex);
                Assert.AreNotEqual(1, charge.TargetIndex);
                Assert.IsTrue(players[charge.TargetIndex].IsAlive);
            }
        }

        [Test]
        public void DecisionsDoNotDependOnTheHiddenChargeCard()
        {
            List<TurnActionRequest> withKing = Choose(Table(true, 900, CardRank.King), 1, 41);
            List<TurnActionRequest> withAce = Choose(Table(true, 901, CardRank.Ace), 1, 41);

            CollectionAssert.AreEqual(withKing, withAce, "Only HasCharge may influence the bot, never the card.");
        }

        [Test]
        public void PolicyAssemblyCannotSeeEngineInternals()
        {
            string[] friends = typeof(ShieldsMatch).Assembly
                .GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
                .Cast<InternalsVisibleToAttribute>()
                .Select(a => a.AssemblyName)
                .ToArray();

            CollectionAssert.DoesNotContain(friends, typeof(RandomBotPolicy).Assembly.GetName().Name,
                "The bot must not be able to read the internal hidden Charge card.");
        }

        [Test]
        public void EveryChoiceIsAcceptedByTheEngineThroughAWholeMatch([Values(3, 6, 8)] int players, [Values(1, 2)] int decks)
        {
            MatchSetup setup = new MatchSetup(new MatchConfig(players, decks, 0, 77 + players));
            setup.RunBotSelections();
            setup.SelectShieldFor(0, setup.LocalPlayer.DealtCards[1]);

            ShieldsMatch match = new ShieldsMatch(setup);
            RandomBotPolicy policy = new RandomBotPolicy(new Random(5));

            for (int turn = 0; turn < 5000 && !match.IsMatchOver; turn++)
            {
                TurnActionRequest request = policy.ChooseAction(match.Players, match.CurrentPlayerIndex);
                TurnActionResult result = match.Submit(request);

                Assert.IsTrue(result.Accepted, "{0} was rejected: {1}", request, result.Rejection);
            }

            Assert.IsTrue(match.IsMatchOver);
            Assert.AreEqual(1, match.LivingPlayerCount);
        }
    }
}
