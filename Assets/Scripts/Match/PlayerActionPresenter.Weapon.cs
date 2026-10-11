using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shields.Players;

namespace Shields.Match
{
    /// <summary>
    /// Weapon presentation for Attack and Charged Attack: the same right hand that pulls
    /// the Charge card draws the weapon from the hip, aims it where the player looks while
    /// a target is chosen, locks onto the chosen player once known, fires, and holsters.
    ///
    /// Presentation only. Nothing here decides a target or an outcome: free aim follows
    /// the local view, and the lock comes from the target the player clicked (cosmetic,
    /// reversible) or from the public action result every machine receives.
    /// </summary>
    public partial class PlayerActionPresenter
    {
        [Header("Weapon Timing (seconds)")]
        [Tooltip("Hand from rest to the holstered weapon. Draw time is this plus the raise.")]
        [SerializeField] float drawReachDuration = 0.12f;

        [Tooltip("Weapon from the hip up into the aiming pose.")]
        [SerializeField] float drawRaiseDuration = 0.2f;

        [Tooltip("How long a confirmed lock stays on target before the weapon fires.")]
        [SerializeField] float fireDelay = 0.12f;

        [Tooltip("How long the weapon stays on target after firing, before holstering.")]
        [SerializeField] float afterFireHold = 0.35f;

        [Tooltip("Weapon back down to the hip. Holster time is this plus the hand's return.")]
        [SerializeField] float holsterLowerDuration = 0.2f;

        [SerializeField] float holsterReturnDuration = 0.12f;

        [Tooltip("A lock the host has not confirmed is dropped after this long.")]
        [SerializeField] float unconfirmedLockTimeout = 3f;

        [Header("Weapon Aiming")]
        [Tooltip("Where the grip is held while aiming, from the right shoulder, in the aim's yaw frame (x right, y up, z forward).")]
        [SerializeField] Vector3 aimGripOffset = new Vector3(0.05f, 0.04f, 0.4f);

        [Tooltip("The muzzle stops at least this far short of the aim point, so a close neighbour is never poked; the reach shortens to suit.")]
        [SerializeField] float muzzleClearance = 0.4f;

        [Tooltip("Shortest forward reach of the aiming grip from the shoulder.")]
        [SerializeField] float minAimReach = 0.15f;

        [Tooltip("The aiming hand swings at most this far left or right of the seat's forward; the muzzle still points at the target.")]
        [SerializeField] float aimSwingLimit = 55f;

        [Tooltip("How quickly the held weapon follows the aim point. Higher is snappier.")]
        [SerializeField] float aimSharpness = 18f;

        [Tooltip("Muzzle within this angle of the target counts as locked on.")]
        [SerializeField] float lockedAngle = 2f;

        [Tooltip("Upper-body target point: 0 is the body's centre, 1 the head pivot.")]
        [Range(0f, 1f)]
        [SerializeField] float targetUpperBody = 0.6f;

        [Tooltip("Free aim ray length, and the distance used when it hits nothing.")]
        [SerializeField] float freeAimRange = 25f;
        [SerializeField] float freeAimFallbackDistance = 6f;

        const string MuzzleName = "MuzzleAnchor";

        sealed class WeaponRig
        {
            public int Seat;
            public PlayerCharacter Player;
            public Transform Hand;
            public Transform Grip;
            public Transform Weapon;
            public Transform Holster;
            public Transform Muzzle;
            public Pose Rest;
            public Coroutine Routine;

            public bool FollowCamera;
            public int LockTarget = -1;
            public bool LockConfirmed;
            public float LockedSince = -1f;
            public float LockRequestedAt;
            public bool HolsterRequested;

            // From the confirmed public result: the damage this shot dealt, and the seed
            // every machine derives from that result for the shot's random details.
            public int Damage;
            public int Seed;

            public bool Fired;
            public float FiredAt;
            public float Recoil;
            public float HitStop;
        }

        readonly Dictionary<int, WeaponRig> _weapons = new Dictionary<int, WeaponRig>();

        // ------------------------------------------------------------------ events

        /// <summary>
        /// The local player entered Attack or Charged Attack targeting: draw now and aim
        /// where the camera looks. Idempotent while the weapon is already out.
        /// </summary>
        public void BeginWeaponAim(int seat, bool followCamera)
        {
            WeaponRig rig;
            if (_weapons.TryGetValue(seat, out rig) && !rig.HolsterRequested && rig.Routine != null)
            {
                rig.FollowCamera = followCamera;
                return;
            }

            rig = StartWeapon(seat);
            if (rig != null) rig.FollowCamera = followCamera;
        }

        /// <summary>
        /// Targeting ended without a confirmed action (cancelled, switched, refused): put
        /// the weapon away. A lock the result confirmed finishes on its own.
        /// </summary>
        public void EndWeaponAim(int seat)
        {
            WeaponRig rig;
            if (!_weapons.TryGetValue(seat, out rig) || rig.Routine == null || rig.LockConfirmed) return;

            rig.HolsterRequested = true;
        }

        /// <summary>
        /// Aims at the selected player and stops following the view. Confirmed comes from
        /// the public action result: once on target the weapon fires and is holstered.
        /// Unconfirmed is the local click before the host answers, and stays reversible.
        /// </summary>
        public void LockWeapon(int seat, int target, bool confirmed)
        {
            Lock(seat, target, confirmed);
        }

        /// <summary>
        /// A resolved Attack or Charged Attack from the public result: lock, fire, and when
        /// it dealt damage, the hit on the target. Damage only ever comes from the confirmed
        /// result, so nothing here predicts or decides a hit. The seed is derived from that
        /// same result, so every machine plays the identical shot.
        /// </summary>
        public void PlayAttack(int seat, int target, int damage, int seed)
        {
            WeaponRig rig = Lock(seat, target, true);
            if (rig != null)
            {
                rig.Damage = Mathf.Max(0, damage);
                rig.Seed = seed;
                return;
            }

            // No weapon to fire with: the hit still shows.
            PlayerCharacter shooter = _table != null ? _table.GetPlayer(seat) : null;
            if (damage > 0 && shooter != null) PlayHit(target, shooter.transform.position, damage, seed);
        }

        WeaponRig Lock(int seat, int target, bool confirmed)
        {
            WeaponRig rig;
            if (!_weapons.TryGetValue(seat, out rig) || rig.Routine == null || rig.HolsterRequested || rig.Fired)
                rig = StartWeapon(seat);

            if (rig == null) return null;

            if (rig.LockTarget != target) rig.LockedSince = -1f;

            rig.LockTarget = target;
            rig.LockRequestedAt = Time.time;
            rig.LockConfirmed |= confirmed;
            rig.FollowCamera = false;
            return rig;
        }

        // ------------------------------------------------------------------ rig

        WeaponRig StartWeapon(int seat)
        {
            PlayerCharacter player = _table != null ? _table.GetPlayer(seat) : null;
            if (player == null || player.RightHandAnchor == null || player.WeaponGripAnchor == null ||
                player.Weapon == null || player.WeaponHolsterAnchor == null)
                return null;

            // One action owns the right hand at a time.
            StopMotion(seat);

            WeaponRig previous;
            bool carried = false;
            if (_weapons.TryGetValue(seat, out previous) && previous.Routine != null)
            {
                StopCoroutine(previous.Routine);
                carried = previous.Weapon.parent == previous.Grip;
            }

            WeaponRig rig = new WeaponRig
            {
                Seat = seat,
                Player = player,
                Hand = player.RightHandAnchor,
                Grip = player.WeaponGripAnchor,
                Weapon = player.Weapon,
                Holster = player.WeaponHolsterAnchor,
                Muzzle = player.Weapon.Find(MuzzleName),
                Rest = RestPoseOf(player.RightHandAnchor)
            };

            _weapons[seat] = rig;
            rig.Routine = StartCoroutine(WeaponRoutine(rig, carried));
            return rig;
        }

        /// <summary>
        /// Instantly holsters this seat's weapon and gives the hand back at rest. A
        /// confirmed hit that had not been fired yet still lands on its target, unless
        /// the whole table is being cleared.
        /// </summary>
        void ReleaseWeapon(int seat, bool landPendingHit)
        {
            WeaponRig rig;
            if (!_weapons.TryGetValue(seat, out rig)) return;

            if (rig.Routine != null) StopCoroutine(rig.Routine);
            if (landPendingHit && rig.LockConfirmed && rig.Damage > 0 && !rig.Fired)
            {
                rig.Fired = true;
                PlayHit(rig.LockTarget, rig.Player != null ? rig.Player.transform.position : Vector3.zero, rig.Damage, rig.Seed);
            }

            Holster(rig);
            RestoreHand(rig);
            _weapons.Remove(seat);
        }

        void ReleaseAllWeapons()
        {
            foreach (int seat in new List<int>(_weapons.Keys)) ReleaseWeapon(seat, false);
        }

        IEnumerator WeaponRoutine(WeaponRig rig, bool carried)
        {
            // Draw: the hand fetches the weapon from the hip, unless it already holds it
            // (a new targeting started while the last one was being put away).
            if (!carried)
            {
                yield return MoveHandToGripPose(rig, () => HolsterPose(rig), drawReachDuration, true);
                if (rig.Hand == null) yield break;

                Attach(rig);
            }

            // Raise into the aim, then follow it; it never ends on its own except through
            // a confirmed lock firing, a holster request or a stale unconfirmed lock.
            Pose raisedFrom = GripPose(rig);
            float raised = 0f;

            // The smoothed aim, without recoil: recoil is layered on top of it and never
            // fed back into the smoothing, so the gun always springs back onto the target.
            Pose held = raisedFrom;

            while (rig.Hand != null)
            {
                Vector3 aimPoint = AimPoint(rig);
                Pose aim = AimGripPose(rig, aimPoint);

                if (raised < drawRaiseDuration)
                {
                    raised += Time.deltaTime;
                    float e = EaseInOutCubic(raised / drawRaiseDuration);
                    held = new Pose(Vector3.LerpUnclamped(raisedFrom.position, aim.position, e),
                        Quaternion.Slerp(raisedFrom.rotation, aim.rotation, e));
                }
                else
                {
                    float k = 1f - Mathf.Exp(-aimSharpness * Time.deltaTime);
                    held = new Pose(Vector3.Lerp(held.position, aim.position, k),
                        Quaternion.Slerp(held.rotation, aim.rotation, k));
                }

                float kicked = rig.Fired ? RecoilEnvelope(Time.time - rig.FiredAt, rig.Recoil, rig.HitStop) : 0f;
                SetGripPose(rig, Recoiled(held, rig.Recoil, kicked));

                if (rig.HolsterRequested) break;

                if (rig.LockTarget >= 0)
                {
                    if (rig.LockedSince < 0f && raised >= drawRaiseDuration && OnTarget(rig, aimPoint))
                        rig.LockedSince = Time.time;

                    // A confirmed shot always goes off, even if the aim never settles.
                    if (rig.LockConfirmed && rig.LockedSince < 0f && Time.time - rig.LockRequestedAt >= unconfirmedLockTimeout)
                        rig.LockedSince = Time.time;

                    if (rig.LockConfirmed && rig.LockedSince >= 0f)
                    {
                        if (!rig.Fired && Time.time - rig.LockedSince >= fireDelay)
                        {
                            rig.Fired = true;
                            rig.FiredAt = Time.time;
                            Fire(rig);
                        }

                        // Held until the recoil has fully settled back onto the target.
                        if (rig.Fired && Time.time - rig.FiredAt >= Mathf.Max(afterFireHold, RecoilLength(rig.Recoil, rig.HitStop)))
                            break;
                    }

                    if (!rig.LockConfirmed && Time.time - rig.LockRequestedAt >= unconfirmedLockTimeout) break;
                }

                yield return null;
            }

            if (rig.Hand == null) yield break;

            // Holster: weapon back to the hip, released there, hand home.
            yield return MoveHandToGripPose(rig, () => HolsterPose(rig), holsterLowerDuration, false);
            if (rig.Hand == null) yield break;

            Holster(rig);

            Vector3 from = rig.Hand.localPosition;
            Quaternion fromRotation = rig.Hand.localRotation;
            for (float elapsed = 0f; elapsed < holsterReturnDuration; elapsed += Time.deltaTime)
            {
                if (rig.Hand == null) yield break;

                float e = EaseInOutCubic(elapsed / holsterReturnDuration);
                rig.Hand.localPosition = Vector3.LerpUnclamped(from, rig.Rest.position, e);
                rig.Hand.localRotation = Quaternion.Slerp(fromRotation, rig.Rest.rotation, e);
                yield return null;
            }

            RestoreHand(rig);

            WeaponRig owner;
            if (_weapons.TryGetValue(rig.Seat, out owner) && owner == rig) _weapons.Remove(rig.Seat);
        }

        /// <summary>
        /// Moves the hand so its grip anchor arrives at a (possibly moving) world pose.
        /// From rest it eases out of the rest pose; with the weapon in hand it carries it.
        /// </summary>
        IEnumerator MoveHandToGripPose(WeaponRig rig, System.Func<Pose> target, float duration, bool easeOut)
        {
            Pose from = GripPose(rig);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (rig.Hand == null) yield break;

                float t = elapsed / duration;
                float e = easeOut ? EaseOutCubic(t) : EaseInOutCubic(t);
                Pose to = target();
                SetGripPose(rig, new Pose(Vector3.LerpUnclamped(from.position, to.position, e),
                    Quaternion.Slerp(from.rotation, to.rotation, e)));
                yield return null;
            }

            if (rig.Hand != null) SetGripPose(rig, target());
        }

        /// <summary>
        /// The firing moment of a confirmed shot, on target. A damaging shot discharges
        /// at the muzzle, kicks the gun by an amount that grows with the damage, and lands
        /// on the target in the same frame; both freeze for the same hit-stop.
        /// </summary>
        void Fire(WeaponRig rig)
        {
            if (rig.Damage <= 0)
            {
                // Zero damage: nothing hits. The failed-attack presentation belongs here.
                return;
            }

            Transform muzzle = rig.Muzzle != null ? rig.Muzzle : rig.Weapon;
            rig.Recoil = RecoilFor(rig.Damage);
            rig.HitStop = HitStopFor(rig.Damage);

            PlayMuzzleFlash(muzzle, rig.Damage, rig.Seed);
            PlayHit(rig.LockTarget, muzzle.position, rig.Damage, rig.Seed);

            PlayerCharacter target = _table != null ? _table.GetPlayer(rig.LockTarget) : null;
            if (target != null) StartShooterLean(rig.Player, target.transform.position - rig.Player.transform.position, rig.Recoil, rig.HitStop);
        }

        // ------------------------------------------------------------------ attach

        /// <summary>
        /// Moves the weapon from the hip into the hand. The hand stands exactly on the
        /// holstered grip at this moment, so the weapon keeps its world pose.
        /// </summary>
        static void Attach(WeaponRig rig)
        {
            rig.Weapon.SetParent(rig.Grip, false);
            rig.Weapon.localPosition = Vector3.zero;
            rig.Weapon.localRotation = Quaternion.identity;
            rig.Weapon.localScale = Vector3.one;
        }

        /// <summary>Puts the weapon back at the hip, exactly on the holster anchor.</summary>
        static void Holster(WeaponRig rig)
        {
            if (rig.Weapon == null || rig.Holster == null) return;

            rig.Weapon.SetParent(rig.Holster, false);
            rig.Weapon.localPosition = Vector3.zero;
            rig.Weapon.localRotation = Quaternion.identity;
            rig.Weapon.localScale = Vector3.one;
        }

        static void RestoreHand(WeaponRig rig)
        {
            if (rig.Hand == null) return;

            rig.Hand.localPosition = rig.Rest.position;
            rig.Hand.localRotation = rig.Rest.rotation;
        }

        // ------------------------------------------------------------------ aiming

        /// <summary>
        /// Where the weapon points. Locked: the selected player's upper body. Otherwise the
        /// local crosshair (centre-screen ray, presentation only), or for anyone without a
        /// camera here, straight along their head's look.
        /// </summary>
        Vector3 AimPoint(WeaponRig rig)
        {
            if (rig.LockTarget >= 0)
            {
                PlayerCharacter target = _table != null ? _table.GetPlayer(rig.LockTarget) : null;
                if (target != null) return target.UpperBodyAimPoint(targetUpperBody);
            }

            Camera view = rig.FollowCamera ? Camera.main : null;
            Ray ray = view != null
                ? view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(rig.Player.HeadPivot != null ? rig.Player.HeadPivot.position : rig.Player.transform.position,
                    rig.Player.HeadPivot != null ? rig.Player.HeadPivot.forward : rig.Player.transform.forward);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, freeAimRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.distance > 0.3f)
                return hit.point;

            return ray.origin + ray.direction * freeAimFallbackDistance;
        }

        /// <summary>
        /// The held grip's world pose for an aim point: in front of the right shoulder,
        /// swung toward the aim (within limits so it never crosses the body), barrel on
        /// the line to the aim point.
        /// </summary>
        Pose AimGripPose(WeaponRig rig, Vector3 aimPoint)
        {
            Transform seat = rig.Player.transform;
            Transform shoulder = rig.Hand.parent != null ? rig.Hand.parent : seat;

            Vector3 flat = aimPoint - seat.position;
            flat.y = 0f;
            float yaw = flat.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(seat.forward, flat, Vector3.up) : 0f;
            yaw = Mathf.Clamp(yaw, -aimSwingLimit, aimSwingLimit);

            Quaternion frame = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.LookRotation(Flat(seat.forward), Vector3.up);

            // Reach out less for a close target, so the barrel ends short of it.
            float barrel = rig.Muzzle != null ? rig.Weapon.InverseTransformPoint(rig.Muzzle.position).z : 0f;
            Vector3 toAim = aimPoint - shoulder.position;
            toAim.y = 0f;
            Vector3 offset = aimGripOffset;
            offset.z = Mathf.Clamp(toAim.magnitude - barrel - muzzleClearance, minAimReach, aimGripOffset.z);

            Vector3 grip = shoulder.position + frame * offset;

            return new Pose(grip, BarrelRotation(rig, grip, aimPoint));
        }

        /// <summary>
        /// Rotation that puts the barrel line, not the grip, through the aim point. The
        /// barrel sits above the grip, so one correction step removes that offset.
        /// </summary>
        Quaternion BarrelRotation(WeaponRig rig, Vector3 grip, Vector3 aimPoint)
        {
            Quaternion rotation = LookOrKeep(aimPoint - grip, Quaternion.identity);
            if (rig.Muzzle == null) return rotation;

            Vector3 muzzle = rig.Weapon.InverseTransformPoint(rig.Muzzle.position);
            Vector3 barrelOffset = rotation * new Vector3(muzzle.x, muzzle.y, 0f);
            return LookOrKeep(aimPoint - (grip + barrelOffset), rotation);
        }

        bool OnTarget(WeaponRig rig, Vector3 aimPoint)
        {
            Transform muzzle = rig.Muzzle != null ? rig.Muzzle : rig.Weapon;
            return Vector3.Angle(rig.Weapon.forward, aimPoint - muzzle.position) <= lockedAngle;
        }

        /// <summary>The hand pose at which the grip anchor would sit on the holstered weapon.</summary>
        static Pose HolsterPose(WeaponRig rig)
        {
            return new Pose(rig.Holster.position, rig.Holster.rotation);
        }

        static Pose GripPose(WeaponRig rig)
        {
            return new Pose(rig.Grip.position, rig.Grip.rotation);
        }

        /// <summary>Places the hand so that its grip anchor lands on a world pose.</summary>
        static void SetGripPose(WeaponRig rig, Pose grip)
        {
            Quaternion gripLocal = Quaternion.Inverse(rig.Hand.rotation) * rig.Grip.rotation;
            rig.Hand.rotation = grip.rotation * Quaternion.Inverse(gripLocal);
            rig.Hand.position += grip.position - rig.Grip.position;
        }

        static Quaternion LookOrKeep(Vector3 direction, Quaternion fallback)
        {
            return direction.sqrMagnitude > 0.000001f ? Quaternion.LookRotation(direction, Vector3.up) : fallback;
        }

        static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
        }
    }
}
