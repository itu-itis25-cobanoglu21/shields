using System;
using System.Collections.Generic;
using Shields.Session;

namespace Shields.Social
{
    /// <summary>
    /// Stable identity of one delivered beer: the order that bought it and the seat it was
    /// served to. A seat gets at most one mug per order, so the pair is unique, and every
    /// machine derives it from the same waiter broadcasts. Never a GameObject id.
    /// </summary>
    public readonly struct BeerId : IEquatable<BeerId>
    {
        public BeerId(int orderId, int seat)
        {
            OrderId = orderId;
            Seat = seat;
        }

        public int OrderId { get; }

        /// <summary>The seat whose DrinkArea holds this beer: its only owner.</summary>
        public int Seat { get; }

        public bool IsValid { get { return OrderId != BeerOrderQueue.NoOrder && Seat >= 0; } }

        /// <summary>Older beers sort first: lower order id, then seat.</summary>
        public int CompareAge(BeerId other)
        {
            int byOrder = OrderId.CompareTo(other.OrderId);
            return byOrder != 0 ? byOrder : Seat.CompareTo(other.Seat);
        }

        public bool Equals(BeerId other) { return OrderId == other.OrderId && Seat == other.Seat; }
        public override bool Equals(object obj) { return obj is BeerId && Equals((BeerId)obj); }
        public override int GetHashCode() { return OrderId * 31 + Seat; }
        public static bool operator ==(BeerId a, BeerId b) { return a.Equals(b); }
        public static bool operator !=(BeerId a, BeerId b) { return !a.Equals(b); }

        public override string ToString() { return "beer #" + OrderId + "/seat " + Seat; }
    }

    /// <summary>Why a drink could not start. Beer is cosmetic; none of these touch the match.</summary>
    public enum BeerDrinkRejection : byte
    {
        None = 0,

        /// <summary>No live match, or the request belongs to an earlier match.</summary>
        NotAvailable = 1,

        /// <summary>The sender maps to no seat.</summary>
        InvalidDrinker = 2,

        /// <summary>The drinker is eliminated, gone, or a bot.</summary>
        DrinkerUnavailable = 3,

        UnknownBeer = 4,

        /// <summary>The beer sits in another seat's DrinkArea.</summary>
        NotOwner = 5,

        Empty = 6,

        /// <summary>The drinker already has a mug at their mouth.</summary>
        AlreadyDrinking = 7,

        StaleGeneration = 8
    }

    public enum BeerDrinkStopReason : byte
    {
        /// <summary>The drinker let go of the drink key.</summary>
        Released = 0,

        /// <summary>The beer ran out.</summary>
        Empty = 1,

        /// <summary>Gameplay needed the hand, or the drinker died or left.</summary>
        Interrupted = 2
    }

    /// <summary>The authority's answer to a drink request; on success, everything every machine needs to animate it.</summary>
    public readonly struct BeerDrinkStart
    {
        public BeerDrinkStart(BeerDrinkRejection rejection, BeerId beer, float startFill, double startTime,
            float leadInSeconds, float ratePerSecond)
        {
            Rejection = rejection;
            Beer = beer;
            StartFill = startFill;
            StartTime = startTime;
            LeadInSeconds = leadInSeconds;
            RatePerSecond = ratePerSecond;
        }

        public static BeerDrinkStart Refused(BeerDrinkRejection rejection, BeerId beer)
        {
            return new BeerDrinkStart(rejection, beer, 0f, 0.0, 0f, 0f);
        }

        public bool Accepted { get { return Rejection == BeerDrinkRejection.None; } }

        public BeerDrinkRejection Rejection { get; }

        public BeerId Beer { get; }

        /// <summary>Remaining beer, 0..1, when the drink was accepted.</summary>
        public float StartFill { get; }

        /// <summary>Authority clock when the drink was accepted.</summary>
        public double StartTime { get; }

        /// <summary>Seconds of reach and lift before any beer is drained.</summary>
        public float LeadInSeconds { get; }

        /// <summary>Normalized beer drained per second of active drinking.</summary>
        public float RatePerSecond { get; }

        /// <summary>Remaining beer this drink implies at a time on the same clock as <see cref="StartTime"/>.</summary>
        public float FillAt(double time)
        {
            return BeerLedger.Deplete(StartFill, RatePerSecond, time - StartTime - LeadInSeconds);
        }
    }

    /// <summary>A finished drink: the authoritative remaining beer everyone reconciles to.</summary>
    public readonly struct BeerDrinkStop
    {
        public BeerDrinkStop(BeerId beer, float finalFill, BeerDrinkStopReason reason)
        {
            Beer = beer;
            FinalFill = finalFill;
            Reason = reason;
        }

        public BeerId Beer { get; }
        public float FinalFill { get; }
        public BeerDrinkStopReason Reason { get; }

        public override string ToString()
        {
            return Beer + " stopped (" + Reason + ") at " + FinalFill.ToString("0.00");
        }
    }

    /// <summary>
    /// The authoritative state of every delivered beer in one match: who owns it, how much
    /// is left, and who is drinking right now. Owned by the authority only (the offline
    /// match, or the host); clients send intent and animate the broadcast results.
    ///
    /// Remaining beer is never ticked frame by frame. A drink is a start fill, a start time
    /// and a rate; the remaining amount at any moment is derived from those, so a stop
    /// computes the exact final fill and every machine can animate the same curve locally.
    ///
    /// Plain C# with an explicit clock, created per match with that match's generation, so
    /// a request from an earlier match is refused by construction.
    /// </summary>
    public sealed class BeerLedger
    {
        public const float DefaultSecondsPerBeer = 5f;

        /// <summary>Reach (0.25 s) + grip settle (0.10 s) + bring to mouth (0.35 s).</summary>
        public const float DefaultLeadInSeconds = 0.7f;

        sealed class Beer
        {
            public BeerId Id;
            public float Fill = 1f;

            // While drinking.
            public bool Drinking;
            public float StartFill;
            public double StartTime;
        }

        readonly Dictionary<BeerId, Beer> _beers = new Dictionary<BeerId, Beer>();
        readonly Dictionary<int, BeerId> _activeBySeat = new Dictionary<int, BeerId>();
        readonly List<BeerId> _scratch = new List<BeerId>();

        public BeerLedger(int generation, float secondsPerBeer = DefaultSecondsPerBeer,
            float leadInSeconds = DefaultLeadInSeconds)
        {
            if (secondsPerBeer <= 0f) throw new ArgumentOutOfRangeException(nameof(secondsPerBeer));
            if (leadInSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(leadInSeconds));

            Generation = generation;
            RatePerSecond = 1f / secondsPerBeer;
            LeadInSeconds = leadInSeconds;
        }

        /// <summary>The match this ledger belongs to. Requests naming another generation are refused.</summary>
        public int Generation { get; }

        public float RatePerSecond { get; }

        public float LeadInSeconds { get; }

        public int Count { get { return _beers.Count; } }

        /// <summary>Bumped on every change, for cheap change detection.</summary>
        public int Revision { get; private set; }

        /// <summary>Remaining beer after draining at a rate for some seconds, clamped to 0..1.</summary>
        public static float Deplete(float startFill, float ratePerSecond, double seconds)
        {
            double drained = seconds > 0.0 ? seconds * ratePerSecond : 0.0;
            return Clamp01((float)(startFill - drained));
        }

        public static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        // ------------------------------------------------------------------ beers

        /// <summary>A mug was handed over to its recipient. Starts full. False if already known.</summary>
        public bool Register(BeerId id)
        {
            if (!id.IsValid || _beers.ContainsKey(id)) return false;

            _beers.Add(id, new Beer { Id = id });
            Revision++;
            return true;
        }

        public bool Contains(BeerId id) { return _beers.ContainsKey(id); }

        /// <summary>Remaining beer at a time, including any drink in progress. 0 for an unknown beer.</summary>
        public float FillAt(BeerId id, double now)
        {
            Beer beer;
            if (!_beers.TryGetValue(id, out beer)) return 0f;

            return beer.Drinking ? Deplete(beer.StartFill, RatePerSecond, now - beer.StartTime - LeadInSeconds) : beer.Fill;
        }

        public bool IsDrinking(int seat) { return _activeBySeat.ContainsKey(seat); }

        public bool TryGetActive(int seat, out BeerId id) { return _activeBySeat.TryGetValue(seat, out id); }

        /// <summary>The seat's oldest beer with anything left in it, the deterministic default choice.</summary>
        public bool TryGetOldestDrinkable(int seat, double now, out BeerId oldest)
        {
            oldest = default(BeerId);
            bool found = false;

            foreach (Beer beer in _beers.Values)
            {
                if (beer.Id.Seat != seat || FillAt(beer.Id, now) <= 0f) continue;
                if (found && beer.Id.CompareAge(oldest) >= 0) continue;

                oldest = beer.Id;
                found = true;
            }

            return found;
        }

        // ------------------------------------------------------------------ drinking

        /// <summary>
        /// Starts a drink for an already authoritative drinker seat (offline: the local seat;
        /// online: from <see cref="TryStartFromParticipant"/>).
        /// </summary>
        public BeerDrinkStart TryStart(int drinkerSeat, BeerId id, int generation,
            IReadOnlyList<BeerSeat> roster, double now)
        {
            if (generation != Generation) return BeerDrinkStart.Refused(BeerDrinkRejection.StaleGeneration, id);

            if (roster == null || drinkerSeat < 0 || drinkerSeat >= roster.Count)
                return BeerDrinkStart.Refused(BeerDrinkRejection.InvalidDrinker, id);

            if (!roster[drinkerSeat].CanDrink) return BeerDrinkStart.Refused(BeerDrinkRejection.DrinkerUnavailable, id);

            Beer beer;
            if (!_beers.TryGetValue(id, out beer)) return BeerDrinkStart.Refused(BeerDrinkRejection.UnknownBeer, id);

            if (id.Seat != drinkerSeat) return BeerDrinkStart.Refused(BeerDrinkRejection.NotOwner, id);

            if (_activeBySeat.ContainsKey(drinkerSeat))
                return BeerDrinkStart.Refused(BeerDrinkRejection.AlreadyDrinking, id);

            if (beer.Fill <= 0f) return BeerDrinkStart.Refused(BeerDrinkRejection.Empty, id);

            beer.Drinking = true;
            beer.StartFill = beer.Fill;
            beer.StartTime = now;
            _activeBySeat[drinkerSeat] = id;
            Revision++;

            return new BeerDrinkStart(BeerDrinkRejection.None, id, beer.StartFill, now, LeadInSeconds, RatePerSecond);
        }

        /// <summary>
        /// Online entry point. The drinker is the sender's seat from the host's registry; the
        /// payload only names a beer, so nobody can drink as somebody else.
        /// </summary>
        public BeerDrinkStart TryStartFromParticipant(SeatRegistry seats, ulong senderParticipantId, BeerId id,
            int generation, IReadOnlyList<BeerSeat> roster, double now)
        {
            int seat = seats != null ? seats.SeatOf(senderParticipantId) : SeatRegistry.NoSeat;
            if (seat == SeatRegistry.NoSeat) return BeerDrinkStart.Refused(BeerDrinkRejection.InvalidDrinker, id);

            return TryStart(seat, id, generation, roster, now);
        }

        /// <summary>
        /// Ends the seat's drink of exactly this beer and fixes its remaining amount. A stop
        /// for another beer, another seat or another match changes nothing.
        /// </summary>
        public bool TryStop(int drinkerSeat, BeerId id, int generation, double now, BeerDrinkStopReason reason,
            out BeerDrinkStop stop)
        {
            stop = default(BeerDrinkStop);
            if (generation != Generation) return false;

            BeerId active;
            if (!_activeBySeat.TryGetValue(drinkerSeat, out active) || active != id) return false;

            return Finish(id, now, reason, out stop);
        }

        public bool TryStopFromParticipant(SeatRegistry seats, ulong senderParticipantId, BeerId id, int generation,
            double now, BeerDrinkStopReason reason, out BeerDrinkStop stop)
        {
            stop = default(BeerDrinkStop);
            int seat = seats != null ? seats.SeatOf(senderParticipantId) : SeatRegistry.NoSeat;

            return seat != SeatRegistry.NoSeat && TryStop(seat, id, generation, now, reason, out stop);
        }

        /// <summary>
        /// Authority housekeeping: ends every drink whose beer ran out, and every drink of a
        /// seat that may no longer drink (died, left). Appends one stop per ended drink.
        /// </summary>
        public void Tick(double now, IReadOnlyList<BeerSeat> roster, List<BeerDrinkStop> stopped)
        {
            if (_activeBySeat.Count == 0) return;

            _scratch.Clear();
            foreach (KeyValuePair<int, BeerId> active in _activeBySeat) _scratch.Add(active.Value);

            for (int i = 0; i < _scratch.Count; i++)
            {
                BeerId id = _scratch[i];
                int seat = id.Seat;
                bool canDrink = roster != null && seat >= 0 && seat < roster.Count && roster[seat].CanDrink;

                BeerDrinkStop stop;
                if (!canDrink)
                {
                    if (Finish(id, now, BeerDrinkStopReason.Interrupted, out stop)) stopped.Add(stop);
                }
                else if (FillAt(id, now) <= 0f)
                {
                    if (Finish(id, now, BeerDrinkStopReason.Empty, out stop)) stopped.Add(stop);
                }
            }
        }

        bool Finish(BeerId id, double now, BeerDrinkStopReason reason, out BeerDrinkStop stop)
        {
            stop = default(BeerDrinkStop);

            Beer beer;
            if (!_beers.TryGetValue(id, out beer) || !beer.Drinking) return false;

            beer.Fill = FillAt(id, now);
            beer.Drinking = false;
            _activeBySeat.Remove(id.Seat);
            Revision++;

            stop = new BeerDrinkStop(id, beer.Fill, reason);
            return true;
        }

        /// <summary>Forgets every beer and every drink. Match end, rematch or teardown.</summary>
        public void Clear()
        {
            if (_beers.Count == 0 && _activeBySeat.Count == 0) return;

            _beers.Clear();
            _activeBySeat.Clear();
            Revision++;
        }

        public static string DescribeRejection(BeerDrinkRejection rejection)
        {
            switch (rejection)
            {
                case BeerDrinkRejection.None: return string.Empty;
                case BeerDrinkRejection.Empty: return "MUG IS EMPTY";
                case BeerDrinkRejection.AlreadyDrinking: return "ALREADY DRINKING";
                default: return "CANNOT DRINK RIGHT NOW";
            }
        }
    }
}
