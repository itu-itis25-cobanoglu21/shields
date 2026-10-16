# TASK 021 — Main Menu UI & Core Navigation

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and this entire TASK.md before modifying anything.

TASK 020 already created the visual `MainMenuScene` diorama.

The existing MainMenuScene visual background is already approved enough to continue.

This task adds the actual main-menu UI on top of that live Unity scene.

IMPORTANT:

Preserve the existing MainMenuScene visual work unless a very small adjustment is genuinely required for UI readability.

Do NOT redesign:
- existing menu camera
- camera breathing
- environment composition
- hero props
- lighting
- light flicker
- night/window setup

Do NOT modify gameplay systems unnecessarily.

---

# 1. Font

The user has already added the font:

`Bowlby One SC`

to the Unity project.

Locate the actual `.ttf` / `.otf` file already present in the project.

Do NOT download another copy from the internet.

Use Bowlby One SC for ALL main-menu UI text in this task:

- `SHIELDS!` logo
- JOIN ROOM
- CREATE ROOM
- CUSTOMIZE
- SETTINGS
- EXIT
- any minimal Join Room sub-panel labels/buttons/input placeholder if required

Create a proper project-owned TextMeshPro Font Asset from the supplied Bowlby One SC font.

Prefer a clean SDF TMP Font Asset suitable for PC rendering.

Do not modify or move the original font file unnecessarily.

---

# 2. Main-menu UI hierarchy

Create a clean hierarchy in `MainMenuScene`.

Conceptually:

`MainMenuCanvas`
- `Logo`
- `MainButtons`
  - `JoinRoomButton`
  - `CreateRoomButton`
  - `CustomizeButton`
  - `SettingsButton`
  - `ExitButton`
- `JoinRoomPanel` if actually required by the existing join flow
- `EventSystem`

Exact hierarchy may follow existing project conventions.

Use standard Unity UI + TextMeshPro.

Do not introduce unnecessary UI frameworks.

---

# 3. Canvas setup

Use screen-space UI over the existing live 3D MainMenuScene.

Preferred setup:

- Canvas: Screen Space - Overlay
- Canvas Scaler:
  - UI Scale Mode: Scale With Screen Size
  - Reference Resolution: `1920 x 1080`
  - choose a sensible width/height match value for PC layouts

The UI must remain stable while the 3D background camera continues its breathing motion.

Do not attach UI to the moving MainMenuCamera.

---

# 4. Logo

Create the game title as real TextMeshPro text:

`SHIELDS!`

Requirements:

- ALL CAPS
- use Bowlby One SC
- large
- positioned in the upper-left negative-space area
- visually aligned with the menu items below
- strong dark/crimson red tone

Suggested starting color:

`#9E2528`

or another nearby deep crimson that looks better in the actual scene.

The logo should feel:
- bold
- aggressive
- medieval/poster-like
- strong
- not modern/minimal corporate

A subtle dark outline or shadow may be used if necessary for readability.

Do NOT make the logo neon or heavily glowing.

---

# 5. Main buttons

Create these buttons in this exact order:

1. JOIN ROOM
2. CREATE ROOM
3. CUSTOMIZE
4. SETTINGS
5. EXIT

Use Bowlby One SC for every button label.

Position them vertically under the logo on the left side.

Keep generous vertical spacing.

Do NOT fill the entire left side.

The composition should continue to breathe.

The text itself should be the main visual element.

Avoid large modern rounded button panels.

---

# 6. Button normal state

Normal button text should be:

- light grey / off-white
- clearly readable
- slightly less bright than hover state

Suggested starting color:

`#C7C4BC`

or nearby warm grey.

Do not make active buttons look disabled.

---

# 7. Hover state

When the mouse pointer enters an ACTIVE button:

- text becomes brighter / near-white
- text gains a subtle glow/highlight if appropriate
- button scales up very slightly
- transition is smooth

Suggested starting values:

Normal scale:
`1.00`

Hover scale:
`1.04`

Hover color:
`#FFFDF6`

Transition duration:
`0.12 seconds`

The hover effect must be subtle.

Desired feel:

responsive
premium
slightly aggressive
clean

Not:

arcade
cartoon bounce
huge zoom

---

# 8. Hover implementation

Create a lightweight reusable UI presentation component such as:

`MainMenuButtonVisual`

or use an equivalent existing project pattern if already available.

It should support:

- pointer enter
- pointer exit
- pointer down
- pointer up
- text color interpolation
- scale interpolation
- optional subtle glow/shadow change

Do not use a third-party tweening package.

Do not accumulate scale drift.

---

# 9. Click feedback

Functional buttons should have minimal click feedback.

Suggested:

Hover:
`1.04`

Pointer down:
approximately `0.99`

Release:
return to correct hover/normal state

Do not add sound in this task unless an existing appropriate UI click sound is already trivially reusable.

No new audio assets.

---

# 10. Functional scope

The following must work:

- JOIN ROOM
- CREATE ROOM
- EXIT

The following should remain visible but intentionally unavailable:

- CUSTOMIZE
- SETTINGS

Do NOT implement fake screens for Customize or Settings.

---

# 11. Inspect the existing multiplayer flow first

Before wiring JOIN ROOM / CREATE ROOM:

inspect the project thoroughly.

Find and understand the existing:

- Unity Multiplayer Services Sessions flow
- Relay flow
- NGO flow
- NetworkSession
- NetworkManager
- session creation entry point
- session join entry point
- join/session code handling
- existing lobby/session state
- existing GameScene transition logic
- any current SessionLobby UI/controller

Reuse existing networking logic.

Do NOT:
- create a second NetworkManager
- duplicate Relay code
- duplicate Sessions logic
- rewrite multiplayer architecture
- assume `clientId == seat`

---

# 12. CREATE ROOM

`CREATE ROOM` should start the project's existing host/create-session flow.

Use existing architecture.

Desired flow:

MainMenuScene
→ click CREATE ROOM
→ current supported create/host flow starts
→ existing lobby/setup/game flow continues

Do not bypass existing required lobby/session setup.

If create-session fails:

- remain in a usable state
- show a minimal readable error if a suitable UI pattern exists
- log useful technical information to Console

Do not redesign networking.

---

# 13. JOIN ROOM

Inspect how the current project expects a user to join an online session.

If it requires a room/session/join code:

create a minimal `JoinRoomPanel`.

Desired flow:

click JOIN ROOM
→ main menu remains visible or is subtly dimmed
→ small join panel appears
→ TMP_InputField for room/session code
→ JOIN button
→ BACK button

Use Bowlby One SC throughout.

Do not build a large separate lobby browser.

If an existing join UI already exists and can be reused cleanly, reuse it instead of duplicating functionality.

---

# 14. Join panel styling

If a new JoinRoomPanel is required:

keep it consistent with the main menu.

Suggested:

- dark translucent backing panel if needed
- warm off-white text
- dark red accent
- compact size
- positioned in the left-side UI area

Do not cover the hero diorama unnecessarily.

Input field may use a simpler TMP text presentation if Bowlby One SC hurts code readability, but prefer Bowlby One SC unless it clearly causes usability issues.

If a fallback font is needed for the room code input itself, report it rather than silently changing the design.

---

# 15. EXIT

EXIT must work.

In a standalone/player build:
- quit application correctly

In Unity Editor:
- stop Play Mode safely if practical
- do NOT close the Unity Editor process

No errors.

---

# 16. CUSTOMIZE

CUSTOMIZE remains visible.

Do NOT implement customization yet.

Preferred:
- non-interactable
- visually slightly dimmer than active buttons
- no misleading hover animation

Do not add a fake customization screen.

---

# 17. SETTINGS

SETTINGS remains visible.

Do NOT implement Settings yet.

Preferred:
- non-interactable
- visually slightly dimmer than active buttons
- no misleading hover animation

Do not add fake settings controls.

---

# 18. Disabled-state appearance

CUSTOMIZE and SETTINGS should still look intentionally part of the menu.

They may be dimmer, for example:

active normal:
`#C7C4BC`

disabled:
approximately `#77746E`

Tune visually.

They must not disappear completely.

---

# 19. Main-menu visual preservation

The UI must not interfere with existing MainMenuScene animation.

Preserve:

- camera breathing
- lantern flame
- light flicker
- moonlight
- hero props
- scene composition

UI stays screen-space stable.

---

# 20. Layout target

Keep approximately the left 30–40% of the screen as the UI area.

Preferred overall structure:

upper-left:
`SHIELDS!`

below:
`JOIN ROOM`
`CREATE ROOM`
`CUSTOMIZE`
`SETTINGS`
`EXIT`

Keep enough spacing between logo and first button.

Keep enough spacing between buttons.

Do not extend button text into the hero shields/weapon area.

---

# 21. Resolution handling

Validate at minimum:

- 1920x1080
- 2560x1440 or another common 16:9 resolution
- one wider aspect ratio if practical

Confirm:

- logo remains properly anchored
- menu remains on left
- text does not clip
- spacing remains visually acceptable
- hero area remains unobstructed
- JoinRoomPanel remains usable

---

# 22. Scene loading / startup

Inspect current project startup and scene-loading architecture.

The final project should be capable of entering MainMenuScene as the intended front-end scene.

However, do not blindly reorder scenes or break development workflows.

If Build Settings / startup configuration needs a minimal update, make it carefully and report it.

Do not redesign bootstrap architecture.

---

# 23. No automatic multiplayer session

While idle on MainMenuScene:

do NOT automatically:

- host
- join
- start Relay
- enter a match
- spawn gameplay players

Networking starts only after the user intentionally uses:

CREATE ROOM

or

JOIN ROOM

unless an existing lightweight service initialization is mandatory.

---

# 24. Validation — idle menu

Run MainMenuScene.

Confirm:

- logo is visible
- all five menu items are visible
- correct Bowlby One SC TMP font is used
- background scene continues animating
- no networking match starts automatically
- no missing references
- no console errors

---

# 25. Validation — hover

For active buttons:

confirm:

- pointer enter brightens text
- scale smoothly reaches approximately 1.04
- pointer exit restores exactly 1.00
- repeated hover does not create scale drift
- click feedback behaves correctly

For Customize / Settings:

confirm:
- disabled state is visually intentional
- they do not falsely behave like functioning buttons

---

# 26. Validation — CREATE ROOM

Test the existing Create Room flow through the new UI.

Confirm:

- button invokes the correct existing host/session code
- there is no duplicate NetworkManager/session object
- existing navigation still works

Report how far the flow was actually tested.

---

# 27. Validation — JOIN ROOM

Test:

- Join Room button
- join panel if created
- invalid/empty code
- back/cancel
- existing join path

If a real second participant is available, validate the real join path.

If not, explicitly state that second-client validation was not performed.

Do not claim it if it was not tested.

---

# 28. Validation — EXIT

Confirm correct editor-safe behavior.

If build testing is available, confirm player-build quit behavior.

Otherwise report build-side behavior as implemented but not manually verified.

---

# 29. Tests

Run existing relevant EditMode tests if MainMenu changes do not require a full suite.

If practical, run the full existing suite.

Do not weaken or modify gameplay tests just to make menu changes pass.

---

# 30. Console

Final MainMenuScene idle session should have:

- 0 project-code errors
- 0 missing-reference errors
- 0 UI exceptions
- 0 scene-navigation exceptions

Report unrelated third-party/editor warnings separately.

---

# 31. Out of scope

Do NOT implement:

- Customize screen
- Settings screen
- character customization
- audio settings
- graphics settings
- keybinds
- menu music
- UI sound pack
- Steam integration
- public server browser
- matchmaking
- friends system
- card-fire polish
- gameplay-animation changes
- MainMenuScene visual redesign

---

# 32. Final report

Report:

1. Exact Bowlby One SC font file found
2. TMP Font Asset created and exact path
3. MainMenuCanvas hierarchy
4. Logo position / font size / color
5. Button font size
6. Button vertical spacing
7. Normal button color
8. Hover button color
9. Hover scale
10. Hover transition duration
11. Click feedback
12. Disabled-state styling
13. Create Room binding
14. Join Room binding
15. JoinRoomPanel implementation
16. Exit implementation
17. Existing networking classes/methods reused
18. Any scene-loading changes
19. 1080p validation
20. higher-resolution validation
21. wide-aspect validation if performed
22. Create Room test result
23. Join Room test result
24. actual second-client test status
25. Exit test result
26. test-suite result
27. Console result
28. exact files created/modified
29. Inspector tuning values
30. any remaining visual/manual tuning issue

Do not commit anything.

Stop when TASK 021 is complete.