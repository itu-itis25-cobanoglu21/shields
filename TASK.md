# TASK 015 — First Charge Hand / Card Animation + Persistent Purple Flame

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and this entire TASK.md before modifying anything.

This task adds the first real player action animation to SHIELDS.

The existing gameplay, networking, hidden-card privacy, seating, cameras, table layout, charge rules, and card presentation are already working.

This task is PRESENTATION ONLY.

Do not change the authoritative Charge rules.

---

## 1. Scope

Implement the visual animation for the FIRST Charge action only:

Player presses Charge
→ right hand quickly reaches to the center deck
→ hand pauses briefly above the deck
→ a face-down card is pulled from the deck toward that player's stored-Charge position
→ hand returns to its original resting position
→ shortly after the card settles, a purple flame begins burning from the center of the card
→ the flame remains continuously active until that stored Charge is cleared by gameplay

Do NOT implement the second Charge / charged-attack reveal animation yet.

Do NOT implement Attack animations yet.

---

# PART A — INSPECT CURRENT SYSTEM FIRST

## 2. Inspect before modifying

Before changing code or prefabs, inspect:

- current Charge input flow
- current host-authoritative Charge action flow
- current public snapshot/result flow
- current stored-Charge presentation
- how the face-down Charge card is currently created / shown
- where the Charge card is positioned for each seat
- existing card-back visual
- `PlayerCharacter` hierarchy
- `RightHandAnchor`
- current right-hand resting transform
- current deck visual position
- current deck presentation anchor / transform
- the manually placed purple fire VFX currently present in GameScene

Identify the exact purple fire object/prefab the user placed.

Do not guess its package prefab or renderer setup.

Inspect:
- root
- child particle systems
- renderer mode
- materials
- simulation behavior
- whether it is camera-oriented
- whether its visible animation fades out / restarts
- whether it contains lights, colliders, scripts or audio

Do not modify anything until this inspection is complete.

---

# PART B — ARCHITECTURE

## 3. Presentation must remain separate from gameplay

Do NOT put animation timing into authoritative game rules.

Do NOT make animation determine:

- whether Charge succeeds
- which card is drawn
- hidden card identity
- charge ownership
- turn advancement
- charge interruption
- damage
- death

The authoritative match state remains the source of truth.

Animation only represents an already-valid gameplay action.

Prefer a presentation component such as:

`PlayerActionPresenter`

or another minimal equivalent if an existing presentation component already serves this purpose.

Do not create unnecessary architecture if an existing clean presentation path can be extended.

---

## 4. Hidden-card privacy is mandatory

The first Charge card remains hidden from EVERYONE, including its owner.

During the animation:

- show only the standard face-down CardBack
- never expose rank
- never expose suit
- never expose hidden card ID/data through presentation DTOs
- never instantiate a face-up version temporarily
- never log hidden identity to clients

The animation must not weaken the existing hidden-charge privacy guarantees.

---

# PART C — HAND ANIMATION

## 5. Use the existing floating right hand

Use the existing:

`RightHandAnchor`

and current floating-hand setup.

Do NOT add:
- visible arms
- humanoid rig
- Animator Controller
- IK package
- animation package

This animation should be procedural.

The hand is intentionally unconstrained by arm length.

---

## 6. Preserve the resting pose

At the start of the animation, capture or use the existing correct resting local transform of the right hand.

The hand MUST return to exactly the correct idle/rest pose after the Charge animation.

Do not hard-code a random world-space idle position if the project already has a proper resting transform.

The animation must continue working for all seat orientations.

---

## 7. Reach timing

When a valid FIRST Charge begins:

the right hand should move rapidly from idle/rest position to the center deck.

Target duration:

**0.20–0.30 seconds**

Preferred starting value:

**0.25 seconds**

Use smooth easing.

The movement should feel fast and deliberate, not robotic constant-speed motion.

Preferred motion character:

- fast initial reach
- slight ease into the deck position

Do not teleport.

---

## 8. Deck hand position

The hand should end slightly above the physical deck, as though it is grabbing the top card.

Do NOT place the hand through the deck.

Create/reuse a clean interaction anchor if useful, for example:

`DeckHandInteractionAnchor`

but only if an appropriate anchor does not already exist.

The target should remain correct if the deck presentation moves slightly later.

Do not change authoritative deck position.

---

## 9. Deck pause

Once the hand reaches the deck:

hold it above the deck for:

**0.15–0.20 seconds**

Preferred starting value:

**0.175 seconds**

During this pause:
- the hand remains stable
- do not jitter
- do not begin moving the Charge card early unless needed for visual continuity

This should visually read as:

"grab card"

---

# PART D — CARD DRAW ANIMATION

## 10. Animate one face-down card from the deck

After the deck pause, visually pull a face-down card from the deck to the player's existing stored-Charge presentation position.

Use the existing CardBack visual/style.

Do not create a new visible card style.

Do not reveal the card.

---

## 11. Card movement timing

Move the card from deck to Charge position quickly.

Target duration:

**0.20–0.30 seconds**

Preferred starting value:

**0.25 seconds**

The motion should feel connected to the hand.

The hand and card should appear to travel together as if the hand is carrying/pulling the card.

It is acceptable for the card to lag the hand by a few milliseconds if that looks better.

---

## 12. Final Charge card position

The card must finish at the SAME authoritative/public presentation position currently used for stored Charge.

Do not invent a different permanent Charge location.

The final state after the animation should be compatible with the existing snapshot-driven Charge presentation.

Avoid duplicate permanent cards.

If the current system instantly creates the final stored-Charge card when the snapshot arrives, use a clean presentation strategy such as:

- temporarily animate that visual from deck to final slot

or

- temporarily hide the final visual while an animation copy travels, then hand off seamlessly

Choose the minimal robust solution.

At the end there must be exactly ONE visible stored-Charge card.

---

# PART E — HAND RETURN

## 13. Return the hand to idle

Once the card reaches the final Charge position:

move the right hand back to its original resting pose.

Target duration:

**0.20–0.30 seconds**

Preferred starting value:

**0.25 seconds**

Use smooth easing.

The hand should not remain attached to the card.

At the end:
- exact idle/rest position restored
- exact idle/rest rotation restored
- no cumulative transform drift after repeated Charge actions across rounds

---

# PART F — PURPLE CHARGE FLAME

## 14. Use the user's purple fire VFX

The user manually placed a purple fire effect in GameScene specifically for this task.

Inspect it and use it as the visual basis for the Charge-card effect if technically suitable.

Do NOT replace it arbitrarily with another effect before inspecting it.

Do NOT destructively modify the package-source prefab/material.

If customization is needed:
- create a project-owned prefab/material variant
- place it under an appropriate SHIELDS art/VFX folder

---

## 15. Flame start timing

Once the face-down Charge card has reached its final position:

wait approximately:

**0.05–0.10 seconds**

Preferred starting value:

**0.075 seconds**

Then begin the purple flame.

The flame should visibly begin after the card settles, not before.

---

## 16. Flame placement

The flame must originate from the CENTER of the face-down Charge card.

It should appear attached to the card.

If the card moves due to presentation/layout updates, the flame must follow it.

Preferred hierarchy conceptually:

`StoredChargeCard`
- `CardBack`
- `ChargeFlameVFX`

Do not make the flame float independently in world space.

---

## 17. Flame orientation consistency

This is important.

Previously some Stylized Fire package effects looked different depending on camera angle.

For the Charge card flame:

ALL players should perceive the flame as centered on the same card.

Do not allow it to visibly jump to different sides of the card depending on viewing camera.

Inspect the purple effect's renderer setup.

If it uses camera-oriented billboards and causes inconsistent perceived placement:

create a project-owned presentation variant that solves the issue.

Possible approaches include:

- world/local-oriented renderer configuration if visually correct
- multiple crossed flame planes/renderers
- another stable setup using the existing effect/material

Choose the solution that looks correct from multiple seats.

Do NOT simply assume changing the root Particle System setting fixes child renderers.

Inspect the actual visible particle/rendering child.

---

## 18. Persistent flame behavior

The Charge flame should NOT behave like:

fade in
→ burn
→ completely disappear
→ restart
→ fade in again

while the Charge remains stored.

The desired behavior is:

start once
→ reach its normal burning state
→ continue burning continuously
→ stop only when the stored Charge ceases to exist

Natural internal flame motion is good.

A repeated full extinction/restart cycle is NOT desired.

Inspect the actual visible particle child/material/shader.

The package may control visible flame motion through shader/material parameters rather than root Particle System `Simulation Speed`.

Tune the correct system.

---

## 19. Flame lifetime follows GAME STATE

The flame must remain active for the entire lifetime of the stored Charge.

It ends when the authoritative stored Charge is cleared, including cases such as:

- player performs the second Charge / charged attack
- stored Charge is interrupted by positive incoming damage
- player dies
- match/state reset clears the Charge
- any other existing authoritative rule that removes stored Charge

Do not use a fixed timer such as "burn for 5 seconds".

Charge state controls lifetime.

---

## 20. Flame must not affect gameplay

The flame / glow must have:

- no Collider
- no Rigidbody
- no NetworkObject unless absolutely required by existing architecture
- no targetable gameplay layer
- no gameplay authority
- no damage logic

It is visual only.

It must not interfere with:
- crosshair raycasts
- card targeting
- Shield targeting
- player targeting

---

# PART G — MULTIPLAYER PRESENTATION

## 21. Local responsiveness

For the LOCAL player:

when Charge input is valid and the current input/action gate accepts the action, the hand should feel immediate.

Target experience:

press Charge
→ hand begins reaching almost immediately

Do not deliberately wait for a long network round trip before showing any response if the existing architecture allows safe cosmetic anticipation.

However:

authoritative game state remains controlled by the host.

If local anticipation creates architectural ambiguity, prefer correctness over speculative gameplay state.

---

## 22. Remote visibility

Other participants must also see the charging player's:

- hand reach
- card draw
- hand return
- resulting burning face-down Charge card

Do not broadcast hidden card identity.

Use the existing authoritative action/result/snapshot flow to trigger remote presentation cleanly.

Do not assume `clientId == seat`.

Use the existing explicit participant/client → seat mapping.

---

## 23. Bots

Bots performing their FIRST Charge should use the SAME presentation:

- hand reaches deck
- card travels to their Charge position
- hand returns
- purple flame begins

Do not special-case bots into instant visual state unless required during fallback/error recovery.

---

# PART H — INTERRUPTION / SNAPSHOT ROBUSTNESS

## 24. Snapshot updates must not reset the animation

The project previously had problems where repeated snapshots reset camera/presentation state.

Do not introduce a similar problem here.

Routine public snapshots must NOT:

- snap a hand back mid-animation unnecessarily
- restart the flame repeatedly
- duplicate the Charge card
- restart the flame's startup animation every snapshot

Persistent Charge presentation should survive snapshot refreshes cleanly.

---

## 25. Late state / fallback

If a client joins or receives a state where a player is ALREADY charged but did not witness the original animation:

show the final state directly:

- face-down Charge card visible
- purple flame already in its stable burning state

Do NOT replay the entire historical hand-draw animation just because a snapshot says the player is charged.

Animation is for the action event.

Snapshot is for current state.

---

# PART I — TUNING VALUES

## 26. Expose timing values cleanly

Do not bury important animation timings across magic numbers.

Expose/centralize reasonable presentation tuning values for:

- Hand reach duration: initial `0.25s`
- Deck hold duration: initial `0.175s`
- Card pull duration: initial `0.25s`
- Hand return duration: initial `0.25s`
- Flame start delay: initial `0.075s`

These can be serialized presentation settings or clean constants/configuration appropriate to the current architecture.

Do not put them into authoritative match rules.

---

## 27. Easing

Use visually pleasing easing.

Avoid purely linear robotic motion.

Suggested character:

Hand reach:
- ease-out / fast reach into deck

Card pull:
- fast but smooth

Hand return:
- slightly softer ease-in-out

Do not introduce a tweening package.

Use project code / coroutine / existing utilities.

---

# PART J — VALIDATION

## 28. Offline validation

Test FIRST Charge with:

### 3 players
- local human Charge
- bot Charge
- card remains hidden
- correct flame
- correct hand reset

### 6 players
- multiple seat orientations
- hand reaches central deck correctly from different seats
- Charge card ends at correct seat-relative position
- flame remains centered

### 8 players
- same validations at maximum seat count
- no hand/card animation crosses into obviously incorrect seat geometry
- multiple simultaneous/paced bot presentations do not corrupt each other

---

## 29. Multi-angle flame check

Inspect a burning Charge card from multiple seat cameras / scene viewpoints.

Confirm:

- flame appears centered on the card
- no camera causes it to jump to a different side
- no obvious flat-card/billboard orientation bug
- effect remains visually acceptable around the full table

Report the exact technique used.

---

## 30. Charge interruption test

Create a stored Charge.

Confirm the flame/card remain active.

Then apply positive incoming damage that interrupts Charge.

Verify:

- authoritative Charge clears
- purple flame stops/disappears
- stored Charge card disappears
- no orphan VFX remains

---

## 31. Death cleanup

If a charged player dies:

confirm:
- Charge card clears
- Charge flame clears
- no particle/VFX object remains detached in the scene

---

## 32. Targeting safety

At 3, 6 and 8 players verify:

- flame does not intercept raycasts
- animated hand does not create new target collider problems
- card presentation remains non-gameplay-blocking
- Shield/body targeting still works

---

## 33. Tests

Run the full existing EditMode suite.

Expected baseline:

260 passing.

Do not weaken existing tests.

Add focused pure tests only if new pure logic genuinely warrants them.

Do not create tests just for transform interpolation.

---

## 34. Console

Final state must have:

- 0 project-code errors
- 0 relevant runtime warnings
- 0 missing references
- no coroutine/presentation exceptions
- no VFX-related errors

---

# PART K — DO NOT IMPLEMENT YET

## 35. Explicitly out of scope

Do NOT implement:

- second Charge / charged-attack reveal animation
- Attack weapon draw
- aiming
- gunfire
- muzzle flash
- blood
- hit reaction
- ragdoll
- miss flag
- death animation
- waiter
- door animation
- general idle animation

This task is FIRST Charge presentation only.

---

# PART L — FINAL REPORT

## 36. Final report

When complete, report:

1. Existing Charge flow found
2. Presentation architecture used/added
3. Exact right-hand object animated
4. Deck interaction target used
5. Hand reach duration
6. Deck pause duration
7. Card pull duration
8. Hand return duration
9. How the existing stored-Charge card presentation was integrated without duplication
10. Exact purple fire object/prefab used
11. Any project-owned fire prefab/material variants created
12. How multi-angle consistency was solved
13. How continuous burn behavior was achieved
14. Flame start delay
15. How flame lifetime follows authoritative Charge state
16. Charge interruption cleanup
17. Death cleanup
18. Bot Charge presentation
19. Remote-human presentation status
20. 3-player validation
21. 6-player validation
22. 8-player validation
23. Targeting regression result
24. EditMode test result
25. Console result
26. Exact files created/modified
27. Any remaining visual/timing issue requiring user tuning

Do not commit anything.

Stop when TASK 015 is complete.