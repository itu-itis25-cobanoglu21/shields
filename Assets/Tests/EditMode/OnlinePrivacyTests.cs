using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shields.Cards;
using Shields.Game;
using Shields.Online;

namespace Shields.Game.Tests
{
    /// <summary>
    /// The hidden Charge card must never reach a client, not even the client that owns it
    /// (SHIELDS_RULES.md section 3 and 11, TASK 009 section 12).
    ///
    /// These tests guard the network boundary specifically: the public snapshot, the
    /// public action result, and the shape of the DTO types themselves.
    /// </summary>
    public class OnlinePrivacyTests
    {
        // ================================================================ helpers

        static PlayingCard Take(CardDeck deck, CardRank rank)
        {
            PlayingCard card;
            Assert.IsTrue(deck.TryTakeFromDrawPile((int)rank, out card), "No {0} left to arrange.", rank);
            return card;
        }

        static PlayerSetupState Seat(CardDeck deck, int index, bool isBot, CardRank left, CardRank right, CardRank shield)
        {
            List<PlayingCard> cards = new List<PlayingCard> { Take(deck, left), Take(deck, right), Take(deck, shield) };
            PlayerSetupState seat = new PlayerSetupState(index, isBot, cards);
            seat.SelectShieldAt(2);
            return seat;
        }

        /// <summary>Arranges the next draws. drawOrder[0] is drawn first.</summary>
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

        /// <summary>A three seat table where seat 0 is human and the rest are bots.</summary>
        static ShieldsMatch Table(CardDeck deck)
        {
            return new ShieldsMatch(deck, new[]
            {
                Seat(deck, 0, false, CardRank.King, CardRank.Jack, CardRank.Five),
                Seat(deck, 1, true, CardRank.Ten, CardRank.Nine, CardRank.Four),
                Seat(deck, 2, true, CardRank.Eight, CardRank.Seven, CardRank.Three)
            }, 0, TurnDirection.Clockwise);
        }

        static PublicMatchSnapshot Snapshot(ShieldsMatch match, uint revision = 1)
        {
            return PublicStateBuilder.BuildMatchSnapshot(match, revision, OnlineMatchPhase.Playing);
        }

        /// <summary>Every card id mentioned anywhere in a snapshot.</summary>
        static List<int> CardIdsIn(PublicMatchSnapshot snapshot)
        {
            List<int> ids = new List<int>();

            for (int i = 0; i < snapshot.PlayerCount; i++)
            {
                PublicPlayerState player = snapshot.Players[i];

                if (player.ShieldCard.IsValid) ids.Add(player.ShieldCard.CardId);
                if (player.LeftHealth.IsCard) ids.Add(player.LeftHealth.Card.CardId);
                if (player.RightHealth.IsCard) ids.Add(player.RightHealth.Card.CardId);
            }

            return ids;
        }

        static List<int> CardIdsIn(PublicActionResult result)
        {
            List<int> ids = new List<int>();

            if (result.RevealedCards != null)
            {
                for (int i = 0; i < result.RevealedCards.Length; i++)
                    if (result.RevealedCards[i].IsValid) ids.Add(result.RevealedCards[i].CardId);
            }

            if (result.OldShieldCard.IsValid) ids.Add(result.OldShieldCard.CardId);
            if (result.NewShieldCard.IsValid) ids.Add(result.NewShieldCard.CardId);

            return ids;
        }

        // ================================================================ structural

        [Test]
        public void PublicPlayerState_HasNoFieldThatCouldHoldAChargeCard()
        {
            // Structural guarantee: if no field can hold it, no future change can leak it.
            FieldInfo[] fields = typeof(PublicPlayerState).GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                if (!field.Name.ToLowerInvariant().Contains("charge")) continue;

                Assert.That(field.FieldType, Is.EqualTo(typeof(bool)),
                    "Only a bool may describe a Charge. Field {0} is {1}.", field.Name, field.FieldType);
            }
        }

        [Test]
        public void PublicPlayerState_ExposesOnlyWhetherAChargeExists()
        {
            Assert.That(typeof(PublicPlayerState).GetField("HasCharge").FieldType, Is.EqualTo(typeof(bool)));

            // No PublicCard typed member anywhere refers to a Charge.
            List<FieldInfo> cardFields = typeof(PublicPlayerState)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(PublicCard))
                .ToList();

            Assert.That(cardFields.Count, Is.GreaterThan(0), "The Shield card should still be public.");

            foreach (FieldInfo field in cardFields)
                Assert.That(field.Name.ToLowerInvariant().Contains("charge"), Is.False,
                    "Field {0} carries a card and mentions Charge.", field.Name);
        }

        // ================================================================ snapshots

        [Test]
        public void Snapshot_WithStoredCharge_SaysOnlyThatOneExists()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck);

            PlayingCard hidden = Stack(deck, CardRank.Queen)[0];
            match.Submit(TurnActionRequest.Charge(0));

            PublicMatchSnapshot snapshot = Snapshot(match);

            Assert.That(snapshot.GetPlayer(0).HasCharge, Is.True, "The public state must say a Charge is stored.");
            Assert.That(CardIdsIn(snapshot), Has.No.Member(hidden.CardId),
                "The stored Charge card's identity appeared in the public snapshot.");
        }

        [Test]
        public void Snapshot_NeverCarriesTheChargeValueAsAnyNumber()
        {
            CardDeck deck = new CardDeck(1, 11);
            ShieldsMatch match = Table(deck);

            // A Queen is 12; no Health, Shield or marker in this table is 12.
            PlayingCard hidden = Stack(deck, CardRank.Queen)[0];
            match.Submit(TurnActionRequest.Charge(0));

            PublicPlayerState charger = Snapshot(match).GetPlayer(0);

            Assert.That(charger.ShieldValue, Is.Not.EqualTo(hidden.Value));
            Assert.That(charger.LeftHealth.Value, Is.Not.EqualTo(hidden.Value));
            Assert.That(charger.RightHealth.Value, Is.Not.EqualTo(hidden.Value));
        }

        [Test]
        public void Snapshot_ChargeOwnerLearnsNoMoreThanAnyoneElse()
        {
            CardDeck deck = new CardDeck(1, 3);
            ShieldsMatch match = Table(deck);

            Stack(deck, CardRank.Seven);
            match.Submit(TurnActionRequest.Charge(0));

            // The snapshot is built once and sent to everyone, so the owner cannot be
            // told anything extra: there is only one projection.
            PublicMatchSnapshot snapshot = Snapshot(match);
            PublicPlayerState asOwnerSees = snapshot.GetPlayer(0);
            PublicPlayerState asOthersSee = Snapshot(match, 2).GetPlayer(0);

            Assert.That(asOwnerSees.HasCharge, Is.EqualTo(asOthersSee.HasCharge));
            Assert.That(asOwnerSees.ShieldCard.CardId, Is.EqualTo(asOthersSee.ShieldCard.CardId));
        }

        // ================================================================ action results

        [Test]
        public void FirstCharge_PublicResultRevealsNoCardAtAll()
        {
            CardDeck deck = new CardDeck(1, 5);
            ShieldsMatch match = Table(deck);

            PlayingCard hidden = Stack(deck, CardRank.King)[0];
            TurnActionResult engineResult = match.Submit(TurnActionRequest.Charge(0));

            PublicActionResult published = PublicStateBuilder.BuildActionResult(engineResult);

            Assert.That(published.StoredHiddenCharge, Is.True);
            Assert.That(published.RevealedCardCount, Is.Zero, "A first Charge must reveal nothing.");
            Assert.That(CardIdsIn(published), Has.No.Member(hidden.CardId));
            Assert.That(published.AttackPower, Is.Zero);
            Assert.That(published.Damage, Is.Zero);
        }

        [Test]
        public void FirstCharge_PublicResultTextNeverNamesTheCard()
        {
            CardDeck deck = new CardDeck(1, 9);
            ShieldsMatch match = Table(deck);

            PlayingCard hidden = Stack(deck, CardRank.Queen)[0];
            PublicActionResult published = PublicStateBuilder.BuildActionResult(
                match.Submit(TurnActionRequest.Charge(0)));

            string text = published.ToString();

            Assert.That(text, Does.Not.Contain("Queen"));
            Assert.That(text, Does.Not.Contain(hidden.CardId.ToString()));
        }

        [Test]
        public void ChargedAttack_RevealsBothCardsOnceItResolves()
        {
            CardDeck deck = new CardDeck(1, 13);
            ShieldsMatch match = Table(deck);

            // Seat 0 charges, the bots take a turn each, then seat 0 completes the Charge.
            PlayingCard first = Stack(deck, CardRank.Eight)[0];
            match.Submit(TurnActionRequest.Charge(0));

            Stack(deck, CardRank.Two);
            match.Submit(TurnActionRequest.Attack(1, 2));
            Stack(deck, CardRank.Two);
            match.Submit(TurnActionRequest.Attack(2, 1));

            PlayingCard second = Stack(deck, CardRank.Queen)[0];
            TurnActionResult engineResult = match.Submit(TurnActionRequest.Charge(0, 1));

            PublicActionResult published = PublicStateBuilder.BuildActionResult(engineResult);

            Assert.That(published.IsChargedAttack, Is.True);
            Assert.That(published.StoredHiddenCharge, Is.False);
            Assert.That(published.RevealedCardCount, Is.EqualTo(2), "A resolved Charged Attack reveals both cards.");

            List<int> revealed = CardIdsIn(published);
            Assert.That(revealed, Has.Member(first.CardId), "The stored Charge card is public once it resolves.");
            Assert.That(revealed, Has.Member(second.CardId));
            Assert.That(published.AttackPower, Is.EqualTo(first.Value + second.Value));
        }

        [Test]
        public void ChargedAttack_ClearsHasChargeInTheNextSnapshot()
        {
            CardDeck deck = new CardDeck(1, 21);
            ShieldsMatch match = Table(deck);

            Stack(deck, CardRank.Eight);
            match.Submit(TurnActionRequest.Charge(0));
            Assert.That(Snapshot(match).GetPlayer(0).HasCharge, Is.True);

            Stack(deck, CardRank.Two);
            match.Submit(TurnActionRequest.Attack(1, 2));
            Stack(deck, CardRank.Two);
            match.Submit(TurnActionRequest.Attack(2, 1));

            Stack(deck, CardRank.Queen);
            match.Submit(TurnActionRequest.Charge(0, 1));

            Assert.That(Snapshot(match, 2).GetPlayer(0).HasCharge, Is.False);
        }

        [Test]
        public void InterruptedCharge_StillNeverRevealsTheLostCard()
        {
            CardDeck deck = new CardDeck(1, 33);
            ShieldsMatch match = Table(deck);

            // Seat 0 stores a Charge, then seat 1 hits it hard enough to break it.
            PlayingCard hidden = Stack(deck, CardRank.Nine)[0];
            match.Submit(TurnActionRequest.Charge(0));

            Stack(deck, CardRank.King);
            TurnActionResult attack = match.Submit(TurnActionRequest.Attack(1, 0));

            PublicActionResult published = PublicStateBuilder.BuildActionResult(attack);

            Assert.That(published.TargetChargeInterrupted, Is.True, "The Charge should have been broken.");
            Assert.That(CardIdsIn(published), Has.No.Member(hidden.CardId),
                "A Charge destroyed by damage is discarded without ever becoming public.");
            Assert.That(CardIdsIn(Snapshot(match, 3)), Has.No.Member(hidden.CardId));
        }

        // ================================================================ setup

        [Test]
        public void SetupSnapshot_ShowsAllThreeDealtCardsBecauseTheyArePublic()
        {
            MatchSetup setup = new MatchSetup(new MatchConfig(3, 1, 0, 99), new[] { false, true, true });

            PublicSetupSnapshot snapshot = PublicStateBuilder.BuildSetupSnapshot(setup, 1);

            for (int seat = 0; seat < snapshot.PlayerCount; seat++)
            {
                PublicSetupPlayer player = snapshot.GetPlayer(seat);
                Assert.That(player.DealtCards.Length, Is.EqualTo(3), "Seat {0} should show three dealt cards.", seat);

                for (int i = 0; i < 3; i++)
                    Assert.That(player.DealtCards[i].IsValid, Is.True);
            }
        }

        [Test]
        public void SetupSnapshot_CarriesNoChargeConceptAtAll()
        {
            // No Charge exists during setup, so the setup DTO has no notion of one.
            FieldInfo[] fields = typeof(PublicSetupPlayer).GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
                Assert.That(field.Name.ToLowerInvariant(), Does.Not.Contain("charge"));
        }
    }
}
