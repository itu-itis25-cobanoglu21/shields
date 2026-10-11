# TASK 018 — Weapon Fire VFX, Blood Hit & Small Body Knockback

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and this entire TASK.md before modifying anything.

The weapon draw / aiming / target-lock presentation from the previous task is already working.

The user has now manually placed TWO new VFX objects in the current GameScene:

1. a weapon impact / firing VFX intended to play at the weapon when the shot happens
2. a blood VFX intended to play on the player who actually takes positive damage

The user has not tuned their scale, orientation, timing, or placement.

Inspect and reuse those exact scene VFX assets.

Do not arbitrarily replace them before inspection.

This task is PRESENTATION ONLY.

Do not change combat rules, damage calculation, Charge rules, targeting, seat mapping, networking authority, or hidden-card privacy.

---

## 1. Goal

For a successful damaging Attack / Charged Attack:

target selected
→ weapon is already locked onto the target
→ weapon fires
→ firing VFX appears at the weapon at the exact firing moment
→ blood VFX appears on the damaged player's body
→ damaged player's upper body gives a very small recoil / knockback away from the incoming shot
→ body smoothly returns to normal seated pose

The reaction should be quick and readable, but subtle.

Do NOT implement ragdoll yet.

Do NOT implement the failed-attack flag gag yet.

---

# PART A — INSPECT CURRENT STATE FIRST

## 2. Inspect before modifying

Inspect the current:

- weapon draw presentation
- weapon grip / aim setup
- target-lock implementation
- weapon asset hierarchy
- existing muzzle / weapon forward direction
- any existing `MuzzleAnchor`, `AimPivot`, `WeaponGripAnchor`, or equivalent
- authoritative Attack result flow
- authoritative Charged Attack result flow
- how positive damage versus zero damage is represented in results
- `PlayerActionPresenter`
- PlayerCharacter body hierarchy
- Body / BodyMesh transforms
- HeadPivot
- hand anchors
- current seated/rest body transform
- current networking presentation flow

Also inspect the two manually placed VFX objects in GameScene.

For EACH VFX identify:

- exact scene object name
- source prefab / asset path
- particle/VFX system type
- root scale
- orientation
- renderer setup
- looping behavior
- duration
- materials
- lights
- colliders
- rigidbodies
- scripts
- audio components

Do not guess what either VFX does based only on its name.

---

# PART B — WEAPON FIRING VFX

## 3. Use the user's firing / impact VFX

Use the manually placed firing/impact VFX as the basis for the weapon firing effect.

Create a project-owned prefab/variant if customization is required.

Do NOT destructively modify the third-party package source.

The manually placed scene reference may be removed after a reusable project-owned presentation asset is created, if it was clearly only a reference/testing object.

---

## 4. Place it at the weapon

The firing VFX must originate from the weapon itself.

Preferred hierarchy conceptually:

Weapon
- WeaponGrip / AimPivot
- MuzzleAnchor
- FireVFX

If a proper muzzle anchor does not already exist, create one.

The anchor must be positioned at the actual end of the barrel.

Validate the weapon's real barrel direction before placing the effect.

Do not assume the model's +Z or -Z direction.

---

## 5. Scale and orientation

The user has not tuned the firing VFX.

Adjust:

- scale
- orientation
- local position
- duration if necessary

so it visually matches the existing weapon and the scale of the PlayerCharacter.

It should look like a small stylized weapon discharge, not a giant explosion.

It must not obscure half the screen in first person.

Tune it from actual gameplay camera views.

---

## 6. Exact timing

The firing VFX must start at the exact presentation frame / moment the weapon is considered to fire.

The desired sequence is:

weapon locked to target
→ very short hold if already present
→ FIRE moment
→ firing VFX starts immediately

Do not start the VFX while the gun is still drawing or before target lock.

---

## 7. Successful attacks only for this task

For this task, use the full fire + blood + knockback presentation when the authoritative result causes:

`positive damage > 0`

This includes applicable normal Attack and Charged Attack results.

If the result is blocked / zero damage:

- do NOT spawn blood
- do NOT play hit knockback
- do NOT invent the miss-flag animation yet

Preserve a clean path so the later failed-attack gag can be added separately.

Do not change gameplay outcome.

---

# PART C — BLOOD HIT VFX

## 8. Use the user's blood VFX

Use the manually placed blood VFX from GameScene.

Inspect it first.

Create a project-owned prefab/variant if tuning is needed.

Do not destructively modify package assets.

---

## 9. Blood placement

When a player actually receives positive damage:

spawn/play the blood effect on that player's body.

Use a stable body hit point, for example:

- upper torso / chest
- an existing body target point if suitable
- or a new dedicated `HitVfxAnchor`

Do NOT attach it to:

- chair
- head unless visually necessary
- table
- random collider edge

Preferred target is the upper torso area.

---

## 10. Blood direction

If the VFX supports orientation:

orient the blood so it reacts to the incoming shot direction.

Conceptually:

shooter position
→ target body
→ blood direction continues slightly away from shooter

Do not make every blood burst face the same world direction.

If the asset is camera-facing and orientation does not matter visually, keep it simple.

---

## 11. Blood scale

Tune the blood VFX to the current stylized bean-like PlayerCharacter.

Desired result:

- clearly readable
- short
- stylized
- not photorealistic
- not a giant gore explosion
- does not cover the whole screen/player

The user has not tuned this asset, so choose a visually appropriate scale.

---

## 12. Blood lifetime / cleanup

Blood burst should be temporary.

Do not leave permanent particle objects accumulating around the scene.

Reuse/pool if an existing presentation pattern makes sense, otherwise clean up safely after the VFX finishes.

No collider.
No Rigidbody.
No gameplay layer.
No NetworkObject unless existing architecture genuinely requires one.

---

# PART D — SMALL "HACIYATMAZ" HIT REACTION

## 13. Desired reaction

When positive damage is applied, the damaged player should perform a VERY SMALL physical-looking reaction.

The intended feeling is similar to a weighted roly-poly / hacıyatmaz toy:

- player's base stays fixed in the seat
- feet/base position does NOT slide
- the lower body remains anchored
- only the upper body leans slightly away from the incoming shot
- then smoothly returns upright

This is NOT a real knockback displacement.

Do not push the entire PlayerCharacter through world space.

---

## 14. Pivot behavior

The reaction should visually pivot from near the bottom/base of the body.

Conceptually:

fixed base
|
| body tilts
|
upper body → slightly away from shooter

If the current Body mesh pivot is unsuitable, create a presentation pivot such as:

`HitReactionPivot`

without changing authoritative PlayerCharacter/root position.

Do not move seat transforms.

Do not move networking roots.

Do not move camera anchors permanently.

---

## 15. Direction

Determine reaction direction from:

attacker / shot origin
→ damaged player

The damaged body should lean a small amount in the OPPOSITE direction from where the shot came from.

Example:

shot arrives from player's left
→ body tilts slightly right

shot arrives from front
→ body tilts slightly backward

Use the table/seat/world orientation correctly.

Do not use a fixed global rotation for every target.

---

## 16. Strength

Keep the reaction SMALL.

This is important.

Initial visual target:

- approximately `4–8 degrees` of body tilt
- very small positional motion only if absolutely needed
- no dramatic chair movement
- no ragdoll

Expose the amount for tuning.

Suggested initial value:

`6 degrees`

---

## 17. Timing

Desired initial timing:

Hit:
- quick impulse away: approximately `0.08–0.12s`

Recovery:
- smooth return: approximately `0.20–0.35s`

Suggested starting values:

- hit impulse: `0.10s`
- recovery: `0.28s`

Use easing.

The reaction should feel snappy but not cartoonishly huge.

---

## 18. Head and hands

The whole presentation should remain visually coherent.

It is acceptable for the:

- Body
- HeadPivot
- floating hands

to receive a small coordinated reaction if needed.

However:

- do not break head-look synchronization
- do not permanently alter camera/head transforms
- do not detach hands from their normal presentation system

For the LOCAL damaged player, avoid violently rotating the actual first-person camera.

Prefer body/world reaction that is visible to others without making the local user uncomfortable.

If a tiny local camera impulse is clearly beneficial, keep it extremely subtle and report it.

---

# PART E — AUTHORITATIVE TIMING

## 19. Trigger from confirmed result

Blood and hit reaction must only occur after an authoritative/public result confirms:

`positive damage > 0`

Do not predict blood locally before the host confirms damage.

The weapon firing presentation may be tightly timed with the resolved attack result, but gameplay state remains authoritative.

Do not allow client-side cosmetics to determine whether damage occurred.

---

## 20. Normal Attack

For successful normal Attack:

target lock
→ fire presentation
→ firing VFX
→ blood on target
→ small hit reaction
→ cleanup / weapon return according to current presentation flow

---

## 21. Charged Attack

For successful Charged Attack:

use the same firing/hit presentation foundation:

target lock
→ fire
→ firing VFX
→ blood
→ small reaction

Do not implement second-Charge card reveal animation in this task unless it is already present.

Do not change Charged Attack gameplay rules.

---

# PART F — BOTS / REMOTE HUMANS

## 22. Bots

Bot successful attacks should use the same visible presentation:

- weapon fire VFX
- blood on damaged player
- body reaction

Do not instant-skip the presentation for bots.

---

## 23. Remote humans

Remote clients must see the same resolved successful-hit presentation.

Do not send unnecessary hidden information.

Use the existing authoritative/public result flow.

Do not assume `clientId == seat`.

---

# PART G — SAFETY

## 24. No targeting interference

All added VFX must have:

- no active Collider
- no Rigidbody unless visually necessary and non-interfering
- no targetable gameplay layer
- no gameplay authority

Validate:

- body targeting
- Shield targeting
- card targeting

after integration.

---

## 25. No gameplay changes

Do NOT modify:

- damage formulas
- Attack success condition
- Charged Attack success condition
- Shield values
- Health rules
- Charge interruption rules
- death rules
- deck logic
- turn logic
- targeting rules

This is presentation only.

---

# PART H — OUT OF SCOPE

## 26. Do NOT implement yet

Do NOT implement:

- failed-attack flag animation
- ragdoll
- death animation
- persistent blood decals
- gore
- bullet projectile simulation
- bullet physics
- bullet holes
- shell casing physics
- gunshot audio
- camera shake system beyond an extremely subtle hit reaction if genuinely necessary
- new gameplay mechanics

---

# PART I — VALIDATION

## 27. 3-player validation

Verify:

- local successful Attack
- bot successful Attack
- firing VFX sits correctly on weapon
- blood sits correctly on target body
- hit reaction moves away from shot direction
- body returns exactly to rest

---

## 28. 6-player validation

Verify from different seat angles:

- muzzle VFX orientation
- blood placement
- directional hit reaction
- no wrong world/local rotation behavior

---

## 29. 8-player validation

Verify:

- all seat orientations work
- no body reaction causes seat/player overlaps
- no reaction creates transform drift
- no targeting regression

---

## 30. Repeated hit test

Hit the same living player multiple times without killing them.

After every reaction confirm:

- body returns to exact resting transform
- no cumulative tilt
- no positional drift
- head/hands remain correct

---

## 31. Zero-damage result

Verify a blocked / zero-damage Attack.

Confirm:

- no blood
- no hit knockback
- no accidental positive-hit feedback

Do not implement miss flag yet.

---

## 32. Tests

Run the full EditMode suite.

Expected baseline:

`260 / 260`

Do not weaken existing tests.

---

## 33. Console

Normal gameplay should finish with:

- 0 project-code errors
- 0 missing references
- 0 presentation coroutine exceptions
- no VFX cleanup warnings

Report unrelated package/editor warnings separately.

---

# PART J — FINAL REPORT

## 34. Report

When finished, report:

1. Exact firing/impact VFX scene object found
2. Its source asset/prefab
3. Exact blood VFX scene object found
4. Its source asset/prefab
5. Project-owned prefab/material variants created
6. Muzzle anchor used/created
7. Firing VFX scale/rotation/offset
8. Blood anchor used/created
9. Blood scale/rotation/offset
10. Hit-reaction pivot implementation
11. Hit-reaction angle
12. Hit impulse duration
13. Recovery duration
14. How incoming shot direction is calculated
15. Local-player reaction behavior
16. Bot result
17. Remote-human path/status
18. 3-player validation
19. 6-player validation
20. 8-player validation
21. Repeated-hit drift test
22. Zero-damage validation
23. Targeting regression result
24. EditMode result
25. Console result
26. Exact files created/modified
27. Inspector tuning values available to the user
28. Anything still requiring visual tuning

Do not commit anything.

Stop when firing VFX + successful blood hit + small directional hit reaction are complete.