using System;
using Shields.Cards;

namespace Shields.Game
{
    /// <summary>
    /// One player's authoritative state during turn play. Only <see cref="ShieldsMatch"/>
    /// changes it. Everything here is public information apart from the identity of a
    /// stored Charge card, which only <see cref="HasCharge"/> reveals publicly.
    /// </summary>
    public sealed class MatchPlayerState
    {
        internal MatchPlayerState(PlayerSetupState setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (!setup.IsSelectionComplete)
                throw new ArgumentException("A player must finish choosing their Shield first.", nameof(setup));

            PlayerIndex = setup.PlayerIndex;
            IsBot = setup.IsBot;
            IsAlive = true;
            Health = setup.StartingHealth;

            // Same order the table uses: HealthCards[0] sits in the left slot.
            LeftHealth = HealthSlot.FromCard(setup.HealthCards[0]);
            RightHealth = HealthSlot.FromCard(setup.HealthCards[1]);
            ShieldCard = setup.ShieldCard;
            ChargeCard = PlayingCard.Nothing;
        }

        public int PlayerIndex { get; }

        public bool IsBot { get; }

        public bool IsAlive { get; internal set; }

        /// <summary>Authoritative numeric Health. The Health slots only represent it.</summary>
        public int Health { get; internal set; }

        public HealthSlot LeftHealth { get; internal set; }

        public HealthSlot RightHealth { get; internal set; }

        /// <summary>The Shield card, or <see cref="PlayingCard.Nothing"/> once eliminated.</summary>
        public PlayingCard ShieldCard { get; internal set; }

        /// <summary>Shield strength, the Shield card's value.</summary>
        public int ShieldValue { get { return ShieldCard.IsValid ? ShieldCard.Value : 0; } }

        /// <summary>True while the player has one face-down Charge card stored.</summary>
        public bool HasCharge { get { return ChargeCard.IsValid; } }

        /// <summary>
        /// The hidden Charge card. Engine internal on purpose: nobody, including the
        /// owner, may know it until a Charged Attack reveals it.
        /// </summary>
        internal PlayingCard ChargeCard { get; set; }

        public HealthSlot GetHealthSlot(HealthSlotSide side)
        {
            return side == HealthSlotSide.Left ? LeftHealth : RightHealth;
        }

        internal void SetHealthSlot(HealthSlotSide side, HealthSlot slot)
        {
            if (side == HealthSlotSide.Left) LeftHealth = slot;
            else RightHealth = slot;
        }

        public override string ToString()
        {
            if (!IsAlive) return string.Format("Player {0}: eliminated", PlayerIndex);

            return string.Format("Player {0}: Health {1} [{2} | {3}], Shield {4}{5}",
                PlayerIndex, Health, LeftHealth, RightHealth, ShieldValue, HasCharge ? ", charged" : "");
        }
    }
}
