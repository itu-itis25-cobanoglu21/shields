# TASK 028 — Main Menu UI/UX Design Exploration

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and this entire TASK.md before modifying anything.

This is a UI/UX DESIGN EXPLORATION task.

The user wants to redesign the game's front-end UI, beginning with the Main Menu, Customize, Shop placeholder, and Settings.

IMPORTANT:

Do NOT modify the production `MainMenuScene` yet.

Create a separate experimental scene where UI ideas can be evaluated safely.

The USER is defining the UX/content structure.

YOU are responsible for the visual/art direction.

Do not ask the user to make detailed graphic-design decisions.

---

# 1. Create a separate concept scene

Create a new scene, for example:

`Assets/Scenes/UIConceptScene.unity`

or another clearly named project-owned UI exploration scene.

This scene is disposable/prototyping space.

Do NOT replace or modify the production `MainMenuScene`.

Do NOT alter existing production menu navigation yet.

---

# 2. Understand SHIELDS before designing

Before creating UI:

inspect the project and existing scenes/assets.

Understand the game's current visual identity:

- dark medieval tavern
- stylized low-poly environment
- warm practical lighting
- cool night contrast
- card/tabletop social game
- slightly dark/humorous gambling atmosphere
- existing SHIELDS logo
- Bowlby One SC usage
- current Main Menu composition
- current gameplay HUD
- existing buttons/hover behavior

Use the provided screenshots/sketches as STRUCTURAL references.

Do NOT treat the sketch as finished visual design.

The visual design should feel native to SHIELDS.

---

# 3. Art direction is your responsibility

The user is NOT prescribing:

- exact panel shapes
- exact colors
- border style
- decorative motifs
- icon style
- gradients
- textures
- button framing
- tab styling
- slider styling
- hover styling
- typography treatment beyond preserving brand compatibility

Choose these yourself based on the game.

The final result should feel intentional and game-ready, not like default Unity UI.

Avoid generic modern SaaS/mobile UI.

Avoid overdecorating until the gameplay scene becomes obscured.

---

# 4. Reuse existing assets

Prefer existing SHIELDS assets.

Do not download random external UI packs or fonts.

You may create project-owned:

- simple sprites
- panel textures
- masks
- borders
- icons
- gradients
- materials

if useful.

Do not destructively modify third-party package assets.

---

# 5. Main Menu structure

Prototype the current Main Menu structure.

Required navigation:

- OFFLINE MODE
- JOIN ROOM
- CREATE ROOM
- CUSTOMIZE
- SETTINGS
- EXIT

Preserve the overall idea that the Main Menu is relatively minimal.

The background remains a live SHIELDS environment rather than a flat menu background.

---

# 6. JOIN ROOM state

Prototype the JOIN ROOM expanded state.

Required content:

- JOIN ROOM title/state
- ROOM CODE
- room-code input
- JOIN
- BACK

The user currently likes the basic compact behavior where this appears within the existing left-side menu space.

You may redesign how it visually looks.

Do not redesign the networking flow.

This is primarily a UI/UX concept.

---

# 7. CUSTOMIZE — overall layout

The Customize screen should use a separate presentation layout.

Required structure:

LEFT / PREVIEW AREA:
- BACK
- player nickname
- large live character preview
- user can click-drag the character to rotate it

RIGHT / CONTENT AREA:
- WARDROBE tab
- SHOP tab
- content grid/list underneath

The exact proportions and visual styling are your design decision.

---

# 8. Customize environment

Use the existing tavern interior as the character-preview environment.

The user specifically likes the existing room corner containing:

- weapon rack
- shields
- stone wall
- warm lantern lighting

Use that general area if practical.

The character should stand clearly in the scene and remain readable against the background.

You may adjust concept-scene-only:

- camera framing
- preview lighting
- small fill light
- character placement
- background composition

Do not alter production GameScene lighting/environment.

---

# 9. Character preview

The preview should show the actual SHIELDS-style character.

Required behavior:

- character stands in the environment
- mouse drag rotates character around vertical axis
- rotation should feel smooth
- releasing mouse stops rotation
- no physics required

Optional if useful:

- very subtle idle/breathing/sway
- `DRAG TO ROTATE` hint
- reset-view control

Do not create a new character-design system in this task.

This is UI presentation.

---

# 10. Nickname area

Include a nickname area near the preview.

Conceptually:

`NICKNAME`

and the current/player name.

It may be presented as:

- editable field
- display + edit affordance

depending on the design.

Do not build account/backend infrastructure.

If nickname persistence is not already supported, keep it prototype/local.

---

# 11. WARDROBE tab

WARDROBE represents cosmetics already owned by the player.

Prototype at least these categories:

- HEAD
- BODY
- HANDS

You may rename these categories if you find clearer player-facing terminology.

Do not change their basic meaning.

---

# 12. Wardrobe item presentation

Wardrobe should contain several placeholder/example cosmetic slots so the UI can be judged visually.

The content may use:

- existing materials
- existing props
- simple placeholder icons
- generated project-owned mock assets

Do not build a full cosmetic backend.

Required states to demonstrate:

- available item
- currently selected item
- equipped item
- locked/unavailable item if useful
- default / no cosmetic option

---

# 13. Equip behavior

For the concept prototype:

selecting a wardrobe item should visibly select it.

Where practical:

- preview it on the character
- allow EQUIP
- indicate EQUIPPED state

Do not overengineer persistence.

The point is to validate the UX.

---

# 14. Default / unequip

There must be a clear way to return a cosmetic category to its default state.

Examples:

- DEFAULT
- NONE
- UNEQUIP

Choose the wording/design that fits best.

---

# 15. SHOP tab

SHOP is NOT implemented yet.

The Shop tab should exist visually.

When opened:

show a deliberate placeholder state such as:

`COMING SOON`

You may include empty/locked item cards behind or around it if this makes the screen feel intentional.

Do not build:

- currency
- purchasing
- Steam inventory
- pricing
- backend store logic

The shop must clearly read as intentionally unavailable, not broken.

---

# 16. SETTINGS — structure

Prototype a Settings screen using the same overall SHIELDS UI design language.

Do NOT add an unnecessarily large graphics-options screen.

Required settings:

AUDIO:
- Master Volume
- Music Volume
- Effects Volume

CONTROLS:
- Mouse Sensitivity

DISPLAY:
- Resolution
- Fullscreen / Windowed mode

You may add:

- V-Sync

only if it fits naturally and is useful.

Do not fill the screen with low-value graphics toggles.

---

# 17. Settings navigation

You may organize Settings however best fits the design.

Possible structures include:

- tabs
- grouped sections
- one compact panel

Choose the clearest UX.

Required:

- BACK
- settings controls are readable
- selected/current values are obvious

If APPLY is needed for the design, include it.

If immediate-apply UX is cleaner, use that instead.

This is currently a concept/prototype.

---

# 18. Settings controls

Create representative interactive controls for:

- sliders
- resolution selector/dropdown/arrows
- fullscreen toggle
- sensitivity

They should visually belong to the same UI system as the Main Menu.

Avoid default Unity UI appearance.

---

# 19. Design consistency

The following screens should feel like one product:

- Main Menu
- Join Room
- Customize
- Wardrobe
- Shop placeholder
- Settings

Establish a consistent design system for:

- heading hierarchy
- normal text
- active/hover text
- disabled text
- panels
- buttons
- tabs
- selection state
- sliders
- toggles
- input fields

The specific art direction is yours to determine.

---

# 20. Explore multiple visual directions

Do NOT settle immediately on the first design.

Create at least THREE distinct but plausible SHIELDS UI directions.

They should share the same UX/content structure but differ meaningfully in visual language.

For example, they may differ in:

- amount of framing
- ornamentation
- material inspiration
- typography treatment
- density
- visual weight
- use of shapes/icons

Do not merely recolor the same UI three times.

They should feel like genuinely different design approaches.

---

# 21. Easy comparison

Make it easy for the user to compare the three directions.

Preferred:

within `UIConceptScene`, allow switching between:

- Concept A
- Concept B
- Concept C

using simple temporary prototype controls / keyboard keys.

Example:

`1` = Concept A  
`2` = Concept B  
`3` = Concept C

The exact implementation is your choice.

Do not make these prototype switching controls part of production UI.

---

# 22. Required states for each design direction

At minimum, each direction should demonstrate:

1. Main Menu default
2. Join Room expanded
3. Customize / Wardrobe
4. Customize / Shop Coming Soon
5. Settings

The user should be able to judge the entire UI language, not only one menu.

---

# 23. Use real 16:9 composition

Primary target:

`1920 × 1080`

Also make sure the layout concept does not immediately break at:

`2560 × 1440`

Do not optimize every resolution yet.

This is design exploration.

---

# 24. Background readability

Do not solve readability by covering the whole tavern with opaque panels.

The environment is part of SHIELDS' identity.

Use whatever visual methods you think fit:

- negative space
- local darkening
- subtle overlays
- vignette
- hierarchy
- framing
- controlled lighting

But preserve atmosphere.

---

# 25. Interactivity scope

This should be more than a static screenshot.

Prototype enough interaction to judge UX:

- hover
- button selection
- tab switching
- Back
- Join Room open/close
- Wardrobe/Shop switching
- Settings category switching if used
- character drag rotation
- representative sliders/toggles

Do not implement production networking/store/settings persistence unless trivial and already available.

---

# 26. Production safety

Do NOT modify:

- production MainMenuScene
- multiplayer architecture
- GameScene gameplay
- Beer systems
- Waiter systems
- SHIELDS gameplay rules
- existing production menu flows

unless absolutely required to access reusable visual assets.

Prefer all exploratory scripts/assets under a dedicated area such as:

`Assets/UIConcept/`

or another clearly isolated project-owned folder.

---

# 27. Do not commit

Do not commit anything.

This is an exploratory design task.

---

# 28. Final report

Report:

1. UIConceptScene path
2. files/assets created
3. how Concept A differs from B and C
4. visual design rationale for Concept A
5. visual design rationale for Concept B
6. visual design rationale for Concept C
7. Main Menu treatment
8. Join Room treatment
9. Customize layout
10. character-preview camera/environment setup
11. drag-to-rotate behavior
12. Wardrobe category treatment
13. item states demonstrated
14. Shop Coming Soon treatment
15. Settings layout
16. Settings controls demonstrated
17. how to switch among Concept A/B/C
18. 1920×1080 result
19. 2560×1440 result
20. anything you recommend combining between the concepts
21. exact files modified outside the isolated concept area, if any

Stop after the UI exploration is complete.

Do NOT convert any concept into production UI yet.