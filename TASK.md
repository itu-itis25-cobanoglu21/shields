# TASK 012 — Tavern Environment Integration

Read CLAUDE.md and SHIELDS_RULES.md completely before modifying anything.

The core game is already working and must remain working:

- offline Shields gameplay
- host-authoritative online multiplayer
- multiple human clients
- bots
- seated first-person camera
- local head-look
- remote head-pose synchronization
- camera persistence across gameplay snapshots
- custom PlayerCharacter visuals
- table/card interaction
- draw/discard presentation
- current automated test baseline: 260 passing

A new custom medieval cellar / tavern environment has now been authored in Blender and re-exported for Unity.

The latest tavern FBX is under:

Assets/Art/Environment/

The latest export was corrected based on the previous inspection:

- the entire room was scaled by 2.7 in Blender
- scale was applied
- the asset should now be authored close to real Unity meter scale
- FBX Path Mode was set to Copy
- Embed Textures was enabled

This task is ONLY about verifying and integrating the new tavern environment.

Do NOT proceed into props, final lighting, fire effects, NPCs, doors, animation, or additional environment art.

---

## 1. Verify the latest tavern FBX first

Before modifying the scene, locate the latest tavern FBX under:

Assets/Art/Environment/

Verify:

- root position is clean
- root rotation is clean
- root scale is 1,1,1
- no duplicate ×2.7 scaling was introduced
- Y is up
- floor is horizontal
- ceiling underside is approximately 3 meters high
- room dimensions are approximately the previously expected 9m × 14m interior
- doorway remains open
- fireplace wall remains correctly oriented
- window walls remain correctly oriented
- floor and ceiling tile placement is intact
- wooden ceiling beams remain intact
- materials/textures now import correctly
- stone and wood textures are visible
- no missing-texture warnings exist
- no cameras, lights, armatures or animations were imported unexpectedly

If the scale or texture import is still genuinely broken:

STOP and report the exact asset problem.

Do not hide asset problems using arbitrary Unity transforms.

---

## 2. Preserve gameplay coordinates

The existing gameplay space is already working.

Do NOT move or redesign:

- Table
- Match
- RuntimePlayers
- RuntimeTableAreas
- MainCamera
- NetworkManager
- NetworkSession
- Networking
- TableLayoutController
- SeatLayout
- PlayerCharacter prefab

The tavern must be positioned around the EXISTING gameplay origin.

The art adapts to the gameplay space, not the other way around.

---

## 3. Integrate under Environment

Add exactly one instance of the new tavern room under:

GameScene
└── Environment

The tavern should be centered around the current table/gameplay origin.

Preferred initial transform:

- Position around world origin, with only the minimal Y offset required to place the walkable floor at y = 0
- Rotation 0,0,0
- Scale 1,1,1

Do not use non-uniform scale.

---

## 4. Preserve the old placeholder room temporarily

Do NOT permanently delete the current grey placeholder room yet.

Instead:

- disable the old Environment/Room visual
- keep it available temporarily as rollback/reference
- make the new tavern the active visible room

Do not leave both rooms visibly overlapping.

---

## 5. Presentation-only environment

The tavern is presentation only.

Do NOT add:

- gameplay authority
- networking authority
- NetworkObject
- player identity logic
- bot logic
- Match state
- card logic
- seat logic

Static room geometry does not need network synchronization.

Every client should load the same deterministic scene environment locally.

---

## 6. No environment colliders yet

Keep generated/imported colliders OFF for the tavern.

Do not add MeshColliders or automatic colliders in this task.

Reason:

the current crosshair targeting uses raycasts and the room geometry must not accidentally intercept:

- player targeting
- Shield targeting
- card interaction

If an environment collider is genuinely required later for NPC/pathing or interaction, that will be handled in a separate task.

---

## 7. Lighting

Preserve the existing gameplay lighting for this first integration pass.

Do NOT perform final lighting polish.

Do NOT add:

- final torch lights
- moonlight
- volumetrics
- fireplace light
- post-processing art pass

Only make a minimal lighting adjustment if the tavern is literally impossible to inspect otherwise.

Any such adjustment must be reported explicitly.

---

## 8. Doorway

The south arched doorway intentionally has no door.

Preserve it as an open architectural opening.

Do NOT add:

- door mesh
- door animation
- interaction
- waiter/NPC
- navigation

Known limitation:

the doorway may currently reveal the skybox / empty space because there is no corridor behind it.

Do not solve that in this task unless the opening causes a severe immediate visual blocker.

Report it as a remaining environment issue.

---

## 9. Windows

Preserve the existing barred/window wall geometry.

Do not implement moonlight yet.

Do not add exterior scenery yet.

Only verify that the windows:

- remain correctly positioned
- do not intersect gameplay
- are visually readable from player seats

---

## 10. Fireplace

Preserve the current fireplace wall geometry.

Do NOT add:

- animated fire
- particle effects
- emissive effects
- light flicker

The fireplace is static presentation for this task.

---

## 11. Ceiling and beams

Validate that:

- ceiling clears all PlayerCharacter heads
- normal seated head-look does not clip into ceiling geometry
- wooden beams do not obstruct camera movement
- the central hanging lamp remains visually plausible relative to the beams
- the ceiling does not intersect player targeting rays in a harmful way

Do not redesign ceiling geometry unless a genuine blocker is found.

---

## 12. Runtime validation — 3 players

Run OFFLINE mode with 3 players.

Verify:

- table remains centered
- all seats/chairs remain inside the room
- characters do not intersect walls
- local camera is correct
- head-look works
- table/cards remain readable
- player targeting works
- Shield targeting works
- no environment geometry blocks gameplay raycasts
- no obvious floor/ceiling clipping
- doorway/fireplace/window placement reads correctly

---

## 13. Runtime validation — 6 players

Run OFFLINE mode with 6 players.

Verify:

- all players fit comfortably
- no wall/chair overlap
- no camera clipping during normal head-look
- room scale feels plausible
- table remains the visual focus
- target selection remains correct

---

## 14. Runtime validation — 8 players

Run OFFLINE mode with 8 players.

This is the most important spatial validation.

Verify:

- all seats fit comfortably
- no player/body/hand/chair intersects room geometry
- nearest players do not feel pressed against walls
- camera does not enter walls during normal seated look limits
- ceiling clearance remains safe
- cards and Shields remain readable
- target boxes remain unaffected
- room does not feel obviously too small

If the room feels excessively large or excessively small from first-person gameplay, report that clearly rather than immediately moving gameplay objects.

---

## 15. Gameplay regression

During the environment validation, perform:

- Attack
- Replace Shield
- Charge
- Charged Attack if possible
- bot turns

Verify that replacing the room visuals does not affect gameplay.

Do not modify gameplay code to address an environment-only issue.

---

## 16. Online sanity check

If available, start one Host session and verify:

- online mode loads the same tavern environment
- table and seats remain in the same positions
- no networking errors are introduced

Do NOT claim Host + Client validation unless a second real participant instance was actually run.

No environment networking changes are expected.

---

## 17. Materials / textures

If the embedded textures now import correctly:

- keep them for this first integration pass
- do not redesign them yet
- do not create the final shared Stone/Wood/Iron material system yet

This task only needs the room to visually match the Blender export.

If Unity creates duplicate imported materials, do not perform a large material cleanup yet unless required for the asset to render correctly.

Material unification is a later art task.

---

## 18. Performance

The room was previously measured at roughly 291k triangles.

Do NOT perform mesh optimization in this task unless there is a demonstrated runtime problem.

Current target is Windows/PC.

Do not:
- decimate meshes
- rebuild floor tiles
- merge the entire room into one mesh
- generate LODs

Performance optimization will be handled later if profiling shows it is needed.

---

## 19. Tests

Run the complete existing EditMode suite.

Expected baseline:

260 passing.

Do not weaken existing tests.

No new pure tests are required unless integration unexpectedly changes code.

---

## 20. Console

Final state must have:

- 0 project-code compile errors
- 0 relevant runtime Console errors
- 0 missing-reference errors

Unrelated Unity AI/subscription errors may be reported separately.

---

## 21. Do not implement yet

Do NOT add:

- environmental props
- barrels
- shelves
- banners
- shields on walls
- weapon racks
- torch props
- final lighting
- moonlight
- fire VFX
- fireplace animation
- doors
- NPCs
- waiter logic
- navigation
- audio
- environment interaction
- new packages
- new gameplay rules

Stop after the tavern room is correctly integrated and validated.

---

## 22. Final report

When complete, report:

1. Exact FBX asset used
2. Import scale/rotation verification
3. Verified room dimensions
4. Material/texture status
5. Environment hierarchy before/after
6. Tavern transform in GameScene
7. Placeholder Room status
8. 3-player validation
9. 6-player validation
10. 8-player validation
11. Camera / ceiling clearance
12. Targeting regression result
13. Gameplay regression result
14. Online Host sanity-check status
15. Automated test result
16. Console result
17. Any remaining environment issue
18. Whether Blender changes are still required

Stop when TASK 012 is complete.