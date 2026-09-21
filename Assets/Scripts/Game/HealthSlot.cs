using Shields.Cards;

namespace Shields.Game
{
    /// <summary>What a Health slot currently shows.</summary>
    public enum HealthSlotKind
    {
        Empty = 0,
        Card = 1,
        Virtual = 2
    }

    /// <summary>The two logical Health slots in front of a player.</summary>
    public enum HealthSlotSide
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// One Health slot: empty, a real <see cref="PlayingCard"/>, or a Virtual Health
    /// Marker. A Virtual Health Marker is only a number. It has no suit and no card id,
    /// and it never enters the draw pile or the discard pile.
    /// </summary>
    public readonly struct HealthSlot
    {
        readonly int _virtualValue;

        HealthSlot(HealthSlotKind kind, PlayingCard card, int virtualValue)
        {
            Kind = kind;
            Card = card;
            _virtualValue = virtualValue;
        }

        public static readonly HealthSlot Empty = default;

        public static HealthSlot FromCard(PlayingCard card)
        {
            return new HealthSlot(HealthSlotKind.Card, card, 0);
        }

        public static HealthSlot VirtualMarker(int value)
        {
            return new HealthSlot(HealthSlotKind.Virtual, PlayingCard.Nothing, value);
        }

        public HealthSlotKind Kind { get; }

        /// <summary>The physical card, or <see cref="PlayingCard.Nothing"/> unless Kind is Card.</summary>
        public PlayingCard Card { get; }

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
                    case HealthSlotKind.Virtual: return _virtualValue;
                    default: return 0;
                }
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case HealthSlotKind.Card: return Card.Rank.ToString();
                case HealthSlotKind.Virtual: return "Virtual " + _virtualValue;
                default: return "-";
            }
        }
    }
}
