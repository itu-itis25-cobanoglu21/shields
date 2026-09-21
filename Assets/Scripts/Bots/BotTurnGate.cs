using Shields.Game;

namespace Shields.Bots
{
    /// <summary>Who controls the seat whose turn it is.</summary>
    public enum SeatControl
    {
        None = 0,
        LocalHuman = 1,
        Bot = 2
    }

    /// <summary>
    /// Decides when a bot may act. It classifies the current seat from runtime match
    /// state only (the local seat index and <see cref="MatchPlayerState.IsBot"/>), never
    /// from GameObjects or components, and holds at most one pending bot action.
    ///
    /// A pending action is bound to one player and one turn. It is dropped the moment the
    /// turn changes or the seat is not a bot, so a delayed action can never fire for a
    /// different turn, a different bot, or the local human. Pure logic; the caller owns
    /// time and the actual submit.
    /// </summary>
    public sealed class BotTurnGate
    {
        int _player = ShieldsMatch.NoPlayer;
        int _turnId = -1;
        double _dueAt;

        /// <summary>True while one bot action is waiting for its time.</summary>
        public bool IsScheduled { get { return _player != ShieldsMatch.NoPlayer; } }

        /// <summary>The bot the pending action belongs to, or NoPlayer.</summary>
        public int ScheduledPlayer { get { return _player; } }

        public static SeatControl ControlOf(ShieldsMatch match, int localPlayerIndex)
        {
            if (match == null || match.IsMatchOver) return SeatControl.None;

            int current = match.CurrentPlayerIndex;
            if (current < 0 || current >= match.PlayerCount) return SeatControl.None;

            // The local seat is human whatever else is true of it.
            if (current == localPlayerIndex) return SeatControl.LocalHuman;

            return match.Players[current].IsBot ? SeatControl.Bot : SeatControl.None;
        }

        /// <summary>
        /// Call every frame. Schedules the current bot once per turn, due at
        /// <paramref name="dueAtIfScheduling"/>, and returns true exactly once when it is
        /// due, with the bot re-verified as the current authoritative player. Returns false
        /// and drops anything pending when the current seat is not a bot.
        /// </summary>
        public bool TryTakeDueBot(ShieldsMatch match, int localPlayerIndex, int turnId, double now,
            double dueAtIfScheduling, out int botIndex)
        {
            botIndex = ShieldsMatch.NoPlayer;

            if (ControlOf(match, localPlayerIndex) != SeatControl.Bot)
            {
                Cancel();
                return false;
            }

            int current = match.CurrentPlayerIndex;

            if (_player != current || _turnId != turnId)
            {
                _player = current;
                _turnId = turnId;
                _dueAt = dueAtIfScheduling;
                return false;
            }

            if (now < _dueAt) return false;

            botIndex = current;
            Cancel();
            return true;
        }

        public void Cancel()
        {
            _player = ShieldsMatch.NoPlayer;
            _turnId = -1;
        }
    }
}
