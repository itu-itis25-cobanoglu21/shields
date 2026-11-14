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
        WaiterMugGrip,
        MiddleFinger,
        Point,
        HandshakeOffer,
        HandshakeGrip
    }

    /// <summary>
    /// One digit's offsets, in degrees, for its three bones (base at the knuckle, middle,
    /// tip; for the thumb the base is the bone from the wrist): x = curl toward the palm,
    /// y = spread toward the thumb side, z = twist (palm face toward the thumb side). The
    /// same numbers mirror correctly on a left and a right hand.
    /// </summary>
    [System.Serializable]
    public struct DigitPose
    {
        public Vector3 Base, Mid, Tip;

        public DigitPose(Vector3 baseJoint, Vector3 mid, Vector3 tip)
        {
            Base = baseJoint;
            Mid = mid;
            Tip = tip;
        }

        public Vector3 Joint(int i)
        {
            switch (i)
            {
                case 0: return Base;
                case 1: return Mid;
                default: return Tip;
            }
        }
    }

    /// <summary>
    /// A whole-hand pose: an optional authored Blender Action it starts from (null = the bind
    /// pose), then a wrist offset (x flex, y deviation toward the thumb, z roll) and four
    /// digits' offsets on top.
    /// </summary>
    public sealed class HandPose
    {
        public readonly string Name;
        public readonly string Authored;
        public readonly Vector3 Wrist;
        public readonly DigitPose Thumb, Index, Middle, Ring;

        public HandPose(string name, Vector3 wrist, DigitPose thumb, DigitPose index, DigitPose middle, DigitPose ring)
            : this(name, null, wrist, thumb, index, middle, ring)
        {
        }

        public HandPose(string name, string authored, Vector3 wrist, DigitPose thumb, DigitPose index, DigitPose middle, DigitPose ring)
        {
            Name = name;
            Authored = authored;
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
    /// The game's hand poses for the final Blender hand.
    ///
    /// Where the hand's FBX has an authored Action for a situation it is used as is: the
    /// rest pose, Point, Middle Finger and the handshake offer. Those Actions are the source
    /// of truth for the finger shapes; only where the hand points comes from the presenters.
    /// Gameplay situations without an authored Action (deck, cards, weapon, mug, waiter) keep
    /// the poses tuned for them, as degrees relative to the bind pose (see <see cref="DigitPose"/>).
    /// Left and right share a definition unless the hands genuinely do something different.
    /// </summary>
    public static class HandPoses
    {
        // ------------------------------------------------------------------ authored Actions

        // Names of the Blender Actions in StylizedHand_Poses_FINAL(.fbx / _Left.fbx).
        // HandRig ignores Blender's ".001" duplicate suffix when matching them.
        public const string RestAction = "Rest_Default";
        public const string PointAction = "Pose_Point";
        public const string MiddleFingerAction = "Pose_MiddleFinger";
        public const string HandshakeAction = "Pose_HandshakeReady";

        // Authored but not used by any game state yet.
        public const string ThumbsUpAction = "Pose_ThumbsUp";

        /// <summary>Digit offsets: base, middle and tip curl, plus spread/twist at the base.</summary>
        static DigitPose D(float b, float m, float t, float spread = 0f, float twist = 0f)
        {
            return new DigitPose(new Vector3(b, spread, twist), new Vector3(m, 0f, 0f), new Vector3(t, 0f, 0f));
        }

        static readonly DigitPose None = new DigitPose(Vector3.zero, Vector3.zero, Vector3.zero);

        static Vector3 W(float flex, float deviation, float roll) { return new Vector3(flex, deviation, roll); }

        static HandPose Authored(string name, string action, Vector3 wrist)
        {
            return new HandPose(name, action, wrist, None, None, None, None);
        }

        // ------------------------------------------------------------------ rest

        // The authored rest pose. The wrist roll is where the floating hand hangs at the seat
        // (the palm turned in toward the body, not at the table), the right a touch more than
        // the left so the pair never reads as mirrored statues.
        public static readonly HandPose IdleLeft = Authored("IdleLeft", RestAction, W(0f, -6f, 42f));
        public static readonly HandPose IdleRight = Authored("IdleRight", RestAction, W(1f, -4f, 50f));

        // ------------------------------------------------------------------ deck and cards

        // On the way to the deck: the hand opens a little, ready to land.
        public static readonly HandPose DeckReach = new HandPose("DeckReach", W(0f, 0f, 0f),
            D(6f, 8f, 6f, -6f), D(10f, 10f, 6f), D(10f, 10f, 6f), D(12f, 12f, 8f));

        // Left hand waiting on the Draw Pile for a target: the fingers lie along the top card,
        // knuckles just inside its near edge and the palm off the deck toward the player (the
        // presenter places them there), with only a soft curl so the pads rest on the card; the
        // thumb stays level with the palm so it never digs into the side.
        public static readonly HandPose DeckRest = new HandPose("DeckRest", W(-4f, -6f, 0f),
            D(6f, 10f, 8f, -6f), D(4f, 6f, 10f), D(4f, 6f, 10f), D(4f, 6f, 10f));

        // Right hand taking the first Charge card: the same fingers on the top card.
        public static readonly HandPose ChargeGrab = new HandPose("ChargeGrab", W(-4f, -4f, 0f),
            D(6f, 10f, 8f, -6f), D(4f, 6f, 10f), D(4f, 6f, 10f), D(4f, 6f, 10f));

        // Dragging the Charge card to its slot: the fingertips press a little harder.
        public static readonly HandPose ChargeCarry = new HandPose("ChargeCarry", W(-4f, -2f, 0f),
            D(8f, 12f, 10f, -6f), D(6f, 8f, 13f), D(6f, 8f, 13f), D(6f, 8f, 13f));

        // Left hand lifting the action card: fingers pressing on top of it, thumb under the near side.
        public static readonly HandPose CardHold = new HandPose("CardHold", W(-6f, 0f, 0f),
            D(28f, 18f, 12f, -12f), D(8f, 10f, 14f), D(8f, 10f, 14f), D(8f, 10f, 14f));

        // Letting the card fly: the wrist lifts and the fingers flick open and fan out.
        public static readonly HandPose CardRelease = new HandPose("CardRelease", W(-12f, 0f, 0f),
            D(-4f, -10f, -6f, 10f), D(-20f, -12f, -6f, 7f), D(-14f, -12f, -4f), D(-20f, -12f, -6f, -7f));

        // ------------------------------------------------------------------ weapon

        // Reaching for the holstered pistol: the hand half-closes on the way, so it does not
        // snap shut when the weapon arrives.
        public static readonly HandPose WeaponReach = new HandPose("WeaponReach", W(0f, 0f, 0f),
            D(15f, 20f, 12f), D(22f, 28f, 18f), D(40f, 42f, 25f), D(42f, 42f, 25f));

        // Holding the pistol. The hand sits OUTSIDE the weapon (WeaponGripAnchor puts the palm flat
        // against the right side of the stock, fingers along the barrel, thumb side up by the
        // hammer); from there middle and ring wrap under and round to the far side of the stock,
        // the index rests by the trigger guard and the thumb lies over the top. The final hand's
        // long fingers need the full wrap; with less, middle and ring hang open below the stock.
        public static readonly HandPose WeaponGrip = new HandPose("WeaponGrip", W(0f, 0f, 0f),
            D(30f, 40f, 25f), D(45f, 55f, 35f), D(80f, 85f, 50f), D(85f, 85f, 50f));

        // The shot: only the trigger finger squeezes; the rest of the grip stays put.
        public static readonly HandPose WeaponFire = new HandPose("WeaponFire", W(0f, 0f, 0f),
            D(30f, 40f, 25f), D(57f, 68f, 45f), D(80f, 85f, 50f), D(85f, 85f, 50f));

        // ------------------------------------------------------------------ beer

        // Approaching the mug: the fingers shape for the barrel on the way, a little open.
        public static readonly HandPose MugReach = new HandPose("MugReach", W(0f, 0f, 0f),
            D(24f, 20f, 12f, -10f), D(29f, 33f, 23f), D(28f, 32f, 23f), D(28f, 33f, 23f));

        // Holding the mug by its barrel (its handle faces away, see DrinkAreaLayout.MugYaw): the
        // palm flat on the barrel's side, index, middle and ring wrapped round its far side and
        // the thumb round the near side. Solved per digit against the mug's real outline (the
        // mug's LeftHandGripAnchor places the hand) so every digit and the palm touch the barrel,
        // about 0.3 mm off its surface: nothing floats, nothing sinks in. Held through the drink.
        public static readonly HandPose MugGrip = new HandPose("MugGrip", W(0f, 0f, 0f),
            D(40.7f, 32.6f, 20.3f, -10f), D(48f, 55.2f, 38.4f), D(47f, 54f, 37.6f), D(47.1f, 54.2f, 37.7f));

        // ------------------------------------------------------------------ waiter

        // The waiter's tray hand: open palm flat under the tray, fingers fanned slightly to
        // spread the load, thumb turned out. Solved against the tray's underside (with the Waiter
        // prefab's tray-hand placement) so the palm and every digit touch it, about 0.3 mm off:
        // nothing floats under the tray and nothing pokes into it.
        public static readonly HandPose TraySupport = new HandPose("TraySupport", W(0f, 0f, 0f),
            D(7.5f, -0.2f, -0.5f, 12f), D(3.5f, 7.8f, 8.3f, 6f), D(4.6f, -0.7f, 5.4f), D(1.6f, 7.8f, 9.1f, -7f));

        // Reaching for a mug from above: the claw half closed, every digit still above the rim.
        public static readonly HandPose WaiterMugReach = new HandPose("WaiterMugReach", W(0f, 0f, 0f),
            D(32f, 24f, 13f, -25f), D(32f, 29f, 19f), D(27f, 25f, 16f), D(30f, 27f, 18f));

        // The waiter holding a mug from above like a claw: the palm just over the cup, index,
        // middle and ring draped over the rim on the side away from the handle and the thumb
        // down the near side. Solved per digit against the mug's real outline (WaiterPresenter's
        // topCarryEuler/topCarryOffset place the hand) so all four digits touch the mug, about
        // 0.3 mm off it, and none enters the cup or meets the handle.
        public static readonly HandPose WaiterMugGrip = new HandPose("WaiterMugGrip", W(0f, 0f, 0f),
            D(63.8f, 47.8f, 26.8f, -25f), D(64.7f, 58.2f, 38.8f), D(54.4f, 49f, 32.6f), D(59.1f, 53.2f, 35.5f));

        // ------------------------------------------------------------------ emotes

        // The authored emote Actions, as the artist posed them. Where the hand goes and which way
        // it faces is PlayerEmotePresenter's job.

        // Left hand: middle finger straight, index and ring folded into the palm, thumb across them.
        public static readonly HandPose MiddleFinger = Authored("MiddleFinger", MiddleFingerAction, Vector3.zero);

        // Right hand: index straight, middle and ring folded under, thumb curled over them.
        public static readonly HandPose Point = Authored("Point", PointAction, Vector3.zero);

        // Right hand offered for a handshake: open and relaxed, the thumb spread up and away.
        public static readonly HandPose HandshakeOffer = Authored("HandshakeOffer", HandshakeAction, Vector3.zero);

        // Clasping the other player's right hand. There is no authored grip Action, so this is
        // the authored offer with the fingers wrapped round the other hand and the thumb lying
        // over its back: the offer closes, it does not change shape.
        public static readonly HandPose HandshakeGrip = new HandPose("HandshakeGrip", HandshakeAction, W(0f, 0f, 0f),
            D(6f, 6f, 4f), D(38f, 28f, 26f), D(36f, 30f, 24f), D(40f, 34f, 24f));

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
                case HandPoseId.MiddleFinger: return MiddleFinger;
                case HandPoseId.Point: return Point;
                case HandPoseId.HandshakeOffer: return HandshakeOffer;
                case HandPoseId.HandshakeGrip: return HandshakeGrip;
                default: return side == HandSide.Left ? IdleLeft : IdleRight;
            }
        }
    }
}
