using System;
using System.Collections.Generic;
using UnityEngine;
using Shields.Game;
using Shields.Match;
using Shields.Network;
using Shields.Online;
using Shields.Players;
using Shields.Social;
using Shields.Table;

namespace Shields.Tavern
{
    /// <summary>One selectable beer recipient, for the order panel only.</summary>
    public struct BeerRecipient
    {
        public int Seat;
        public string Label;
        public bool IsBot;
        public bool IsLocal;
    }

    /// <summary>
    /// Connects beer ordering to whichever match is running, so the order panel does not
    /// care whether it is offline or online.
    ///
    /// Offline this is the authority: it owns the local <see cref="BeerOrderQueue"/> for
    /// the running offline match, and submits the local seat's orders straight into it.
    /// Online the host's <see cref="NetworkBeerOrderService"/> owns the queue and this
    /// only forwards intent. Either way validation, FIFO, cooldown and capacity are the
    /// same <see cref="BeerOrderQueue"/> code, and the requester only ever learns the
    /// outcome through a <see cref="BeerOrderReceipt"/>.
    ///
    /// Beer never reaches <see cref="ShieldsMatch"/>. Match state is only read to know
    /// who is alive.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeerOrderCoordinator : MonoBehaviour
    {
        [Header("Offline")]
        [SerializeField] MatchBootstrap offlineBootstrap;

        [Tooltip("Seconds between two accepted orders from the same requester, offline.")]
        [Min(0f)]
        [SerializeField] float offlineCooldownSeconds = (float)BeerOrderQueue.DefaultCooldownSeconds;

        [Tooltip("Most undelivered offline orders at once, counting the one being delivered.")]
        [Min(1)]
        [SerializeField] int offlineMaxQueuedOrders = BeerOrderQueue.DefaultMaxOrders;

        [Header("Online")]
        [SerializeField] NetworkMatchController onlineMatch;
        [SerializeField] NetworkSeatRegistry onlineSeats;
        [SerializeField] OnlineMatchDriver onlineDriver;
        [SerializeField] NetworkBeerOrderService onlineOrders;

        [Header("Presentation")]
        [SerializeField] TableLayoutController tableLayout;

        BeerOrderQueue _offlineQueue;
        ShieldsMatch _offlineMatch;
        bool _onlinePlaying;
        float _localCooldownUntil;

        /// <summary>Raised on this machine when its own order was accepted or refused.</summary>
        public event Action<BeerOrderReceipt> ReceiptReceived;

        bool Online { get { return onlineMatch != null && onlineMatch.IsSpawned; } }

        public bool IsOnline { get { return Online; } }

        /// <summary>The online service carrying requests and waiter broadcasts.</summary>
        public NetworkBeerOrderService OnlineOrders { get { return onlineOrders; } }

        public TableLayoutController TableLayout { get { return tableLayout; } }

        /// <summary>A match is in turn play on this machine, offline or online, authority or not.</summary>
        public bool TurnPlayActive
        {
            get { return Online ? onlineMatch.Phase == OnlineMatchPhase.Playing : _offlineMatch != null; }
        }

        /// <summary>
        /// A match table is standing on this machine: dealt, in play or showing its final
        /// result. False once the match is torn down or the session is back in its lobby.
        /// </summary>
        public bool MatchTableLive
        {
            get
            {
                if (!Online) return offlineBootstrap != null && offlineBootstrap.MatchRunning;

                OnlineMatchPhase phase = onlineMatch.Phase;
                return phase == OnlineMatchPhase.InitialDeal || phase == OnlineMatchPhase.WaitingForShieldSelections ||
                    phase == OnlineMatchPhase.Playing || phase == OnlineMatchPhase.MatchOver;
            }
        }

        /// <summary>
        /// Who may be served right now, from authoritative state. Offline from the local
        /// match, online on the host only. Null on a client or with no live match.
        /// </summary>
        public BeerSeat[] BuildAuthoritativeRoster()
        {
            if (Online) return onlineOrders != null ? onlineOrders.BuildHostRoster() : null;

            return _offlineMatch != null ? BuildOfflineRoster() : null;
        }

        /// <summary>
        /// The authoritative queue when this machine is the authority: the offline match,
        /// or the host online. Null on an online client. The future waiter consumes it.
        /// </summary>
        public BeerOrderQueue AuthoritativeQueue
        {
            get
            {
                if (Online) return onlineOrders != null ? onlineOrders.HostQueue : null;

                return _offlineQueue;
            }
        }

        public int LocalSeat
        {
            get
            {
                if (Online) return onlineMatch.LocalSeatIndex;

                return offlineBootstrap != null ? offlineBootstrap.LocalPlayerIndex : -1;
            }
        }

        /// <summary>
        /// A live match is in turn play and the local seat is a living human. UX only;
        /// the authority validates every order again.
        /// </summary>
        public bool OrderingAvailable
        {
            get
            {
                int seat = LocalSeat;

                if (Online)
                {
                    if (onlineMatch.Phase != OnlineMatchPhase.Playing || onlineOrders == null) return false;

                    PublicMatchSnapshot snapshot = onlineMatch.MatchSnapshot;
                    if (!snapshot.IsValid || snapshot.MatchOver || seat < 0 || seat >= snapshot.PlayerCount) return false;

                    PublicPlayerState local = snapshot.GetPlayer(seat);
                    return local.IsAlive && !local.IsBot;
                }

                if (_offlineMatch == null || _offlineQueue == null || seat < 0 || seat >= _offlineMatch.PlayerCount)
                    return false;

                MatchPlayerState player = _offlineMatch.Players[seat];
                return player.IsAlive && !player.IsBot;
            }
        }

        /// <summary>True while the local player is choosing an action target with the crosshair.</summary>
        public bool LocalTargetingActive
        {
            get
            {
                if (Online) return onlineDriver != null && onlineDriver.LocalTargetingActive;

                MatchTurnController turns = offlineBootstrap != null ? offlineBootstrap.TurnController : null;
                return turns != null && turns.LocalTargetingActive;
            }
        }

        /// <summary>
        /// Local estimate of the requester cooldown, from the last receipt. Display only;
        /// the authority's own clock decides.
        /// </summary>
        public float LocalCooldownRemaining { get { return Mathf.Max(0f, _localCooldownUntil - Time.unscaledTime); } }

        // ------------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            if (onlineOrders != null) onlineOrders.ReceiptReceived += OnReceipt;
        }

        void OnDisable()
        {
            if (onlineOrders != null) onlineOrders.ReceiptReceived -= OnReceipt;

            ForgetOfflineMatch();
        }

        void Update()
        {
            TrackOfflineMatch();
            TrackOnlineMatch();
        }

        /// <summary>
        /// The offline queue lives exactly as long as one offline match is in turn play.
        /// A new match, a finished match or leaving turn play drops it with its cooldowns.
        /// </summary>
        void TrackOfflineMatch()
        {
            ShieldsMatch live = !Online && offlineBootstrap != null && offlineBootstrap.MatchRunning
                ? offlineBootstrap.Match
                : null;

            if (live != null && live.IsMatchOver) live = null;
            if (live == _offlineMatch) return;

            ForgetOfflineMatch();

            _offlineMatch = live;
            if (live != null) _offlineQueue = new BeerOrderQueue(offlineCooldownSeconds, offlineMaxQueuedOrders);
        }

        void ForgetOfflineMatch()
        {
            if (_offlineQueue != null) _offlineQueue.Clear();

            _offlineQueue = null;
            _offlineMatch = null;
            _localCooldownUntil = 0f;
        }

        /// <summary>The host clears its own queue; this only forgets the local cooldown estimate.</summary>
        void TrackOnlineMatch()
        {
            bool playing = Online && onlineMatch.Phase == OnlineMatchPhase.Playing;
            if (playing == _onlinePlaying) return;

            _onlinePlaying = playing;
            if (!Online || !playing) _localCooldownUntil = 0f;
        }

        // ------------------------------------------------------------------ roster

        /// <summary>
        /// Every seat that can be bought a beer right now: living and present, bots
        /// included, the local player first. Built from match state, never from the table.
        /// </summary>
        public void GetRecipients(List<BeerRecipient> into)
        {
            into.Clear();
            if (!OrderingAvailable) return;

            int localSeat = LocalSeat;

            if (Online)
            {
                PublicMatchSnapshot snapshot = onlineMatch.MatchSnapshot;
                for (int seat = 0; seat < snapshot.PlayerCount; seat++)
                {
                    PublicPlayerState player = snapshot.GetPlayer(seat);
                    bool present = player.IsBot || onlineSeats == null || onlineSeats.OccupantAt(seat).IsHuman;

                    if (player.IsAlive && present) Add(into, seat, localSeat, player.IsBot);
                }
            }
            else
            {
                for (int seat = 0; seat < _offlineMatch.PlayerCount; seat++)
                {
                    MatchPlayerState player = _offlineMatch.Players[seat];
                    if (player.IsAlive) Add(into, seat, localSeat, player.IsBot);
                }
            }

            // YOU first, then seats in order.
            into.Sort((a, b) => a.IsLocal != b.IsLocal ? (a.IsLocal ? -1 : 1) : a.Seat.CompareTo(b.Seat));
        }

        static void Add(List<BeerRecipient> into, int seat, int localSeat, bool isBot)
        {
            string label = MatchHud.PlayerName(seat, localSeat, false);
            if (isBot) label += "  (BOT)";

            into.Add(new BeerRecipient { Seat = seat, Label = label, IsBot = isBot, IsLocal = seat == localSeat });
        }

        // ------------------------------------------------------------------ submit

        /// <summary>
        /// Sends one order for the local seat. Offline the receipt arrives before this
        /// returns; online it arrives when the host answers. Returns false when nothing
        /// could be sent at all.
        /// </summary>
        public bool Submit(IReadOnlyList<int> recipientSeats)
        {
            if (Online) return onlineOrders != null && onlineOrders.RequestOrder(recipientSeats);

            if (_offlineQueue == null || _offlineMatch == null) return false;

            BeerOrderReceipt receipt = _offlineQueue.Submit(LocalSeat, recipientSeats, BuildOfflineRoster(),
                Time.unscaledTimeAsDouble);

            if (receipt.Accepted) Debug.Log("Beer order accepted\n" + _offlineQueue.Describe(), this);

            OnReceipt(receipt);
            return true;
        }

        BeerSeat[] BuildOfflineRoster()
        {
            BeerSeat[] roster = new BeerSeat[_offlineMatch.PlayerCount];
            for (int i = 0; i < roster.Length; i++)
            {
                MatchPlayerState player = _offlineMatch.Players[i];
                roster[i] = new BeerSeat(true, player.IsAlive, player.IsBot);
            }

            return roster;
        }

        void OnReceipt(BeerOrderReceipt receipt)
        {
            if (receipt.Accepted || receipt.Rejection == BeerOrderRejection.Cooldown)
                _localCooldownUntil = Time.unscaledTime + receipt.CooldownRemaining;

            Action<BeerOrderReceipt> received = ReceiptReceived;
            if (received != null) received(receipt);
        }

        // ------------------------------------------------------------------ presentation

        /// <summary>The local seat's head look, which the order panel pauses while it has the mouse.</summary>
        public SeatedHeadLook LocalHeadLook()
        {
            PlayerCharacter character = tableLayout != null ? tableLayout.GetPlayer(LocalSeat) : null;
            return character != null ? character.GetComponent<SeatedHeadLook>() : null;
        }
    }
}
