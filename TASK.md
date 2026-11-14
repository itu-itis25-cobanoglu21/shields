# TASK 036 — Final FBX Hand Replacement & Emote Animation Integration

Read `CLAUDE.md`, `SHIELDS_RULES.md`, inspect the CURRENT project, and inspect the NEW final hand FBX before modifying anything.

The user has now manually finalized the hand in Blender.

Only the FINAL RIGHT-hand FBX has been added to Unity.

This FBX is now intended to become the authoritative hand visual/rig source for SHIELDS.

It also contains Blender-authored poses/actions. Their names/descriptions indicate what each pose represents.

Your task is to:

1. inspect the final Right-hand FBX
2. create a proper mirrored LEFT-hand asset from it
3. replace ALL currently used old hand visuals/rigs throughout the project
4. use the Blender-authored poses/actions as the new source of truth
5. connect/blend those poses into the existing gameplay and emote systems
6. preserve the existing gameplay/network architecture

Do not commit anything.

---

# 1. Inspect the new final FBX first

Locate the newly added final hand FBX.

Before changing prefabs, inspect:

- exact FBX path
- mesh hierarchy
- armature hierarchy
- bone names
- skin weights
- bind/rest pose
- materials
- normals
- triangle count
- scale
- axes/orientation
- thumb side
- wrist/root convention
- all imported animation clips / Blender Actions

List every Action/clip found.

The Action names/descriptions should make their purpose clear.

Use those names as the primary guide for mapping poses.

Do not assume the old Meshy bone hierarchy still applies.

---

# 2. Final hand source

Treat the new FBX as the new FINAL hand source.

The previous GLB/Meshy hand system was useful for development but should no longer be the active visual source once this integration is validated.

Keep old assets/code for rollback where harmless.

Do not delete old source GLBs yet.

---

# 3. Create the LEFT hand from the final RIGHT hand

Only the final RIGHT hand was supplied.

Create a proper mirrored LEFT-hand version.

Preferred:

use Blender or another proper asset-authoring step to mirror:

- mesh
- armature
- bind pose
- skin weights
- animation Actions

together.

Do NOT use permanent runtime:

`scale.x = -1`

as the final solution.

The final Left asset must have:

- correct normals
- correct winding
- correct skinning
- positive final transform scale
- equivalent rig hierarchy
- mirrored thumb position
- equivalent Actions/poses

Keep the original Right FBX unchanged.

Create a derived Left asset with a clear project path/name.

---

# 4. Mirror the authored Actions too

The supplied Right-hand FBX contains final Blender-authored hand poses/actions.

The generated Left hand must have correctly mirrored versions of the relevant poses.

Do not simply mirror the mesh while leaving animations oriented as Right-hand poses.

Verify each pose visually on the Left hand.

---

# 5. Inspect Action names and map them semantically

Read every imported Blender Action/clip.

Their names should describe their intended purpose.

Examples may include concepts such as:

- Idle / Neutral
- Point
- Middle Finger
- Handshake Offer
- Handshake Grip
- Weapon Grip
- Beer Grip
- Deck/Card pose
- Charge pose
- Waiter poses

Do not require these exact names.

Use the actual names in the asset.

Build a clear mapping from:

`FBX Action`
→
`SHIELDS gameplay/emote state`

Report that mapping.

---

# 6. Blender-authored poses are source of truth

Where the FBX provides a specific authored pose:

use that authored pose.

Do NOT keep old guessed Unity curl values simply because they already exist.

The manually authored Blender poses should replace the old approximations.

The existing Unity hand system should become responsible mainly for:

- selecting a pose
- blending between poses
- moving the floating hand through space
- networking emote state

not re-inventing the finger pose.

---

# 7. Preserve outer procedural architecture

Do NOT rewrite the existing floating-hand movement system.

Preserve:

- `LeftShoulderAnchor`
- `RightShoulderAnchor`
- `LeftHandAnchor`
- `RightHandAnchor`
- `Hand_L`
- `Hand_R`
- `PalmContact`
- existing procedural movement trajectories

The new FBX rig sits INSIDE the existing hand wrapper.

Conceptually:

`Hand_R`
→ FinalRiggedHand_R

`Hand_L`
→ FinalRiggedHand_L

Outer transforms continue controlling WHERE the hand moves.

The FBX pose/action controls HOW the hand/fingers are posed.

---

# 8. Replace all active old hands

Audit the project for old-hand visual references.

Replace them wherever they are actively used.

At minimum inspect:

- `PlayerCharacter.prefab`
- `Waiter.prefab`
- production Customize character preview
- Main Menu character preview if one exists
- any current UI/wardrobe preview character
- any gameplay test/presentation prefab that is actually used

Do not leave obvious active characters using the previous hands.

Do not blindly modify obsolete/disposable concept scenes unless they are still actively used.

Report what you changed and what intentionally remained old.

---

# 9. Update semantic rig mapping

The project currently has `HandRig` / semantic hand support.

Adapt it to the FINAL FBX skeleton.

Map at minimum:

- Wrist / root
- Thumb
- Index
- Middle
- Ring

There are only four digits.

Do not keep old `Bone_00X` assumptions if the final FBX uses different names.

Prefer explicit semantic references based on the new final skeleton.

---

# 10. Preserve pose priority

Keep the existing priority architecture:

`Action > Grip > Emote > Rest`

This remains important.

Gameplay always wins over emotes.

Do not allow an emote pose to overwrite:

- weapon
- beer
- card/deck
- Charge
- required waiter interaction

---

# 11. Pose blending

Connect the authored FBX poses with smooth transitions.

The hand should not snap between poses.

Examples:

Idle
→ Point

Idle
→ Middle Finger

Idle
→ Handshake Offer

Handshake Offer
→ Handshake Grip

Idle
→ Weapon Grip

Idle
→ Beer Grip

Use the existing pose-blending architecture if possible.

Do not introduce a giant Animator Controller if lightweight procedural clip/pose sampling fits better.

---

# 12. Pose implementation strategy

If the Blender Actions are short static pose clips:

extract/sample their target bone rotations and feed them into the existing pose system.

If they contain actual multi-frame authored animation:

preserve useful authored timing where appropriate.

Do not force every Action into one frame if it clearly contains intentional motion.

Choose the simplest robust integration after inspecting the real FBX.

---

# 13. Neutral / Idle

Use the authored final Neutral/Idle pose if one exists.

This becomes the actual gameplay rest pose.

`ResetToNeutral()`

should return to this final authored pose.

Do not return to the old Meshy bind pose.

---

# 14. Pointing

Use the final authored Point pose if present.

Preserve the existing Point emote behavior:

- RIGHT hand
- Hold `W`
- free look direction
- NO player target lock
- palm/down orientation established by the final pose
- scroll-based small reach adjustment if currently working

Do not reintroduce target snapping.

The FBX determines the final wrist/finger pose.

The existing emote presenter determines the floating-hand direction/position.

---

# 15. Middle Finger

Use the final authored Middle Finger pose if present.

Preserve:

- LEFT hand
- Hold `Q`

The authored pose should determine:

- straight Middle finger
- closed neighbouring fingers
- sideways/inward Thumb
- wrist orientation

Do not approximate it again with old Unity curl values.

---

# 16. Handshake

Use authored:

- Handshake Offer
- Handshake Grip

poses if present.

Preserve the current handshake state/network architecture.

Offer:
→ open hand toward table

Matched:
→ both hands move to shared handshake position
→ blend into authored Grip pose
→ perform existing short shake
→ release
→ return

Do not bake table/world movement into finger animation.

---

# 17. Existing gameplay poses

Inspect whether the FBX contains authored poses for current gameplay states.

Where available, use them.

Validate:

- deck rest
- Charge draw
- card pickup
- card carry
- card throw
- weapon grip
- beer grip
- waiter tray support
- waiter mug delivery

If an exact authored pose does NOT exist:

preserve the best current pose rather than inventing unnecessary new behavior.

---

# 18. Weapon

Ensure the new final hand still visually holds the weapon from the OUTSIDE.

No deep palm/weapon intersection.

Preserve:

- draw
- aim
- lock
- fire
- holster

If an authored weapon pose exists, prefer it.

---

# 19. Beer

Ensure the final hand works with:

- pickup
- drinking
- return
- partial beer
- empty beer

If an authored Beer Grip exists, use it.

Retune only grip anchor/alignment if necessary.

Do not change BeerLedger/networking.

---

# 20. Deck/card interactions

Validate the final hand against the physical deck/card geometry.

No floating fingers.

No excessive overlap.

Keep existing action timing and trajectories.

Use authored poses where provided.

---

# 21. Waiter

Update the waiter to the new final hands.

Preserve:

- tray
- circular route
- mug delivery
- bob/height
- delivery trajectory

Use final authored waiter/tray/delivery pose if present.

---

# 22. Remove dependency on GLB-specific fixes for final hands

The FINAL FBX hands should not require the custom GLB importer.

Do not run GLB-specific:

- winding correction
- GLB weight cleanup
- GLB smooth-normal generation

on the new FBX.

Those systems may remain for legacy assets/rollback.

Do not delete them yet.

Report whether any current runtime system still depends on them.

---

# 23. Validate rig deformation

Because the final Blender asset was manually prepared:

test the authored poses for:

- no tearing
- no spike vertices
- no knuckle collapse
- no neighbour finger dragging
- no palm collapse

If a final FBX pose itself has a visible defect, report it rather than silently replacing the authored pose.

---

# 24. Networking

Do not redesign networking.

Preserve current:

- Point emote synchronization
- Middle Finger synchronization
- Handshake mutual intent / host pairing
- MatchGeneration safety
- participant → seat mapping

Only change how the hands visually pose.

Never assume:

`clientId == seat`

---

# 25. Re-seat/rematch

Verify final hands survive:

- initial seating
- randomized seating
- rematch
- player rebuild/rebind

No old hands left behind.

No duplicate hand rigs.

---

# 26. 3 / 6 / 8 players

Validate at:

- 3 players
- 6 players
- 8 players

Check:

- correct Left/Right hands
- correct scale
- no missing materials
- no old hands
- no duplicate renderers

---

# 27. Emote validation

Explicitly test:

### Point
- hold W
- look around freely
- scroll reach
- release
- returns cleanly

### Middle Finger
- hold Q
- authored pose clearly visible
- release
- returns cleanly

### Handshake
- offer pose
- matched grip pose
- shake
- return

If a real second client cannot be run, clearly report that handshake network visuals still need manual 2-client validation.

---

# 28. Gameplay conflict validation

Verify:

Point
→ weapon begins
→ Point yields

Middle Finger
→ beer/deck action begins
→ Middle yields

Handshake
→ right-hand gameplay action begins
→ handshake cancels/yields safely

Do not break pose priority.

---

# 29. Performance

Report:

- triangles per hand
- bone count
- SkinnedMeshRenderer count
- approximate total hand triangles at 8 players

Do not unnecessarily optimize the final asset if performance is already acceptable.

---

# 30. Tests

Run the complete CURRENT EditMode suite.

Do not assume an old count.

Report exact result.

Do not weaken tests.

---

# 31. Console

Final validation:

- 0 missing bone errors
- 0 FBX animation errors
- 0 pose mapping errors
- 0 stuck hand state
- 0 gameplay presentation exceptions

Report unrelated warnings separately.

---

# 32. Final report

Report:

1. Exact final Right FBX path
2. Right-hand mesh/rig structure
3. All Actions/clips found
4. How the Left hand was generated
5. Exact Left-hand asset path
6. Confirmation no permanent negative scale
7. Confirmation Actions were mirrored correctly
8. New semantic bone mapping
9. Action → SHIELDS state mapping
10. Final Neutral/Idle integration
11. Point integration
12. Middle Finger integration
13. Handshake Offer integration
14. Handshake Grip integration
15. Weapon result
16. Beer result
17. Deck/card result
18. Charge result
19. Waiter result
20. Customize/Main Menu preview hand result
21. Old active hand references removed/replaced
22. What old GLB/importer system remains only for rollback
23. 3-player result
24. 6-player result
25. 8-player result
26. rematch/reseat result
27. online validation status
28. performance summary
29. full EditMode result
30. Console result
31. exact files created/modified
32. any Blender re-export still required

Do not commit anything.

Stop when the FINAL FBX HANDS and their authored poses are fully integrated.