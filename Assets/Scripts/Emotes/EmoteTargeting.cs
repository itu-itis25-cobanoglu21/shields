using System;
using UnityEngine;
using Shields.Players;
using Shields.Table;

namespace Shields.Emotes
{
    /// <summary>
    /// Which other player a seat is looking at, for emotes only (never gameplay targeting).
    /// Computed from the seat's head pose, which every machine already has, so the local
    /// player's machine and everyone else's agree without sending anything.
    /// </summary>
    public static class EmoteTargeting
    {
        /// <summary>How far up the body the looked-at point is, from the body's centre (0) to the head (1).</summary>
        public const float UpperBody = 0.7f;

        /// <summary>
        /// The seat whose upper body is closest to the line of sight of <paramref name="seat"/>,
        /// within <paramref name="maxAngle"/> degrees, among seats <paramref name="accept"/>
        /// allows (null: every other seat). -1 when nobody is that close.
        /// </summary>
        public static int FindLookTarget(TableLayoutController table, int seat, float maxAngle, Func<int, bool> accept,
            out Vector3 point)
        {
            point = Vector3.zero;
            if (table == null) return -1;

            PlayerCharacter self = table.GetPlayer(seat);
            Transform view = self != null ? (self.CameraAnchor != null ? self.CameraAnchor : self.HeadPivot) : null;
            if (view == null) return -1;

            int best = -1;
            float bestAngle = maxAngle;
            for (int i = 0; i < table.Players.Count; i++)
            {
                PlayerCharacter other = table.Players[i];
                if (other == null || other == self || other.PlayerIndex == seat) continue;
                if (accept != null && !accept(other.PlayerIndex)) continue;

                Vector3 at = other.UpperBodyAimPoint(UpperBody);
                float angle = Vector3.Angle(view.forward, at - view.position);
                if (angle >= bestAngle) continue;

                bestAngle = angle;
                best = other.PlayerIndex;
                point = at;
            }

            return best;
        }
    }
}
