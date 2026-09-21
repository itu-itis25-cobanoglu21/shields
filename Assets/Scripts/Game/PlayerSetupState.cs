using System;
using System.Collections.Generic;
using Shields.Cards;

namespace Shields.Game
{
    /// <summary>
    /// One player's authoritative state during initial setup: the three cards they were
    /// dealt, which one they chose as their Shield, and the numeric starting Health that
    /// follows from the other two.
    ///
    /// This is the source of truth. The cards on the 3D table only represent it, and no
    /// value here is ever read back from a GameObject.
    /// </summary>
    public sealed class PlayerSetupState
    {
        /// <summary>Every player is dealt exactly three cards at the start of a match.</summary>
        public const int DealtCardCount = 3;

        /// <summary>Two of the three dealt cards become Health.</summary>
        public const int HealthCardCount = 2;

        readonly PlayingCard[] _dealtCards;
        readonly List<PlayingCard> _healthCards = new List<PlayingCard>(HealthCardCount);

        public PlayerSetupState(int playerIndex, bool isBot, IReadOnlyList<PlayingCard> dealtCards)
        {
            if (playerIndex < 0) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            if (dealtCards == null) throw new ArgumentNullException(nameof(dealtCards));

            if (dealtCards.Count != DealtCardCount)
            {
                throw new ArgumentException(
                    string.Format("A player is dealt exactly {0} cards, got {1}.", DealtCardCount, dealtCards.Count),
                    nameof(dealtCards));
            }

            _dealtCards = new PlayingCard[DealtCardCount];
            for (int i = 0; i < DealtCardCount; i++)
            {
                if (!dealtCards[i].IsValid)
                    throw new ArgumentException("A dealt card must be a real card.", nameof(dealtCards));

                _dealtCards[i] = dealtCards[i];
            }

            PlayerIndex = playerIndex;
            IsBot = isBot;
            ShieldCard = PlayingCard.Nothing;
        }

        public int PlayerIndex { get; }

        public bool IsBot { get; }

        /// <summary>The three cards this player was dealt, in the order they were dealt.</summary>
        public IReadOnlyList<PlayingCard> DealtCards { get { return _dealtCards; } }

        /// <summary>The chosen Shield card, or <see cref="PlayingCard.Nothing"/> before choosing.</summary>
        public PlayingCard ShieldCard { get; private set; }

        /// <summary>Shield strength is the Shield card's value. Zero before choosing.</summary>
        public int ShieldStrength { get { return ShieldCard.IsValid ? ShieldCard.Value : 0; } }

        /// <summary>The two cards left over once the Shield is chosen. Empty before choosing.</summary>
        public IReadOnlyList<PlayingCard> HealthCards { get { return _healthCards; } }

        /// <summary>
        /// Authoritative numeric Health, the sum of the two Health card values. Zero
        /// before choosing. Later damage changes this number, not the cards.
        /// </summary>
        public int StartingHealth { get; private set; }

        /// <summary>True once this player has chosen their Shield.</summary>
        public bool IsSelectionComplete { get; private set; }

        /// <summary>
        /// Chooses one of the three dealt cards as the Shield. The other two become
        /// Health and set the starting Health. Choosing is a one time setup step.
        /// </summary>
        public void SelectShield(PlayingCard card)
        {
            SelectShieldAt(IndexOfDealtCard(card));
        }

        /// <summary>Chooses the Shield by its position in <see cref="DealtCards"/>.</summary>
        public void SelectShieldAt(int dealtIndex)
        {
            if (IsSelectionComplete)
                throw new InvalidOperationException("This player has already chosen their Shield.");

            if (dealtIndex < 0 || dealtIndex >= DealtCardCount)
            {
                throw new ArgumentOutOfRangeException(nameof(dealtIndex), dealtIndex,
                    "The Shield must be one of the three dealt cards.");
            }

            ShieldCard = _dealtCards[dealtIndex];

            _healthCards.Clear();
            for (int i = 0; i < DealtCardCount; i++)
                if (i != dealtIndex) _healthCards.Add(_dealtCards[i]);

            StartingHealth = 0;
            for (int i = 0; i < _healthCards.Count; i++)
                StartingHealth += _healthCards[i].Value;

            IsSelectionComplete = true;
        }

        /// <summary>Position of a dealt card, or -1 when this player was not dealt it.</summary>
        public int IndexOfDealtCard(PlayingCard card)
        {
            for (int i = 0; i < DealtCardCount; i++)
                if (_dealtCards[i] == card) return i;

            return -1;
        }

        /// <summary>True when the given card is one of this player's three dealt cards.</summary>
        public bool WasDealt(PlayingCard card)
        {
            return IndexOfDealtCard(card) >= 0;
        }

        public override string ToString()
        {
            if (!IsSelectionComplete)
                return string.Format("Player {0} ({1}): choosing", PlayerIndex, IsBot ? "bot" : "local");

            return string.Format("Player {0} ({1}): Health {2} = {3} + {4}, Shield {5}",
                PlayerIndex, IsBot ? "bot" : "local", StartingHealth,
                _healthCards[0].Value, _healthCards[1].Value, ShieldStrength);
        }
    }
}
