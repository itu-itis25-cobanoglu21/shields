using System;

namespace Shields.Game
{
    /// <summary>
    /// Where seats sit around the round table, as plain numbers on the floor plane
    /// (x to the right, z away from the viewer when the table is seen from above).
    ///
    /// The presentation layout (<c>SeatLayout</c>) places every seat with exactly these
    /// numbers, so the turn order rule can be checked against the real table: seat yaw
    /// is index × 360 / N, and a yaw of a degrees puts the seat at
    /// (-sin a, -cos a) × radius. Seen from above that walks 6 o'clock → 9 o'clock →
    /// 12 o'clock → 3 o'clock as the index grows, which is clockwise. So clockwise turn
    /// order is seat index + 1, and the seated player on your left plays after you.
    /// </summary>
    public static class TableGeometry
    {
        /// <summary>Yaw of a seat around the table in degrees: the 360 / N step.</summary>
        public static double SeatYawDegrees(int seatIndex, int seatCount)
        {
            if (seatCount <= 0) throw new ArgumentOutOfRangeException(nameof(seatCount));

            return seatIndex * 360.0 / seatCount;
        }

        /// <summary>Seat position relative to the table centre on the floor plane.</summary>
        public static void SeatFloorPosition(int seatIndex, int seatCount, double radius, out double x, out double z)
        {
            double yaw = SeatYawDegrees(seatIndex, seatCount) * Math.PI / 180.0;

            // Euler(0, yaw, 0) * (0, 0, -radius) in a left handed, y up frame.
            x = -Math.Sin(yaw) * radius;
            z = -Math.Cos(yaw) * radius;
        }

        /// <summary>The seat that is next clockwise from <paramref name="seatIndex"/>.</summary>
        public static int NextClockwise(int seatIndex, int seatCount)
        {
            if (seatCount <= 0) throw new ArgumentOutOfRangeException(nameof(seatCount));

            return (seatIndex + 1) % seatCount;
        }
    }
}
