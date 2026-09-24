using Unity.Netcode;
using Shields.Game;

namespace Shields.Online
{
    /// <summary>One Health slot as clients see it: nothing, a real card, or a virtual marker.</summary>
    public struct PublicHealthSlot : INetworkSerializable
    {
        public byte KindCode;
        public PublicCard Card;
        public byte VirtualValue;

        public static PublicHealthSlot Empty
        {
            get { return new PublicHealthSlot { KindCode = (byte)HealthSlotKind.Empty, Card = PublicCard.None }; }
        }

        public static PublicHealthSlot From(HealthSlot slot)
        {
            switch (slot.Kind)
            {
                case HealthSlotKind.Card:
                    return new PublicHealthSlot
                    {
                        KindCode = (byte)HealthSlotKind.Card,
                        Card = PublicCard.From(slot.Card),
                        VirtualValue = 0
                    };

                case HealthSlotKind.Virtual:
                    return new PublicHealthSlot
                    {
                        KindCode = (byte)HealthSlotKind.Virtual,
                        Card = PublicCard.None,
                        VirtualValue = (byte)slot.Value
                    };

                default:
                    return Empty;
            }
        }

        public HealthSlotKind Kind { get { return (HealthSlotKind)KindCode; } }

        public bool IsEmpty { get { return Kind == HealthSlotKind.Empty; } }

        public bool IsCard { get { return Kind == HealthSlotKind.Card; } }

        public bool IsVirtual { get { return Kind == HealthSlotKind.Virtual; } }

        /// <summary>The Health this slot represents. Zero when empty.</summary>
        public int Value
        {
            get
            {
                switch (Kind)
                {
                    case HealthSlotKind.Card: return Card.Value;
                    case HealthSlotKind.Virtual: return VirtualValue;
                    default: return 0;
                }
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref KindCode);
            serializer.SerializeValue(ref Card);
            serializer.SerializeValue(ref VirtualValue);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case HealthSlotKind.Card: return Card.Rank.ToString();
                case HealthSlotKind.Virtual: return "Virtual " + VirtualValue;
                default: return "-";
            }
        }
    }

    /// <summary>
    /// One player exactly as every client is allowed to see them.
    ///
    /// There is deliberately no field anywhere in this struct that can hold a stored
    /// Charge card. An unresolved Charge is the one hidden card in Shields, unknown even
    /// to its owner, so the only thing that crosses the wire is
    /// <see cref="HasCharge"/>. Because the identity has nowhere to live in this type, no
    /// future serializer change, debug dump or careless assignment can leak it: the
    /// privacy is structural rather than a rule someone has to remember.
    /// </summary>
    public struct PublicPlayerState : INetworkSerializable
    {
        public byte PlayerIndex;
        public bool IsAlive;
        public bool IsBot;
        public short Health;
        public PublicHealthSlot LeftHealth;
        public PublicHealthSlot RightHealth;
        public PublicCard ShieldCard;

        /// <summary>
        /// That a Charge is stored. Never which card it is: see the type remarks.
        /// </summary>
        public bool HasCharge;

        public int ShieldValue { get { return ShieldCard.IsValid ? ShieldCard.Value : 0; } }

        public PublicHealthSlot GetHealthSlot(HealthSlotSide side)
        {
            return side == HealthSlotSide.Left ? LeftHealth : RightHealth;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerIndex);
            serializer.SerializeValue(ref IsAlive);
            serializer.SerializeValue(ref IsBot);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref LeftHealth);
            serializer.SerializeValue(ref RightHealth);
            serializer.SerializeValue(ref ShieldCard);
            serializer.SerializeValue(ref HasCharge);
        }

        public override string ToString()
        {
            if (!IsAlive) return "Player " + PlayerIndex + ": eliminated";

            return "Player " + PlayerIndex + ": Health " + Health +
                " [" + LeftHealth + " | " + RightHealth + "], Shield " + ShieldValue +
                (HasCharge ? ", charged" : "");
        }
    }
}
