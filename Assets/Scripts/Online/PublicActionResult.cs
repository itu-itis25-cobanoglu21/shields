using Unity.Netcode;
using Shields.Game;

namespace Shields.Online
{
    /// <summary>
    /// A resolved turn action, as every client is allowed to see it.
    ///
    /// The privacy rule lives in how <see cref="RevealedCards"/> is filled, and it is the
    /// same rule the engine already follows: a first Charge stores a card nobody may
    /// know, so it reveals nothing and <see cref="StoredHiddenCharge"/> is the only thing
    /// said about it. A Charged Attack resolves both cards, and only then do they become
    /// public and travel here.
    ///
    /// This mirrors <see cref="TurnActionResult"/>, which was already written to carry
    /// public information only. Copying it field by field rather than serializing it keeps
    /// that guarantee explicit at the network boundary.
    /// </summary>
    public struct PublicActionResult : INetworkSerializable
    {
        public bool Accepted;
        public byte RejectionCode;

        public byte ActorIndex;
        public byte ActionTypeCode;
        public sbyte TargetIndex;

        /// <summary>True for a first Charge: a card was stored face down, and that is all.</summary>
        public bool StoredHiddenCharge;

        public bool IsChargedAttack;

        /// <summary>
        /// Cards this action turned face up: the Attack card, both Charge cards of a
        /// Charged Attack, or the new Shield card. Always empty for a first Charge.
        /// </summary>
        public PublicCard[] RevealedCards;

        public short AttackPower;
        public short TargetShieldValue;
        public short Damage;
        public short TargetHealthBefore;
        public short TargetHealthAfter;
        public bool TargetChargeInterrupted;

        public PublicCard OldShieldCard;
        public PublicCard NewShieldCard;

        public bool TargetEliminated;
        public bool MatchOver;
        public sbyte WinnerIndex;
        public sbyte NextPlayerIndex;

        public ActionRejection Rejection { get { return (ActionRejection)RejectionCode; } }

        public TurnActionType ActionType { get { return (TurnActionType)ActionTypeCode; } }

        public int RevealedCardCount { get { return RevealedCards != null ? RevealedCards.Length : 0; } }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Accepted);
            serializer.SerializeValue(ref RejectionCode);
            serializer.SerializeValue(ref ActorIndex);
            serializer.SerializeValue(ref ActionTypeCode);
            serializer.SerializeValue(ref TargetIndex);
            serializer.SerializeValue(ref StoredHiddenCharge);
            serializer.SerializeValue(ref IsChargedAttack);
            serializer.SerializeValue(ref AttackPower);
            serializer.SerializeValue(ref TargetShieldValue);
            serializer.SerializeValue(ref Damage);
            serializer.SerializeValue(ref TargetHealthBefore);
            serializer.SerializeValue(ref TargetHealthAfter);
            serializer.SerializeValue(ref TargetChargeInterrupted);
            serializer.SerializeValue(ref OldShieldCard);
            serializer.SerializeValue(ref NewShieldCard);
            serializer.SerializeValue(ref TargetEliminated);
            serializer.SerializeValue(ref MatchOver);
            serializer.SerializeValue(ref WinnerIndex);
            serializer.SerializeValue(ref NextPlayerIndex);

            PublicSetupPlayer.SerializeCards(serializer, ref RevealedCards);
        }

        public override string ToString()
        {
            if (!Accepted) return "Player " + ActorIndex + " " + ActionType + " rejected: " + Rejection;

            if (StoredHiddenCharge) return "Player " + ActorIndex + " stored a hidden Charge card";

            if (ActionType == TurnActionType.ReplaceShield)
            {
                return "Player " + ActorIndex + " replaced player " + TargetIndex + "'s Shield " +
                    OldShieldCard.Value + " -> " + NewShieldCard.Value;
            }

            return "Player " + ActorIndex + (IsChargedAttack ? " charge-attacked player " : " attacked player ") +
                TargetIndex + ": power " + AttackPower + " vs Shield " + TargetShieldValue + ", " + Damage +
                " damage, Health " + TargetHealthBefore + " -> " + TargetHealthAfter +
                (TargetChargeInterrupted ? ", Charge interrupted" : "") +
                (TargetEliminated ? ", eliminated" : "");
        }
    }
}
