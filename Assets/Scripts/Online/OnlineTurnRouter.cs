using Shields.Game;
using Shields.Session;

namespace Shields.Online
{
    /// <summary>What happened to a routed request, for the host and for the sender.</summary>
    public struct RoutedAction
    {
        /// <summary>The seat the host decided the sender owns, or -1.</summary>
        public int ActorIndex;

        public ActionRejection Rejection;

        /// <summary>The authoritative result, or null when nothing reached the engine.</summary>
        public TurnActionResult Result;

        public bool Accepted { get { return Result != null && Result.Accepted; } }

        /// <summary>True when the request was refused before the engine ever saw it.</summary>
        public bool RejectedBeforeEngine { get { return Result == null; } }
    }

    /// <summary>
    /// Turns a network request into at most one authoritative
    /// <see cref="ShieldsMatch.Submit"/>.
    ///
    /// The actor is never taken from the payload. It is derived from the sender's NGO
    /// client id through the authoritative <see cref="SeatRegistry"/>, so a client can
    /// only ever act as the seat the host gave it, and cannot act for a bot seat or for
    /// another human however the message is crafted.
    ///
    /// The engine is still the final word: this class refuses what it can name cheaply
    /// (wrong sender, wrong turn, dead match) and lets <see cref="ShieldsMatch"/> decide
    /// everything about the action itself, including targets. A refusal at either level
    /// draws no card and changes no state.
    ///
    /// Plain C# with no NetworkBehaviour, so the whole authority path is unit testable
    /// without a Relay connection.
    /// </summary>
    public sealed class OnlineTurnRouter
    {
        readonly SeatRegistry _seats;
        readonly ShieldsMatch _match;

        public OnlineTurnRouter(SeatRegistry seats, ShieldsMatch match)
        {
            _seats = seats;
            _match = match;
        }

        /// <summary>
        /// How many times this router has reached <see cref="ShieldsMatch.Submit"/>.
        /// Exists so tests can prove one request produces exactly one submit, including
        /// on the host, where the same process is both client and server.
        /// </summary>
        public int SubmitCount { get; private set; }

        public ShieldsMatch Match { get { return _match; } }

        /// <summary>
        /// Routes a request that arrived from a connected participant.
        /// <paramref name="senderClientId"/> is the authenticated NGO sender, never a
        /// value the payload chose.
        /// </summary>
        public RoutedAction SubmitFromParticipant(ulong senderClientId, TurnActionType actionType, int targetIndex)
        {
            int seat = _seats != null ? _seats.SeatOf(senderClientId) : SeatRegistry.NoSeat;

            // The sender holds no seat in this match.
            if (seat == SeatRegistry.NoSeat)
                return Refuse(SeatRegistry.NoSeat, ActionRejection.InvalidPlayer);

            return SubmitForSeat(seat, actionType, targetIndex, false);
        }

        /// <summary>
        /// Routes a decision the host made for one of its own bots. Bots never travel over
        /// the network; only the host runs them, and they land in the same engine.
        /// </summary>
        public RoutedAction SubmitBot(TurnActionRequest request)
        {
            return SubmitForSeat(request.PlayerIndex, request.ActionType, request.TargetIndex, true);
        }

        // ------------------------------------------------------------------ internals

        RoutedAction SubmitForSeat(int seat, TurnActionType actionType, int targetIndex, bool asBot)
        {
            if (_match == null) return Refuse(seat, ActionRejection.MatchOver);
            if (_match.IsMatchOver) return Refuse(seat, ActionRejection.MatchOver);

            if (seat < 0 || seat >= _match.PlayerCount) return Refuse(seat, ActionRejection.InvalidPlayer);

            MatchPlayerState player = _match.Players[seat];

            // A participant may only drive a human seat, and the host may only drive a
            // bot seat. Neither side can borrow the other's.
            if (player.IsBot != asBot) return Refuse(seat, ActionRejection.InvalidPlayer);

            if (!player.IsAlive) return Refuse(seat, ActionRejection.PlayerEliminated);

            // Ordinary stale request: the turn moved on before the message landed.
            if (_match.CurrentPlayerIndex != seat) return Refuse(seat, ActionRejection.NotYourTurn);

            TurnActionRequest request = new TurnActionRequest(seat, actionType, targetIndex);

            SubmitCount++;
            TurnActionResult result = _match.Submit(request);

            return new RoutedAction
            {
                ActorIndex = seat,
                Rejection = result.Rejection,
                Result = result
            };
        }

        static RoutedAction Refuse(int seat, ActionRejection rejection)
        {
            return new RoutedAction { ActorIndex = seat, Rejection = rejection, Result = null };
        }

        /// <summary>A short reason to show the sender. Never reveals hidden state.</summary>
        public static string DescribeRejection(ActionRejection rejection)
        {
            switch (rejection)
            {
                case ActionRejection.None: return string.Empty;
                case ActionRejection.MatchOver: return "The match is not active.";
                case ActionRejection.NotYourTurn: return "Not your turn.";
                case ActionRejection.InvalidPlayer: return "That seat is not yours.";
                case ActionRejection.PlayerEliminated: return "You are eliminated.";
                case ActionRejection.TargetRequired: return "That action needs a target.";
                case ActionRejection.InvalidTarget: return "Invalid target.";
                case ActionRejection.TargetEliminated: return "That player is already out.";
                case ActionRejection.SelfTargetNotAllowed: return "You cannot attack yourself.";
                case ActionRejection.DeckExhausted: return "No cards left to draw.";
                default: return "Action refused.";
            }
        }
    }
}
