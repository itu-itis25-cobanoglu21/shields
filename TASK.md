# TASK 023 — Main menu logo sharpness polish + charged card flame containment

## Goal

There are two goals in this task:

1. **Sharpen the SHIELDS! logo in the main menu**
2. **Rework the charged-card flame effect so it sits cleanly on the card and does not drift or spill differently from different camera angles**

Do both in one pass if possible.

---

## Part A — Main menu logo sharpness

### Problem
The main menu is in a good place overall, but the **SHIELDS!** logo still looks a little soft / blurry / less crisp than the button labels.

The button text is acceptable.  
The logo is the part that needs polish.

### What to do
Investigate the likely causes and try the most reasonable fixes, then keep **the sharpest-looking result**.

Most likely suspects to check:

- TMP / SDF material settings on the logo
- outline thickness
- outline color / contrast
- softness / dilate / underlay / glow-like settings
- RectTransform scaling that may be making the text look soft
- anything else that plausibly affects crispness

### Constraints
- Keep the same overall look:
  - full uppercase
  - dark red / burgundy logo
  - same font
- Do **not** redesign the logo
- Do **not** add an underline
- The goal is simply: **make it look sharper and cleaner**

### Acceptance target
At native 1920×1080 Game View, the logo should look visibly crisper and closer in sharpness to the menu buttons.

---

## Part B — Charged card flame effect containment

### Current problem
The charged-card flame effect still does not read correctly.  
The current effect looks too detached / too tall / too free-floating, and depending on perspective it can feel like it shifts or spills to the sides.

The desired visual is:

- a **single flame effect per charged card**
- the effect should feel **attached to the card**
- it should read as the card itself / the card surface burning
- it should not look like multiple separate flames
- it should not visibly slide sideways relative to the card when viewed from different seats
- it should not extend far outside the card footprint

### Target look
The flame should occupy roughly the **card’s face area** as its base and rise upward from that footprint.

Think of it as:

- the **base / visible footprint of the fire = the card’s top face area**
- the fire can rise upward visually
- but it should feel visually “contained” to the card instead of being a loose billboard beside it

### Suggested implementation approach
Please try the cleanest technically appropriate solution.

**Preferred direction:**
Use some form of **containment / masking / clipping** so the effect is only visible within the card footprint area.

Examples of acceptable solutions:
- card-sized mask / clipping volume / box-based containment if the current shader supports it cleanly
- stencil/depth/custom mask solution
- a card-sized quad / plane with a flame material
- a custom material or mesh-based fire sheet anchored to the card
- another approach, if it produces the same visible result

### Important note
Do **not** force a box-clipping solution if the current particle system/shader does not support it well.  
If box clipping is messy or brittle, switch to the most robust alternative that achieves the desired visual result.

The real goal is the result:
- one clean fire effect
- locked to the card
- consistent from all player seat views
- no obvious sideways drift or spill beyond the card footprint

### Constraints
- Keep the existing charge gameplay behavior unchanged
- Keep the existing charge animation system intact unless a small integration change is needed
- Do **not** break the already-working hand/card timing unless absolutely necessary
- Prefer modifying only the flame presentation layer

### Validation
Please test the charged-card fire from:
- local seat
- at least 2–3 other player seat angles
- 3, 6, and 8 player setups if practical

The flame should:
- remain visually attached to the charged card
- not visibly drift left/right off the card between angles
- feel like a single coherent burning-card effect
- not interfere with targeting

---

## Non-goals
- Do not redesign the whole menu
- Do not redesign charge gameplay rules
- Do not revisit unrelated attack / gun / blood / impact systems in this task

---

## Deliverable report
Please report:

1. What caused the logo softness and what you changed
2. What implementation you chose for the flame containment
3. Whether the box-mask idea worked or whether you used a fallback solution
4. What assets / materials / prefabs / scripts were created or modified
5. Validation results
6. Any remaining limitations