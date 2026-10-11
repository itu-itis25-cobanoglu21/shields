# TASK 014 — Lantern Practical Lights & Moonlight Leak Cleanup

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and this entire TASK.md before modifying anything.

This task is a focused environment polish pass.

The tavern environment, gameplay table/chairs, lighting pass, and fireplace fire VFX are already integrated.

The user has also manually updated the wall lantern assets:
- lantern body and glass geometry are now separated
- transparent glass materials have been created
- all relevant lantern instances in GameScene have been replaced with the updated lantern version
- the fireplace fire VFX has been manually positioned and tuned by the user

Preserve those manual changes.

Do NOT redo the room, table, chairs, prop layout, fireplace VFX setup, or general lighting pass.

---

## 1. Goals

This task has three goals:

1. Make the updated wall lanterns visibly look like they contain a light source.
2. Ensure the lantern light appears to originate from inside the lantern glass.
3. Fix the current moonlight / night-light leaking through solid stone walls.

The final result should preserve the existing cozy dark tavern atmosphere.

---

## 2. Inspect first

Before modifying anything, inspect the current GameScene and identify:

- all updated lantern instances
- each lantern's:
  - body mesh
  - glass mesh
  - glass material
  - corresponding Point Light
- current fireplace VFX
- current fireplace lights
- current chandelier
- current `Light_Moon`
- current skybox / ambient settings
- all open barred windows
- any closed window geometry
- wall geometry around the windows
- shadow settings / bias / normal bias relevant to moonlight

Do not assume previous hierarchy names are still exact.

Report internally what each current light corresponds to before changing it.

---

# PART A — LANTERN INTERNAL GLOW

## 3. Lantern visual problem

The current lanterns correctly illuminate the room, but visually their interiors still look too empty.

A viewer should be able to look at a lantern and clearly understand:

"the light is coming from inside this lantern."

The physical lantern mesh already has:
- opaque frame/body
- separate transparent glass

Do not remodel the lantern again.

---

## 4. Add a visible internal light source

For every relevant wall lantern, create a small visible internal light source inside the glass.

Preferred implementation:

- small flame-like mesh, capsule, sphere, or simple custom primitive
- dedicated emissive URP material
- warm yellow/orange color
- positioned inside the lantern body, behind the glass
- no collider
- no gameplay components
- no networking components
- Cast Shadows OFF
- Receive Shadows OFF if appropriate

The visible core should be small enough that the metal lantern frame remains readable.

It must NOT look like a giant white light bulb.

Preferred visual character:

- bright warm center
- yellow / amber
- slightly orange edge if possible
- medieval flame / candle feel
- no modern LED appearance

---

## 5. Emissive material

Create a project-owned material, for example:

`Assets/Art/Environment/Materials/M_Lantern_Core.mat`

Use an appropriate URP shader.

The material should:

- visibly glow
- use warm HDR emission
- remain readable through the transparent lantern glass
- not blow out into pure white at normal gameplay exposure

Do NOT modify package-source materials directly if avoidable.

Do not make the lantern glass itself fully emissive.

The intended structure is conceptually:

Lantern
- Body / Frame
- Glass
- EmissiveCore
- Existing practical Point Light

The glass remains glass.
The internal core is the visible light source.
The Point Light illuminates the environment.

---

## 6. Optional small flame VFX

The project now contains:

`Stylized Fire VFX URP`

The user is already using a manually tuned fire effect from this package in the fireplace.

You MAY use an existing small flame effect from this package inside a lantern ONLY if it clearly looks better than a static emissive core.

Requirements if using package VFX:

- use a small flame suitable for a lantern/candle
- do not use the large fireplace flame unchanged
- do not edit the package source prefab/material destructively
- create a project-owned variant if customization is required
- keep the effect subtle
- ensure it loops continuously
- ensure it does not visibly clip through the glass/frame
- no collider
- no additional light unless needed

Do not add a flame VFX merely because one exists.

If a simple emissive core looks better and cleaner, prefer the emissive core.

---

## 7. Preserve user's fireplace fire

The fireplace flame VFX has already been manually tuned by the user.

Do NOT:

- replace it
- reset it
- change its scale
- change its speed
- change its material
- change its prefab variant
- move it

unless there is an actual blocking technical issue.

Treat the current fireplace flame as approved visual work.

You may only adjust the surrounding LIGHT values slightly later if required for overall balance.

---

## 8. Align practical Point Lights

For each updated lantern, verify that its existing Point Light is physically centered around the visible internal flame/core.

The visible source and actual illumination should feel like the same source.

Adjust only the LIGHT transform if needed.

Do not move the user's lantern props.

Keep the existing warm lighting hierarchy from TASK 013.

Do not significantly increase the number of realtime lights.

---

## 9. Lantern brightness balance

After adding internal glow:

- lantern frame must remain visible
- glass must remain visibly transparent
- internal source should look bright but not pure-white
- nearby wall illumination should still look natural
- lantern should not become a glowing white box

Inspect from real seated first-person views.

If the existing Point Light becomes visually too strong once the internal source is visible, reduce it slightly rather than making the emissive source excessively bright.

---

# PART B — MOONLIGHT / WALL LIGHT LEAK

## 10. Existing problem

The current cool night / moon light creates attractive blue illumination, but some of the light appears to pass through solid stone wall geometry rather than entering only through valid openings.

This is visually incorrect.

Do not accept obvious blue light appearing through solid wall sections.

---

## 11. Diagnose before changing

Inspect the current `Light_Moon` and determine whether the leak is caused by:

- Directional Light behavior
- shadow bias
- normal bias
- wall seams
- non-shadow-casting environment meshes
- thin / overlapping modular wall geometry
- incorrect light direction
- another light being mistaken for moonlight

Do not blindly change shadow bias first.

Identify the real cause.

---

## 12. Preferred moonlight strategy

If the current global Directional moonlight cannot produce convincing window-only illumination without leaking:

reduce its role.

Preferred structure:

### Global moon / exterior fill

Use the directional moon only as a VERY SUBTLE cool environmental fill if needed.

It should not create strong blue floor patches through solid geometry.

### Window moonlight

For actual moonlight entering the room, prefer localized lights positioned outside valid window openings.

For example:

`Moonlight_Window_West_01`
`Moonlight_Window_West_02`

Use Spot Lights or another appropriate local-light setup aimed through the actual barred openings.

Requirements:

- light origin must be outside the room
- aim inward through the opening
- cool blue / blue-grey
- weaker than the warm interior practical lights
- finite range
- shadows enabled if required for window bars / wall occlusion
- no illumination through adjacent solid wall sections

The barred window geometry should naturally shape or occlude the light where possible.

---

## 13. Do not fake light through closed geometry

If a window is physically closed / sealed, do not place a strong moon beam through it.

Only valid open windows should visibly admit moonlight.

Do not make the stone walls translucent.

Do not disable wall shadow casting simply to make the effect easier.

---

## 14. Doorway / exterior

Keep the current night sky.

The doorway currently opens into empty night space.

Do NOT add:

- corridor
- exterior building
- terrain
- new sky assets
- door
- exterior props

in this task.

Only make sure the moonlight cleanup does not make the doorway look like bright daytime.

---

## 15. Shadow performance

TASK 013 previously avoided shadow-atlas warnings by using shadows selectively.

Preserve that goal.

Do not enable realtime shadows on every lantern.

Preferred:

- lantern internal emissive cores: no shadows
- secondary lantern Point Lights: shadows remain OFF unless absolutely required
- localized moonlight spots: use shadows selectively where they are necessary for correct wall/window occlusion
- keep total shadow cost reasonable

If a shadow atlas warning appears, solve it before finishing.

---

# PART C — FINAL LIGHT BALANCE

## 16. Fireplace lighting re-check

Now that the fireplace has a real visible flame VFX, inspect the existing fireplace lighting from gameplay cameras.

The current fireplace wall may be somewhat over-lit.

Do not automatically change it.

Only reduce:

- `Light_Fire_Main`
- `Light_Fire_Glow`
- nearby fire torch light contribution

if the real flame now makes the wall appear unnaturally flooded.

Goal:

- fireplace remains a strong warm focal point
- flame visually explains the light source
- surrounding stones have warm falloff
- entire fireplace wall should not look uniformly orange

Do not modify the flame VFX itself.

---

## 17. Preserve overall art direction

Final look:

- cozy
- dark
- warm interior
- cold night outside
- visible practical light sources
- believable local light pools
- readable gameplay
- darker unused corners

Avoid:

- orange floodlighting everywhere
- white glowing lantern boxes
- blue moonlight through stone
- uniformly lit walls
- pitch-black gameplay areas

---

# PART D — VALIDATION

## 18. First-person validation

Inspect from actual seated first-person cameras.

Check multiple seat directions toward:

- fireplace
- left wall lantern
- right wall lantern
- doorway lanterns
- barred windows
- moonlight floor patches
- chandelier
- gameplay table

Confirm that:

- lanterns visually contain a light source
- glass remains readable
- glow does not clip through metal
- moonlight comes through valid openings
- no obvious blue wall leak remains

---

## 19. 3 / 6 / 8 player validation

Run offline:

### 3 players
Verify:
- gameplay readability unchanged
- lantern/glow looks correct
- fireplace still reads correctly

### 6 players
Verify:
- all player positions remain readable
- no VFX/light interferes with targeting

### 8 players
Verify:
- table/cards/players remain readable
- no environment light creates visual obstruction
- no new collider/raycast interference exists

---

## 20. Targeting / collider safety

New lantern visual cores / flame effects must have:

- no collider
- no Rigidbody
- no targetable gameplay layer
- no NetworkObject

They must not interfere with:

- body targeting
- Shield targeting
- card targeting
- crosshair raycasts

---

## 21. Tests

Run the existing full EditMode suite.

Expected baseline:

260 passing.

Do not modify or weaken gameplay tests.

---

## 22. Console

Final state:

- 0 project-code errors
- 0 missing-reference errors
- 0 relevant runtime warnings
- no new URP/shadow-atlas warnings

Unrelated Unity AI / licensing / bridge messages may be reported separately.

---

## 23. Do NOT implement yet

Do NOT start the animation phase.

Do NOT add:

- player hand animation
- card draw animation
- slap animation
- waiter/NPC animation
- door animation
- environmental prop animation
- fire flicker scripts
- audio
- smoke pass
- volumetric fog
- post-processing redesign

Stop after lantern practical-light polish and moonlight leak cleanup.

---

## 24. Final report

Report:

1. Which lantern instances were updated
2. Exact emissive material(s) created
3. What visible internal source was used
4. Whether Stylized Fire VFX was used inside lanterns
5. Any package prefab/material variants created
6. Point Light alignment changes
7. Any intensity/range adjustments to lantern lights
8. Existing fireplace VFX status
9. Any fireplace lighting changes
10. Root cause of the moonlight wall leak
11. Changes made to `Light_Moon`
12. Local window moonlight lights added
13. Which moon/window lights cast shadows
14. Confirmation that light no longer visibly passes through solid walls
15. 3/6/8 player validation
16. Targeting regression result
17. EditMode test result
18. Console/warning result
19. Exact files created/modified
20. Any remaining environment-lighting issue

Do not commit anything.

Stop when TASK 014 is complete.