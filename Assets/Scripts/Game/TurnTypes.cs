namespace Shields.Game
{
    /// <summary>
    /// Which way turns travel around the table. By rule every match is played clockwise
    /// (SHIELDS_RULES.md section 7). Seat indices increase clockwise when the table is
    /// viewed from above (see <see cref="TableGeometry"/>), so clockwise is index + 1.
    /// Kept as a type because public snapshots and the HUD name the direction.
    /// </summary>
    public enum TurnDirection
    {
        Clockwise = 0
    }

    /// <summary>The only three things a player can do on their turn.</summary>
    public enum TurnActionType
    {
        Attack = 0,
        ReplaceShield = 1,
        Charge = 2
    }

    /// <summary>Why the engine refused a request. None means it resolved.</summary>
    public enum ActionRejection
    {
        None = 0,
        MatchOver,
        InvalidPlayer,
        PlayerEliminated,
        NotYourTurn,
        UnknownAction,
        TargetRequired,
        InvalidTarget,
        TargetEliminated,
        SelfTargetNotAllowed,
        DeckExhausted
    }

    /// <summary>
    /// A committed turn decision: who acts, which action, and the target if one is needed.
    /// It is built before any card is drawn, and the engine draws only after validating
    /// it, so a caller can never see a card and then choose what to do with it.
    /// </summary>
    public readonly struct TurnActionRequest
    {
        /// <summary>The <see cref="TargetIndex"/> of a request that names no target.</summary>
        public const int NoTarget = -1;

        public TurnActionRequest(int playerIndex, TurnActionType actionType, int targetIndex)
        {
            PlayerIndex = playerIndex;
            ActionType = actionType;
            TargetIndex = targetIndex;
        }

        public int PlayerIndex { get; }

        public TurnActionType ActionType { get; }

        /// <summary>The target seat, or <see cref="NoTarget"/>.</summary>
        public int TargetIndex { get; }

        public static TurnActionRequest Attack(int playerIndex, int targetIndex)
        {
            return new TurnActionRequest(playerIndex, TurnActionType.Attack, targetIndex);
        }

        public static TurnActionRequest ReplaceShield(int playerIndex, int targetIndex)
        {
            return new TurnActionRequest(playerIndex, TurnActionType.ReplaceShield, targetIndex);
        }

        /// <summary>
        /// Charge. The target is ignored when the player has no stored Charge yet, and is
        /// required when this Charge completes a Charged Attack.
        /// </summary>
        public static TurnActionRequest Charge(int playerIndex, int targetIndex = NoTarget)
        {
            return new TurnActionRequest(playerIndex, TurnActionType.Charge, targetIndex);
        }

        public override string ToString()
        {
            return TargetIndex == NoTarget
                ? string.Format("Player {0}: {1}", PlayerIndex, ActionType)
                : string.Format("Player {0}: {1} -> player {2}", PlayerIndex, ActionType, TargetIndex);
        }
    }
}
