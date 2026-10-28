using System.Collections.Generic;
using UnityEngine;
using Shields.Table;

namespace Shields.Tavern
{
    /// <summary>
    /// Where mugs sit on the waiter's tray, for 1 to 8 mugs. Tuned to the real tray
    /// (Plate.fbx: round, inner floor about 0.015 m up, usable radius about 0.19 m) and the
    /// real mug at its 0.4 scale (body radius about 0.045 m, handle out to about 0.061 m
    /// on the mesh's -X side). Handles point outward so they never poke a neighbour.
    /// </summary>
    public static class TrayMugLayout
    {
        public const int MaxMugs = 8;
        public const float FloorHeight = 0.015f;

        /// <summary>Local pose of mug <paramref name="index"/> of <paramref name="count"/> on the tray.</summary>
        public static void Pose(int index, int count, out Vector3 localPosition, out Quaternion localRotation)
        {
            count = Mathf.Clamp(count, 1, MaxMugs);

            float radius;
            float startDegrees;
            int ringCount;
            bool center;

            switch (count)
            {
                case 1: center = true; ringCount = 0; radius = 0f; startDegrees = 0f; break;
                case 2: center = false; ringCount = 2; radius = 0.055f; startDegrees = 0f; break;
                case 3: center = false; ringCount = 3; radius = 0.068f; startDegrees = 90f; break;
                case 4: center = false; ringCount = 4; radius = 0.078f; startDegrees = 45f; break;
                case 5: center = false; ringCount = 5; radius = 0.095f; startDegrees = 90f; break;
                case 6: center = false; ringCount = 6; radius = 0.11f; startDegrees = 0f; break;
                // With a centre mug the ring is offset so the centre handle points into a gap.
                case 7: center = true; ringCount = 6; radius = 0.11f; startDegrees = 30f; break;
                default: center = true; ringCount = 7; radius = 0.12f; startDegrees = 360f / 14f; break;
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

            // The mesh's handle is on -X; this yaw points it along phi, outward.
            localRotation = Quaternion.Euler(0f, 180f - phi, 0f);
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

            // Handle (mesh -X) toward the player, over the table edge: within easy reach,
            // and only the round body faces the neighbouring cards.
            localRotation = Quaternion.Euler(0f, 270f, 0f);
        }
    }
}
