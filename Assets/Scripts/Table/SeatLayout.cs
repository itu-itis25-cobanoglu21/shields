using System;
using UnityEngine;

namespace Shields.Table
{
    /// <summary>
    /// Tunable, human scale numbers for how players and their card areas sit around
    /// the round table. All values are meters. Nothing here touches Unity objects, so
    /// the placement math stays testable and reusable.
    /// </summary>
    [Serializable]
    public class TableLayoutSettings
    {
        [Header("Table")]
        [Tooltip("Table edge length reserved per player. Drives how the tabletop grows with the player count.")]
        [Range(0.5f, 1.2f)] public float arcPerPlayer = 0.70f;

        [Tooltip("Smallest tabletop radius, so a 3 player table is still a real card table.")]
        [Range(0.4f, 1.5f)] public float minTableRadius = 0.70f;

        [Tooltip("Largest tabletop radius, so an 8 player table stays readable across the felt.")]
        [Range(0.6f, 2.0f)] public float maxTableRadius = 1.10f;

        [Header("Seats")]
        [Tooltip("Distance from the tabletop edge back to the seated player's body center.")]
        public float seatSetback = 0.30f;

        [Header("Card Areas")]
        [Tooltip("How far inside the tabletop edge a player's card area origin sits.")]
        public float cardAreaInset = 0.20f;

        [Tooltip("Uniform scale of each card area, cards and slot spacing together. Presentation only; above 1 reads better across the table.")]
        [Range(1f, 2f)] public float cardAreaScale = 1.5f;
    }

    /// <summary>Resolved layout numbers for one player count.</summary>
    public struct TableLayoutMetrics
    {
        public int PlayerCount;
        public float TableRadius;
        public float SeatRadius;
        public float CardAreaRadius;
        public float SeatAngleStep;
    }

    /// <summary>
    /// Even distribution of N players around a round table. Seat 0 sits on -Z and
    /// faces +Z, and every seat's forward axis points at the table center.
    /// </summary>
    public static class SeatLayout
    {
        public const int MinPlayers = 3;
        public const int MaxPlayers = 8;

        public static int ClampPlayerCount(int playerCount)
        {
            return Mathf.Clamp(playerCount, MinPlayers, MaxPlayers);
        }

        /// <summary>
        /// Scales the tabletop and the seat ring so every supported player count keeps
        /// a comfortable amount of table edge per player.
        /// </summary>
        public static TableLayoutMetrics Resolve(int playerCount, TableLayoutSettings settings)
        {
            if (settings == null) settings = new TableLayoutSettings();

            int count = ClampPlayerCount(playerCount);

            float maxRadius = Mathf.Max(settings.minTableRadius, settings.maxTableRadius);
            float idealRadius = count * settings.arcPerPlayer / (2f * Mathf.PI);
            float tableRadius = Mathf.Clamp(idealRadius, settings.minTableRadius, maxRadius);

            return new TableLayoutMetrics
            {
                PlayerCount = count,
                TableRadius = tableRadius,
                SeatRadius = tableRadius + settings.seatSetback,
                CardAreaRadius = Mathf.Max(0.05f, tableRadius - settings.cardAreaInset),
                SeatAngleStep = 360f / count
            };
        }

        /// <summary>
        /// Yaw of a seat around the table, in degrees: the 360 / N step. Shared with the
        /// pure <see cref="Shields.Game.TableGeometry"/>, which the clockwise turn rule is
        /// tested against, so the turn order and the visible seats cannot disagree.
        /// </summary>
        public static float SeatYaw(int playerIndex, in TableLayoutMetrics metrics)
        {
            return (float)Shields.Game.TableGeometry.SeatYawDegrees(playerIndex, Mathf.Max(1, metrics.PlayerCount));
        }

        /// <summary>Rotation whose forward axis points at the table center.</summary>
        public static Quaternion SeatRotation(int playerIndex, in TableLayoutMetrics metrics)
        {
            return Quaternion.Euler(0f, SeatYaw(playerIndex, metrics), 0f);
        }

        /// <summary>Seat position relative to the table center, on the floor plane.</summary>
        public static Vector3 SeatOffset(int playerIndex, in TableLayoutMetrics metrics)
        {
            double x, z;
            Shields.Game.TableGeometry.SeatFloorPosition(playerIndex, Mathf.Max(1, metrics.PlayerCount), metrics.SeatRadius, out x, out z);
            return new Vector3((float)x, 0f, (float)z);
        }

        /// <summary>Card area origin relative to the table center, on the table plane.</summary>
        public static Vector3 CardAreaOffset(int playerIndex, in TableLayoutMetrics metrics)
        {
            return SeatRotation(playerIndex, metrics) * (Vector3.back * metrics.CardAreaRadius);
        }

        /// <summary>Straight line distance between two neighbouring seats.</summary>
        public static float NeighbourSpacing(in TableLayoutMetrics metrics)
        {
            return 2f * metrics.SeatRadius * Mathf.Sin(Mathf.PI / Mathf.Max(1, metrics.PlayerCount));
        }
    }
}
