using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shields.Cards;
using Shields.Game;

namespace Shields.Game.Tests
{
    /// <summary>
    /// EditMode tests for the TASK 006 turn engine. Every test arranges an unshuffled deck
    /// and stacks the exact cards each action will draw, so nothing here is probabilistic.
    /// </summary>
    public class ShieldsMatchTests
    {
        const CardRank A = CardRank.Ace;
        const CardRank C2 = CardRank.Two;
        const CardRank C3 = CardRank.Three;
        const CardRank C4 = CardRank.Four;
        const CardRank C5 = CardRank.Five;
        const CardRank C6 = CardRank.Six;
        const CardRank C7 = CardRank.Seven;
        const CardRank C8 = CardRank.Eight;
        const CardRank C9 = CardRank.Nine;
        const CardRank C10 = CardRank.Ten;
        const CardRank J = CardRank.Jack;
        const CardRank Q = CardRank.Queen;
        const CardRank K = CardRank.King;


        const int NoTarget = TurnActionRequest.NoTarget;

        // ================================================================ helpers

        static PlayingCard Take(CardDeck deck, CardRank rank)
        {
            PlayingCard card;
            Assert.IsTrue(deck.TryTakeFromDrawPile((int)rank, out card), "No {0} left to arrange.", rank);
            return card;
        }

        /// <summary>A seat whose left Health, right Health and Shield are the given ranks.</summary>
        static PlayerSetupState Seat(CardDeck deck, int index, CardRank left, CardRank right, CardRank shield)
        {
            List<PlayingCard> cards = new List<PlayingCard> { Take(deck, left), Take(deck, right), Take(deck, shield) };
            PlayerSetupState seat = new PlayerSetupState(index, index != 0, cards);
            seat.SelectShieldAt(2);
            return seat;
        }

        /// <summary>Arranges the next draws. drawOrder[0] is drawn first. Returns those cards.</summary>
        static List<PlayingCard> Stack(CardDeck deck, params CardRank[] drawOrder)
        {
            List<PlayingCard> stacked = new List<PlayingCard>();

            for (int i = drawOrder.Length - 1; i >= 0; i--)
            {
                CardRank rank = drawOrder[i];
                PlayingCard card = deck.DrawPile.First(c => c.Rank == rank && !stacked.Contains(c));
                deck.MoveToTopOfDrawPile(card);
                stacked.Add(card);
            }

            stacked.Reverse();
            return stacked;
        }

        static ShieldsMatch Start(CardDeck deck, int startingPlayer, params PlayerSetupState[] seats)
        {
            return new ShieldsMatch(deck, seats, startingPlayer);
        }

        static TurnActionResult Act(ShieldsMatch match, TurnActionRequest request)
        {
            TurnActionResult result = match.Submit(request);
            Assert.IsTrue(result.Accepted, "{0} was rejected: {1}", request, result.Rejection);
            AssertInvariants(match);
            return result;
        }

        static void AssertRejected(ShieldsMatch match, TurnActionRequest request, ActionRejection expected)
        {
            int draw = match.Deck.DrawCount;
            int discard = match.Deck.DiscardCount;
            int current = match.CurrentPlayerIndex;

            TurnActionResult result = match.Submit(request);

            Assert.IsFalse(result.Accepted, "{0} should be rejected.", request);
            Assert.AreEqual(expected, result.Rejection);
            Assert.AreEqual(draw, match.Deck.DrawCount, "A rejected request must not draw.");
            Assert.AreEqual(discard, match.Deck.DiscardCount, "A rejected request must not discard.");
            Assert.AreEqual(current, match.CurrentPlayerIndex, "A rejected request must not pass the turn.");
        }

        /// <summary>Every physical card is in exactly one place; Virtual markers are not cards.</summary>
        static void AssertCardAccounting(ShieldsMatch match)
        {
            List<int> ids = new List<int>();
            ids.AddRange(match.Deck.DrawPile.Select(c => c.CardId));
            ids.AddRange(match.Deck.DiscardPile.Select(c => c.CardId));

            foreach (MatchPlayerState p in match.Players)
            {
                if (p.LeftHealth.IsCard) ids.Add(p.LeftHealth.Card.CardId);
                if (p.RightHealth.IsCard) ids.Add(p.RightHealth.Card.CardId);
                if (p.ShieldCard.IsValid) ids.Add(p.ShieldCard.CardId);
                if (p.ChargeCard.IsValid) ids.Add(p.ChargeCard.CardId);
            }

            Assert.AreEqual(match.Deck.TotalCardCount, ids.Count, "Physical card total must stay constant.");
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "A card identity is in two places at once.");
        }

        static void AssertInvariants(ShieldsMatch match)
        {
            AssertCardAccounting(match);

            int[] virtualCounts = new int[14];

            foreach (MatchPlayerState p in match.Players)
            {
                if (p.IsAlive)
                {
                    Assert.Greater(p.Health, 0);
                    Assert.AreEqual(p.Health, p.LeftHealth.Value + p.RightHealth.Value,
                        "Player {0}'s Health slots must represent their Health.", p.PlayerIndex);
                    Assert.IsTrue(p.ShieldCard.IsValid);

                    if (p.LeftHealth.IsVirtual) virtualCounts[p.LeftHealth.Value]++;
                    if (p.RightHealth.IsVirtual) virtualCounts[p.RightHealth.Value]++;
                }
                else
                {
                    Assert.IsTrue(p.LeftHealth.IsEmpty && p.RightHealth.IsEmpty);
                    Assert.IsFalse(p.ShieldCard.IsValid);
                    Assert.IsFalse(p.HasCharge);
                }
            }

            for (int v = 1; v <= 13; v++)
                Assert.AreEqual(virtualCounts[v], match.WaitingMarkerCount(v), "Waiting queue for {0} is out of sync.", v);
        }

        static MatchSetup CompletedSetup(int players, int decks, int seed)
        {
            MatchSetup setup = new MatchSetup(new MatchConfig(players, decks, 0, seed));
            setup.RunBotSelections();
            setup.SelectShieldFor(0, setup.LocalPlayer.DealtCards[0]);
            return setup;
        }

        /// <summary>
        /// Standard three seat table: P0 2+3 Shield 4, P1 King+Jack (24) Shield 5,
        /// P2 6+7 Shield 8. Player 0 starts, clockwise.
        /// </summary>
        static ShieldsMatch StandardTable(CardDeck deck)
        {
            return Start(deck, 0,
                Seat(deck, 0, C2, C3, C4),
                Seat(deck, 1, K, J, C5),
                Seat(deck, 2, C6, C7, C8));
        }

        /// <summary>Kills player 2 (2 + Ace, Shield Ace) on player 1's turn. Player 0 is then current.</summary>
        static ShieldsMatch TableWithPlayerTwoDead(CardDeck deck)
        {
            ShieldsMatch match = Start(deck, 1,
                Seat(deck, 0, C2, C3, C4),
                Seat(deck, 1, K, J, C5),
                Seat(deck, 2, C2, A, A));

            Stack(deck, K);
            Act(match, TurnActionRequest.Attack(1, 2));

            Assert.IsFalse(match.Players[2].IsAlive);
            Assert.AreEqual(0, match.CurrentPlayerIndex);
            return match;
        }

        // ================================================================ start

        [Test]
        public void Start_FromSetup_StartingPlayerIsLivingAndDirectionIsClockwise(
            [Values(3, 5, 8)] int players, [Values(1, 2, 3, 4)] int seed)
        {
            ShieldsMatch match = new ShieldsMatch(CompletedSetup(players, 1, seed));

            Assert.That(match.StartingPlayerIndex, Is.InRange(0, players - 1));
            Assert.IsTrue(match.Players[match.StartingPlayerIndex].IsAlive);
            Assert.AreEqual(match.StartingPlayerIndex, match.CurrentPlayerIndex);
            Assert.AreEqual(TurnDirection.Clockwise, match.Direction, "Rule: every match is played clockwise.");
            Assert.IsFalse(match.IsMatchOver);
            Assert.AreEqual(ShieldsMatch.NoPlayer, match.WinnerIndex);
        }

        [Test]
        public void Start_SameSeedReproducesTheSameStart()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                ShieldsMatch a = new ShieldsMatch(CompletedSetup(6, 1, seed));
                ShieldsMatch b = new ShieldsMatch(CompletedSetup(6, 1, seed));

                Assert.AreEqual(a.StartingPlayerIndex, b.StartingPlayerIndex, "Seed {0}", seed);
                Assert.AreEqual(a.Direction, b.Direction, "Seed {0}", seed);
            }
        }

        [Test]
        public void Start_DifferentSeedsProduceDifferentStartsButAlwaysClockwise()
        {
            HashSet<int> starts = new HashSet<int>();

            for (int seed = 1; seed <= 30; seed++)
            {
                ShieldsMatch match = new ShieldsMatch(CompletedSetup(6, 1, seed));
                starts.Add(match.StartingPlayerIndex);
                Assert.AreEqual(TurnDirection.Clockwise, match.Direction, "Seed {0}", seed);
            }

            Assert.Greater(starts.Count, 1, "The starting player is still random.");
        }

        [Test]
        public void Start_ContinuesTheSetupDeckAndCardsWithoutRedealing([Values(1, 2)] int decks)
        {
            MatchSetup setup = CompletedSetup(8, decks, 11);
            int drawBefore = setup.Deck.DrawCount;

            ShieldsMatch match = new ShieldsMatch(setup);

            Assert.AreSame(setup.Deck, match.Deck, "The match must keep the setup's deck.");
            Assert.AreEqual(drawBefore, match.Deck.DrawCount, "Starting turn play must not deal.");
            Assert.AreEqual(0, match.Deck.DiscardCount);

            for (int i = 0; i < 8; i++)
            {
                PlayerSetupState s = setup.Players[i];
                MatchPlayerState p = match.Players[i];

                Assert.AreEqual(i, p.PlayerIndex);
                Assert.IsTrue(p.IsAlive);
                Assert.AreEqual(s.IsBot, p.IsBot);
                Assert.AreEqual(s.StartingHealth, p.Health);
                Assert.AreEqual(s.HealthCards[0], p.LeftHealth.Card);
                Assert.AreEqual(s.HealthCards[1], p.RightHealth.Card);
                Assert.AreEqual(s.ShieldCard, p.ShieldCard);
                Assert.IsFalse(p.HasCharge);
            }

            AssertInvariants(match);
        }

        [Test]
        public void Start_RequiresEveryPlayerToHaveChosen()
        {
            MatchSetup setup = new MatchSetup(new MatchConfig(4, 1, 0, 5));
            setup.RunBotSelections();

            Assert.Throws<System.InvalidOperationException>(() => new ShieldsMatch(setup));
        }

        // ================================================================ turn order

        [Test]
        public void Turn_ClockwiseAdvancesToTheNextSeat()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 1,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, C5, C6, C7),
                Seat(deck, 2, C8, C9, C10), Seat(deck, 3, J, Q, K));

            List<int> order = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                int actor = match.CurrentPlayerIndex;
                order.Add(actor);
                Act(match, TurnActionRequest.ReplaceShield(actor, actor));
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0, 1 }, order);
        }

        [Test]
        public void Turn_EliminatedPlayersAreSkippedGoingClockwise()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 1,
                Seat(deck, 0, Q, Q, Q), Seat(deck, 1, J, J, J),
                Seat(deck, 2, C2, A, A), Seat(deck, 3, C10, C10, C10));

            Stack(deck, K);
            Act(match, TurnActionRequest.Attack(1, 2));
            Assert.IsFalse(match.Players[2].IsAlive);

            List<int> order = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                int actor = match.CurrentPlayerIndex;
                order.Add(actor);
                Act(match, TurnActionRequest.ReplaceShield(actor, actor));
            }

            CollectionAssert.AreEqual(new[] { 3, 0, 1, 3, 0, 1 }, order);
            CollectionAssert.DoesNotContain(order, 2);
        }

        [Test]
        public void Turn_OnlyTheCurrentPlayerMayAct()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);

            AssertRejected(match, TurnActionRequest.Attack(1, 2), ActionRejection.NotYourTurn);
            AssertRejected(match, TurnActionRequest.ReplaceShield(2, 2), ActionRejection.NotYourTurn);
            AssertRejected(match, TurnActionRequest.Charge(1), ActionRejection.NotYourTurn);
        }

        [Test]
        public void Turn_InvalidPlayerIndicesAreRejected([Values(-1, 3, 99)] int playerIndex)
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);

            AssertRejected(match, TurnActionRequest.Charge(playerIndex), ActionRejection.InvalidPlayer);
        }

        [Test]
        public void Turn_EliminatedPlayersCannotAct()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = TableWithPlayerTwoDead(deck);

            AssertRejected(match, TurnActionRequest.Charge(2), ActionRejection.PlayerEliminated);
        }

        [Test]
        public void Turn_APlayerCannotActTwiceInOneTurn()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);

            Act(match, TurnActionRequest.ReplaceShield(0, 0));

            AssertRejected(match, TurnActionRequest.ReplaceShield(0, 0), ActionRejection.NotYourTurn);
            Assert.AreEqual(1, match.CurrentPlayerIndex);
        }

        // ================================================================ attack

        [Test]
        public void Attack_SelfTargetIsRejectedBeforeDrawing()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);

            AssertRejected(match, TurnActionRequest.Attack(0, 0), ActionRejection.SelfTargetNotAllowed);
        }

        [Test]
        public void Attack_DeadTargetIsRejectedBeforeDrawing()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = TableWithPlayerTwoDead(deck);

            AssertRejected(match, TurnActionRequest.Attack(0, 2), ActionRejection.TargetEliminated);
        }

        [Test]
        public void Attack_InvalidTargetIsRejectedBeforeDrawing()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);

            AssertRejected(match, TurnActionRequest.Attack(0, NoTarget), ActionRejection.TargetRequired);
            AssertRejected(match, TurnActionRequest.Attack(0, 3), ActionRejection.InvalidTarget);
            AssertRejected(match, TurnActionRequest.Attack(0, -7), ActionRejection.InvalidTarget);
        }

        [Test]
        public void Attack_ValidTargetDrawsExactlyOneCardWhichEndsInTheDiscardPile()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard attackCard = Stack(deck, C3)[0];

            int drawBefore = deck.DrawCount;
            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(drawBefore - 1, deck.DrawCount);
            Assert.AreEqual(1, deck.DiscardCount);
            Assert.AreEqual(attackCard, deck.DiscardPile[0]);
            CollectionAssert.AreEqual(new[] { attackCard }, result.RevealedCards);
        }

        [Test]
        public void Attack_AboveShieldDealsTheDifferenceAndLeavesTheShield()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard shield = match.Players[1].ShieldCard;
            PlayingCard attackCard = Stack(deck, Q)[0];

            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(TurnActionType.Attack, result.ActionType);
            Assert.AreEqual(0, result.ActorIndex);
            Assert.AreEqual(1, result.TargetIndex);
            Assert.AreEqual(12, result.AttackPower);
            Assert.AreEqual(5, result.TargetShieldValue);
            Assert.AreEqual(7, result.Damage);
            Assert.AreEqual(24, result.TargetHealthBefore);
            Assert.AreEqual(17, result.TargetHealthAfter);
            Assert.AreEqual(17, match.Players[1].Health);

            Assert.AreEqual(shield, match.Players[1].ShieldCard, "Attacks never consume the Shield.");
            CollectionAssert.Contains(deck.DiscardPile, attackCard);
        }

        [Test]
        public void Attack_EqualToShieldDealsZero()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            HealthSlot left = match.Players[1].LeftHealth;
            HealthSlot right = match.Players[1].RightHealth;
            Stack(deck, C5);

            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(5, result.AttackPower);
            Assert.AreEqual(0, result.Damage);
            Assert.AreEqual(24, match.Players[1].Health);
            Assert.AreEqual(left.Card, match.Players[1].LeftHealth.Card);
            Assert.AreEqual(right.Card, match.Players[1].RightHealth.Card);
        }

        [Test]
        public void Attack_BelowShieldDealsZero()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard shield = match.Players[1].ShieldCard;
            Stack(deck, C3);

            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(0, result.Damage);
            Assert.AreEqual(24, match.Players[1].Health);
            Assert.AreEqual(shield, match.Players[1].ShieldCard);
            Assert.AreEqual(1, match.CurrentPlayerIndex, "A failed attack still resolves and passes the turn.");
        }

        [Test]
        public void Attack_SuccessfulDamageInterruptsTheTargetsCharge()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 1,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, K, J, C5), Seat(deck, 2, C6, C7, C8));
            List<PlayingCard> draws = Stack(deck, C9, C10, Q);

            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Charge(2));
            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(7, result.Damage);
            Assert.IsTrue(result.TargetChargeInterrupted);
            Assert.IsFalse(match.Players[1].HasCharge);
            CollectionAssert.Contains(deck.DiscardPile, draws[0], "The interrupted Charge card is discarded.");
            Assert.IsTrue(match.Players[2].HasCharge, "Bystanders keep their Charge.");
        }

        [Test]
        public void Attack_ZeroDamageDoesNotInterruptTheTargetsCharge()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 1,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, K, J, C5), Seat(deck, 2, C6, C7, C8));
            List<PlayingCard> draws = Stack(deck, C9, C10, C3);

            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Charge(2));
            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(0, result.Damage);
            Assert.IsFalse(result.TargetChargeInterrupted);
            Assert.IsTrue(match.Players[1].HasCharge);
            Assert.AreEqual(draws[0], match.Players[1].ChargeCard);
        }

        [Test]
        public void Attack_PassesTheTurn()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            Stack(deck, Q);

            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(1, match.CurrentPlayerIndex);
            Assert.AreEqual(1, result.NextPlayerIndex);
        }

        // ================================================================ replace shield

        [Test]
        public void ReplaceShield_SelfTargetWorks()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard oldShield = match.Players[0].ShieldCard;
            PlayingCard newShield = Stack(deck, C9)[0];

            TurnActionResult result = Act(match, TurnActionRequest.ReplaceShield(0, 0));

            Assert.AreEqual(newShield, match.Players[0].ShieldCard);
            Assert.AreEqual(9, match.Players[0].ShieldValue);
            CollectionAssert.Contains(deck.DiscardPile, oldShield);
            Assert.AreEqual(oldShield, result.OldShieldCard);
            Assert.AreEqual(newShield, result.NewShieldCard);
            Assert.AreEqual(0, result.TargetIndex);
            Assert.AreEqual(1, match.CurrentPlayerIndex);
        }

        [Test]
        public void ReplaceShield_WeakerCardStillReplacesAnotherPlayersStrongerShield()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, K, J, C10), Seat(deck, 2, C6, C7, C8));
            PlayingCard oldShield = match.Players[1].ShieldCard;
            PlayingCard newShield = Stack(deck, C2)[0];

            TurnActionResult result = Act(match, TurnActionRequest.ReplaceShield(0, 1));

            Assert.AreEqual(newShield, match.Players[1].ShieldCard);
            Assert.AreEqual(2, match.Players[1].ShieldValue);
            Assert.AreEqual(4, match.Players[0].ShieldValue, "The actor's own Shield is untouched.");
            CollectionAssert.Contains(deck.DiscardPile, oldShield);
            CollectionAssert.DoesNotContain(deck.DiscardPile, newShield);
            Assert.AreEqual(1, result.TargetIndex);
            Assert.AreEqual(24, match.Players[1].Health, "Replacing a Shield does not change Health.");
        }

        [Test]
        public void ReplaceShield_DeadOrInvalidTargetIsRejectedBeforeDrawing()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = TableWithPlayerTwoDead(deck);

            AssertRejected(match, TurnActionRequest.ReplaceShield(0, 2), ActionRejection.TargetEliminated);
            AssertRejected(match, TurnActionRequest.ReplaceShield(0, NoTarget), ActionRejection.TargetRequired);
            AssertRejected(match, TurnActionRequest.ReplaceShield(0, 5), ActionRejection.InvalidTarget);
        }

        [Test]
        public void ReplaceShield_LeavesAStoredChargeAlone()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            List<PlayingCard> draws = Stack(deck, C9, C10, C10, Q, J);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.ReplaceShield(1, 1));
            Act(match, TurnActionRequest.ReplaceShield(2, 2));
            Act(match, TurnActionRequest.ReplaceShield(0, 0));

            Assert.IsTrue(match.Players[0].HasCharge);
            Assert.AreEqual(draws[0], match.Players[0].ChargeCard);
            Assert.AreEqual(draws[3], match.Players[0].ShieldCard);

            // Replacing a charged opponent's Shield leaves their Charge alone too.
            Act(match, TurnActionRequest.ReplaceShield(1, 0));
            Assert.AreEqual(draws[0], match.Players[0].ChargeCard);
            Assert.AreEqual(draws[4], match.Players[0].ShieldCard);
        }

        // ================================================================ charge

        [Test]
        public void FirstCharge_NeedsNoTargetDrawsOneCardAndStoresIt()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard chargeCard = Stack(deck, C9)[0];
            int drawBefore = deck.DrawCount;

            TurnActionResult result = Act(match, TurnActionRequest.Charge(0));

            Assert.IsTrue(result.StoredHiddenCharge);
            Assert.IsFalse(result.IsChargedAttack);
            Assert.AreEqual(NoTarget, result.TargetIndex);
            Assert.AreEqual(drawBefore - 1, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
            Assert.IsTrue(match.Players[0].HasCharge);
            Assert.AreEqual(chargeCard, match.Players[0].ChargeCard);
            Assert.AreEqual(1, match.CurrentPlayerIndex);
        }

        [Test]
        public void FirstCharge_IgnoresATargetItDoesNotNeed()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);

            TurnActionResult result = Act(match, TurnActionRequest.Charge(0, 1));

            Assert.IsTrue(result.StoredHiddenCharge);
            Assert.AreEqual(NoTarget, result.TargetIndex);
            Assert.AreEqual(24, match.Players[1].Health);
        }

        [Test]
        public void FirstCharge_ResultAndPublicStateNeverExposeTheHiddenCard()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard chargeCard = Stack(deck, C9)[0];

            TurnActionResult result = Act(match, TurnActionRequest.Charge(0));

            Assert.AreEqual(0, result.RevealedCards.Count);
            Assert.AreEqual(0, result.AttackPower);
            AssertNoPublicPropertyExposes(result, chargeCard);
            AssertNoPublicPropertyExposes(match.Players[0], chargeCard);
            StringAssert.DoesNotContain("Nine", result.ToString());
            StringAssert.DoesNotContain("Nine", match.DescribeState());
        }

        static void AssertNoPublicPropertyExposes(object target, PlayingCard hidden)
        {
            foreach (PropertyInfo property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = property.GetValue(target);

                if (value is PlayingCard card)
                    Assert.AreNotEqual(hidden, card, "{0}.{1} leaks the hidden Charge card.", target.GetType().Name, property.Name);

                if (value is HealthSlot slot)
                    Assert.AreNotEqual(hidden, slot.Card, "{0}.{1} leaks the hidden Charge card.", target.GetType().Name, property.Name);

                if (value is IEnumerable<PlayingCard> cards)
                    CollectionAssert.DoesNotContain(cards, hidden, "{0}.{1} leaks the hidden Charge card.", target.GetType().Name, property.Name);
            }
        }

        [Test]
        public void WhileCharged_NormalAttackKeepsTheActorsCharge()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            List<PlayingCard> draws = Stack(deck, C9, C10, C10, Q);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.ReplaceShield(1, 1));
            Act(match, TurnActionRequest.ReplaceShield(2, 2));
            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            Assert.IsFalse(result.IsChargedAttack);
            Assert.AreEqual(12, result.AttackPower);
            Assert.IsTrue(match.Players[0].HasCharge);
            Assert.AreEqual(draws[0], match.Players[0].ChargeCard);
        }

        [Test]
        public void SecondCharge_NeedsAnOpponentAndDrawsNothingUntilItHasOne()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard first = Stack(deck, C9)[0];

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.ReplaceShield(1, 1));
            Act(match, TurnActionRequest.ReplaceShield(2, 2));

            AssertRejected(match, TurnActionRequest.Charge(0), ActionRejection.TargetRequired);
            AssertRejected(match, TurnActionRequest.Charge(0, 0), ActionRejection.SelfTargetNotAllowed);
            AssertRejected(match, TurnActionRequest.Charge(0, 8), ActionRejection.InvalidTarget);

            Assert.AreEqual(first, match.Players[0].ChargeCard, "A rejected completion keeps the stored Charge.");
        }

        [Test]
        public void SecondCharge_DeadTargetIsRejectedBeforeDrawing()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = TableWithPlayerTwoDead(deck);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.ReplaceShield(1, 1));

            AssertRejected(match, TurnActionRequest.Charge(0, 2), ActionRejection.TargetEliminated);
            Assert.IsTrue(match.Players[0].HasCharge);
        }

        [Test]
        public void SecondCharge_CombinesBothCardsAppliesTheShieldOnceAndDiscardsBoth()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            // P0 charges 8, P1 charges 2, P2 charges 3, P0 completes with a Queen.
            List<PlayingCard> draws = Stack(deck, C8, C2, C3, Q);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Charge(2));

            int drawBefore = deck.DrawCount;
            TurnActionResult result = Act(match, TurnActionRequest.Charge(0, 1));

            Assert.IsTrue(result.IsChargedAttack);
            Assert.IsFalse(result.StoredHiddenCharge);
            Assert.AreEqual(1, result.TargetIndex);
            CollectionAssert.AreEqual(new[] { draws[0], draws[3] }, result.RevealedCards);
            Assert.AreEqual(20, result.AttackPower, "8 + Queen 12.");
            Assert.AreEqual(5, result.TargetShieldValue);
            Assert.AreEqual(15, result.Damage, "The Shield applies once to the combined power.");
            Assert.AreEqual(9, match.Players[1].Health);

            Assert.IsFalse(match.Players[0].HasCharge, "The stored Charge clears.");
            CollectionAssert.Contains(deck.DiscardPile, draws[0]);
            CollectionAssert.Contains(deck.DiscardPile, draws[3]);

            Assert.IsTrue(result.TargetChargeInterrupted);
            Assert.IsFalse(match.Players[1].HasCharge);
            CollectionAssert.Contains(deck.DiscardPile, draws[1]);

            // One card drawn for the attack, one physical 9 taken for the Health.
            Assert.AreEqual(drawBefore - 2, deck.DrawCount);
            Assert.AreEqual(1, match.CurrentPlayerIndex);
        }

        [Test]
        public void SecondCharge_EqualToShieldDealsZeroStillDiscardsBothAndKeepsTheTargetsCharge()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, K, J, C10), Seat(deck, 2, C6, C7, C8));
            List<PlayingCard> draws = Stack(deck, C4, C2, C3, C6);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Charge(2));
            TurnActionResult result = Act(match, TurnActionRequest.Charge(0, 1));

            Assert.AreEqual(10, result.AttackPower);
            Assert.AreEqual(0, result.Damage);
            Assert.AreEqual(24, match.Players[1].Health);
            Assert.IsFalse(match.Players[0].HasCharge);
            CollectionAssert.Contains(deck.DiscardPile, draws[0]);
            CollectionAssert.Contains(deck.DiscardPile, draws[3]);

            Assert.IsFalse(result.TargetChargeInterrupted);
            Assert.AreEqual(draws[1], match.Players[1].ChargeCard);
        }

        // ================================================================ health

        [Test]
        public void Health_KingJackTakingEightKeepsJackAndTakesAFiveFromTheDrawPile()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard king = match.Players[1].LeftHealth.Card;
            PlayingCard jack = match.Players[1].RightHealth.Card;
            PlayingCard attackCard = Stack(deck, K)[0];

            List<PlayingCard> drawOrder = deck.DrawPile.Where(c => c != attackCard).ToList();

            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1));

            MatchPlayerState target = match.Players[1];
            Assert.AreEqual(8, result.Damage);
            Assert.AreEqual(16, target.Health);
            Assert.AreEqual(jack, target.RightHealth.Card, "The lower card, Jack, stays.");
            Assert.IsTrue(target.LeftHealth.IsCard);
            Assert.AreEqual(5, target.LeftHealth.Value);
            CollectionAssert.Contains(deck.DiscardPile, king, "The higher card, King, is discarded.");

            // The 5 came out of the draw pile without disturbing anything else.
            PlayingCard five = target.LeftHealth.Card;
            CollectionAssert.Contains(drawOrder, five);
            CollectionAssert.AreEqual(drawOrder.Where(c => c != five).ToList(), deck.DrawPile.ToList());
            Assert.AreEqual(0, deck.ReshuffleCount);
        }

        [Test]
        public void Health_DiscardPileMatchIsUsedBeforeTheDrawPile()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = StandardTable(deck);
            PlayingCard discardedFive = Take(deck, C5);
            deck.Discard(discardedFive);
            int fivesInDrawPile = deck.DrawPile.Count(c => c.Value == 5);
            Stack(deck, K);

            Act(match, TurnActionRequest.Attack(0, 1));

            Assert.AreEqual(discardedFive, match.Players[1].LeftHealth.Card);
            CollectionAssert.DoesNotContain(deck.DiscardPile, discardedFive);
            Assert.AreEqual(fivesInDrawPile, deck.DrawPile.Count(c => c.Value == 5), "The draw pile was not touched.");
        }

        [Test]
        public void Health_BelowTheLowerCardDiscardsBothAndUsesOnePhysicalCard()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, K, J, A), Seat(deck, 2, C6, C7, C8));
            PlayingCard king = match.Players[1].LeftHealth.Card;
            PlayingCard jack = match.Players[1].RightHealth.Card;
            // P0 charges 8, P1 replaces P2's Shield with 10, P2 charges 3, P0 completes with 8.
            Stack(deck, C8, C10, C3, C8);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.ReplaceShield(1, 2));
            Act(match, TurnActionRequest.Charge(2));
            TurnActionResult result = Act(match, TurnActionRequest.Charge(0, 1));

            MatchPlayerState target = match.Players[1];
            Assert.AreEqual(15, result.Damage, "16 against Shield 1.");
            Assert.AreEqual(9, target.Health);
            CollectionAssert.Contains(deck.DiscardPile, king);
            CollectionAssert.Contains(deck.DiscardPile, jack);
            Assert.IsTrue(target.LeftHealth.IsCard);
            Assert.AreEqual(9, target.LeftHealth.Value);
            Assert.IsTrue(target.RightHealth.IsEmpty);
        }

        [Test]
        public void Health_BelowTheLowerCardUsesAVirtualMarkerWhenNoPhysicalCardExists()
        {
            CardDeck deck = new CardDeck(1);
            // Every 9 is held by players 0 and 2, so no physical 9 can be found.
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C9, C9, C4), Seat(deck, 1, K, J, A), Seat(deck, 2, C9, C9, C8));
            Stack(deck, C8, C10, C3, C8);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.ReplaceShield(1, 2));
            Act(match, TurnActionRequest.Charge(2));
            Act(match, TurnActionRequest.Charge(0, 1));

            MatchPlayerState target = match.Players[1];
            Assert.AreEqual(9, target.Health);
            Assert.IsTrue(target.LeftHealth.IsVirtual);
            Assert.AreEqual(9, target.LeftHealth.Value);
            Assert.AreEqual(PlayingCard.Nothing, target.LeftHealth.Card, "A Virtual marker is not a card.");
            Assert.IsTrue(target.RightHealth.IsEmpty);
            Assert.AreEqual(1, match.WaitingMarkerCount(9));
        }

        [Test]
        public void Health_EqualCardsDiscardTheLeftAndKeepTheRight()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, C10, C10, C2), Seat(deck, 2, C6, C7, C8));
            PlayingCard left = match.Players[1].LeftHealth.Card;
            PlayingCard right = match.Players[1].RightHealth.Card;
            Stack(deck, C7);

            Act(match, TurnActionRequest.Attack(0, 1));

            MatchPlayerState target = match.Players[1];
            Assert.AreEqual(15, target.Health);
            Assert.AreEqual(right, target.RightHealth.Card, "The RIGHT 10 stays.");
            CollectionAssert.Contains(deck.DiscardPile, left, "The LEFT 10 is discarded.");
            Assert.AreEqual(5, target.LeftHealth.Value);
        }

        [Test]
        public void Health_EqualToTheLowerCardKeepsItAlone()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, C10, C3, C2), Seat(deck, 2, C6, C7, C8));
            PlayingCard ten = match.Players[1].LeftHealth.Card;
            PlayingCard three = match.Players[1].RightHealth.Card;
            Stack(deck, Q);

            Act(match, TurnActionRequest.Attack(0, 1));

            MatchPlayerState target = match.Players[1];
            Assert.AreEqual(3, target.Health);
            Assert.AreEqual(three, target.RightHealth.Card);
            Assert.IsTrue(target.LeftHealth.IsEmpty);
            CollectionAssert.Contains(deck.DiscardPile, ten);
        }

        /// <summary>
        /// Four seats where every 5 is held (P0 5+5 Shield 5, P3 Shield 5) and P1, P2 are
        /// King + Jack with Shield Ace, so a 9 leaves them on Jack + Virtual 5.
        /// </summary>
        static ShieldsMatch NoFivesTable(CardDeck deck)
        {
            return Start(deck, 0,
                Seat(deck, 0, C5, C5, C5), Seat(deck, 1, K, J, A),
                Seat(deck, 2, K, J, A), Seat(deck, 3, C6, C7, C5));
        }

        [Test]
        public void Health_PhysicalCardIsKeptOverAVirtualMarker()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = NoFivesTable(deck);
            PlayingCard jack = match.Players[1].RightHealth.Card;
            Stack(deck, C9, C3, C3, C3, C4);

            Act(match, TurnActionRequest.Attack(0, 1));
            Assert.IsTrue(match.Players[1].LeftHealth.IsVirtual);
            Assert.AreEqual(1, match.WaitingMarkerCount(5));

            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Charge(2));
            Act(match, TurnActionRequest.Charge(3));
            Act(match, TurnActionRequest.Attack(0, 1)); // 4 vs Shield 1: 3 damage, 16 -> 13

            MatchPlayerState target = match.Players[1];
            Assert.AreEqual(13, target.Health);
            Assert.AreEqual(jack, target.RightHealth.Card, "The physical Jack is kept over the Virtual 5.");
            Assert.IsTrue(target.LeftHealth.IsCard);
            Assert.AreEqual(2, target.LeftHealth.Value);
            Assert.AreEqual(0, match.WaitingMarkerCount(5), "The dropped marker leaves its queue.");
        }

        [Test]
        public void Health_PhysicalCardTooHighWithVirtualMarkerFallsBackToOneValue()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = NoFivesTable(deck);
            PlayingCard jack = match.Players[1].RightHealth.Card;
            Stack(deck, C9, C3, C3, C3, C8);

            Act(match, TurnActionRequest.Attack(0, 1));
            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Charge(2));
            Act(match, TurnActionRequest.Charge(3));
            Act(match, TurnActionRequest.Attack(0, 1)); // 8 vs Shield 1: 7 damage, 16 -> 9

            MatchPlayerState target = match.Players[1];
            Assert.AreEqual(9, target.Health);
            CollectionAssert.Contains(deck.DiscardPile, jack);
            Assert.IsTrue(target.LeftHealth.IsCard);
            Assert.AreEqual(9, target.LeftHealth.Value);
            Assert.IsTrue(target.RightHealth.IsEmpty);
            Assert.AreEqual(0, match.WaitingMarkerCount(5));
        }

        // ================================================================ virtual FIFO

        [Test]
        public void VirtualFifo_EarliestWaitingMarkerReceivesTheFirstDiscardedCard()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = NoFivesTable(deck);
            PlayingCard p3OldShield = match.Players[3].ShieldCard;
            PlayingCard p0OldShield = match.Players[0].ShieldCard;
            Stack(deck, C9, C9, C3, Q, C10);

            Act(match, TurnActionRequest.Attack(0, 1)); // P1 waits for a 5
            Act(match, TurnActionRequest.Attack(1, 2)); // then P2 waits for a 5
            Assert.AreEqual(2, match.WaitingMarkerCount(5));
            Assert.IsTrue(match.Players[1].LeftHealth.IsVirtual);
            Assert.IsTrue(match.Players[2].LeftHealth.IsVirtual);

            Act(match, TurnActionRequest.Charge(2));
            Act(match, TurnActionRequest.ReplaceShield(3, 3)); // discards a real 5

            Assert.AreEqual(p3OldShield, match.Players[1].LeftHealth.Card, "The first waiter gets the first 5.");
            Assert.IsTrue(match.Players[2].LeftHealth.IsVirtual, "The second waiter keeps waiting.");
            CollectionAssert.DoesNotContain(deck.DiscardPile, p3OldShield);
            Assert.AreEqual(1, match.WaitingMarkerCount(5));

            Act(match, TurnActionRequest.ReplaceShield(0, 0)); // discards the next real 5

            Assert.AreEqual(p0OldShield, match.Players[2].LeftHealth.Card);
            CollectionAssert.DoesNotContain(deck.DiscardPile, p0OldShield);
            Assert.AreEqual(0, match.WaitingMarkerCount(5));
            Assert.AreEqual(16, match.Players[1].Health);
            Assert.AreEqual(16, match.Players[2].Health);
        }

        [Test]
        public void VirtualFifo_EliminatedPlayersMarkerLeavesTheQueue()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C5, C5, C5), Seat(deck, 1, K, J, A), Seat(deck, 2, Q, C4, C5));
            PlayingCard p2OldShield = match.Players[2].ShieldCard;
            Stack(deck, K, C2, C9, K, C10);

            Act(match, TurnActionRequest.Charge(0));
            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Attack(2, 1)); // 8 damage: P1 on Jack + Virtual 5
            Assert.AreEqual(1, match.WaitingMarkerCount(5));

            TurnActionResult kill = Act(match, TurnActionRequest.Charge(0, 1)); // 26 vs Shield 1
            Assert.IsTrue(kill.TargetEliminated);
            Assert.AreEqual(0, match.WaitingMarkerCount(5));
            Assert.AreEqual(2, match.CurrentPlayerIndex, "The dead player is skipped.");

            Act(match, TurnActionRequest.ReplaceShield(2, 2)); // a real 5 is discarded
            CollectionAssert.Contains(deck.DiscardPile, p2OldShield, "Nobody is waiting any more.");
        }

        // ================================================================ death and winner

        [Test]
        public void Death_EliminatesThePlayerAndReleasesEveryCardIncludingCharge()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 1,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, C5, C3, A), Seat(deck, 2, C6, C7, C8));
            MatchPlayerState target = match.Players[1];
            PlayingCard[] owned = { target.LeftHealth.Card, target.RightHealth.Card, target.ShieldCard };
            List<PlayingCard> draws = Stack(deck, C9, C10, K);

            Act(match, TurnActionRequest.Charge(1));
            Act(match, TurnActionRequest.Charge(2));
            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 1)); // 12 damage vs 8 Health

            Assert.IsTrue(result.TargetEliminated);
            Assert.IsFalse(target.IsAlive);
            Assert.LessOrEqual(target.Health, 0);
            Assert.AreEqual(result.TargetHealthAfter, target.Health);
            Assert.IsTrue(target.LeftHealth.IsEmpty);
            Assert.IsTrue(target.RightHealth.IsEmpty);
            Assert.IsFalse(target.ShieldCard.IsValid);
            Assert.IsFalse(target.HasCharge);

            foreach (PlayingCard card in owned) CollectionAssert.Contains(deck.DiscardPile, card);
            CollectionAssert.Contains(deck.DiscardPile, draws[0], "The stored Charge is released.");
            CollectionAssert.Contains(deck.DiscardPile, draws[2], "The Attack card is discarded.");

            Assert.AreEqual(2, match.LivingPlayerCount);
            Assert.IsFalse(match.IsMatchOver);
            Assert.AreEqual(2, match.CurrentPlayerIndex, "Turn skips the dead player.");
        }

        [Test]
        public void Death_ReleasedCardsServeOtherPlayersWaitingMarkers()
        {
            CardDeck deck = new CardDeck(1);
            // Every 5 is held: three by P0, one as P1's left Health.
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C5, C5, C5), Seat(deck, 1, C5, C3, A), Seat(deck, 2, K, J, A));
            PlayingCard dyingFive = match.Players[1].LeftHealth.Card;
            List<PlayingCard> draws = Stack(deck, C9, C4, K);

            Act(match, TurnActionRequest.Attack(0, 2)); // P2 on Jack + Virtual 5
            Assert.IsTrue(match.Players[2].LeftHealth.IsVirtual);

            Act(match, TurnActionRequest.Charge(1));
            TurnActionResult result = Act(match, TurnActionRequest.Attack(2, 1)); // P1 dies

            Assert.IsTrue(result.TargetEliminated);
            Assert.AreEqual(dyingFive, match.Players[2].LeftHealth.Card);
            CollectionAssert.DoesNotContain(deck.DiscardPile, dyingFive);
            CollectionAssert.Contains(deck.DiscardPile, draws[1], "The dead player's Charge is released.");
            Assert.AreEqual(0, match.WaitingMarkerCount(5));
        }

        [Test]
        public void Death_LastLivingPlayerWinsAndNothingElseResolves()
        {
            CardDeck deck = new CardDeck(1);
            ShieldsMatch match = Start(deck, 0,
                Seat(deck, 0, C2, C3, C4), Seat(deck, 1, C2, A, A), Seat(deck, 2, C3, A, A));
            Stack(deck, K, C6, Q);

            Act(match, TurnActionRequest.Attack(0, 1));
            Assert.IsFalse(match.IsMatchOver);
            Assert.AreEqual(2, match.CurrentPlayerIndex);

            Act(match, TurnActionRequest.Charge(2));
            TurnActionResult result = Act(match, TurnActionRequest.Attack(0, 2));

            Assert.IsTrue(result.TargetEliminated);
            Assert.IsTrue(result.MatchOver);
            Assert.AreEqual(0, result.WinnerIndex);
            Assert.IsTrue(match.IsMatchOver);
            Assert.AreEqual(0, match.WinnerIndex);
            Assert.AreEqual(1, match.LivingPlayerCount);
            Assert.AreEqual(ShieldsMatch.NoPlayer, match.CurrentPlayerIndex);

            AssertRejected(match, TurnActionRequest.Charge(0), ActionRejection.MatchOver);
            AssertRejected(match, TurnActionRequest.Attack(0, 1), ActionRejection.MatchOver);
            AssertRejected(match, TurnActionRequest.ReplaceShield(0, 0), ActionRejection.MatchOver);
        }

        // ================================================================ whole match

        /// <summary>
        /// Plays complete matches from a real seeded setup with a fixed, simple policy and
        /// checks card accounting and Health representation after every action.
        /// </summary>
        [Test]
        public void FullMatch_PlaysToOneWinnerWithEveryCardAccountedFor(
            [Values(3, 5, 8)] int players, [Values(1, 2)] int decks, [Values(7, 99)] int seed)
        {
            ShieldsMatch match = new ShieldsMatch(CompletedSetup(players, decks, seed));
            AssertInvariants(match);

            for (int turn = 0; turn < 5000 && !match.IsMatchOver; turn++)
            {
                int actor = match.CurrentPlayerIndex;
                int target = actor;
                do target = (target + 1) % players; while (!match.Players[target].IsAlive);

                TurnActionRequest request;
                if (match.Players[actor].HasCharge) request = TurnActionRequest.Charge(actor, target);
                else if (turn % 3 == 0) request = TurnActionRequest.ReplaceShield(actor, target);
                else if (turn % 3 == 1) request = TurnActionRequest.Charge(actor);
                else request = TurnActionRequest.Attack(actor, target);

                Act(match, request);
            }

            Assert.IsTrue(match.IsMatchOver);
            Assert.AreEqual(1, match.LivingPlayerCount);
            Assert.IsTrue(match.Players[match.WinnerIndex].IsAlive);
        }
    }
}
