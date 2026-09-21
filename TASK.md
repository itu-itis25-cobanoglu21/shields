# TASK 007.1 — Fix Local Human Turn Being Auto-Played By Bot Logic

Read CLAUDE.md and SHIELDS_RULES.md first.

There is a runtime bug in the currently working local match.

Observed real-player behavior:

- Initial Shield selection works correctly.
- Bots correctly take random turns.
- When the authoritative turn reaches the LOCAL HUMAN player, the game does not wait for human input.
- Instead, a random action appears to be selected/resolved automatically and the turn advances.

This is incorrect.

Do NOT change ShieldsMatch gameplay rules.
Do NOT redesign bot logic.
Do NOT modify bot probabilities.
Do NOT add networking.
Do NOT change presentation unrelated to this bug.

Inspect the current MatchTurnController / MatchBootstrap / PlayerCharacter ownership flow and find the actual cause before changing code.

---

## Required behavior

There is exactly one local human in the current offline prototype.

When:

ShieldsMatch.CurrentPlayerIndex == localHumanPlayerIndex

the controller MUST:

- NOT schedule RandomBotPolicy
- NOT start a bot-think coroutine for that player
- NOT auto-submit any action
- NOT skip the turn
- NOT choose a fallback action
- wait indefinitely for human input

The local human turn must remain active until the human successfully resolves one legal action through:

1 = Attack
2 = Replace Shield
3 = Charge

If the selected action requires a target, the turn must remain active while the player aims/selects a target.

No timer should consume the human turn.

---

## Bot turns

RandomBotPolicy may run ONLY when the current authoritative player is actually bot-controlled.

Do not decide this merely from assumptions such as:

- current player is not seat 0
- presence of BotController component
- PlayerCharacter prefab containing a BotController
- absence of current keyboard input

Use the existing authoritative/runtime player ownership information.

There must be an explicit reliable distinction between:

- local human
- bot

Do not infer it from GameObject names.

---

## Important prefab detail

The shared PlayerCharacter prefab may contain components used by both human and bot players.

The mere existence of a BotController component on a PlayerCharacter must NOT make that player a bot.

Control identity must come from runtime player configuration/state such as:

- isLocalPlayer
- isBot

or the existing equivalent.

Preserve the shared PlayerCharacter prefab architecture.

---

## Bot coroutine safety

Inspect the existing bot scheduling carefully.

Ensure that:

- a coroutine scheduled for a previous bot cannot fire after the authoritative turn has changed
- a delayed bot callback verifies the current player again immediately before submitting
- changing to the human turn invalidates/cancels any stale bot action
- only one bot action can ever be scheduled for a turn

A stale delayed bot action must never submit on behalf of the local human.

This is especially important because bot actions now use approximately 5 seconds of pacing.

---

## Human input

On the local player's turn:

### Press 1

Enter Attack targeting.

Do NOT submit until a valid target is confirmed with crosshair + left click.

### Press 2

Enter Replace Shield targeting.

Do NOT submit until a valid target is confirmed.

### Press 3

If no Charge is stored:
submit first Charge immediately.

If already Charged:
enter Charged Attack targeting and wait for target confirmation.

The mouse remains locked to first-person head look.

Do not revert to free cursor interaction.

---

## Turn HUD

When it is the local player's turn:

display:

YOUR TURN

and, when no action is selected, a clear prototype hint such as:

1 — ATTACK
2 — REPLACE SHIELD
3 — CHARGE

This is only to make manual testing obvious.

When waiting for a target, preserve the existing targeting prompt.

---

## Tests

Add focused tests where practical around the turn-controller decision logic.

At minimum establish/test that:

- local human turn is classified as human
- bot turn is classified as bot
- RandomBotPolicy is never invoked for local human
- bot scheduling is invalidated if the authoritative turn changes
- stale delayed bot action cannot submit for a different turn/player

Do not weaken any existing tests.

All existing 155 EditMode tests must continue passing.

---

## Runtime validation

Use a real Play Mode run with at least 4 players.

Validate:

1. Initial Shield selection works.
2. Bots play automatically with existing pacing.
3. When local human turn begins:
   - YOUR TURN appears
   - wait at least 10 seconds without pressing anything
   - NO gameplay action resolves
   - CurrentPlayerIndex remains the local player
4. Press 1:
   - Attack targeting mode appears
   - without clicking a target, wait several seconds
   - turn still does NOT resolve
5. Confirm a valid target:
   - exactly one Attack resolves
   - turn advances
6. Wait until local turn returns.
7. Test 3 / Charge manually.
8. Ensure bots resume normally after human action.
9. No duplicate submits.
10. No Console errors.

Report the actual root cause.

Do not merely add a workaround timer.

Stop after this bug is fixed.