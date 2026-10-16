using System.Collections.Generic;
using UnityEngine;
using Shields.Players;

namespace Shields.Match
{
    /// <summary>
    /// What a damaging shot looks like: a discharge and a flash of light at the muzzle, a
    /// recoil that grows with the damage, a blood burst on the target's upper body, a
    /// brief white flash of the target, and a snappy roly-poly reaction: the upper body
    /// snaps away from the shot around the fixed base of the body, holds for an instant
    /// (hit-stop), then rocks back past upright a few times and settles.
    ///
    /// Everything here is one world event. Its only inputs are the confirmed public result
    /// (who, whom, how much damage, and a seed derived from it) and the seat geometry, so
    /// every machine plays the same thing in the same place and each viewer simply sees it
    /// from their own seat. Nothing is aligned to a viewer's camera and every particle
    /// random number comes from the shared seed.
    ///
    /// Presentation only. The reaction never moves the character root, its chair or its
    /// targeting collider, and is always computed from the captured rest poses, so
    /// repeated hits cannot drift.
    ///
    /// The same parts also carry the anticipation lean of a targeted action: a small
    /// tilt toward the table centre around the same fixed base. The lean sits underneath
    /// any reaction, so a recoil or a hit plays on top of it instead of cancelling it.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public partial class PlayerActionPresenter
    {
        [Header("Weapon Fire VFX")]
        [Tooltip("One-shot discharge played at the MuzzleAnchor the moment a damaging shot fires. World-aligned, seeded.")]
        [SerializeField] GameObject muzzleFlashPrefab;

        [Tooltip("How far in front of the barrel end the discharge is centred.")]
        [SerializeField] float muzzleFlashForwardOffset = 0.035f;

        [Tooltip("Extra discharge size per point of damage, as a fraction of its prefab size.")]
        [SerializeField] float muzzleFlashGrowthPerDamage = 0.03f;

        [SerializeField] float muzzleFlashMaxGrowth = 1.8f;

        [Header("Muzzle Light")]
        [SerializeField] Color muzzleLightColor = new Color(1f, 0.62f, 0.3f);

        [Tooltip("Peak intensity, before the damage growth.")]
        [SerializeField] float muzzleLightIntensity = 8f;

        [SerializeField] float muzzleLightRange = 3f;

        [Tooltip("How long the flash of light takes to fade out.")]
        [SerializeField] float muzzleLightDuration = 0.14f;

        [Header("Recoil")]
        [Tooltip("Recoil in degrees (muzzle flips up) for the damage dealt. High damage is deliberately absurd.")]
        [SerializeField] AnimationCurve recoilByDamage = new AnimationCurve(
            new Keyframe(1f, 5f), new Keyframe(5f, 12f), new Keyframe(10f, 25f),
            new Keyframe(15f, 45f), new Keyframe(20f, 75f), new Keyframe(25f, 100f));

        [Tooltip("How far the gun is shoved back per degree of recoil, in metres.")]
        [SerializeField] float recoilKickPerDegree = 0.0014f;

        [Tooltip("Time for the gun to snap to full recoil.")]
        [SerializeField] float recoilSnapDuration = 0.035f;

        [Tooltip("Recovery back onto the target, plus more time per degree of recoil.")]
        [SerializeField] float recoilRecoveryDuration = 0.22f;

        [SerializeField] float recoilRecoveryPerDegree = 0.004f;

        [Tooltip("Above this recoil the shooter's own body is rocked back too.")]
        [SerializeField] float shooterLeanThreshold = 20f;

        [Tooltip("Shooter lean in degrees per degree of recoil above the threshold.")]
        [SerializeField] float shooterLeanPerDegree = 0.12f;

        [Header("Blood")]
        [Tooltip("One-shot blood burst played on the damaged player's upper body. World-aligned, seeded.")]
        [SerializeField] GameObject hitBloodPrefab;

        [Tooltip("Distance from the body's centre line, toward the shooter, where the burst is centred. Clear of the body surface.")]
        [SerializeField] float bloodSurfaceOffset = 0.2f;

        [Tooltip("Extra blood size per point of damage, as a fraction of its prefab size.")]
        [SerializeField] float bloodGrowthPerDamage = 0.025f;

        [SerializeField] float bloodMaxGrowth = 1.4f;

        [Header("Hit Reaction")]
        [Tooltip("Peak tilt away from the shot in degrees, for the damage dealt.")]
        [SerializeField] AnimationCurve hitTiltByDamage = new AnimationCurve(
            new Keyframe(1f, 4f), new Keyframe(5f, 6.5f), new Keyframe(10f, 9f),
            new Keyframe(20f, 13f), new Keyframe(25f, 15f));

        [Tooltip("Time to snap to the peak tilt. Very short: it is the impact.")]
        [SerializeField] float hitSnapDuration = 0.05f;

        [Tooltip("Hit-stop: the pose freezes at the peak for this long, plus more per point of damage.")]
        [SerializeField] float hitStopDuration = 0.04f;

        [SerializeField] float hitStopPerDamage = 0.004f;

        [SerializeField] float hitStopMax = 0.12f;

        [Tooltip("Roly-poly rocking after the hit-stop: oscillations per second.")]
        [SerializeField] float wobbleFrequency = 2.6f;

        [Tooltip("0 rocks forever, 1 settles without overshoot. Low values rock more.")]
        [Range(0.05f, 1f)]
        [SerializeField] float wobbleDamping = 0.3f;

        [Tooltip("Total rocking time; it fades to exactly upright at the end.")]
        [SerializeField] float wobbleDuration = 0.9f;

        [Tooltip("Body squash at the impact (fraction of height), for a hit of 15 damage or more.")]
        [Range(0f, 0.2f)]
        [SerializeField] float hitSquash = 0.07f;

        [Tooltip("The head follows the body's tilt this much later, for a little whiplash.")]
        [SerializeField] float headLag = 0.05f;

        [Tooltip("The head's tilt relative to the body's.")]
        [SerializeField] float headWhip = 1.25f;

        [Header("Hit Flash")]
        [SerializeField] Color hitFlashColor = new Color(1f, 0.92f, 0.88f);

        [Range(0f, 1f)]
        [SerializeField] float hitFlashStrength = 0.85f;

        [SerializeField] float hitFlashDuration = 0.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        sealed class TiltPart
        {
            public Transform Transform;
            public Pose Rest;
            public Vector3 RestScale;
            public bool Squashes;
        }

        /// <summary>
        /// One player's body pose layer: a reaction (a hit, or a shooter rocked back by
        /// recoil) and/or the anticipation lean underneath it.
        /// </summary>
        sealed class BodyReaction
        {
            public PlayerCharacter Player;
            public Vector3 PivotLocal;
            public readonly List<TiltPart> Parts = new List<TiltPart>();

            // The head is rotated as well unless it carries this machine's camera.
            public Transform Head;
            public Pose HeadRest;
            public Quaternion HeadBase;
            public Quaternion HeadWritten;
            public bool HeadWrote;

            public Vector3 Axis;
            public float Peak;
            public float Squash;
            public float Hold;
            public float StartTime;
            public Quaternion From = Quaternion.identity;
            public Quaternion Current = Quaternion.identity;
            public bool Reacting;

            // Anticipation lean: eases from LeanFrom to LeanTo degrees about LeanAxis (root space).
            public Vector3 LeanAxis = Vector3.right;
            public float LeanFrom;
            public float LeanTo;
            public float LeanStart;
            public float LeanDuration;
        }

        struct LightFlash
        {
            public Light Light;
            public float Peak;
            public float StartTime;
        }

        sealed class BodyFlash
        {
            public Renderer[] Renderers;
            public Color[] BaseColors;
            public float StartTime;
        }

        readonly Dictionary<PlayerCharacter, BodyReaction> _reactions = new Dictionary<PlayerCharacter, BodyReaction>();
        readonly Dictionary<PlayerCharacter, BodyFlash> _bodyFlashes = new Dictionary<PlayerCharacter, BodyFlash>();
        readonly List<LightFlash> _lightFlashes = new List<LightFlash>();
        readonly List<PlayerCharacter> _finished = new List<PlayerCharacter>();

        MaterialPropertyBlock _flashBlock;

        // ------------------------------------------------------------------ events

        /// <summary>A damaging shot from a world point landed on this seat.</summary>
        void PlayHit(int seat, Vector3 shotOrigin, int damage, int seed)
        {
            PlayerCharacter target = _table != null ? _table.GetPlayer(seat) : null;
            if (target == null) return;

            Vector3 aimPoint = target.UpperBodyAimPoint(targetUpperBody);
            Vector3 shot = Flat(aimPoint - shotOrigin);

            PlayBlood(target, aimPoint, shot, damage, seed);
            StartBodyFlash(target);

            // Tipping the up axis toward the shot direction leans the top away from the shooter.
            StartBodyReaction(target, shot, hitTiltByDamage.Evaluate(damage), HitStopFor(damage),
                hitSquash * Mathf.Clamp01(damage / 15f));
        }

        /// <summary>The discharge, in world space where the shot left the barrel.</summary>
        void PlayMuzzleFlash(Transform muzzle, int damage, int seed)
        {
            if (muzzle == null) return;

            float growth = Mathf.Min(muzzleFlashMaxGrowth, 1f + muzzleFlashGrowthPerDamage * damage);
            Vector3 position = muzzle.position + muzzle.forward * muzzleFlashForwardOffset;

            // Left where it was fired: the gun kicks out of it, the flash does not follow.
            GameObject flash = muzzleFlashPrefab != null
                ? PlaySeeded(muzzleFlashPrefab, position, muzzle.rotation, null, growth, seed)
                : null;

            GameObject lightHost = flash != null ? flash : new GameObject("MuzzleLight");
            if (flash == null)
            {
                lightHost.transform.position = position;
                Destroy(lightHost, muzzleLightDuration + 0.1f);
            }

            Light light = lightHost.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = muzzleLightColor;
            light.range = muzzleLightRange;
            light.shadows = LightShadows.None;
            light.intensity = muzzleLightIntensity * growth;

            _lightFlashes.Add(new LightFlash { Light = light, Peak = light.intensity, StartTime = Time.time });
        }

        /// <summary>
        /// Centred just clear of the body surface facing the shooter, its +Z toward the
        /// shooter, parented to the body so it rides the reaction.
        /// </summary>
        void PlayBlood(PlayerCharacter target, Vector3 aimPoint, Vector3 shot, int damage, int seed)
        {
            if (hitBloodPrefab == null) return;

            Transform parent = target.Body != null ? target.Body : target.transform;
            float growth = Mathf.Min(bloodMaxGrowth, 1f + bloodGrowthPerDamage * damage);

            PlaySeeded(hitBloodPrefab, aimPoint - shot * bloodSurfaceOffset,
                Quaternion.LookRotation(-shot, Vector3.up), parent, growth, seed + 7919);
        }

        /// <summary>
        /// Spawns a one-shot effect whose every random number comes from the shared seed,
        /// so all machines play the identical burst. It cleans itself up.
        /// </summary>
        static GameObject PlaySeeded(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent,
            float growth, int seed)
        {
            GameObject effect = Instantiate(prefab, position, rotation, parent);
            effect.transform.localScale = prefab.transform.localScale * growth;

            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = unchecked((uint)(seed * 7 + i * 104729));
            }

            ParticleSystem root = effect.GetComponent<ParticleSystem>();
            if (root != null) root.Play(true);
            else for (int i = 0; i < systems.Length; i++) systems[i].Play(false);

            Destroy(effect, OneShotLifetime(systems));
            return effect;
        }

        /// <summary>Long enough for every particle of a non-looping effect to finish.</summary>
        static float OneShotLifetime(ParticleSystem[] systems)
        {
            float lifetime = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }

            return lifetime > 0f ? lifetime : 2f;
        }

        float HitStopFor(int damage)
        {
            return Mathf.Min(hitStopMax, hitStopDuration + hitStopPerDamage * damage);
        }

        // ------------------------------------------------------------------ recoil

        float RecoilFor(int damage)
        {
            return Mathf.Max(0f, recoilByDamage.Evaluate(damage));
        }

        float RecoilLength(float recoil, float hold)
        {
            return recoilSnapDuration + hold + recoilRecoveryDuration + recoilRecoveryPerDegree * recoil;
        }

        /// <summary>Recoil amount, 0 to 1, at a time after the shot: snap, hold, springy return.</summary>
        float RecoilEnvelope(float t, float recoil, float hold)
        {
            if (t < 0f) return 0f;
            if (t < recoilSnapDuration) return EaseOutCubic(t / recoilSnapDuration);
            if (t < recoilSnapDuration + hold) return 1f;

            float recovery = recoilRecoveryDuration + recoilRecoveryPerDegree * recoil;
            return DampedSpring(t - recoilSnapDuration - hold, 3.2f, 0.5f, recovery);
        }

        /// <summary>The held grip pose kicked by recoil: muzzle flipped up around the hand, gun shoved back.</summary>
        Pose Recoiled(Pose held, float recoil, float amount)
        {
            if (amount == 0f || recoil <= 0f) return held;

            float angle = recoil * amount;
            Vector3 right = held.rotation * Vector3.right;
            Vector3 back = -(held.rotation * Vector3.forward);
            float kick = recoil * recoilKickPerDegree * amount;

            return new Pose(held.position + back * kick + Vector3.up * (kick * 0.3f),
                Quaternion.AngleAxis(-angle, right) * held.rotation);
        }

        /// <summary>A recoil big enough rocks the shooter's own body back, away from the target.</summary>
        void StartShooterLean(PlayerCharacter shooter, Vector3 toTarget, float recoil, float hold)
        {
            // A negligible lean would only cut short whatever the shooter's body was doing.
            float lean = (recoil - shooterLeanThreshold) * shooterLeanPerDegree;
            if (shooter == null || lean < 0.5f) return;

            StartBodyReaction(shooter, -Flat(toTarget), lean, hold, 0f);
        }

        // ------------------------------------------------------------------ body reaction

        void StartBodyReaction(PlayerCharacter player, Vector3 away, float peak, float hold, float squash)
        {
            if (peak <= 0f) return;

            BodyReaction reaction;
            if (!_reactions.TryGetValue(player, out reaction))
            {
                reaction = CreateBodyReaction(player);
                if (reaction == null) return;
                _reactions[player] = reaction;
            }

            reaction.From = reaction.Reacting ? reaction.Current : Quaternion.identity;
            reaction.Reacting = true;
            reaction.Axis = player.transform.InverseTransformDirection(Vector3.Cross(Vector3.up, away));
            reaction.Peak = peak;
            reaction.Hold = hold;
            reaction.Squash = squash;
            reaction.StartTime = Time.time;
        }

        /// <summary>
        /// Eases this player's anticipation lean to an angle, tipping the top of the body
        /// toward a flat direction around the fixed base. Zero eases it back upright.
        /// </summary>
        void SetLean(PlayerCharacter player, Vector3 toward, float angle, float duration)
        {
            if (player == null) return;

            BodyReaction reaction;
            if (!_reactions.TryGetValue(player, out reaction))
            {
                if (angle == 0f) return;

                reaction = CreateBodyReaction(player);
                if (reaction == null) return;
                _reactions[player] = reaction;
            }

            reaction.LeanFrom = LeanAt(reaction);
            reaction.LeanTo = angle;
            reaction.LeanStart = Time.time;
            reaction.LeanDuration = Mathf.Max(0f, duration);

            Vector3 flat = new Vector3(toward.x, 0f, toward.z);
            if (angle != 0f && flat.sqrMagnitude > 0.000001f)
                reaction.LeanAxis = player.transform.InverseTransformDirection(Vector3.Cross(Vector3.up, flat.normalized)).normalized;
        }

        float LeanAt(BodyReaction r)
        {
            float t = r.LeanDuration > 0f ? (Time.time - r.LeanStart) / r.LeanDuration : 1f;
            return Mathf.LerpUnclamped(r.LeanFrom, r.LeanTo, EaseInOutCubic(t));
        }

        BodyReaction CreateBodyReaction(PlayerCharacter player)
        {
            Transform root = player.transform;
            Transform pivot = player.HitReactionPivot;

            BodyReaction reaction = new BodyReaction
            {
                Player = player,
                PivotLocal = pivot != null ? root.InverseTransformPoint(pivot.position) : Vector3.zero
            };

            AddTiltPart(reaction, player.Body, true);
            AddTiltPart(reaction, root.Find("LeftShoulderAnchor"), false);
            AddTiltPart(reaction, player.RightHandAnchor != null ? player.RightHandAnchor.parent : null, false);
            AddTiltPart(reaction, player.WeaponHolsterAnchor, false);

            // The head carrying this machine's camera stays put: the local player is never
            // shoved around, only their body and hands react. Everyone else sees their head.
            Camera view = Camera.main;
            Transform head = player.HeadPivot;
            bool headHoldsCamera = view != null && head != null && view.transform.IsChildOf(head);
            if (head != null && !headHoldsCamera && head.parent == root)
            {
                reaction.Head = head;
                reaction.HeadRest = RestPoseOf(head);
            }

            return reaction.Parts.Count > 0 ? reaction : null;
        }

        void AddTiltPart(BodyReaction reaction, Transform part, bool squashes)
        {
            // Every part tilts in the character root's space around the same base.
            if (part == null || part.parent != reaction.Player.transform) return;

            reaction.Parts.Add(new TiltPart
            {
                Transform = part,
                Rest = RestPoseOf(part),
                RestScale = part.localScale,
                Squashes = squashes
            });
        }

        /// <summary>Tilt of a reaction at a time after it started, as a rotation in root space.</summary>
        Quaternion TiltAt(BodyReaction r, float t)
        {
            if (t <= 0f) return r.From;

            Quaternion peak = Quaternion.AngleAxis(r.Peak, r.Axis);
            if (t < hitSnapDuration) return Quaternion.Slerp(r.From, peak, EaseOutCubic(t / hitSnapDuration));
            if (t < hitSnapDuration + r.Hold) return peak;

            float rock = DampedSpring(t - hitSnapDuration - r.Hold, wobbleFrequency, wobbleDamping, wobbleDuration);
            return Quaternion.AngleAxis(r.Peak * rock, r.Axis);
        }

        /// <summary>Squash amount at a time: in with the snap, then a faster springy rebound.</summary>
        float SquashAt(BodyReaction r, float t)
        {
            if (r.Squash <= 0f || t <= 0f) return 0f;
            if (t < hitSnapDuration) return r.Squash * EaseOutCubic(t / hitSnapDuration);
            if (t < hitSnapDuration + r.Hold) return r.Squash;

            return r.Squash * DampedSpring(t - hitSnapDuration - r.Hold, wobbleFrequency * 2f, 0.4f, wobbleDuration * 0.6f);
        }

        bool ReactionFinished(BodyReaction r, float t)
        {
            return t >= hitSnapDuration + r.Hold + wobbleDuration + headLag;
        }

        void LateUpdate()
        {
            UpdateBodyReactions();
            UpdateLightFlashes();
            UpdateBodyFlashes();
        }

        void UpdateBodyReactions()
        {
            if (_reactions.Count == 0) return;

            _finished.Clear();
            foreach (KeyValuePair<PlayerCharacter, BodyReaction> pair in _reactions)
            {
                BodyReaction r = pair.Value;
                if (pair.Key == null) { _finished.Add(pair.Key); continue; }

                float t = Time.time - r.StartTime;
                if (r.Reacting && ReactionFinished(r, t))
                {
                    r.Reacting = false;
                    r.Current = Quaternion.identity;
                    r.From = Quaternion.identity;
                }

                float lean = LeanAt(r);
                if (!r.Reacting && lean == 0f && r.LeanTo == 0f)
                {
                    RestoreReaction(r);
                    _finished.Add(pair.Key);
                    continue;
                }

                Quaternion leanTilt = Quaternion.AngleAxis(lean, r.LeanAxis);
                if (r.Reacting) ApplyReaction(r, TiltAt(r, t), SquashAt(r, t), TiltAt(r, t - headLag), leanTilt);
                else ApplyReaction(r, Quaternion.identity, 0f, Quaternion.identity, leanTilt);
            }

            for (int i = 0; i < _finished.Count; i++) _reactions.Remove(_finished[i]);
        }

        void ApplyReaction(BodyReaction r, Quaternion tilt, float squash, Quaternion headTilt, Quaternion lean)
        {
            r.Current = tilt;

            // The reaction plays on top of the lean, both around the same fixed base.
            Quaternion body = tilt * lean;

            // Squash about the base: shorter and a little wider, in the body's own frame.
            Vector3 squashScale = new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f);

            for (int i = 0; i < r.Parts.Count; i++)
            {
                TiltPart part = r.Parts[i];
                if (part.Transform == null) continue;

                part.Transform.localPosition = r.PivotLocal + body * Vector3.Scale(squashScale, part.Rest.position - r.PivotLocal);
                part.Transform.localRotation = body * part.Rest.rotation;
                if (part.Squashes) part.Transform.localScale = Vector3.Scale(part.RestScale, squashScale);
            }

            if (r.Head == null) return;

            r.Head.localPosition = r.PivotLocal + body * Vector3.Scale(squashScale, r.HeadRest.position - r.PivotLocal);

            // Head look owns the pivot's rotation and may rewrite it any frame; the whip is
            // layered on top of whatever look last wrote, and never fed back into it.
            if (!r.HeadWrote || r.Head.localRotation != r.HeadWritten) r.HeadBase = r.Head.localRotation;

            Quaternion headOffset = Quaternion.SlerpUnclamped(Quaternion.identity, headTilt, headWhip) * lean;
            r.HeadWritten = headOffset * r.HeadBase;
            r.Head.localRotation = r.HeadWritten;
            r.HeadWrote = true;
        }

        /// <summary>Puts every reacting part back on its exact rest pose.</summary>
        static void RestoreReaction(BodyReaction r)
        {
            r.Current = Quaternion.identity;
            r.From = Quaternion.identity;
            r.Reacting = false;
            r.LeanFrom = 0f;
            r.LeanTo = 0f;

            for (int i = 0; i < r.Parts.Count; i++)
            {
                TiltPart part = r.Parts[i];
                if (part.Transform == null) continue;

                part.Transform.localPosition = part.Rest.position;
                part.Transform.localRotation = part.Rest.rotation;
                if (part.Squashes) part.Transform.localScale = part.RestScale;
            }

            if (r.Head == null) return;

            r.Head.localPosition = r.HeadRest.position;
            if (r.HeadWrote && r.Head.localRotation == r.HeadWritten) r.Head.localRotation = r.HeadBase;
            r.HeadWrote = false;
        }

        // ------------------------------------------------------------------ flashes

        void UpdateLightFlashes()
        {
            for (int i = _lightFlashes.Count - 1; i >= 0; i--)
            {
                LightFlash flash = _lightFlashes[i];
                if (flash.Light == null) { _lightFlashes.RemoveAt(i); continue; }

                float k = 1f - Mathf.Clamp01((Time.time - flash.StartTime) / muzzleLightDuration);
                flash.Light.intensity = flash.Peak * k * k;

                if (k <= 0f)
                {
                    flash.Light.enabled = false;
                    _lightFlashes.RemoveAt(i);
                }
            }
        }

        /// <summary>The target's body and head flash bright for an instant: an unmistakable "hit".</summary>
        void StartBodyFlash(PlayerCharacter player)
        {
            if (hitFlashStrength <= 0f || hitFlashDuration <= 0f) return;

            BodyFlash flash;
            if (_bodyFlashes.TryGetValue(player, out flash))
            {
                flash.StartTime = Time.time;
                return;
            }

            List<Renderer> renderers = new List<Renderer>();
            if (player.Body != null) renderers.AddRange(player.Body.GetComponentsInChildren<Renderer>());
            if (player.HeadVisual != null) renderers.AddRange(player.HeadVisual.GetComponentsInChildren<Renderer>());
            if (renderers.Count == 0) return;

            flash = new BodyFlash { Renderers = renderers.ToArray(), BaseColors = new Color[renderers.Count], StartTime = Time.time };
            for (int i = 0; i < flash.Renderers.Length; i++)
            {
                Material material = flash.Renderers[i].sharedMaterial;
                flash.BaseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }

            _bodyFlashes[player] = flash;
        }

        void UpdateBodyFlashes()
        {
            if (_bodyFlashes.Count == 0) return;
            if (_flashBlock == null) _flashBlock = new MaterialPropertyBlock();

            _finished.Clear();
            foreach (KeyValuePair<PlayerCharacter, BodyFlash> pair in _bodyFlashes)
            {
                BodyFlash flash = pair.Value;
                float k = 1f - Mathf.Clamp01((Time.time - flash.StartTime) / hitFlashDuration);

                for (int i = 0; i < flash.Renderers.Length; i++)
                {
                    Renderer renderer = flash.Renderers[i];
                    if (renderer == null) continue;

                    if (k <= 0f || pair.Key == null) { renderer.SetPropertyBlock(null); continue; }

                    renderer.GetPropertyBlock(_flashBlock);
                    _flashBlock.SetColor(BaseColorId, Color.Lerp(flash.BaseColors[i], hitFlashColor, hitFlashStrength * k * k));
                    renderer.SetPropertyBlock(_flashBlock);
                }

                if (k <= 0f || pair.Key == null) _finished.Add(pair.Key);
            }

            for (int i = 0; i < _finished.Count; i++) _bodyFlashes.Remove(_finished[i]);
        }

        void ClearHitReactions()
        {
            foreach (BodyReaction reaction in _reactions.Values) RestoreReaction(reaction);
            _reactions.Clear();

            foreach (BodyFlash flash in _bodyFlashes.Values)
                for (int i = 0; i < flash.Renderers.Length; i++)
                    if (flash.Renderers[i] != null) flash.Renderers[i].SetPropertyBlock(null);
            _bodyFlashes.Clear();

            for (int i = 0; i < _lightFlashes.Count; i++)
                if (_lightFlashes[i].Light != null) _lightFlashes[i].Light.enabled = false;
            _lightFlashes.Clear();
        }

        // ------------------------------------------------------------------ curves

        /// <summary>
        /// Under-damped spring released from 1: swings past 0 and back, decaying, and is
        /// faded to exactly 0 over the last part of its duration.
        /// </summary>
        static float DampedSpring(float t, float frequency, float damping, float duration)
        {
            if (t >= duration) return 0f;

            float omega = 2f * Mathf.PI * frequency;
            float zeta = Mathf.Clamp(damping, 0.01f, 0.99f);
            float swing = Mathf.Exp(-zeta * omega * t) * Mathf.Cos(omega * Mathf.Sqrt(1f - zeta * zeta) * t);

            float fadeStart = duration * 0.7f;
            float fade = t <= fadeStart ? 1f : 1f - Smooth((t - fadeStart) / (duration - fadeStart));
            return swing * fade;
        }
    }
}
