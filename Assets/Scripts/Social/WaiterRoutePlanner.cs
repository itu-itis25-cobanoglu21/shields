using System;
using System.Collections.Generic;

namespace Shields.Social
{
    /// <summary>How the waiter walks the ring around the table, starting and ending at the door.</summary>
    public enum WaiterRouteMode : byte
    {
        /// <summary>Increasing angle out to the last stop, then back the same way.</summary>
        ForwardAndBack = 0,

        /// <summary>Decreasing angle out to the last stop, then back the same way.</summary>
        BackwardAndBack = 1,

        /// <summary>Increasing angle all the way round, ending back at the door.</summary>
        FullLoop = 2
    }

    /// <summary>A walking order for one delivery. Presentation only; who is served never changes.</summary>
    public readonly struct WaiterRoutePlan
    {
        public WaiterRoutePlan(int[] visitOrder, WaiterRouteMode mode)
        {
            VisitOrder = visitOrder;
            Mode = mode;
        }

        /// <summary>Indices into the stop list, in the order they are visited.</summary>
        public int[] VisitOrder { get; }

        public WaiterRouteMode Mode { get; }
    }

    /// <summary>
    /// Everything every machine needs to present one delivery identically: which order,
    /// which seats in which visiting order, and which way round the ring. Public social
    /// data only; it carries no card or match state.
    /// </summary>
    public readonly struct WaiterDeliveryPlan
    {
        public WaiterDeliveryPlan(int orderId, int requesterSeat, int[] visitSeats, WaiterRouteMode mode)
        {
            OrderId = orderId;
            RequesterSeat = requesterSeat;
            VisitSeats = visitSeats ?? new int[0];
            Mode = mode;
        }

        public int OrderId { get; }

        public int RequesterSeat { get; }

        /// <summary>Recipient seats in visiting order. One tray mug each, mug i for seat i.</summary>
        public int[] VisitSeats { get; }

        public WaiterRouteMode Mode { get; }

        public bool IsValid { get { return OrderId != BeerOrderQueue.NoOrder && VisitSeats.Length > 0; } }

        public override string ToString()
        {
            return string.Format("Waiter #{0}: visit [{1}] {2}", OrderId, string.Join(",", VisitSeats), Mode);
        }
    }

    /// <summary>
    /// Picks the shortest walk around the table ring that starts and ends at the door
    /// and passes every stop, without ever cutting across the table.
    ///
    /// Stops are given as angles in degrees measured around the ring from the door, so
    /// 0 is the door and angles grow in one fixed direction. Three candidate routes are
    /// compared: forward and back (2 × furthest angle), backward and back
    /// (2 × (360 − nearest angle)) and a full loop (360). Only the visiting order and the
    /// direction are chosen here; the set of recipients is never altered.
    /// </summary>
    public static class WaiterRoutePlanner
    {
        public static WaiterRoutePlan Plan(IReadOnlyList<double> stopAnglesFromDoor)
        {
            if (stopAnglesFromDoor == null) throw new ArgumentNullException(nameof(stopAnglesFromDoor));

            int count = stopAnglesFromDoor.Count;
            int[] ascending = new int[count];
            double[] angles = new double[count];

            for (int i = 0; i < count; i++)
            {
                ascending[i] = i;
                angles[i] = Normalize(stopAnglesFromDoor[i]);
            }

            // Stable: equal angles keep their given order.
            SortStable(ascending, angles);

            if (count == 0) return new WaiterRoutePlan(ascending, WaiterRouteMode.ForwardAndBack);

            double min = angles[ascending[0]];
            double max = angles[ascending[count - 1]];

            double forward = 2.0 * max;
            double backward = 2.0 * (360.0 - min);
            const double loop = 360.0;

            if (forward <= backward && forward <= loop)
                return new WaiterRoutePlan(ascending, WaiterRouteMode.ForwardAndBack);

            if (loop <= backward)
                return new WaiterRoutePlan(ascending, WaiterRouteMode.FullLoop);

            int[] descending = new int[count];
            for (int i = 0; i < count; i++) descending[i] = ascending[count - 1 - i];

            return new WaiterRoutePlan(descending, WaiterRouteMode.BackwardAndBack);
        }

        /// <summary>Total ring distance in degrees a plan walks, for diagnostics and tests.</summary>
        public static double RingDegrees(IReadOnlyList<double> stopAnglesFromDoor, WaiterRoutePlan plan)
        {
            if (plan.VisitOrder.Length == 0) return 0.0;

            switch (plan.Mode)
            {
                case WaiterRouteMode.ForwardAndBack:
                    return 2.0 * Normalize(stopAnglesFromDoor[plan.VisitOrder[plan.VisitOrder.Length - 1]]);
                case WaiterRouteMode.BackwardAndBack:
                    return 2.0 * (360.0 - Normalize(stopAnglesFromDoor[plan.VisitOrder[plan.VisitOrder.Length - 1]]));
                default:
                    return 360.0;
            }
        }

        public static double Normalize(double degrees)
        {
            double value = degrees % 360.0;
            return value < 0.0 ? value + 360.0 : value;
        }

        static void SortStable(int[] indices, double[] keys)
        {
            // Insertion sort: tiny inputs (at most 8 stops) and stable by construction.
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            for (int i = 1; i < indices.Length; i++)
            {
                int current = indices[i];
                int j = i - 1;
                while (j >= 0 && keys[indices[j]] > keys[current])
                {
                    indices[j + 1] = indices[j];
                    j--;
                }

                indices[j + 1] = current;
            }
        }
    }
}
