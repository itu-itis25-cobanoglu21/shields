# TASK 032 — Rigged Hand Contact, Grip & Rest-Pose Corrections

Read `CLAUDE.md`, `SHIELDS_RULES.md`, inspect the CURRENT rigged-hand implementation and the provided reference screenshots, then execute this task.

TASK 030 integrated the new rigged hands.
TASK 031 added the pose/blending system and existing interaction poses.

The architecture is now correct.

DO NOT redesign the hand system.

DO NOT implement emotes yet.

This task fixes FOUR specific visual problems visible in the current game:

1. Weapon hand intersects deeply through the gun.
2. Deck-contact hand does not fully sit on the deck.
3. Waiter delivery hand intersects the beer mug and follows an unnecessarily strange path.
4. Idle hands are pitched too far forward even though their inward rotation is good.

These are visual pose/contact corrections only.

---

# 1. Use the supplied screenshots as the source of truth

Inspect the four screenshots supplied with this task.

They show the actual current problems.

Do not tune only from prefab/Scene view.

Reproduce the relevant gameplay state in Play Mode and inspect the result from the same kinds of angles.

---

# PART A — WEAPON GRIP

# 2. Current weapon problem

The new rigged right hand currently passes deeply THROUGH the weapon.

The screenshot shows:

- palm intersecting the weapon body
- fingers penetrating the stock/trigger area
- weapon visually embedded inside the hand

This is not acceptable.

The hand should HOLD the outside of the weapon.

---

# 3. Desired weapon relationship

The weapon grip should look approximately like:

weapon grip/stock
→ palm resting against OUTSIDE surface
→ fingers curling AROUND it
→ thumb supporting/opposing grip
→ index near trigger/trigger guard

The weapon must not occupy the centre of the palm volume.

---

# 4. Fix position before over-curling fingers

Do not try to solve the clipping only by curling fingers differently.

First inspect the actual relation between:

- weapon grip anchor
- hand wrapper
- rigged-hand visual
- palm surface

Move/rotate the HAND relative to the weapon so the palm sits externally against the grip.

Then tune fingers around that relationship.

---

# 5. Weapon grip anchor

If the current weapon attachment point was authored for the old hand geometry:

retune the grip attachment/visual offset for the NEW hand.

Do not change weapon aiming logic.

Do not change muzzle position.

Do not change target locking.

Do not change firing trajectory.

This is only how the hand visually holds the weapon.

---

# 6. Weapon fingers

After the hand is correctly positioned:

pose the fingers to read as a stylized grip.

Desired:

- Middle + Ring: wrap around grip without penetrating through it.
- Index: near/on trigger or trigger guard.
- Thumb: naturally opposing/supporting the grip.

Do NOT force extreme >60° curls if they cause source-rig pinching.

Moderate curls + proper hand placement are preferred.

---

# 7. Weapon validation

Inspect from:

- side
- front/3⁄4
- across-table
- local player perspective

Acceptance:

- palm does not intersect weapon body
- weapon does not pass through hand
- fingers visibly wrap externally
- index reads near trigger
- no large air gap
- firing animation still works
- fire/index squeeze still works

---

# PART B — DECK CONTACT

# 8. Current deck problem

When the hand rests on the deck, one/more fingers — especially the middle region visible in the screenshot — remain above the deck.

The pose looks like:

some fingers contact
while
another finger floats.

The hand should look physically placed on the deck.

---

# 9. Re-measure deck contact from the actual rigged hand

Do not rely on the old `PalmContact` geometry.

Inspect:

- palm plane
- fingertip positions
- deck top plane
- finger joint rotations

Tune the contact pose so the hand actually follows the deck surface.

---

# 10. Desired deck pose

When resting on the draw pile:

- palm should sit just above/on the top card
- all fingers intended to touch should visually contact the deck
- no finger should obviously float several millimetres above
- no finger should penetrate deeply through the deck

Small stylized contact overlap is okay.

---

# 11. Finger-specific pose is allowed

Do not force the same curl/value onto each digit.

The middle/index/ring may require slightly different joint angles because:

- finger lengths differ
- the hand mesh is asymmetrical
- deck surface is flat

Tune each relevant finger individually.

---

# 12. Preserve deck animation

Do not change:

- reach timing
- card draw timing
- Charge timing
- card throw trajectory
- target-selection logic

Only pose/contact geometry.

---

# PART C — WAITER MUG DELIVERY

# 13. Current waiter mug problem

The screenshot shows the delivery hand wrapped THROUGH/AROUND the mug in an unnatural way.

The hand intersects deeply with the mug body/handle.

The desired waiter delivery does NOT require a realistic closed grip.

---

# 14. Desired waiter delivery hand

Return to the simpler readable presentation:

waiter hand approaches mug from ABOVE
→ hand remains relatively OPEN
→ palm/fingers hover just above / lightly around the mug top
→ mug moves with the hand
→ hand places mug on table
→ hand returns

It should look like the waiter is guiding/carrying the mug from above.

Do NOT close the puffy fingers around the mug body.

---

# 15. Waiter mug pose

Create a waiter-specific top-carry pose.

Suggested visual direction:

- palm approximately facing down toward mug
- fingers gently open
- fingers may bend slightly around the mug's upper silhouette
- no finger should pierce the mug
- no deep handle interaction required

This pose may intentionally differ from the PLAYER drinking grip.

Do not reuse the player mug grip if it looks worse.

---

# 16. Same mug object

Preserve:

`Tray → Waiter Hand → DrinkArea`

The actual same mug object still moves.

Do not replace it with another object.

---

# 17. Simplify delivery-hand trajectory

The current delivery-hand route is visually strange/overcomplicated.

The waiter body already stays on the constant-radius circular route.

Keep that.

The DELIVERY HAND should now use a much simpler route.

Desired:

mug on tray
→ small lift
→ direct smooth reach toward target DrinkArea
→ small controlled descent onto table
→ release
→ direct smooth return to its normal/rest position

Do not make the hand:

- rise dramatically
- travel through unnecessary control points
- swing around the player
- perform a large arc
- behave like a catapult

---

# 18. Direct but collision-conscious path

The hand should move almost directly from tray to table.

A small curve is allowed only to clear:

- tray edge
- chair back
- player's left side

Use the minimum necessary clearance.

The movement should read as:

"waiter reaches and places beer"

not:

"floating hand performs a complicated pathing maneuver."

---

# 19. Return position

After placing the mug:

the delivery hand must return to its correct waiter rest position.

Do not leave it:

- offset
- partially extended
- in mug grip
- at a different height

Pose and wrapper transform should both return cleanly.

---

# 20. Waiter validation

Test:

- 3 players
- 6 players
- 8 players

and multiple delivery angles.

Acceptance:

- hand does not go inside mug
- hand is visibly above/around mug
- trajectory is simple and direct
- no player/body/chair intersection
- mug reaches correct DrinkArea
- same mug object delivered
- hand returns correctly

---

# PART D — IDLE / NEUTRAL HAND ORIENTATION

# 21. Current idle problem

The inward-facing angle of the hands is GOOD.

Keep that character.

The problem is the additional forward pitch.

From the screenshot:

the hand's top and bottom are at noticeably different front/back depth, so the hand looks excessively tilted forward.

This is too much.

---

# 22. Desired idle orientation

Keep:

- palms rotated inward toward body
- slight Left/Right asymmetry
- relaxed finger curl

Reduce:

- forward/backward pitch

The hand should be much more vertically neutral.

Conceptually, when viewed from the side:

top of hand
and
bottom/fingertips

should be at approximately similar forward/back depth.

Not mathematically exact, but visually close.

---

# 23. Preserve inward cant

Do NOT simply zero all idle wrist rotations.

The current inward orientation gives the hands personality and looks good.

Only reduce the axis responsible for the excessive forward lean.

---

# 24. Natural relaxed pose

Final idle should feel:

- slightly inward
- slightly relaxed
- fingers softly curled
- mostly vertical
- not mechanically straight
- not pitched dramatically forward

Use Play Mode and visual judgement to find the best angle.

Try multiple values.

Do not keep the first number that technically works.

---

# PART E — POSE SYSTEM

# 25. Preserve existing HandPoses architecture

Keep:

- HandRig
- HandPoses
- pose blending
- Action > Grip > Emote > Rest priority
- ResetToNeutral
- smooth normals

Do not rewrite these.

Update the existing pose definitions/offsets cleanly.

---

# 26. State-specific poses are expected

Do not try to make one universal pose handle everything.

The following should remain distinct:

- Idle
- Deck contact
- Card carry
- Weapon grip
- Player mug grip
- Waiter tray support
- Waiter mug top-carry

This is intentional.

---

# 27. Do not regress existing interactions

After these corrections verify:

- First Charge
- target-selection deck rest
- card pickup
- card throw
- weapon draw
- weapon aim
- weapon fire
- weapon holster
- beer drinking
- waiter tray
- waiter delivery

---

# 28. First-person and remote visual checks

Weapon and deck pose must look good from:

- first person
- another player's perspective

Waiter delivery should be checked from seated players around the table.

---

# 29. No emotes yet

Do NOT implement:

- point
- middle finger
- handshake
- wave
- emote UI
- emote network messages

Finish these four defects first.

---

# 30. Tests

Run the full CURRENT EditMode suite.

Current known baseline:

`355 / 355`

Report the actual result.

---

# 31. Console

Final clean gameplay validation should have:

- 0 HandRig errors
- 0 pose exceptions
- 0 weapon-presentation errors
- 0 waiter-delivery errors
- 0 stuck hand poses

Report unrelated existing warnings separately.

---

# 32. Final report

Report:

1. Weapon hand root/grip offset before and after
2. Weapon wrist orientation change
3. Final weapon finger pose
4. Weapon/index trigger result
5. Confirmation weapon no longer intersects palm
6. Deck-contact offset changes
7. Final per-finger deck pose
8. Confirmation all intended fingers contact deck
9. Waiter top-carry mug pose
10. Waiter hand/mug relationship
11. Old waiter hand trajectory
12. New simplified trajectory
13. Maximum hand lift during delivery
14. Confirmation hand returns to original rest position
15. Idle pitch before/after
16. Idle inward angle preserved
17. Final Left idle orientation
18. Final Right idle orientation
19. Charge regression
20. Card throw regression
21. Player beer-drinking regression
22. Waiter tray regression
23. 3-player result
24. 6-player result
25. 8-player result
26. EditMode result
27. Console result
28. exact files modified
29. anything still visually wrong before starting emotes

Do not commit anything.

Stop before implementing emotes.