using System;
using System.Collections.Generic;
using Shields.Game;

namespace Shields.Bots
{
    /// <summary>
    /// The temporary prototype bot: picks Attack, Replace Shield or Charge with equal
    /// chance, then a random legal target. It is intentionally not strategic and reads
    /// only public match state (who is alive, who holds a Charge).
    ///
    /// It lives in its own assembly so the engine's internal hidden Charge card is not
    /// even visible to it. It only builds a request; the caller submits it to the same
    /// <see cref="ShieldsMatch.Submit"/> a human uses, and the engine validates it.
    /// </summary>
    public sealed class RandomBotPolicy
    {
        readonly Random _random;
        readonly List<int> _candidates = new List<int>();

        public RandomBotPolicy(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Chooses one request for the given seat. The seat must be alive.</summary>
        public TurnActionRequest ChooseAction(IReadOnlyList<MatchPlayerState> players, int botIndex)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (botIndex < 0 || botIndex >= players.Count) throw new ArgumentOutOfRangeException(nameof(botIndex));

            MatchPlayerState bot = players[botIndex];

            switch ((TurnActionType)_random.Next(3))
            {
                case TurnActionType.Attack:
                    return TurnActionRequest.Attack(botIndex, RandomLivingPlayer(players, botIndex, false));

                case TurnActionType.ReplaceShield:
                    return TurnActionRequest.ReplaceShield(botIndex, RandomLivingPlayer(players, botIndex, true));

                default:
                    // Only whether a Charge is stored matters, never which card it is.
                    return bot.HasCharge
                        ? TurnActionRequest.Charge(botIndex, RandomLivingPlayer(players, botIndex, false))
                        : TurnActionRequest.Charge(botIndex);
            }
        }

        int RandomLivingPlayer(IReadOnlyList<MatchPlayerState> players, int botIndex, bool allowSelf)
        {
            _candidates.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].IsAlive) continue;
                if (!allowSelf && i == botIndex) continue;

                _candidates.Add(i);
            }

            return _candidates.Count > 0 ? _candidates[_random.Next(_candidates.Count)] : TurnActionRequest.NoTarget;
        }
    }
}
