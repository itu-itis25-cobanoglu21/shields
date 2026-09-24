using Unity.Netcode;
using Shields.Game;

namespace Shields.Online
{
    /// <summary>
    /// Where an online match is. The host owns every transition; clients read it and
    /// never infer the phase from whether some UI object happens to exist.
    /// </summary>
    public enum OnlineMatchPhase
    {
        /// <summary>Connected, seats assigned, no match created yet.</summary>
        SessionLobby = 0,

        /// <summary>The host has dealt. Clients are receiving the public setup state.</summary>
        InitialDeal = 1,

        /// <summary>Waiting for every seat to choose its Shield.</summary>
        WaitingForShieldSelections = 2,

        /// <summary>Turn play is running.</summary>
        Playing = 3,

        /// <summary>One player remains, or the match was ended.</summary>
        MatchOver = 4,

        /// <summary>A participant vanished mid match, so the match was stopped.</summary>
        Aborted = 5
    }

    /// <summary>
    /// The whole public gameplay state of the authoritative match, as one snapshot.
    ///
    /// This is an explicit projection, not a serialized <see cref="ShieldsMatch"/>. The
    /// engine, its <see cref="Shields.Cards.CardDeck"/> and every hidden Charge card stay
    /// on the host; only the fields named here ever travel.
    ///
    /// <see cref="Revision"/> increases by one per authoritative change so a client can
    /// drop a snapshot that arrives after a newer one. The game is turn based, so a
    /// confirmed snapshot is always enough and there is no prediction or rollback.
    /// </summary>
    public struct PublicMatchSnapshot : INetworkSerializable
    {
        /// <summary>The revision of a snapshot that has never been published.</summary>
        public const uint NoRevision = 0;

        public uint Revision;
        public byte PhaseCode;
        public sbyte CurrentPlayerIndex;
        public byte DirectionCode;
        public bool MatchOver;
        public sbyte WinnerIndex;

        /// <summary>Cards left in the host's draw pile, for the existing deck counter only.</summary>
        public short RemainingDrawCount;

        public PublicPlayerState[] Players;

        public OnlineMatchPhase Phase { get { return (OnlineMatchPhase)PhaseCode; } }

        public TurnDirection Direction { get { return (TurnDirection)DirectionCode; } }

        public int PlayerCount { get { return Players != null ? Players.Length : 0; } }

        public bool IsValid { get { return Revision != NoRevision; } }

        public PublicPlayerState GetPlayer(int playerIndex)
        {
            if (Players == null || playerIndex < 0 || playerIndex >= Players.Length)
                return default;

            return Players[playerIndex];
        }

        /// <summary>True when that seat is alive and it is currently their turn.</summary>
        public bool IsTurnOf(int playerIndex)
        {
            return !MatchOver
                && CurrentPlayerIndex == playerIndex
                && GetPlayer(playerIndex).IsAlive;
        }

        public int LivingPlayerCount
        {
            get
            {
                if (Players == null) return 0;

                int count = 0;
                for (int i = 0; i < Players.Length; i++)
                    if (Players[i].IsAlive) count++;

                return count;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref PhaseCode);
            serializer.SerializeValue(ref CurrentPlayerIndex);
            serializer.SerializeValue(ref DirectionCode);
            serializer.SerializeValue(ref MatchOver);
            serializer.SerializeValue(ref WinnerIndex);
            serializer.SerializeValue(ref RemainingDrawCount);

            // Length first, so the reader can size the array before filling it.
            int count = Players != null ? Players.Length : 0;
            serializer.SerializeValue(ref count);

            if (serializer.IsReader) Players = new PublicPlayerState[count];

            for (int i = 0; i < count; i++) serializer.SerializeValue(ref Players[i]);
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("rev ").Append(Revision).Append(' ').Append(Phase);
            sb.Append(MatchOver ? ", winner player " + WinnerIndex : ", turn player " + CurrentPlayerIndex);
            sb.Append(", draw ").Append(RemainingDrawCount);

            for (int i = 0; i < PlayerCount; i++) sb.Append("\n  ").Append(Players[i]);

            return sb.ToString();
        }
    }
}
