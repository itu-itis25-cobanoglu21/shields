using System.Collections.Generic;
using UnityEngine;
using Shields.Table;

namespace Shields.Tavern
{
    /// <summary>
    /// Where mugs sit on the waiter's tray, for 1 to 8 mugs. Tuned to the real tray
    /// (Plate.fbx: round, inner floor about 0.015 m up, usable radius about 0.19 m) and the
    /// real mug at its 0.4 scale (body radius about 0.046 m, handle out to about 0.079 m
    /// on the mesh's -X side). Every handle points the same way, toward the waiter's
    /// right-front (<see cref="MugYaw"/>), so the serving hand always takes a mug from the
    /// side away from its handle; the rings are laid out so no handle reaches a neighbour.
    /// </summary>
    public static class TrayMugLayout
    {
        public const int MaxMugs = 8;
        public const float FloorHeight = 0.015f;

        /// <summary>
        /// Mug yaw in the waiter's (and tray's) frame: the handle (mesh -X) points 35 degrees
        /// right of the waiter's forward. The serving hand's fingers hook the rim on the other
        /// side, and a mug keeps this yaw while it is carried, so it lands on a player's table
        /// with its handle facing away from them (<see cref="DrinkAreaLayout.MugYaw"/>).
        /// </summary>
        public const float MugYaw = 125f;

        /// <summary>Local pose of mug <paramref name="index"/> of <paramref name="count"/> on the tray.</summary>
        public static void Pose(int index, int count, out Vector3 localPosition, out Quaternion localRotation)
        {
            count = Mathf.Clamp(count, 1, MaxMugs);

            float radius;
            float startDegrees;
            int ringCount;
            bool center;

            // With every handle pointing the same way, each ring is turned (and sized) so no
            // handle reaches a neighbour: measured against the mug's real outline, at least
            // 12 mm clear for 2 to 7 mugs and 3 mm for 8 (that ring is at the usable edge).
            switch (count)
            {
                case 1: center = true; ringCount = 0; radius = 0f; startDegrees = 0f; break;
                case 2: center = false; ringCount = 2; radius = 0.052f; startDegrees = 4f; break;
                case 3: center = false; ringCount = 3; radius = 0.070f; startDegrees = 53f; break;
                case 4: center = false; ringCount = 4; radius = 0.074f; startDegrees = 50f; break;
                case 5: center = false; ringCount = 5; radius = 0.112f; startDegrees = 18f; break;
                case 6: center = false; ringCount = 6; radius = 0.120f; startDegrees = 24f; break;
                case 7: center = true; ringCount = 6; radius = 0.120f; startDegrees = 24f; break;
                default: center = true; ringCount = 7; radius = 0.144f; startDegrees = 3f; break;
            }

            float phi;
            if (center && index == 0)
            {
                localPosition = new Vector3(0f, FloorHeight, 0f);
                phi = 0f;
            }
            else
            {
                int ringIndex = center ? index - 1 : index;
                phi = startDegrees + ringIndex * 360f / ringCount;
                float rad = phi * Mathf.Deg2Rad;
                localPosition = new Vector3(Mathf.Cos(rad) * radius, FloorHeight, Mathf.Sin(rad) * radius);
            }

            localRotation = Quaternion.Euler(0f, MugYaw, 0f);
        }
    }

    /// <summary>
    /// Seat-local spots for delivered mugs, on the player's LEFT near the table edge:
    /// the Health cards sit in the middle, the Shield, Charge and reveal cards toward the
    /// centre and the player's right. Built from the live layout metrics, so it adapts to
    /// 3 to 8 players: rows run along the edge from the player toward the left neighbour,
    /// stopping clear of both seats' cards, then continue on rows further in.
    /// </summary>
    public static class DrinkAreaLayout
    {
        const float MugSpacing = 0.12f;
        const float MugRadius = 0.05f;
        const float EdgeInset = 0.10f;
        const float RowStep = 0.11f;
        const float MinRowRadius = 0.30f;

        /// <summary>
        /// Sideways clearance from a seat's own card column to a mug's centre: the Health
        /// cards reach about 0.103 m, the mug body 0.045 m, leaving about 3 cm between them.
        /// </summary>
        const float CardClearance = 0.18f;

        const int MaxSlots = 10;

        /// <summary>
        /// Mug yaw on the table, in the DrinkArea's frame: the handle (mesh -X) faces away from
        /// the player, 55 degrees round to their right, so the left hand takes the mug by its
        /// barrel without meeting the handle. It is also the yaw relative to the face while
        /// drinking. Matches <see cref="TrayMugLayout.MugYaw"/> plus the waiter's ~20 degree
        /// turn toward the slot, so a carried mug barely turns as it is set down.
        /// </summary>
        public const float MugYaw = 145f;

        /// <summary>
        /// Slot positions in DrinkArea local space: meters, origin at the card area origin,
        /// +Z toward the table centre, +X the player's right.
        /// </summary>
        public static List<Vector3> Slots(TableLayoutMetrics metrics)
        {
            List<Vector3> slots = new List<Vector3>();
            float step = metrics.SeatAngleStep > 0f ? metrics.SeatAngleStep : 120f;

            for (int row = 0; slots.Count < MaxSlots; row++)
            {
                float r = metrics.TableRadius - EdgeInset - row * RowStep;
                if (r < MinRowRadius) break;

                float start = Mathf.Asin(Mathf.Clamp01(CardClearance / r)) * Mathf.Rad2Deg;
                float halfMug = Mathf.Asin(Mathf.Clamp01(MugRadius / r)) * Mathf.Rad2Deg;

                // The edge row may run up to the neighbour's own card column; rows further
                // in stop at the midline so they never reach the neighbour's Charge card.
                float end = row == 0 ? step - start : step * 0.5f - halfMug;
                float delta = 2f * Mathf.Asin(Mathf.Clamp01(MugSpacing * 0.5f / r)) * Mathf.Rad2Deg;

                for (float a = start; a <= end + 0.01f && slots.Count < MaxSlots; a += delta)
                {
                    float rad = a * Mathf.Deg2Rad;

                    // Polar around the table centre, toward the player's left, then made
                    // relative to the card area origin.
                    slots.Add(new Vector3(-r * Mathf.Sin(rad), 0f, -r * Mathf.Cos(rad) + metrics.CardAreaRadius));
                }
            }

            if (slots.Count == 0) slots.Add(new Vector3(-CardClearance, 0f, -EdgeInset * 0.5f));
            return slots;
        }

        /// <summary>
        /// Local pose for the <paramref name="index"/>-th mug delivered to one seat. Past
        /// the last slot the pattern repeats half a mug further in, so mugs never land on
        /// exactly the same spot.
        /// </summary>
        public static void Pose(int index, TableLayoutMetrics metrics, out Vector3 localPosition, out Quaternion localRotation)
        {
            List<Vector3> slots = Slots(metrics);
            int lap = index / slots.Count;

            localPosition = slots[index % slots.Count] + new Vector3(0f, 0f, lap * MugSpacing * 0.5f);

            // Handle away from the player (see MugYaw): the left hand wraps the barrel and keeps
            // the very same grip all the way to the mouth.
            localRotation = Quaternion.Euler(0f, MugYaw, 0f);
        }

        /// <summary>
        /// The first slot index whose spot no mug in <paramref name="drinkArea"/> occupies. Slots
        /// free up when the waiter takes an empty mug back, so this is not simply the child count.
        /// </summary>
        public static int FreeSlotIndex(Transform drinkArea, TableLayoutMetrics metrics)
        {
            for (int index = 0; ; index++)
            {
                Vector3 position;
                Quaternion rotation;
                Pose(index, metrics, out position, out rotation);

                bool taken = false;
                for (int i = 0; i < drinkArea.childCount && !taken; i++)
                    taken = (drinkArea.GetChild(i).localPosition - position).sqrMagnitude < 0.0004f;
                if (!taken) return index;
            }
        }
    }
}
