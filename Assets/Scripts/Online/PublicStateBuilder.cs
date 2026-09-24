using System.Collections.Generic;
using Shields.Cards;
using Shields.Game;

namespace Shields.Online
{
    /// <summary>
    /// The one place authoritative host state becomes public network state.
    ///
    /// Everything crossing from the engine to the wire goes through here, so the privacy
    /// rules are enforced in a single readable place instead of being spread across every
    /// RPC. Nothing in this class can read a stored Charge card even if it wanted to:
    /// <see cref="MatchPlayerState.ChargeCard"/> is internal to Shields.Game, and this
    /// assembly is not a friend of it.
    ///
    /// Pure C#: no Unity objects, no NetworkBehaviour, so the projection is fully testable.
    /// </summary>
    public static class PublicStateBuilder
    {
        // ------------------------------------------------------------------ match

        /// <summary>Projects the authoritative match into a public snapshot.</summary>
        public static PublicMatchSnapshot BuildMatchSnapshot(ShieldsMatch match, uint revision, OnlineMatchPhase phase)
        {
            PublicMatchSnapshot snapshot = new PublicMatchSnapshot
            {
                Revision = revision,
                PhaseCode = (byte)phase,
                CurrentPlayerIndex = -1,
                DirectionCode = 0,
                MatchOver = false,
                WinnerIndex = -1,
                RemainingDrawCount = 0,
                Players = new PublicPlayerState[0]
            };

            if (match == null) return snapshot;

            snapshot.CurrentPlayerIndex = (sbyte)match.CurrentPlayerIndex;
            snapshot.DirectionCode = (byte)match.Direction;
            snapshot.MatchOver = match.IsMatchOver;
            snapshot.WinnerIndex = (sbyte)match.WinnerIndex;
            snapshot.RemainingDrawCount = (short)match.Deck.DrawCount;

            snapshot.Players = new PublicPlayerState[match.PlayerCount];
            for (int i = 0; i < match.PlayerCount; i++)
                snapshot.Players[i] = BuildPlayer(match.Players[i]);

            return snapshot;
        }

        /// <summary>
        /// Projects one player. Note what is absent: the Charge card itself. Only
        /// <see cref="MatchPlayerState.HasCharge"/> is read, and
        /// <see cref="PublicPlayerState"/> has nowhere to put the card even if it were
        /// reachable from here.
        /// </summary>
        public static PublicPlayerState BuildPlayer(MatchPlayerState player)
        {
            if (player == null) return default;

            return new PublicPlayerState
            {
                PlayerIndex = (byte)player.PlayerIndex,
                IsAlive = player.IsAlive,
                IsBot = player.IsBot,
                Health = (short)player.Health,
                LeftHealth = PublicHealthSlot.From(player.LeftHealth),
                RightHealth = PublicHealthSlot.From(player.RightHealth),
                ShieldCard = PublicCard.From(player.ShieldCard),
                HasCharge = player.HasCharge
            };
        }

        // ------------------------------------------------------------------ setup

        /// <summary>Projects the initial deal and Shield choices. Every card here is public.</summary>
        public static PublicSetupSnapshot BuildSetupSnapshot(MatchSetup setup, uint revision)
        {
            PublicSetupSnapshot snapshot = new PublicSetupSnapshot
            {
                Revision = revision,
                AllSelectionsComplete = false,
                Players = new PublicSetupPlayer[0]
            };

            if (setup == null) return snapshot;

            snapshot.AllSelectionsComplete = setup.InitialSetupComplete;
            snapshot.Players = new PublicSetupPlayer[setup.Players.Count];

            for (int i = 0; i < setup.Players.Count; i++)
                snapshot.Players[i] = BuildSetupPlayer(setup.Players[i]);

            return snapshot;
        }

        public static PublicSetupPlayer BuildSetupPlayer(PlayerSetupState player)
        {
            if (player == null) return default;

            return new PublicSetupPlayer
            {
                PlayerIndex = (byte)player.PlayerIndex,
                IsBot = player.IsBot,
                DealtCards = PublicCard.From(player.DealtCards),
                SelectionComplete = player.IsSelectionComplete,
                ShieldCard = PublicCard.From(player.ShieldCard),
                HealthCards = PublicCard.From(player.HealthCards),
                StartingHealth = (short)player.StartingHealth
            };
        }

        // ------------------------------------------------------------------ results

        /// <summary>
        /// Projects a resolved action.
        ///
        /// <see cref="TurnActionResult.RevealedCards"/> is already empty for a first
        /// Charge and already holds both cards once a Charged Attack resolves, so copying
        /// it verbatim preserves the rule rather than reimplementing it. The assertion
        /// that a stored Charge reveals nothing is kept explicit below so it cannot
        /// regress silently.
        /// </summary>
        public static PublicActionResult BuildActionResult(TurnActionResult result)
        {
            if (result == null) return default;

            PublicCard[] revealed;
            if (result.StoredHiddenCharge)
            {
                // A stored Charge is unknown to everyone, including its owner. Nothing
                // about the card travels, whatever the engine happened to put in the list.
                revealed = new PublicCard[0];
            }
            else
            {
                revealed = PublicCard.From(result.RevealedCards);
            }

            return new PublicActionResult
            {
                Accepted = result.Accepted,
                RejectionCode = (byte)result.Rejection,
                ActorIndex = (byte)result.ActorIndex,
                ActionTypeCode = (byte)result.ActionType,
                TargetIndex = (sbyte)result.TargetIndex,
                StoredHiddenCharge = result.StoredHiddenCharge,
                IsChargedAttack = result.IsChargedAttack,
                RevealedCards = revealed,
                AttackPower = (short)result.AttackPower,
                TargetShieldValue = (short)result.TargetShieldValue,
                Damage = (short)result.Damage,
                TargetHealthBefore = (short)result.TargetHealthBefore,
                TargetHealthAfter = (short)result.TargetHealthAfter,
                TargetChargeInterrupted = result.TargetChargeInterrupted,
                OldShieldCard = PublicCard.From(result.OldShieldCard),
                NewShieldCard = PublicCard.From(result.NewShieldCard),
                TargetEliminated = result.TargetEliminated,
                MatchOver = result.MatchOver,
                WinnerIndex = (sbyte)result.WinnerIndex,
                NextPlayerIndex = (sbyte)result.NextPlayerIndex
            };
        }

        /// <summary>A rejection carrying no state, for telling one client its request failed.</summary>
        public static PublicActionResult BuildRejection(int actorIndex, TurnActionType actionType,
            int targetIndex, ActionRejection rejection)
        {
            return new PublicActionResult
            {
                Accepted = false,
                RejectionCode = (byte)rejection,
                ActorIndex = (byte)(actorIndex < 0 ? 0 : actorIndex),
                ActionTypeCode = (byte)actionType,
                TargetIndex = (sbyte)targetIndex,
                RevealedCards = new PublicCard[0],
                OldShieldCard = PublicCard.None,
                NewShieldCard = PublicCard.None,
                WinnerIndex = -1,
                NextPlayerIndex = -1
            };
        }

        // ------------------------------------------------------------------ occupancy

        /// <summary>
        /// Turns the authoritative seat map into the per seat bot flags a
        /// <see cref="MatchSetup"/> needs: every seat a connected human holds stays human,
        /// and every remaining seat up to the target count becomes a bot.
        /// </summary>
        public static bool[] BuildSeatOccupancy(Shields.Session.SeatRegistry seats, int targetPlayerCount)
        {
            bool[] isBot = new bool[targetPlayerCount];

            for (int seat = 0; seat < targetPlayerCount; seat++)
            {
                bool human = seats != null && seat < seats.SeatCount && seats.OccupantAt(seat).IsHuman;
                isBot[seat] = !human;
            }

            return isBot;
        }

        /// <summary>Same as above, from an already replicated occupancy list.</summary>
        public static bool[] BuildSeatOccupancy(IReadOnlyList<bool> humanSeats, int targetPlayerCount)
        {
            bool[] isBot = new bool[targetPlayerCount];

            for (int seat = 0; seat < targetPlayerCount; seat++)
                isBot[seat] = humanSeats == null || seat >= humanSeats.Count || !humanSeats[seat];

            return isBot;
        }
    }
}
