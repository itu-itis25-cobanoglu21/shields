namespace Shields.Cards
{
    /// <summary>
    /// The four standard suits. Suits carry no gameplay effect in Shields; they exist
    /// so a card is a real playing card and so visuals have something to show.
    /// </summary>
    public enum CardSuit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    /// <summary>
    /// The thirteen standard ranks. The underlying value IS the gameplay value,
    /// Ace = 1 through King = 13, so no lookup table can drift out of sync.
    /// </summary>
    public enum CardRank
    {
        Ace = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13
    }
}
