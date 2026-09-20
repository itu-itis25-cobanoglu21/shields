# TASK 002 — Scene and Player Foundation Refactor

The current prototype was useful for validating the visual concept, but its architecture needs to be changed before gameplay development.

Do not add networking yet.
Do not implement Shields rules yet.
Do not improve the art beyond what is necessary.
Preserve the current approximate visual dimensions and feel where sensible.

The game is intended to be a 3–8 player multiplayer tabletop game, so the architecture must be designed with that future requirement in mind.

## 1. Static scene architecture

The room, decoration, lighting, and table must be normal persistent scene objects.

Do not procedurally regenerate the room or lighting.

Restructure the scene approximately as:

GameScene
    Environment
        Room
        Lighting

    Table
        TableVisual
        CenterDeckAnchor
        CenterPlayArea

    Match
        MatchBootstrap
        TableLayoutController

    RuntimePlayers
    RuntimeTableAreas

The environment and table must survive player-layout rebuilds.

Remove any builder behavior that changes global RenderSettings or recreates the room.

---

## 2. Match player count

Before the prototype match begins, provide a simple temporary UI allowing the user to select a total player count from 3 through 8.

Call this targetPlayerCount.

For the current offline prototype:

- exactly one player is the local human
- every remaining player is a bot placeholder

Example:

targetPlayerCount = 6

Player 0 = Local Human
Player 1–5 = Bots

Bots do not need any gameplay AI yet.

Their purpose is only to occupy seats and provide player objects for testing.

Do not create a networking lobby yet.

---

## 3. Player prefab

Create one reusable PlayerCharacter prefab.

Human players and bots must use the SAME physical PlayerCharacter prefab.

Do not create separate BotCharacter and HumanCharacter prefabs.

The prefab should contain a clean hierarchy similar to:

PlayerCharacter
    Body
    HeadPivot
        HeadVisual
        CameraAnchor

The current primitive placeholder character visuals are sufficient.

The architecture must make it easy to replace the placeholder body/head with real character art later.

Add a PlayerCharacter component containing at least the runtime identity needed for the prototype:

- playerIndex
- isLocalPlayer
- isBot

Avoid networking-specific APIs for now, but keep the design compatible with future networking.

---

## 4. Head and first-person camera architecture

The first-person camera must behave as if it is attached to the local player's head.

Mouse look must rotate the player's HeadPivot.

Because the CameraAnchor is a child of HeadPivot, the camera follows the same rotation.

Other players should therefore eventually be able to visually observe where another player is looking.

Important:

- Only ONE gameplay camera may be active.
- Bot players must not have active cameras.
- Prefer having a single MainCamera that is attached/reparented to the local PlayerCharacter's CameraAnchor at runtime.
- Do not hard-code Player 0 inside the camera system except that Player 0 is currently chosen as the local player by the offline prototype bootstrap.
- The local HeadVisual may be hidden from the local camera if necessary to avoid first-person clipping.
- There is no walking movement.
- The player remains seated.
- Clamp head rotation to sensible seated limits.

---

## 5. Player placement around the table

Players must be positioned evenly around the round table.

For targetPlayerCount N:

angleStep = 360 / N

Each player receives one evenly spaced position around the table and faces the table center.

Support all counts:

3
4
5
6
7
8

The system should calculate this dynamically.

Keep the placement logic separate from PlayerCharacter.

---

## 6. Table sizing

The table may adjust its visible diameter depending on targetPlayerCount if necessary for comfortable spacing.

Do NOT scale the entire Table hierarchy.

If dynamic sizing is used, only resize/scale the appropriate visual tabletop geometry.

CenterDeckAnchor and other gameplay anchors must remain logically stable and must not inherit undesirable scaling.

Prioritize:

- readable cards
- comfortable player spacing
- players remaining close enough for a social tabletop feeling
- all other players being reasonably visible

Keep the system simple and tunable.

---

## 7. Shields card layout — replace the old hand concept

Important gameplay information:

Players do NOT hold cards in their hands in Shields.

Each player has exactly three visible cards placed on the table in front of them.

The layout is:

two portrait/vertical cards side by side closer to the player

and

one landscape/horizontal card in front of those two, toward the table center.

Conceptually:

[ Vertical ] [ Vertical ]

      [ Horizontal ]

All three cards are face-up and lie on the table.

The remaining deck is located at the center of the table.

Remove the old hand/fanned-card placeholder concept.

Do not call this a HandAnchor.

For each player, create a PlayerTableArea associated with that seat/player.

Each PlayerTableArea should expose three future gameplay card slots:

- VerticalCardSlotLeft
- VerticalCardSlotRight
- HorizontalCardSlot

These slots belong to the TABLE layout, not to the character body.

Also maintain:

- CenterDeckAnchor

at the center of the table.

For now, create simple placeholder face-up cards in these slots only to validate position and first-person readability.

Do not implement card rules or card state yet.

---

## 8. Runtime organization

Runtime-created objects should be clearly organized:

RuntimePlayers
    Player_00
    Player_01
    ...

RuntimeTableAreas
    PlayerArea_00
    PlayerArea_01
    ...

The static Environment and Table should NOT be children of generated/runtime containers.

---

## 9. Bot placeholders

Create a minimal BotController component if useful.

It should NOT make decisions yet.

Do not implement AI behavior.

It may simply mark the PlayerCharacter as bot-controlled for future development.

---

## 10. Remove obsolete prototype architecture

Inspect the current implementation before changing anything.

Reuse useful existing code such as seat-layout math or seated-look logic where appropriate.

However, remove or simplify systems that are now architecturally obsolete, including:

- procedural room generation
- procedural lighting generation
- global ambient/render setting manipulation
- old hand/fanned-card preview
- camera permanently owned by Seat_00
- unnecessary primitive/material generation infrastructure that is no longer needed

Do NOT perform unnecessary abstraction or broad rewrites beyond what this new architecture requires.

---

## 11. Multiplayer readiness

Do not add Netcode for GameObjects yet.

However, ensure the design does not assume there will only ever be one human player.

Avoid code that finds players by GameObject name.

Avoid gameplay logic being coupled to Player 0.

Player index, local/remote ownership, bot/human control, and visual character representation should remain conceptually separate.

---

## 12. Validation

Validate all player counts from 3 to 8.

Specifically test at least:

3 players
6 players
8 players

Confirm:

- correct number of PlayerCharacter instances
- one local human
- remaining players marked as bots
- exactly one active camera
- camera is attached to the local player's CameraAnchor
- rotating the camera/head rotates HeadPivot
- player bodies remain facing the table
- players are evenly distributed using 360 / playerCount
- all players have one corresponding PlayerTableArea
- every PlayerTableArea has exactly:
  - 2 portrait card slots
  - 1 landscape card slot
- CenterDeckAnchor exists
- cards are readable from the local first-person camera
- room and lighting remain unchanged when rebuilding player layout
- no compile errors
- no Console errors

Report:

1. Final hierarchy
2. Files created
3. Files modified
4. Files removed
5. Prefabs created
6. Any architecture decisions that differ from this task and why

Stop when this task is complete.

Do not implement Shields gameplay.
Do not add networking.