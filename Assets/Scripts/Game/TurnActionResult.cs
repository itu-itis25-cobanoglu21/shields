using System.Collections.Generic;
using Shields.Cards;

namespace Shields.Game
{
    /// <summary>
    /// What happened when a request was submitted. Meant for presentation, bots, logs and
    /// later replication, so it only ever carries public information: a first Charge
    /// reports that a hidden card was stored, never which card it is.
    /// </summary>
    public sealed class TurnActionResult
    {
        static readonly PlayingCard[] NoCards = new PlayingCard[0];

        internal TurnActionResult(TurnActionRequest request)
        {
            Request = request;
            TargetIndex = TurnActionRequest.NoTarget;
            RevealedCards = NoCards;
            OldShieldCard = PlayingCard.Nothing;
            NewShieldCard = PlayingCard.Nothing;
            WinnerIndex = ShieldsMatch.NoPlayer;
            NextPlayerIndex = ShieldsMatch.NoPlayer;
        }

        /// <summary>The request exactly as submitted.</summary>
        public TurnActionRequest Request { get; }

        public bool Accepted { get { return Rejection == ActionRejection.None; } }

        public ActionRejection Rejection { get; internal set; }

        public int ActorIndex { get { return Request.PlayerIndex; } }

        public TurnActionType ActionType { get { return Request.ActionType; } }

        /// <summary>The resolved target, or NoTarget for a first Charge.</summary>
        public int TargetIndex { get; internal set; }

        /// <summary>True for a first Charge: one hidden card was stored face-down.</summary>
        public bool StoredHiddenCharge { get; internal set; }

        /// <summary>True when this Charge completed a Charged Attack.</summary>
        public bool IsChargedAttack { get; internal set; }

        /// <summary>
        /// Cards turned face up by this action: the Attack card, both Charge cards of a
        /// Charged Attack, or the new Shield card. Empty for a first Charge.
        /// </summary>
        public IReadOnlyList<PlayingCard> RevealedCards { get; internal set; }

        public int AttackPower { get; internal set; }

        public int TargetShieldValue { get; internal set; }

        public int Damage { get; internal set; }

        public int TargetHealthBefore { get; internal set; }

        public int TargetHealthAfter { get; internal set; }

        /// <summary>True when the target lost their stored Charge to this attack.</summary>
        public bool TargetChargeInterrupted { get; internal set; }

        public PlayingCard OldShieldCard { get; internal set; }

        public PlayingCard NewShieldCard { get; internal set; }

        public bool TargetEliminated { get; internal set; }

        public bool MatchOver { get; internal set; }

        public int WinnerIndex { get; internal set; }

        /// <summary>Whose turn it is after this action, or NoPlayer once the match is over.</summary>
        public int NextPlayerIndex { get; internal set; }

        public override string ToString()
        {
            if (!Accepted) return string.Format("{0} rejected: {1}", Request, Rejection);

            if (StoredHiddenCharge) return string.Format("Player {0} stored a hidden Charge card", ActorIndex);

            if (ActionType == TurnActionType.ReplaceShield)
            {
                return string.Format("Player {0} replaced player {1}'s Shield {2} -> {3}",
                    ActorIndex, TargetIndex, OldShieldCard.Value, NewShieldCard.Value);
            }

            return string.Format("Player {0} {1} player {2}: power {3} vs Shield {4}, {5} damage, Health {6} -> {7}{8}{9}",
                ActorIndex, IsChargedAttack ? "charge-attacked" : "attacked", TargetIndex,
                AttackPower, TargetShieldValue, Damage, TargetHealthBefore, TargetHealthAfter,
                TargetChargeInterrupted ? ", Charge interrupted" : "",
                TargetEliminated ? ", eliminated" : "");
        }
    }
}
