# TASK.md — SHIELDS Hat Cosmetics Set 01 Polish Pass

Continue from the CURRENT:

`Tools/Blender/HatCosmetics/Set01/SHIELDS_HatCosmetics_Set01.blend`

Do not redesign the hats.

This task is a focused visual polish pass for:

- surface smoothness / faceting
- silhouette quality
- Dice Bandana volume

Do not export to Unity yet.
Do not commit anything.

---

# 1. MAIN PROBLEM — HATS LOOK TOO FACETED

The current hats generally have good shapes, but several surfaces look too visibly polygonal / “LEGO-like”.

Important:

DO NOT assume this automatically means the triangle count is too low.

Current counts are approximately:

- Tavern Cap: 1,824 tris
- Rogue Gambler: 2,716 tris
- Tricorn Scout: 3,600 tris
- Dice Bandana: 4,248 tris

These counts are already reasonable for these assets.

First inspect the real cause.

Check:

- flat vs smooth shading
- split / hard normals
- shade-smooth setup
- smoothing angle
- weighted normals if useful
- radial segment distribution
- silhouette topology
- whether too few large polygons are defining curved surfaces

---

# 2. SHADING PASS

For organic / cloth / curved surfaces, use smoother shading where appropriate.

Goal:

The hats should remain stylized low-poly, but individual polygon faces should NOT distractingly read like LEGO facets.

Use an approach such as:

- Shade Smooth / Smooth by Angle
- intentional hard edges where required
- weighted normals if they improve the result

Do NOT blindly smooth every edge.

Keep hard/readable edges on things such as:

- Tricorn trim
- hat bands
- Rogue Gambler brim edge where intentional
- playing card
- dice patch
- structural seams / intentionally sharp folds

But large cloth/crown surfaces should read more smoothly.

---

# 3. TOPOLOGY / SILHOUETTE PASS

If smooth normals alone are not enough, improve topology ONLY where visually useful.

Prioritize:

- round crown silhouettes
- curved brim silhouettes
- cloth dome shapes
- Bandana crown
- Tavern Cap curved surfaces

Add a few additional radial / silhouette segments only where the actual outer shape visibly looks polygonal.

Do NOT globally subdivide the entire mesh.

Spend geometry on silhouette, not hidden/internal surfaces.

---

# 4. KEEP THE LOW-POLY STYLE

The goal is NOT to make the hats perfectly smooth high-poly assets.

Desired result:

- clean stylized low-poly
- smooth enough that the shapes feel intentional
- subtle faceting is okay
- obvious blocky / LEGO-like surfaces are not

The finished hats should visually match the smoother SHIELDS character and body cosmetics.

---

# 5. DICE BANDANA — MAJOR FIT/VOLUME REVISION

The Dice Bandana needs additional work.

Current problem:

It currently follows the head too tightly and reads like a thin cap glued directly onto the skull.

I want it to feel like:

**there is a small amount of hair / natural head volume underneath the bandana, and the cloth is wrapped over that volume.**

This does NOT mean creating a large hairstyle.

The main goal is to give the bandana more upward volume.

---

# 6. BANDANA CROWN SHAPE

Raise / expand the upper bandana volume slightly.

Requirements:

- do not make it massively oversized
- do not turn it into a chef hat or beanie
- do not make it perfectly spherical
- preserve the wrapped-cloth character

The upper cloth should sit slightly away from the skull.

It should have:

- subtle vertical fullness
- a slightly irregular cloth silhouette
- visible wrapped-layer structure
- a believable cloth mass above the head

The reference turnaround should remain the primary guide.

---

# 7. UNDER-BANDANA VOLUME / HAIR IMPRESSION

Create the impression that there is some natural volume underneath the cloth.

Preferred solution:

First try achieving this primarily through the BANDANA SHAPE itself.

The cloth should not exactly trace the head sphere.

If the visual result still needs it, a very subtle dark hair-underlayer may be added around limited lower edges such as:

- side/temple
- rear edge

But:

- do NOT create a full hairstyle
- do NOT make hair the visual focus
- do NOT cover the face
- keep it extremely subtle

The important result is:

“cloth wrapped over something”

instead of:

“cloth vacuum-sealed onto a bald sphere.”

---

# 8. BANDANA FOLDS

Preserve and improve the existing layered wrap construction.

The bandana should have broad cloth folds wrapping around the head.

Avoid:

- perfectly uniform horizontal rings
- excessive tiny wrinkles
- random noisy geometry

Use a few larger intentional folds.

The knot and tails should remain readable.

---

# 9. DICE PATCH

Keep the small dice patch.

It should:

- remain attached naturally to the cloth
- not float
- not become oversized

Do not redesign it.

---

# 10. OTHER THREE HATS

Tavern Cap, Rogue Gambler and Tricorn Scout are generally approved in design.

Do NOT redesign them.

Only:

- improve shading
- improve visible curved silhouettes where necessary
- preserve their current proportions and identity

Particularly avoid accidentally softening intentional Tricorn edges.

---

# 11. TRIANGLE BUDGET

Do NOT increase triangle count just because additional budget exists.

Current counts are already good.

A small increase is acceptable when needed for silhouette quality.

Approximate practical limits for this pass:

- Tavern Cap: ~2k–3k max if needed
- Rogue Gambler: ~3k–4k max if needed
- Tricorn Scout: ~4k–5k max if needed
- Dice Bandana: ~4.5k–6k max if needed

These are ceilings, not targets.

If the existing topology plus improved normals/shading solves the problem, keep the current counts.

---

# 12. REVIEW

After the polish pass, show all 4 hats in:

- Front
- 3/4
- Side
- Back

Also show a closer 3/4 view of Dice Bandana.

Compare before/after specifically for:

- visible polygon faceting
- silhouette smoothness
- intentional hard edges
- bandana crown volume
- bandana head fit
- knot/tail placement

---

# 13. REPORT

Report:

1. What caused the blocky/faceted appearance
2. Which objects were changed from flat/hard shading to smoother shading
3. Whether weighted normals / smooth-by-angle were used
4. Any topology added and why
5. Before → after triangle counts
6. Exact Dice Bandana volume changes
7. Whether any subtle hair-underlayer was necessary

Do not export to Unity yet.

Do not commit anything.