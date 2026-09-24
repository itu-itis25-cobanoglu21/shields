using Unity.Netcode;
using Shields.Cards;

namespace Shields.Online
{
    /// <summary>
    /// A physical card as it travels over the wire, carrying only what a client needs to
    /// identify and draw a card that is already public.
    ///
    /// This is deliberately a separate type from <see cref="PlayingCard"/> rather than a
    /// serializer wrapped around it. A card only ever becomes a <see cref="PublicCard"/>
    /// by an explicit call, so there is no path by which a card the rules keep hidden can
    /// reach a client just because some serializer could reach the field.
    ///
    /// The copy a client holds is presentation data. Authoritative card ownership stays
    /// with the host's <see cref="CardDeck"/> and match state.
    /// </summary>
    public struct PublicCard : INetworkSerializable
    {
        /// <summary>Matches <see cref="PlayingCard.NoCardId"/>.</summary>
        public const int NoCardId = PlayingCard.NoCardId;

        public int CardId;
        public byte DeckIndex;
        public byte SuitCode;
        public byte RankCode;

        /// <summary>Nothing: what an empty Shield slot or an unrevealed card serialises as.</summary>
        public static PublicCard None
        {
            get { return new PublicCard { CardId = NoCardId, DeckIndex = 0, SuitCode = 0, RankCode = 0 }; }
        }

        /// <summary>
        /// Converts a card the rules have already made public. Callers are responsible for
        /// only ever passing a public card; see <see cref="PublicStateBuilder"/>.
        /// </summary>
        public static PublicCard From(PlayingCard card)
        {
            if (!card.IsValid) return None;

            return new PublicCard
            {
                CardId = card.CardId,
                DeckIndex = (byte)card.DeckIndex,
                SuitCode = (byte)card.Suit,
                RankCode = (byte)card.Rank
            };
        }

        public static PublicCard[] From(System.Collections.Generic.IReadOnlyList<PlayingCard> cards)
        {
            if (cards == null || cards.Count == 0) return new PublicCard[0];

            PublicCard[] result = new PublicCard[cards.Count];
            for (int i = 0; i < cards.Count; i++) result[i] = From(cards[i]);

            return result;
        }

        public bool IsValid { get { return CardId >= 0; } }

        public CardSuit Suit { get { return (CardSuit)SuitCode; } }

        public CardRank Rank { get { return (CardRank)RankCode; } }

        /// <summary>Gameplay value, Ace = 1 through King = 13. Zero when there is no card.</summary>
        public int Value { get { return IsValid ? RankCode : 0; } }

        /// <summary>
        /// Rebuilds the equivalent <see cref="PlayingCard"/> for presentation. The result
        /// is a faithful copy for drawing, never an authoritative card the client owns.
        /// </summary>
        public PlayingCard ToPlayingCard()
        {
            return IsValid ? new PlayingCard(CardId, DeckIndex, Suit, Rank) : PlayingCard.Nothing;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CardId);
            serializer.SerializeValue(ref DeckIndex);
            serializer.SerializeValue(ref SuitCode);
            serializer.SerializeValue(ref RankCode);
        }

        public override string ToString()
        {
            return IsValid ? Rank + " of " + Suit + " (id " + CardId + ")" : "NoCard";
        }
    }
}
