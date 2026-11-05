using UnityEngine;

namespace Shields.Players
{
    /// <summary>Every hand pose the game uses. Each has one definition in <see cref="HandPoses"/>.</summary>
    public enum HandPoseId
    {
        Idle,
        DeckReach,
        DeckRest,
        ChargeGrab,
        ChargeCarry,
        CardHold,
        CardRelease,
        WeaponReach,
        WeaponGrip,
        WeaponFire,
        MugReach,
        MugGrip,
        TraySupport,
        WaiterMugReach,
        WaiterMugGrip
    }

    /// <summary>
    /// One joint's offset from the imported bind pose, in degrees:
    /// x = curl toward the palm, y = spread toward the thumb side, z = twist (palm face
    /// toward the thumb side). The same numbers mirror correctly on a left and a right hand.
    /// </summary>
    [System.Serializable]
    public struct DigitPose
    {
        // Base (inside the palm), knuckle, middle joint, tip. The middle finger has no fourth bone.
        public Vector3 Base, Knuckle, Mid, Tip;

        public DigitPose(Vector3 baseJoint, Vector3 knuckle, Vector3 mid, Vector3 tip)
        {
            Base = baseJoint;
            Knuckle = knuckle;
            Mid = mid;
            Tip = tip;
        }

        public Vector3 Joint(int i)
        {
            switch (i)
            {
                case 0: return Base;
                case 1: return Knuckle;
                case 2: return Mid;
                default: return Tip;
            }
        }
    }

    /// <summary>A whole-hand pose: wrist offset (x flex, y deviation toward the thumb, z roll) and four digits.</summary>
    public sealed class HandPose
    {
        public readonly string Name;
        public readonly Vector3 Wrist;
        public readonly DigitPose Thumb, Index, Middle, Ring;

        public HandPose(string name, Vector3 wrist, DigitPose thumb, DigitPose index, DigitPose middle, DigitPose ring)
        {
            Name = name;
            Wrist = wrist;
            Thumb = thumb;
            Index = index;
            Middle = middle;
            Ring = ring;
        }

        public DigitPose Digit(HandDigit d)
        {
            switch (d)
            {
                case HandDigit.Thumb: return Thumb;
                case HandDigit.Index: return Index;
                case HandDigit.Middle: return Middle;
                default: return Ring;
            }
        }

        public override string ToString() { return Name; }
    }

    /// <summary>
    /// The game's hand poses, tuned by eye against the real gameplay situations. Values are
    /// degrees relative to the imported bind pose (see <see cref="DigitPose"/>). Left and
    /// right share a definition unless the hands genuinely do something different.
    /// </summary>
    public static class HandPoses
    {
        /// <summary>Curl-only digit: base, knuckle, middle and tip joints, plus spread/twist at the base.</summary>
        static DigitPose D(float b, float k, float m, float t, float spread = 0f, float twist = 0f)
        {
            return new DigitPose(new Vector3(b, spread, twist), new Vector3(k, 0f, 0f), new Vector3(m, 0f, 0f), new Vector3(t, 0f, 0f));
        }

        static Vector3 W(float flex, float deviation, float roll) { return new Vector3(flex, deviation, roll); }

        // ------------------------------------------------------------------ rest

        // Relaxed seated idle: the wrist rolled so the palm turns in toward the body (not at
        // the table), fingers drifting slightly away from the thumb, and a soft curl that grows
        // from index to ring. No wrist flex: seen from the side the hand hangs upright, its top
        // and fingertips at about the same depth (flex here pitched it ~12 degrees forward). The
        // right hand is rolled and curled a touch more than the left so the pair never reads as
        // mirrored statues.
        public static readonly HandPose IdleLeft = new HandPose("IdleLeft", W(0f, -6f, 42f),
            D(12f, 16f, 12f, 6f, -8f), D(5f, 16f, 18f, 12f), D(6f, 20f, 24f, 0f), D(8f, 26f, 30f, 22f));

        public static readonly HandPose IdleRight = new HandPose("IdleRight", W(1f, -4f, 50f),
            D(14f, 18f, 12f, 6f, -6f), D(6f, 18f, 20f, 14f), D(7f, 22f, 26f, 0f), D(8f, 28f, 32f, 24f));

        // ------------------------------------------------------------------ deck and cards

        // Note on the middle finger: its chain has only three bones and the first one starts
        // at the knuckle, so its knuckle bend goes in the FIRST value (on the other fingers the
        // first bone lies inside the palm and the knuckle is the second value).

        // On the way to the deck: the hand opens a little, ready to land.
        public static readonly HandPose DeckReach = new HandPose("DeckReach", W(0f, 0f, 0f),
            D(6f, 8f, 6f, 2f, -6f), D(0f, 10f, 10f, 6f), D(10f, 10f, 6f, 0f), D(0f, 12f, 12f, 8f));

        // Left hand waiting on the Draw Pile for a target: palm flat on the top card, the
        // knuckles on the far edge (the presenter places them there) and every finger draped
        // over it by its own amount, measured against the deck; the thumb stays level with the
        // palm so it never digs into the side.
        public static readonly HandPose DeckRest = new HandPose("DeckRest", W(-4f, -6f, 0f),
            D(6f, 10f, 8.5f, 5.5f, -6f), D(0f, 22f, 18f, 12f), D(30f, 25.5f, 16.5f, 0f), D(2f, 24f, 20f, 13f));

        // Right hand taking the first Charge card: the same measured drape over the far edge.
        public static readonly HandPose ChargeGrab = new HandPose("ChargeGrab", W(-4f, -4f, 0f),
            D(6f, 10f, 8.5f, 5.5f, -6f), D(0f, 22f, 18f, 12f), D(30f, 25.5f, 16.5f, 0f), D(2f, 24f, 20f, 13f));

        // Dragging the Charge card to its slot: the fingertips press a little harder.
        public static readonly HandPose ChargeCarry = new HandPose("ChargeCarry", W(-4f, -2f, 0f),
            D(8f, 12f, 10f, 6f, -6f), D(0f, 26f, 20f, 13f), D(32f, 27f, 17f, 0f), D(2f, 26f, 22f, 14f));

        // Left hand lifting the action card: fingers wrapped over its far edge, thumb under the near side.
        public static readonly HandPose CardHold = new HandPose("CardHold", W(-6f, 0f, 0f),
            D(28f, 18f, 12f, 6f, -12f), D(0f, 26f, 20f, 13f), D(32f, 27f, 17f, 0f), D(2f, 26f, 22f, 14f));

        // Letting the card fly: the wrist lifts and the fingers flick open and fan out.
        public static readonly HandPose CardRelease = new HandPose("CardRelease", W(-12f, 0f, 0f),
            D(-4f, -10f, -6f, 0f, 10f), D(-4f, -16f, -12f, -6f, 7f), D(-14f, -12f, -4f, 0f), D(-4f, -16f, -12f, -6f, -7f));

        // ------------------------------------------------------------------ weapon

        // Reaching for the holstered pistol: the hand half-closes on the way, so it does not
        // snap shut when the weapon arrives.
        public static readonly HandPose WeaponReach = new HandPose("WeaponReach", W(0f, 0f, 0f),
            D(12f, 15f, 10f, 8f), D(2f, 12f, 15f, 10f), D(4f, 20f, 22f, 0f), D(4f, 22f, 24f, 20f));

        // Holding the pistol. The hand sits OUTSIDE the weapon (WeaponGripAnchor puts the palm flat
        // against the right side of the stock, fingers along the barrel, thumb side up by the
        // hammer); from there middle and ring wrap under and round the stock, the index rests by
        // the trigger guard and the thumb opposes over the top. Moderate curls: past ~60 degrees
        // the source rig pinches at the knuckles. The stock sits about 1 cm into the puffy palm so
        // the fingers close round it instead of hovering beside it.
        public static readonly HandPose WeaponGrip = new HandPose("WeaponGrip", W(0f, 0f, 0f),
            D(25f, 34f, 24f, 18f), D(5f, 30f, 34f, 22f), D(8f, 50f, 52f, 0f), D(8f, 55f, 55f, 45f));

        // The shot: only the trigger finger squeezes; the rest of the grip stays put.
        public static readonly HandPose WeaponFire = new HandPose("WeaponFire", W(0f, 0f, 0f),
            D(25f, 34f, 24f, 18f), D(5f, 42f, 48f, 32f), D(8f, 50f, 52f, 0f), D(8f, 55f, 55f, 45f));

        // ------------------------------------------------------------------ beer

        // Approaching the mug: the fingers start curling for the handle on the way.
        public static readonly HandPose MugReach = new HandPose("MugReach", W(0f, 0f, 0f),
            D(8f, 14f, 10f, 6f, -4f), D(3f, 28f, 26f, 18f), D(26f, 26f, 18f, 0f), D(3f, 28f, 26f, 18f));

        // Holding the mug by its handle: a squeezed fist, palm against the outside of the handle,
        // index, middle and ring curled through and round it (the puffy fingers overlap the
        // narrow handle gap a little), thumb resting over the top of the handle. Held unchanged
        // through the drink. Middle's knuckle bend is its first value (see note above).
        public static readonly HandPose MugGrip = new HandPose("MugGrip", W(0f, 0f, 0f),
            D(14f, 24f, 18f, 10f, -4f), D(5f, 60f, 60f, 40f), D(60f, 60f, 40f, 0f), D(5f, 60f, 60f, 40f));

        // ------------------------------------------------------------------ waiter

        // The waiter's tray hand: open palm flat under the tray, fingers fanned slightly to
        // spread the load, thumb turned out. No digit curls up into the tray.
        public static readonly HandPose TraySupport = new HandPose("TraySupport", W(0f, 0f, 0f),
            D(-4f, -6f, -4f, 0f, 12f), D(0f, -4f, 4f, 6f, 6f), D(0f, -3f, 4f, 0f), D(0f, -2f, 6f, 8f, -7f));

        // Reaching for a mug on the tray from above: the hand stays open and relaxed.
        public static readonly HandPose WaiterMugReach = new HandPose("WaiterMugReach", W(0f, 0f, 0f),
            D(6f, 4f, 3f, 2f), D(0f, 6f, 8f, 4f), D(6f, 8f, 4f, 0f), D(0f, 6f, 8f, 6f));

        // The waiter guiding a mug from above: palm flat a few millimetres over the rim, fingers
        // gently open and only just curving down past the far rim. Deliberately not a closed
        // grip: nothing wraps the mug body or handle.
        public static readonly HandPose WaiterMugGrip = new HandPose("WaiterMugGrip", W(0f, 0f, 0f),
            D(10f, 8f, 6f, 4f), D(0f, 14f, 16f, 10f), D(14f, 16f, 10f, 0f), D(2f, 14f, 16f, 10f));

        // ------------------------------------------------------------------ lookup

        public static HandPose Get(HandPoseId id, HandSide side)
        {
            switch (id)
            {
                case HandPoseId.Idle: return side == HandSide.Left ? IdleLeft : IdleRight;
                case HandPoseId.DeckReach: return DeckReach;
                case HandPoseId.DeckRest: return DeckRest;
                case HandPoseId.ChargeGrab: return ChargeGrab;
                case HandPoseId.ChargeCarry: return ChargeCarry;
                case HandPoseId.CardHold: return CardHold;
                case HandPoseId.CardRelease: return CardRelease;
                case HandPoseId.WeaponReach: return WeaponReach;
                case HandPoseId.WeaponGrip: return WeaponGrip;
                case HandPoseId.WeaponFire: return WeaponFire;
                case HandPoseId.MugReach: return MugReach;
                case HandPoseId.MugGrip: return MugGrip;
                case HandPoseId.TraySupport: return TraySupport;
                case HandPoseId.WaiterMugReach: return WaiterMugReach;
                case HandPoseId.WaiterMugGrip: return WaiterMugGrip;
                default: return side == HandSide.Left ? IdleLeft : IdleRight;
            }
        }
    }
}
