# TASK — Integrate Final 16 Body Outfits into SHIELDS Customization

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and inspect the CURRENT customization/cosmetic architecture before modifying anything.

The 16 finished Blender-authored torso cosmetics have now been exported into the Unity project.

This task is to integrate them into the existing production customization system.

Do NOT create a parallel cosmetic framework.

Do NOT modify the final hand system.

Do NOT redesign the outfits.

Do NOT commit anything.

---

# 1. Inspect the exported outfit assets first

Locate all newly exported body-cosmetic FBX/assets inside the Unity project.

Inspect:

- exact asset paths
- mesh/object names
- hierarchy
- materials
- scale/orientation
- common BodyCosmeticAnchor convention
- pivots/origins
- child meshes
- triangle counts
- whether any outfit contains special separately-authored pieces such as a hood

Do not assume naming if the exported files use slightly different names.

Expected outfit set conceptually contains these 16 designs:

1. Tavern Vest
2. Gambler’s Longcoat
3. Hooded Wrap / final hooded-wrap equivalent
4. Merchant Robe Shell
5. Guard Chestpiece
6. Noble Doublet
7. Barkeep Apron
8. Occult Cardseer Garment
9. Patchwork Rogue Jerkin
10. Traveling Minstrel Wrap
11. Brewer’s Leather Harness
12. Monk’s Shoulder Mantle
13. Duelist’s Short Jacket
14. Ragged Outcast Wrap
15. Winter Fur Collar Outfit
16. Tavern Champion Outfit

Use the ACTUAL exported asset names as source of truth.

---

# 2. Preserve the current cosmetic architecture

The game already has working customization for things such as:

- HEAD
- FACE
- BODY
- HANDS

and existing:

- preview
- selection
- equip
- persistence
- thumbnails/item cards
- PlayerCharacter application
- online cosmetic handling if already implemented

Extend that system.

Do not build a second wardrobe/customization implementation.

---

# 3. BODY category changes completely

Previously, BODY mainly contained different body-color cosmetics.

That should now change.

BODY should become the category for the new actual torso outfits.

When BODY is selected, the item grid should show:

- Bare / No Outfit / Default
- the 16 new body outfits

Use the same existing Wardrobe card style as HEAD/FACE.

The old body-color choices must NOT continue appearing as individual BODY item cards.

---

# 4. Preserve body colors as a separate Color Bar

Do NOT remove the existing body-color customization.

Instead, move it out of the BODY item grid.

When BODY is selected, show a compact **Body Color Bar / Palette** underneath the character preview.

This bar should contain the existing body colors that were previously represented as BODY cosmetic cards.

Requirements:

- preserve all currently supported body colors
- reuse their existing color values/material logic
- no need to redesign the actual palette
- display them as compact color swatches
- clearly indicate current selected color
- hover/selection should match the current SHIELDS UI language
- clicking a color should immediately preview it on the character

The color bar should only be visible while BODY is the active major category.

When switching to HEAD / FACE / HANDS:
- hide the Body Color Bar

---

# 5. Body Color and Outfit are independent choices

This is important.

A player now has TWO separate body-related customization values:

1. Body Outfit
2. Base Body Color

They are independent.

Example:

`Tavern Vest + Burgundy Body`

or

`Winter Fur Collar + Green Body`

The outfit should NOT replace or reset the selected base body color.

The selected body color should continue affecting the original PlayerCharacter body exactly as it did before.

Do not tint the authored outfit meshes with the body color unless the current outfit/material architecture explicitly requires that.

The outfits should keep their authored clothing colors/materials.

The lower/exposed PlayerCharacter body remains controlled by Body Color.

---

# 6. Bare / No Outfit option

Add a clear default BODY option:

- `Bare`
or
- `No Outfit`

Use whichever naming fits the existing UI better.

When equipped:

- no torso cosmetic mesh is active
- the original PlayerCharacter body remains visible
- selected Body Color still applies

This should be a valid persistent outfit choice.

---

# 7. Outfit attachment hierarchy

Create/reuse a clean body cosmetic attachment point.

Conceptually:

`PlayerCharacter`
- `Body`
  - `BodyMesh`
  - `BodyCosmeticAnchor`
    - active outfit

Adapt to the actual existing hierarchy rather than blindly creating duplicates.

The Blender assets were authored against a shared body reference and anchor.

Prefer:

- localPosition = 0
- localRotation = 0
- localScale = 1

for outfit roots.

Do not manually invent different offsets for every outfit unless a verified FBX import issue requires it.

---

# 8. Only the equipped outfit should render

Do NOT instantiate/render all 16 outfits on every PlayerCharacter.

Each gameplay character should have only its currently equipped body cosmetic active.

On outfit change:

- cleanly remove/disable previous cosmetic
- apply selected cosmetic
- no duplicates
- no stale child meshes

Keep runtime overhead small.

---

# 9. Outfit materials

Preserve the authored colors and material separation.

Where possible:

- reuse shared project-owned materials
- avoid unnecessary duplicated material instances
- keep cloth/leather/metal appearance compatible with SHIELDS lighting

Do not perform a major recoloring pass in this task.

If an imported material is visibly incompatible with URP/current shaders, create project-owned compatible material copies rather than modifying third-party/source data destructively.

---

# 10. Special multi-part outfits

Inspect every outfit hierarchy.

If an outfit contains intentionally separate functional pieces, preserve them.

Especially inspect the final Hooded Wrap / hooded outfit.

If its hood was authored as a separate head-following mesh:

- torso/wrap portion remains attached to Body
- face-framing Hood portion should attach/follow the appropriate HeadVisual/HeadPivot
- the hood should rotate with head look
- the lower neck/shoulder connection should remain visually coherent

Do NOT merge such intentionally separate pieces merely for convenience.

If no separate hood was actually exported, do not invent one.

Report the real exported structure.

---

# 11. Hooded outfit vs HEAD cosmetics

If the final Hooded Wrap includes a real head-covering hood:

treat it as incompatible with ordinary headwear.

When that BODY outfit is equipped:

- suppress/hide the currently equipped HEAD cosmetic visually while the hood is active

Do NOT delete/change the player's saved HEAD selection.

When changing away from the hooded BODY outfit:

- restore the player's equipped HEAD cosmetic automatically

This is a visual compatibility rule only.

Do not create two overlapping hat/hood meshes.

Only implement this if the actual final exported outfit contains the hood.

---

# 12. Customize BODY presentation

When BODY is selected:

show:

- outfit cards in the existing left-side/body item area
- Body Color Bar underneath the character preview

The preview should remain large enough to inspect the entire torso outfit.

Do not use the FACE close-up camera.

Use the normal BODY/full-character wardrobe framing.

The current drag-to-rotate preview should remain available so we can inspect:

- front
- sides
- back

of the outfits.

---

# 13. Live outfit preview

Clicking an outfit card should immediately preview that outfit on the Customize character.

Preserve the current distinction between:

- selected/previewing
- equipped

if the existing wardrobe already uses this.

Do not silently auto-equip if current UX requires an Equip action.

---

# 14. Live body-color preview

Clicking a Body Color swatch should immediately update the preview body color.

Use the existing BODY color behavior/persistence semantics if possible.

Do not rebuild the material system unnecessarily.

The color change must remain visible on exposed body areas while an outfit is worn.

---

# 15. Thumbnails

Create/update actual wardrobe thumbnails for all 16 outfits plus Bare/Default.

Reuse the existing thumbnail pipeline if one exists.

Thumbnail should clearly show the outfit silhouette, not just a tiny generic torso.

Do not manually maintain screenshots if the current system already renders cosmetic thumbnails.

---

# 16. Actual PlayerCharacter integration

This must not be Customize-only.

The equipped outfit must appear on actual gameplay PlayerCharacters.

Apply:

- Body Outfit ID
- Body Color ID/value

through the existing participant cosmetic state.

Cosmetics belong to the PLAYER/PARTICIPANT, not the seat.

Never assume:

`clientId == seat`

---

# 17. Rematch / random seating

The project supports rematches and randomized seats.

Verify:

- outfit follows the participant after re-seat
- body color follows the participant
- no cosmetic remains attached to the previous seat character
- no duplicate outfit objects remain after PlayerCharacter rebuild/rebind

---

# 18. Online synchronization

Inspect the CURRENT cosmetic networking architecture.

If HEAD/FACE/etc already synchronize through cosmetic IDs:

extend that SAME system with:

- `BodyOutfitId`
- existing/new `BodyColorId`

Send identifiers only.

Do not send meshes/materials over the network.

Do not create a second cosmetic RPC framework.

If general cosmetic networking is not currently implemented, preserve the current architecture and clearly report the limitation instead of inventing unrelated networking.

---

# 19. Bots

Do not create a complicated bot-fashion system.

Preserve current bot cosmetic behavior.

If bots currently use defaults:
- keep default body color
- use Bare/Default outfit unless there is already a defined cosmetic policy

Do not randomly distribute all 16 outfits unless existing game design already supports random bot cosmetics.

---

# 20. Gameplay behavior

Outfits are VISUAL ONLY.

They must not affect:

- hit detection
- targeting
- crosshair
- card interactions
- Charge
- weapon
- beer
- waiter
- hand movement
- player seating

Do not add colliders to clothing.

Do not make outfits targetable gameplay geometry.

---

# 21. Floating hands

Preserve the finalized floating hands exactly.

The torso cosmetics were designed without actual arms.

Verify outfits do not introduce:

- arm meshes
- sleeves extending toward hands
- hand rig changes
- hand anchor changes

All existing hand animations/emotes must remain unchanged.

---

# 22. Visual validation of all 16

Inspect every outfit on the actual Customize character.

Check:

- front
- 3/4
- side
- back

Look for:

- clipping
- floating
- wrong scale
- wrong orientation
- missing materials
- malformed normals
- exposed unintended interior faces
- headwear conflicts
- hand conflicts

Do not silently remodel Blender source meshes inside Unity.

If an outfit genuinely requires a Blender correction, report exactly which asset needs re-export instead.

---

# 23. Performance

Report:

- triangle count of each outfit
- material/submesh count
- approximate active outfit cost at 8 players

Only ONE body outfit per player should be active.

These meshes being approximately several thousand triangles each should be completely reasonable, but validate that no accidental duplicate instances/material explosion occurs.

---

# 24. Customize UI validation

Validate at 1920×1080.

Check:

- BODY category
- 17 outfit entries including Bare/Default
- scrolling/grid behavior
- color bar location
- color swatch readability
- currently selected swatch
- outfit preview
- outfit equip
- normal character rotation
- no overlap with existing UI

Also check 2560×1440 if practical.

---

# 25. Persistence

Verify both selections persist correctly:

- equipped Body Outfit
- selected Body Color

Test:

Customize
→ leave screen
→ return

and if existing system supports saved customization across scene load:

Main Menu
→ gameplay
→ return

Do not reset body color when changing outfit.

Do not reset outfit when changing body color.

---

# 26. Regression validation

Verify existing customization still works:

- HEAD
- FACE
  - Eyes
  - Eyebrows
  - Mouth
- HANDS
- nickname
- Shop Coming Soon
- preview rotation

Do not break previously completed FACE integration.

---

# 27. Tests

Run the complete CURRENT EditMode suite.

Do not assume an old test count.

Add focused tests where appropriate for:

- outfit and body-color independence
- Bare outfit
- participant → cosmetic persistence
- re-seat/rematch
- hood/headwear compatibility rule if implemented

Do not weaken existing tests.

---

# 28. Console

Final clean validation should show no project-code:

- missing mesh errors
- missing material errors
- invalid cosmetic ID errors
- duplicate outfit warnings
- preview errors
- gameplay presentation exceptions

Report unrelated third-party warnings separately.

---

# 29. Final report

Report:

1. exact exported outfit asset paths discovered
2. all 16 outfits discovered
3. final outfit IDs/names
4. Bare/Default implementation
5. BodyCosmeticAnchor hierarchy
6. BODY item-grid changes
7. old body-color cards removal
8. Body Color Bar implementation
9. outfit/color independence
10. preview/equip behavior
11. thumbnail implementation
12. PlayerCharacter integration
13. hooded outfit special handling
14. HEAD compatibility behavior
15. online synchronization status
16. rematch/reseat result
17. performance / triangle summary
18. 1920×1080 UI result
19. tests
20. console
21. exact files created/modified
22. any outfit that needs a Blender re-export

Do not commit anything.

Stop when all 16 outfits and the new BODY color-bar workflow are integrated and ready for manual visual testing.