using Unity.Netcode;

namespace Shields.Online
{
    /// <summary>
    /// One seat during the initial deal and Shield choice.
    ///
    /// Every card here is public by the rules: all three dealt cards are visible to
    /// everyone while the choice is made, and the resulting Health and Shield cards stay
    /// visible afterwards. No Charge exists yet at this stage, so there is nothing hidden
    /// to protect.
    /// </summary>
    public struct PublicSetupPlayer : INetworkSerializable
    {
        public byte PlayerIndex;
        public bool IsBot;

        /// <summary>The three dealt cards, in the order they were dealt.</summary>
        public PublicCard[] DealtCards;

        public bool SelectionComplete;

        /// <summary>The chosen Shield, or nothing before the choice.</summary>
        public PublicCard ShieldCard;

        /// <summary>The two Health cards, empty before the choice.</summary>
        public PublicCard[] HealthCards;

        public short StartingHealth;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerIndex);
            serializer.SerializeValue(ref IsBot);
            serializer.SerializeValue(ref SelectionComplete);
            serializer.SerializeValue(ref ShieldCard);
            serializer.SerializeValue(ref StartingHealth);

            SerializeCards(serializer, ref DealtCards);
            SerializeCards(serializer, ref HealthCards);
        }

        internal static void SerializeCards<T>(BufferSerializer<T> serializer, ref PublicCard[] cards)
            where T : IReaderWriter
        {
            int count = cards != null ? cards.Length : 0;
            serializer.SerializeValue(ref count);

            if (serializer.IsReader) cards = new PublicCard[count];

            for (int i = 0; i < count; i++) serializer.SerializeValue(ref cards[i]);
        }

        public override string ToString()
        {
            return "Player " + PlayerIndex + (IsBot ? " (bot)" : " (human)") +
                (SelectionComplete ? ": Health " + StartingHealth + ", Shield " + ShieldCard.Value : ": choosing");
        }
    }

    /// <summary>
    /// The public initial setup of every seat. Replicated instead of the host's
    /// <see cref="Shields.Game.MatchSetup"/>, which owns the only real deck.
    /// </summary>
    public struct PublicSetupSnapshot : INetworkSerializable
    {
        /// <summary>Which match this belongs to; revisions restart for every generation.</summary>
        public int Generation;
        public uint Revision;
        public bool AllSelectionsComplete;
        public PublicSetupPlayer[] Players;

        public int PlayerCount { get { return Players != null ? Players.Length : 0; } }

        public bool IsValid { get { return Revision != PublicMatchSnapshot.NoRevision; } }

        public PublicSetupPlayer GetPlayer(int playerIndex)
        {
            if (Players == null || playerIndex < 0 || playerIndex >= Players.Length) return default;

            return Players[playerIndex];
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Generation);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref AllSelectionsComplete);

            int count = Players != null ? Players.Length : 0;
            serializer.SerializeValue(ref count);

            if (serializer.IsReader) Players = new PublicSetupPlayer[count];

            for (int i = 0; i < count; i++) serializer.SerializeValue(ref Players[i]);
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("setup gen ").Append(Generation).Append(" rev ").Append(Revision).Append(AllSelectionsComplete ? " (complete)" : " (choosing)");

            for (int i = 0; i < PlayerCount; i++) sb.Append("\n  ").Append(Players[i]);

            return sb.ToString();
        }
    }
}
