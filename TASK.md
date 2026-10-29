# TASK 027 — Waiter Grounding, Left-Side Service & Realistic Beer Drinking

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and this entire TASK.md before modifying anything.

The Beer Order foundation and Waiter Delivery presentation already exist.

This task has TWO related goals:

1. Fix the remaining waiter presentation problems.
2. Add the first complete player beer-drinking presentation.

Do NOT redesign Beer Ordering, queue authority, waiter networking, or SHIELDS gameplay rules.

Beer remains purely cosmetic/social.

---

# 1. Existing systems to preserve

Inspect the current project before editing.

Preserve the existing:

- BeerOrderQueue
- BeerOrderCoordinator
- NetworkBeerOrderService
- WaiterDeliveryCoordinator
- WaiterDeliveryScheduler
- WaiterRoutePlanner
- WaiterPresenter
- WaiterCharacter
- BeerMug prefab
- WaiterTray prefab
- PlayerTableArea DrinkArea
- multiplayer participant → seat mapping
- rematch/match-generation architecture if currently present
- existing gameplay animation systems

Do not rewrite working systems unless a small targeted change is necessary.

---

# PART A — WAITER PRESENTATION FIXES

# 2. Current waiter grounding problem

The waiter currently looks like it is moving/floating in the air.

The walking bob itself is acceptable in concept, but its LOWEST point needs to visually correspond to ground contact.

The waiter should feel like an invisible pair of feet is touching the floor.

Desired visual:

lowest point of walking cycle
→ grounded

upper part of walking cycle
→ small lift

lowest point again
→ grounded

Not:

floating baseline
→ bob slightly above floor
→ still floating

---

# 3. Ground-contact baseline

Inspect the actual waiter prefab, mesh bounds, root position and tavern floor height.

Create/use a reliable waiter ground reference.

For example:

`WaiterGroundPoint`

or equivalent.

At the LOWEST point of the walking bob, the intended ground-contact point must visually align with the floor.

Do not guess using an arbitrary Y offset.

Determine it from:

- actual waiter visual dimensions
- current route Y
- actual tavern floor

---

# 4. Walking bob must rise FROM the ground

The walking bob should no longer oscillate around a floating vertical centre.

Use a one-sided ground-relative cycle.

Conceptually:

`groundedY + bobAmount`

where:

`bobAmount >= 0`

at all times.

The minimum of the cycle must be exactly the grounded baseline.

The body may rise slightly during the step and return to the ground.

Preferred amplitude remains subtle:

approximately:

`0.02–0.03 m`

Start near the existing:

`0.025 m`

Do not make it cartoonishly bouncy.

---

# 5. Preserve tray stability

The tray-carrying hand and tray must remain visually stable.

The waiter body/head may bob.

The tray should NOT inherit the full body bob.

Keep the existing architectural separation where possible.

Acceptance:

walking body visibly moves slightly
while
tray remains substantially level/stable.

---

# 6. Grounding vs proportions

If simply lowering the existing visual makes the waiter look unnaturally short:

do not blindly force the entire body mesh to floor level.

Instead establish a believable conceptual foot/contact point and adjust:

- body offset
- head offset
- standing visual offset
- bob baseline

as needed.

The final waiter should:

- look grounded
- retain believable SHIELDS-character proportions
- still align correctly with table/players
- not look like a seated character sliding around

Visual result is more important than preserving the old arbitrary standing Y.

---

# 7. Waiter service-side problem

The waiter currently tends to stop behind the player's RIGHT side and then reach across the player toward the player's LEFT DrinkArea.

This causes the delivery hand/path to visually pass through the seated player.

Fix this.

---

# 8. Waiter must always serve from player LEFT-REAR

For each recipient, calculate the waiter stop from the RECIPIENT'S local orientation.

The waiter should stop:

**behind and to the LEFT of the recipient.**

Think:

player facing table
→ waiter stands over player's left-back shoulder

Do NOT define this using global left/right.

Use seat-local/player-local orientation.

Conceptually:

`stop = seat position + behind offset + left offset`

Tune visually.

---

# 9. Suggested left-rear geometry

Use the current seat-relative system but change it so the waiter is approximately:

- 20–35° around toward the player's LEFT rear
- outside the chair/player footprint
- sufficiently behind the player not to block first-person view

Preserve the efficient route around the table.

Do not force all stops to global coordinates.

Must continue working for:

- 3 players
- 6 players
- 8 players

---

# 10. Beer must be placed on player's LEFT

Delivered mugs should remain on the player's LEFT side of the table presentation.

The existing DrinkArea is already intended to live on that side.

Inspect and retune it if necessary.

The delivered mug must avoid:

- Health cards
- Shield
- Charge card
- action/reveal card
- neighbouring player's cards
- table edge

---

# 11. No hand-through-body delivery

Because the waiter is now behind-left and the beer destination is also on the player's left:

the delivery hand path should remain primarily on the player's left side.

Do not simply lerp in a straight line if that line intersects:

- player's body
- head
- chair

Use a small procedural arc/control point if needed.

Preferred conceptual path:

tray
→ lift
→ outside/left clearance point
→ DrinkArea
→ release

A small Bezier/arc is acceptable.

Do not make a huge theatrical swing.

---

# 12. Revalidate route after the stop change

Changing stops from right-rear to left-rear may alter the best waiter walking route.

Re-run/retune route planning if required.

The waiter should still:

- remain outside chairs
- avoid the table
- avoid players
- not zig-zag unnecessarily
- return to the doorway cleanly

---

# PART B — DRINKABLE BEER STATE

# 13. Delivered mugs become drinkable

Every successfully delivered BeerMug should now represent an actual drinkable beer.

Each delivered mug needs a stable logical identity.

Prefer an identity based on existing authoritative delivery information, for example:

`BeerId = OrderId + RecipientSeat`

or another deterministic equivalent.

Do not use transient GameObject instance IDs as network identity.

---

# 14. Beer ownership

Only the player whose DrinkArea contains that beer may drink it.

Host validates ownership online.

A client must not be able to tell the host:

"I am seat X."

Use the sender → participant → seat mapping.

Never assume:

`clientId == seat`

---

# 15. Beer state

Track at minimum:

- BeerId
- owner seat
- remaining amount normalized `0..1`
- whether that beer is currently being drunk
- whether it is empty

Starting fill:

`1.0`

Empty:

`0.0`

Clamp all values safely.

---

# 16. Authority

Beer drinking state must be host-authoritative online.

Offline uses the equivalent local-authority path.

Do not make each client independently decide how much beer remains.

The authoritative state decides the final remaining amount.

Visual interpolation may run locally between authoritative events.

---

# 17. Do not spam transform/RPC updates every frame

This is a low-frequency cosmetic interaction.

Avoid sending mug/head transforms every frame.

Preferred architecture:

DRINK START:
host validates
→ broadcasts BeerId
→ authoritative starting fill
→ authoritative start time
→ drink rate

Clients locally animate from that data.

DRINK STOP / EMPTY:
host calculates authoritative final fill
→ broadcasts final remaining amount
→ everyone reconciles

Use the project's current NGO/network time architecture if appropriate.

A small occasional correction is fine.

---

# PART C — DRINK INPUT

# 18. Input

Preferred drinking input:

**hold `B` to drink**

Before using B, inspect existing controls.

If B is already meaningfully occupied:

choose another unused key and report it.

Do not silently break an existing input.

---

# 19. Drink prompt

When the local living human player has at least one non-empty delivered beer and drinking is currently allowed:

show a small gameplay prompt such as:

`DRINK (B)`

Keep it subtle and consistent with:

`ORDER! (O)`

Do not create a large new HUD.

---

# 20. Which beer is selected

Do NOT add a complex mug-selection UI yet.

If multiple beers exist:

drink the OLDEST non-empty beer belonging to that player.

This produces deterministic behavior on every machine.

Future tasks may add manual beer selection.

---

# 21. Hold behavior

Desired input:

B pressed
→ initiate pickup

B held
→ drink continuously

B released
→ stop drinking
→ return mug to its table slot

If the beer becomes empty while B is still held:

finish the drink naturally
→ return empty mug

Do not require repeated rapid key tapping.

---

# 22. Who can drink

For this task:

- living human players may drink their own beer
- bots do not automatically drink
- dead players cannot begin drinking

A human may drink beer during general match play as long as it does not conflict with an active gameplay animation/input state.

---

# PART D — HAND / MUG PICKUP

# 23. Preferred player hand

Use the player's **LEFT HAND** for beer drinking.

Reason:

- delivered beers live on the player's left
- this creates a natural reach
- it avoids reaching across the entire body

However, inspect existing left-hand gameplay animation usage.

Drinking must yield to gameplay actions that need the same hand.

---

# 24. No finger rig

The current hand meshes do NOT have individually animated finger bones.

Do NOT attempt to build a finger rig in this task.

Instead create a carefully tuned fixed mug-grip pose.

A small amount of hand/mug mesh overlap is acceptable and desirable if it makes the grip visually convincing.

It should look like the hand is actually holding the mug.

It must NOT look like:

- mug floating 5 cm away from palm
- mug attached only by air
- palm obviously missing the mug
- hand intersecting through the entire mug

---

# 25. Mug grip anchor

Add a project-owned grip reference to BeerMug.

For example:

`LeftHandGripAnchor`

Tune it using the actual mug geometry.

Prefer gripping around:

- the handle if the fixed hand mesh can convincingly fit it

or:

- the mug body/handle junction if that looks more believable with the current non-articulated hand.

Do not force a mathematically correct handle grip if it looks worse.

Visual realism wins.

---

# 26. Pickup contact

Sequence:

1. left hand leaves rest position
2. moves to the mug
3. palm/grip aligns accurately
4. brief contact settle
5. actual SAME mug object attaches to the hand
6. hand carries it toward mouth

Do not replace the mug with another spawned mug.

Same object:

DrinkArea
→ Hand
→ DrinkArea

---

# 27. Suggested pickup timing

Initial tuning:

reach:
`0.20–0.30 s`

preferred:
`0.25 s`

grip settle:
`0.08–0.12 s`

preferred:
`0.10 s`

bring to mouth:
`0.30–0.40 s`

preferred:
`0.35 s`

Use smooth easing.

---

# PART E — REALISTIC DRINKING POSE

# 28. Mouth target

Create/use a reliable presentation target near the character's mouth.

For example:

`DrinkMouthAnchor`

Place it relative to the visual head.

The mug rim should arrive naturally at the mouth.

Do not aim the mug at the centre of the face/head.

Inspect the actual HeadMesh/FaceMarker.

---

# 29. Mug must contact mouth visually

At drinking position:

- mug rim should be visually near/touching the mouth area
- hand must remain correctly attached to mug
- mug should not float in front of face
- mug should not bury deeply through the head

Small stylized overlap is acceptable.

---

# 30. Head raises while drinking

During active drinking:

the character's VISUAL head should pitch upward slightly.

This procedural drinking pitch must be layered onto the existing head presentation.

Do NOT destroy or replace the normal player head-yaw/head-pose system.

---

# 31. Head raise depends on remaining beer

The amount the head pitches upward should be inversely related to how much beer remains.

Reason:

full mug
→ only modest tilt required

nearly empty mug
→ character must tilt head/mug more to get the remaining beer

Use remaining normalized beer amount:

`fill = 1 → small tilt`
`fill → 0 → larger tilt`

Suggested initial visual range:

full beer:
approximately `3–5°` upward head pitch

nearly empty:
approximately `10–14°` upward head pitch

Preferred starting values:

`4° → 12°`

Tune after visual inspection.

Do not make the character stare vertically at the ceiling.

---

# 32. Mug tilt also increases as beer decreases

For the same reason, mug tilt should increase gradually.

Suggested initial range:

full beer:
approximately `30–40°`

near empty:
approximately `65–75°`

Preferred starting values:

`35° → 70°`

Tune so the rim stays believable against the mouth.

The head and mug should feel like ONE coordinated action.

---

# 33. Smooth relationship

Do not jump between discrete tilt states.

Use a smooth curve/interpolation based on normalized remaining beer.

A slightly eased/non-linear curve is acceptable if it looks more natural.

---

# PART F — LOCAL CAMERA / HEAD-LOOK BEHAVIOR

# 34. Do not move the camera with the procedural drinking head tilt

This is critical.

The local player's camera should NOT inherit the additional drinking head pitch/vertical movement.

The visual character should appear to tilt their head to other players, but the local camera must remain comfortable.

Given the current hierarchy, prefer applying the drinking offset to the VISUAL head layer rather than the gameplay CameraAnchor/HeadPivot if possible.

Inspect the actual hierarchy before changing it.

---

# 35. Lock vertical look while actively drinking

During the actual drink phase:

the local player may continue turning LEFT/RIGHT.

Yaw remains enabled.

But vertical mouse-look/pitch should be temporarily frozen at the value it had when drinking began.

The player must not manually look up/down while the procedural drinking head pose is active.

This avoids the animation looking broken.

---

# 36. Camera position/height remains unchanged

Do not bob or lift the local camera because the character's visual head raises.

Camera world/local height remains unchanged by drinking.

Do not create a fake "camera drinks with mug" effect.

---

# 37. Restore look correctly

When drinking ends/cancels:

- vertical look control returns
- previous pitch remains coherent
- yaw remains continuous
- no camera snap
- no cursor state change
- no stuck suppression flag

Reuse/extend `SeatedHeadLook` cleanly rather than disabling the entire component.

---

# PART G — BEER LIQUID

# 38. Add visible liquid to BeerMug

The BeerMug should contain visible beer.

Use a project-owned child/material.

Desired color:

- warm yellow/golden/amber
- slightly translucent if appropriate
- readable but not neon
- not strongly emissive

Think simple stylized beer.

---

# 39. Do not modify third-party mug source destructively

The current `BeerMug.prefab` is project-owned.

Add liquid support there or in another project-owned variant.

Do not modify original third-party package FBX/materials destructively.

---

# 40. Liquid amount must visually decrease

When:

remaining beer = 1.0
→ mug appears full

remaining beer decreases
→ visible liquid level decreases

remaining beer = 0
→ no beer volume remains visible

The visual amount must correspond to authoritative beer state.

---

# 41. Preferred liquid implementation

Inspect the actual mug interior.

Use the cleanest robust implementation.

Preferred options in order:

1. small interior beer-volume mesh + controlled fill shader
2. interior volume whose visible height is driven by fill
3. correctly constrained liquid surface/volume solution

Avoid a loose yellow plane that visibly sticks outside the mug when tilted.

If a custom lightweight shader produces a cleaner fill, use it.

Keep it project-owned.

---

# 42. Gravity-aware liquid appearance

When the mug tilts, the beer should not obviously look glued to the mug at an impossible angle.

Aim for at least an approximate gravity-aware liquid surface.

A full fluid simulation is NOT required.

Good enough solutions include:

- shader clipping by a world-up fill plane
- counter-oriented liquid surface constrained inside the mug
- another simple deterministic visual technique

Do not add real fluid physics.

---

# 43. Liquid must not leak visually

Test at:

- full
- 75%
- 50%
- 25%
- almost empty

and at the major drinking tilt angles.

The yellow beer should not visibly project far outside the cup.

Small hidden/interior clipping is acceptable.

---

# 44. Drink rate

Expose a tunable drink rate.

Suggested starting behavior:

a full beer should take roughly:

`4–6 seconds`

of continuous active drinking to empty.

Preferred starting point:

approximately `5 seconds`

from 100% to 0%.

Do not empty a full mug instantly.

---

# 45. Beginning and end of drinking do not consume beer

Beer depletion should occur primarily during the actual mouth/drink phase.

Do not drain significant beer while the hand is still reaching from the table.

Stop draining when the mug begins returning.

---

# PART H — RETURN / EMPTY MUG

# 46. Releasing B

When B is released before empty:

- stop beer depletion
- smoothly reduce head drink pitch
- smoothly untip mug
- move hand/mug back to original DrinkSlot
- place the SAME mug
- hand returns to rest

Remaining beer persists.

The next B hold continues from that remaining amount.

---

# 47. Empty beer

When remaining amount reaches zero:

- finish a short natural end pose
- mug becomes visually empty
- head/mug return
- empty mug is returned to its original DrinkSlot

Do NOT automatically delete the mug yet.

The empty mug remains as table clutter for now.

Future cleanup can handle empty mugs.

---

# 48. Slot identity

A mug must return to the same logical DrinkSlot it came from.

Do not let returning a mug corrupt the DrinkArea slot allocation used by waiter deliveries.

Track occupancy explicitly if needed.

This is important because future waiter deliveries must not reuse an occupied slot accidentally.

---

# PART I — GAMEPLAY ANIMATION CONFLICTS

# 49. Drinking must never block authoritative gameplay

Beer is cosmetic.

Gameplay always wins.

If drinking conflicts with a required gameplay animation/action:

cancel or gracefully return the drink presentation.

Do NOT delay:

- Attack
- Replace Shield
- Charge
- Charged Attack
- targeting
- authoritative turns

because the player is drinking.

---

# 50. Do not start drinking while left hand is already busy

Do not start a drink during states such as:

- target card draw/throw
- another left-hand presentation
- another modal action that needs that hand
- death
- match transition

Use existing presentation state where possible.

Do not infer solely from animation time if a clearer busy flag exists.

---

# 51. If gameplay needs the hand during drinking

If gameplay begins while the player is drinking:

preferred:

1. stop beer depletion immediately
2. quickly and safely return/release mug
3. clear drink head pose
4. restore look state
5. let gameplay presentation proceed

Use a short emergency return if necessary.

Do not wait for the full leisurely drinking animation.

---

# 52. Weapon compatibility

Because drinking uses the left hand:

the right-hand weapon presentation may remain independent where safe.

However, if an action requires coordinated two-hand/card presentation and conflicts visually:

gameplay presentation wins.

Do not redesign weapon logic.

---

# PART J — MULTIPLAYER PRESENTATION

# 53. Everyone sees drinking

When a human drinks online:

all participants should see:

- correct player
- correct mug
- hand picking up mug
- mug reaching mouth
- head visual tilting
- beer level dropping
- mug returning
- correct final fill

The owner and remote clients should agree.

---

# 54. Local camera is owner-only behavior

Remote clients do not need camera-lock information.

The local player:

- keeps yaw
- loses vertical look only while actively drinking

Remote clients only see the resulting character presentation.

Do not network camera input.

---

# 55. Host validation

At DrinkStart host validates:

- match state allows the player to exist/drink
- sender maps to the owner seat
- player is alive
- BeerId exists
- BeerId belongs to that seat
- remaining amount > 0
- player is not already drinking another beer
- request is not obviously stale from an old match generation

If current rematch architecture has `MatchGeneration`, include/use it.

Do not allow a prior-match DrinkStop message to affect a current-match mug.

---

# 56. One active beer per player

A player may drink only one mug at a time.

Other mugs remain on the table.

Do not permit overlapping drink animations.

---

# 57. Disconnect/death during drinking

If the drinking player:

- dies
- disconnects
- match ends
- rematch starts
- GameScene tears down

stop drinking safely.

Clear:

- active drink state
- mug hand attachment
- visual head offset
- look suppression
- stale network callbacks

No exceptions.

---

# PART K — WAITER + LIQUID INTEGRATION

# 58. Waiter tray mugs should already contain beer

When mugs are created on the waiter's tray:

they should visually start full.

The same mug object then moves:

tray
→ waiter hand
→ player's DrinkArea

and remains full until the recipient actually drinks it.

---

# 59. Waiter does not spill beer

Do not animate liquid depletion during waiter delivery.

Waiter movement should keep mug beer state at 1.0.

No need to simulate sloshing.

---

# 60. Invalid/unserved mug

If a mug remains on the waiter tray because its recipient became invalid:

it leaves with the waiter and is cleaned up normally.

Do not register it as a persistent drinkable beer.

---

# PART L — MANUAL VISUAL VALIDATION

# 61. Waiter grounding test

Watch waiter from multiple seats.

Confirm:

- lowest bob point visually reaches ground
- waiter no longer appears to hover
- body bob remains subtle
- tray remains stable
- no ground penetration that looks obviously wrong

---

# 62. Left-rear service test

At 3 / 6 / 8 players:

confirm waiter always stops on recipient LEFT-REAR.

Confirm:

- delivery hand does not pass through recipient body
- mug ends on recipient LEFT
- route still avoids chairs/table

---

# 63. Grip validation

Observe drinking:

- front/side view
- remote player view if available
- first-person local view

Confirm:

- hand visually touches/grips mug
- mug does not float from hand
- slight natural overlap is acceptable
- no severe hand-through-mug artifact

Tune the grip anchor rather than accepting a visibly floating mug.

---

# 64. Head/mug relationship

Test beer at approximately:

- 100%
- 75%
- 50%
- 25%
- near empty

Confirm:

less beer
→ progressively more mug tilt
→ progressively more head-up tilt

The result should look like the character is trying to get the remaining liquid.

---

# 65. Camera validation

While actively drinking:

move mouse horizontally
→ yaw works

move mouse vertically
→ camera/head gameplay pitch does not change

Camera position/height stays unchanged.

Release drink:
→ normal vertical look returns with no snap.

---

# 66. Partial drinking

Hold B for only part of a beer.

Release.

Confirm:

- mug returns
- remaining beer stays partially full
- liquid remains visible at correct level

Drink again.

Confirm:

- same mug is used
- it resumes from existing amount
- head/mug tilt begins according to current remaining amount

---

# 67. Empty beer

Drink one full beer to zero.

Confirm:

- liquid disappears/reads empty
- mug returns to table
- B does not try to drink that empty mug again if another non-empty mug exists
- if all beers are empty, DRINK prompt disappears/disables

---

# 68. Multiple beers

Have at least two delivered beers.

Confirm:

- B chooses oldest non-empty beer
- partial/empty state of one does not alter another
- after first becomes empty, next drink uses the next non-empty mug
- DrinkArea slot occupancy remains correct

---

# 69. Gameplay interruption

Start drinking and then trigger/enter a gameplay presentation conflict if practical.

Confirm:

- drink cancels cleanly
- no stuck mug
- no stuck hand
- no stuck head tilt
- no stuck vertical-look lock
- gameplay animation proceeds normally

---

# 70. Offline validation

Test at minimum:

- 3 players
- 6 players
- 8 players

Validate:

- waiter grounding
- left-rear service
- left-side mug placement
- drinking
- partial beer
- full empty
- multiple mugs
- gameplay continuing during drinks

Bots do not need to drink.

---

# 71. Online validation

Prefer a real two-human test:

Human A orders for A + B.

After delivery:

A drinks their beer.

Both A and B should see A's:

- hand pickup
- mug movement
- head tilt
- liquid decrease
- return

Then B drinks.

Both should see B correctly.

If a real second client is NOT tested, explicitly state that.

---

# PART M — TESTS

# 72. Pure/state tests

Add focused tests where appropriate for:

- BeerId ownership
- full → partial remaining amount
- clamping 0..1
- wrong sender cannot drink another seat's beer
- empty beer cannot start
- one active drink per player
- start/stop calculates correct authoritative fill
- old match-generation messages ignored if generation exists
- drink state clears on match reset

Do not write brittle transform-animation unit tests.

---

# 73. Existing tests

Run the COMPLETE current EditMode suite.

Previous historical baseline before later rematch work was 291/291.

Do NOT blindly expect 291 if TASK 026 added more tests.

Report the CURRENT exact total and result.

Do not weaken existing tests.

---

# 74. Console

Final validation should have:

- 0 project-code errors
- 0 missing references
- 0 drink-state exceptions
- 0 stale BeerId/network warnings
- no stuck hand/head state
- no invalid slot reuse
- no waiter routing exceptions

Report unrelated third-party warnings separately.

---

# 75. Out of scope

Do NOT add:

- gameplay buffs/debuffs from alcohol
- drunken camera effects
- screen blur
- player intoxication stats
- bot drinking AI
- finger rigging
- skeletal IK package
- physics liquid simulation
- mug throwing
- breaking mugs
- beer purchasing/currency
- audio

Those can come later.

---

# 76. Final report

Report:

1. Root cause of waiter floating appearance
2. Ground-contact solution
3. Final waiter grounded Y/contact method
4. Bob amplitude/frequency after tuning
5. Confirmation tray still remains stable
6. Left-rear stop calculation
7. Player-local left/right basis used
8. Delivery-hand path change
9. DrinkArea position changes
10. BeerId/state architecture
11. Drinking authority/network flow
12. Drink input chosen
13. Beer-selection rule
14. Which player hand drinks
15. Mug grip-anchor implementation
16. Grip pose tuning values
17. Mouth-anchor implementation
18. Pickup/carry/return timings
19. Head tilt min/max
20. Mug tilt min/max
21. How tilt depends on remaining beer
22. Camera/head-look separation
23. Vertical-look lock implementation
24. Liquid visual implementation
25. Liquid material/color
26. How fill amount is rendered
27. How liquid behaves while mug tilts
28. Full-beer drink duration/rate
29. Partial-drink behavior
30. Empty-mug behavior
31. DrinkArea slot occupancy handling
32. Gameplay interruption behavior
33. Rematch/match teardown cleanup
34. 3-player validation
35. 6-player validation
36. 8-player validation
37. Partial beer validation
38. Empty beer validation
39. Multiple-beer validation
40. Real second-client status
41. Gameplay regression
42. Full EditMode test result
43. Console result
44. Exact files created/modified
45. Inspector/material tuning values
46. Remaining visual limitations

Do not commit anything.

Stop when WAITER FIXES + BEER DRINKING PRESENTATION are complete.