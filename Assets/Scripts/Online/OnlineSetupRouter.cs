using Shields.Cards;
using Shields.Game;
using Shields.Session;

namespace Shields.Online
{
    /// <summary>Why an initial Shield choice was refused.</summary>
    public enum SetupRejection
    {
        None = 0,

        /// <summary>The sender holds no seat in this match.</summary>
        NoSeat,

        /// <summary>Setup is not the active phase.</summary>
        SetupNotActive,

        /// <summary>That seat is played by a bot; only the host chooses for bots.</summary>
        BotSeat,

        /// <summary>That seat has already chosen its Shield.</summary>
        AlreadyChosen,

        /// <summary>The card named was not one of that seat's own three dealt cards.</summary>
        CardNotDealtToYou
    }

    /// <summary>
    /// Validates initial Shield choices arriving from participants.
    ///
    /// A client click never touches <see cref="MatchSetup"/>. It names a card, the host
    /// works out which seat the sender owns, and only then does the host make the choice.
    /// The seat comes from the authoritative <see cref="SeatRegistry"/>, so one human can
    /// never choose another human's Shield, and naming a card dealt to somebody else is
    /// refused rather than silently retargeted.
    ///
    /// A refused request leaves setup state untouched.
    /// </summary>
    public sealed class OnlineSetupRouter
    {
        readonly SeatRegistry _seats;
        readonly MatchSetup _setup;

        public OnlineSetupRouter(SeatRegistry seats, MatchSetup setup)
        {
            _seats = seats;
            _setup = setup;
        }

        /// <summary>How many choices this router applied. Tests use it to prove idempotence.</summary>
        public int AppliedCount { get; private set; }

        public MatchSetup Setup { get { return _setup; } }

        /// <summary>
        /// Applies a participant's Shield choice, identified by the card id they clicked.
        /// The seat is derived from the sender, never from the payload.
        /// </summary>
        public SetupRejection SelectShieldFromParticipant(ulong senderClientId, int cardId, out int seat)
        {
            seat = _seats != null ? _seats.SeatOf(senderClientId) : SeatRegistry.NoSeat;

            if (seat == SeatRegistry.NoSeat) return SetupRejection.NoSeat;

            return SelectShieldForSeat(seat, cardId, false);
        }

        /// <summary>
        /// Makes every bot choose, on the host only. Uses the existing confirmed random
        /// behaviour in <see cref="MatchSetup.RunBotSelections"/>; no client ever runs it.
        /// </summary>
        public void RunBotSelections()
        {
            if (_setup == null) return;

            int before = CompletedCount();
            _setup.RunBotSelections();
            AppliedCount += CompletedCount() - before;
        }

        // ------------------------------------------------------------------ internals

        SetupRejection SelectShieldForSeat(int seat, int cardId, bool asBot)
        {
            if (_setup == null) return SetupRejection.SetupNotActive;
            if (seat < 0 || seat >= _setup.Players.Count) return SetupRejection.NoSeat;

            PlayerSetupState player = _setup.GetPlayer(seat);

            // Only the host chooses for bots, and only a participant chooses for a human.
            if (player.IsBot != asBot) return SetupRejection.BotSeat;

            if (player.IsSelectionComplete) return SetupRejection.AlreadyChosen;

            int dealtIndex = IndexOfDealtCardId(player, cardId);

            // Naming a card from somebody else's hand, or no card at all.
            if (dealtIndex < 0) return SetupRejection.CardNotDealtToYou;

            player.SelectShieldAt(dealtIndex);
            AppliedCount++;

            return SetupRejection.None;
        }

        /// <summary>
        /// Finds a dealt card by identity. Card id is used rather than the whole card so a
        /// client cannot describe a card that does not exist and have it accepted.
        /// </summary>
        static int IndexOfDealtCardId(PlayerSetupState player, int cardId)
        {
            if (cardId < 0) return -1;

            for (int i = 0; i < player.DealtCards.Count; i++)
                if (player.DealtCards[i].CardId == cardId) return i;

            return -1;
        }

        int CompletedCount()
        {
            int count = 0;
            for (int i = 0; i < _setup.Players.Count; i++)
                if (_setup.Players[i].IsSelectionComplete) count++;

            return count;
        }

        public static string DescribeRejection(SetupRejection rejection)
        {
            switch (rejection)
            {
                case SetupRejection.None: return string.Empty;
                case SetupRejection.NoSeat: return "You have no seat in this match.";
                case SetupRejection.SetupNotActive: return "Setup is not active.";
                case SetupRejection.BotSeat: return "That seat is played by a bot.";
                case SetupRejection.AlreadyChosen: return "You have already chosen your Shield.";
                case SetupRejection.CardNotDealtToYou: return "That card was not dealt to you.";
                default: return "Choice refused.";
            }
        }
    }
}
