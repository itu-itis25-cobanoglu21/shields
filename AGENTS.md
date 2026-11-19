# SHIELDS Project Instructions

## Project

SHIELDS is a 3-8 player first-person social tabletop card game built in Unity 6.

Target:
- Windows / Steam
- Multiplayer
- 3-8 players
- Bots supported
- Fast MVP development

Read `SHIELDS_RULES.md` before implementing or changing any gameplay rule.

Never invent missing Shields rules.
If a gameplay rule is ambiguous, stop and ask.

---

## Architecture

Keep authoritative gameplay logic independent from Unity scene objects.

Existing pure assemblies:

- Shields.Cards
- Shields.Game

Gameplay state must not be derived from GameObjects, CardViews, transforms, text labels, or other presentation objects.

Presentation reflects gameplay state; presentation does not define gameplay state.

Keep these concepts separate:

- Card / deck logic
- Match / Shields rules
- Player state
- Bot decisions
- Multiplayer/network ownership
- Unity presentation

The game will become multiplayer later.

Do not hard-code gameplay around Player 0 being the only human player.

Do not add Netcode until explicitly requested.

---

## Current Foundation

The following systems already exist and work:

- Static room/environment
- Round dynamically sized table
- Dynamic 3-8 player seating
- PlayerCharacter prefab
- HeadPivot / first-person camera architecture
- PlayerTableArea
- Two Health card slots
- One Shield card slot
- Center deck
- One/two standard deck support
- PlayingCard model
- CardDeck draw/discard/reshuffle
- Initial three-card deal
- Local Shield selection
- Random bot initial Shield selection
- Numeric starting Health
- CardView presentation
- EditMode tests

Do not rewrite these systems unless required by the current task or unless a real bug is found.

---

## Development Rules

- Inspect existing code before editing.
- Prefer extending working systems over rewriting them.
- Do not install packages unless explicitly requested.
- Do not redesign unrelated systems.
- Do not modify working room/environment art during gameplay tasks.
- Keep tasks narrow.
- Keep code simple.
- Avoid unnecessary interfaces, service locators, dependency injection, generic frameworks, or event systems.
- Add tests for pure gameplay logic.
- Run existing tests after gameplay changes.
- Validate Unity compilation and Console errors.
- Report files created, modified, and removed after every task.

---

## Input Direction

This is a first-person seated game.

The mouse normally always controls the player's head/camera.

Gameplay interaction should use a small center-screen crosshair and a ray from the camera center.

Do not unlock the mouse just to click world objects unless a true menu requires it.

---

## Git / Safety

Do not make unrelated changes.

Do not delete working systems without explaining why.

Before broad refactors, inspect dependencies first.

Stop after the requested task is complete.