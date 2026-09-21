# TASK 006 — Authoritative Shields Gameplay Engine

Read CLAUDE.md and SHIELDS_RULES.md completely before making changes.

The project currently has:

- working 3–8 player scene/layout
- first-person head/camera interaction
- center-screen crosshair interaction
- standard 1/2-deck PlayingCard/CardDeck system
- initial 3-card deal
- local initial Shield selection
- random bot initial Shield selection
- numeric starting Health
- CardView presentation
- passing EditMode gameplay/card tests

This task creates the authoritative Shields rules engine.

IMPORTANT:

Do NOT implement the turn UI yet.
Do NOT implement keyboard 1/2/3 controls yet.
Do NOT implement gameplay bots yet.
Do NOT add floating action text yet.
Do NOT implement networking yet.
Do NOT animate cards yet.
Do NOT redesign the scene.

This task is primarily PURE GAME LOGIC + TESTS.

The goal is that after this task, a complete Shields match can theoretically be played through code/tests without requiring Unity GameObjects.

---

# 1. Core architecture requirement

The authoritative Shields gameplay engine must remain independent from Unity presentation.

Prefer implementing it inside the existing pure `Shields.Game` assembly.

The rule engine must NOT depend on:

- GameObjects
- MonoBehaviours
- transforms
- CardView
- PlayerCharacter
- scene hierarchy
- Unity UI
- physics/raycasting

Presentation will call the game engine later.

Bots will call the same game engine later.

Multiplayer host/server will eventually call the same game engine.

Do not create separate gameplay rules for humans, bots, or networking.

---

# 2. Transition from initial setup to active match

The existing MatchSetup / PlayerSetupState system already creates:

- CardDeck
- players
- two selected Health cards
- one selected Shield card
- numeric starting Health
- completed initial setup

Create the minimum clean transition from completed initial setup into an active Shields match.

Conceptually something like:

MatchSetup
    ->
ShieldsMatch

The exact naming may differ.

Do not redeal cards.

Do not recreate the deck.

The active match must continue using the SAME logical CardDeck state left after initial deal.

Cards already owned by players remain outside DrawPile and DiscardPile.

---

# 3. Active player state

Create a pure gameplay representation for each active match player.

It must contain at least:

- player index
- alive/eliminated state
- current numeric Health
- Health-card representation
- current Shield card
- optional stored Charge card

Do not derive any of these values from CardViews.

Numeric Health is authoritative.

---

# 4. Health-card representation

Health presentation is part of the rules and therefore needs pure logical state.

A player can visually represent Health using:

- two physical PlayingCards
- one physical PlayingCard
- physical card + Virtual Health Marker
- one Virtual Health Marker

depending on current Health.

Create a simple pure representation for Health slots.

Do NOT turn Virtual Health Markers into fake PlayingCards.

A Virtual Health Marker:

- contains a numeric value
- is not a card
- has no suit
- has no CardId
- never enters DrawPile
- never enters DiscardPile

Keep explicit logical LEFT and RIGHT Health slots where relevant.

This matters because the confirmed tie rule says:

If both Health cards have the same value and one must be removed,
discard the LEFT card and keep the RIGHT card.

---

# 5. Starting player and direction

When an active match begins:

Randomly select:

- one living starting player
- turn direction

Turn direction is either:

- Clockwise
- CounterClockwise

Support an optional deterministic seed / injectable RNG approach for tests.

Do not use deterministic RNG as a fairness/security mechanism.

Expose pure match state containing at least:

- current player index
- current turn direction
- match-over state
- winner if one exists

---

# 6. Turn actions

The authoritative gameplay engine supports exactly these actions:

- Attack
- ReplaceShield
- Charge

Create a small action/request representation.

The caller must commit the action and any required target BEFORE the engine draws/reveals the card used by that action.

This is important.

Do not expose an API where the caller:

1. draws a card
2. sees its value
3. chooses an action afterward

The action method itself should perform the required draw only after validating the committed request.

---

# 7. Turn ownership validation

Only the current living player may perform an action.

Reject requests from:

- non-current players
- eliminated players
- invalid player indices

A player cannot perform two actions in one turn.

After a successfully resolved action, advance to the next living player according to the selected direction.

Skip eliminated players.

If the match becomes over, do not advance into another gameplay turn.

---

# 8. Normal Attack

Attack requires:

- current acting player
- another living target player

Self Attack is forbidden.

Validate the target BEFORE drawing.

If target is invalid:

- reject the action
- do not draw a card
- do not advance the turn

For a valid Attack:

Draw one card.

Reveal it logically as the Attack card.

AttackPower = card.Value

Attack succeeds only when:

AttackPower > TargetShieldValue

If:

AttackPower <= TargetShieldValue

damage = 0

The attack still resolves and the turn advances.

The Attack card goes to DiscardPile after resolution.

The target Shield is NEVER consumed by an attack.

---

# 9. Successful normal Attack damage

When:

AttackPower > Shield

Damage:

AttackPower - Shield

Subtract damage from authoritative numeric Health.

If new Health remains above zero:

run Health representation redistribution rules.

If the target has one stored Charge card:

a successful attack that deals positive damage destroys that stored Charge.

The stored Charge card goes to DiscardPile.

A failed attack dealing zero damage does NOT interrupt Charge.

---

# 10. Replace Shield

ReplaceShield requires a living target.

The target may be:

- the acting player
- another living player

Validate target before drawing.

For a valid request:

1. draw one card
2. reveal it
3. discard target's existing Shield
4. new drawn card becomes target's Shield

Replacement is mandatory even if the new Shield is weaker.

Example:

Old Shield = 10
Draw = 2

New Shield = 2.

Turn then advances normally.

Stored Charge is unaffected by Shield replacement.

---

# 11. Charge — first use

If the acting player currently has NO stored Charge card and chooses Charge:

1. draw one card
2. do NOT reveal its gameplay face to presentation callers that should not know it
3. store it as the player's Charge card
4. advance the turn

No target is required for the first Charge.

The Charge card remains outside both DrawPile and DiscardPile.

The authoritative engine obviously knows which PlayingCard it is internally, but public-facing match state/results must support treating it as hidden information.

Do not leak its value through action results intended for presentation.

---

# 12. Player already holding a Charge

A stored Charge does NOT force the player to complete it on the next turn.

A charged player may still choose:

- normal Attack
- ReplaceShield
- Charge

If they choose normal Attack or ReplaceShield:

their existing stored Charge remains unchanged.

---

# 13. Completing Charge / Charged Attack

If a player already has one stored Charge card and chooses Charge again:

a valid target is required BEFORE drawing the second card.

Target must be:

- another living player
- not the acting player

Validate target first.

Only then:

1. draw the second Charge card
2. reveal both Charge cards
3. calculate combined AttackPower

ChargedAttackPower =
ChargeCard1.Value + ChargeCard2.Value

The Charged Attack succeeds only when:

ChargedAttackPower > TargetShieldValue

If equal or lower:

damage = 0

If higher:

Damage =
ChargedAttackPower - TargetShieldValue

After resolving the Charged Attack:

BOTH Charge cards always go to DiscardPile,
whether the attack succeeds or fails.

The acting player's stored Charge becomes empty.

Then advance the turn unless the match ended.

---

# 14. Charged Attack and target Charge interruption

If a Charged Attack deals positive damage:

and the target has a stored Charge:

the target's stored Charge card is discarded and cleared.

If the Charged Attack deals zero damage:

the target keeps their stored Charge.

---

# 15. Damage and numeric Health

Numeric Health is authoritative.

Damage directly updates:

CurrentHealth = CurrentHealth - Damage

Do not compute current Health by summing GameObjects or CardViews.

If CurrentHealth <= 0:

do NOT perform normal Health redistribution.

Instead eliminate the player using the death rules.

If CurrentHealth > 0:

update the physical/logical Health representation according to the rules below.

---

# 16. Health redistribution — standard case

Initial Health normally has two Health cards.

After taking damage:

Prefer keeping the LOWER-valued existing Health card.

Discard the HIGHER-valued Health card.

Then determine what additional value is needed to represent CurrentHealth.

Example:

Health:

King + Jack = 24

Take 8 damage.

CurrentHealth = 16

Higher card:
King = 13 -> discard

Keep:
Jack = 11

Required second value:
16 - 11 = 5

Obtain a physical value-5 card using the Health-card lookup rules.

Result:

Jack + 5 = 16

---

# 17. Equal Health-card tie

If both existing physical Health cards have the same value:

and only one should remain:

discard the LEFT Health card.

Keep the RIGHT Health card.

Example:

10 + 10

If redistribution can keep one 10:

LEFT 10 is discarded.
RIGHT 10 remains.

This deterministic rule must be tested.

---

# 18. When no existing Health card can remain

If CurrentHealth is less than the value of the lower existing Health card:

both existing Health cards must be discarded.

Then represent CurrentHealth with a single value.

Example:

King + Jack = 24

CurrentHealth becomes 9.

Neither King nor Jack can remain.

Discard both.

Then obtain a value-9 card using the normal lookup rules.

If unavailable:

use a Virtual Health Marker with value 9.

---

# 19. Obtaining a required Health card

When Health representation requires a real card of a specific value:

Search in this exact priority:

1. DiscardPile
2. DrawPile
3. Virtual Health Marker

### DiscardPile

If any card with required Value exists in DiscardPile:

remove one matching card from DiscardPile
and use it as Health.

### DrawPile

If none exists in DiscardPile:

search DrawPile for a card with the required Value.

If found:

remove that specific card from DrawPile.

IMPORTANT:

Removing a Health replacement card from DrawPile must preserve the relative order of every other DrawPile card.

Do NOT shuffle.

Do NOT reorder the remaining pile.

### Neither pile

If no physical card with that Value exists:

create a Virtual Health Marker.

---

# 20. Minimal CardDeck extensions

The current CardDeck foundation is already tested.

Modify it ONLY if necessary to support Health representation.

It is acceptable to add narrowly scoped APIs such as conceptually:

- find/remove a card of a given Value from DiscardPile
- find/remove a card of a given Value from DrawPile while preserving order

Exact method names are your choice.

Do NOT rewrite CardDeck.

Do NOT break existing behavior.

Add tests for any new CardDeck API.

All previous card/deck tests must continue passing.

---

# 21. Virtual Health waiting queues

When a Virtual Health Marker is created for required value N:

register that Health slot in a FIFO waiting queue for value N.

Example:

Player A waits for 5.
Later Player B waits for 5.

A must receive the first real discarded 5 that becomes available.

B remains waiting.

Maintain separate logical FIFO ordering per required value.

---

# 22. Servicing Virtual Health Markers

Whenever a real PlayingCard enters DiscardPile:

check whether its Value has any waiting Virtual Health Marker.

If yes:

the oldest waiting slot for that value immediately receives the real card.

The card is removed from DiscardPile.

The Virtual Health Marker disappears.

Example:

Player A has Virtual 5.

A real value-5 card is discarded.

Instead of remaining in DiscardPile:

that physical card is transferred to Player A's waiting Health slot.

This must be part of authoritative gameplay state.

Do not implement this in CardView/presentation.

---

# 23. Discard flow

All gameplay card discards should go through a consistent authoritative path so Virtual Health waiting queues can be serviced correctly.

Relevant examples include:

- resolved Attack cards
- old replaced Shields
- completed Charge cards
- interrupted Charge cards
- discarded Health cards
- cards released when a player dies

Avoid direct ad-hoc mutations to DiscardPile from many unrelated code paths if doing so would bypass Virtual Health servicing.

Do not build a large event framework.

A small centralized gameplay discard helper is sufficient.

---

# 24. Player death

If CurrentHealth <= 0:

mark the player eliminated.

All physical cards currently owned by that player are released/discarded, including:

- Health cards
- Shield
- stored Charge

Virtual Health Markers disappear and are removed from their waiting queues.

A dead player's cards may service other players' waiting Virtual Health Markers as they enter discard.

The eliminated player must never receive another turn.

---

# 25. Win condition

After elimination:

count living players.

If exactly one living player remains:

MatchOver = true

Winner = that player

No further actions may resolve after MatchOver.

---

# 26. DrawPile exhaustion

Continue using the existing CardDeck rule:

When a normal draw is required and DrawPile is empty:

- move DiscardPile to DrawPile
- shuffle
- draw

Active cards are not included.

Virtual Health Markers are not cards.

IMPORTANT:

If discard cards are transferred to waiting Virtual Health slots before they can remain in DiscardPile, they are active Health cards and therefore are not available to reshuffle.

If a required draw cannot produce a card even after the existing reshuffle logic:

fail gracefully.

Do not invent a card.

Do not leave partially-mutated gameplay state.

---

# 27. Action results for future presentation

Every resolved action should return a small structured result suitable for future presentation.

The result should contain only useful information.

Conceptually it may include:

- success / rejection
- acting player
- action type
- target if any
- revealed card(s), where allowed
- AttackPower
- target Shield value
- damage dealt
- resulting Health
- whether target Charge was interrupted
- Shield old/new value for replacement
- whether a player died
- whether match ended
- winner

Exact structure is your choice.

IMPORTANT:

First-Charge action result must NOT publicly expose the hidden Charge card's rank/value/suit.

The engine may know the card internally.

Presentation should only learn that:

"Player X has one hidden Charge card."

This result will later drive:

- world-space action text
- card animations
- sound
- bots/debugging
- multiplayer replication

Do not create those presentation systems now.

---

# 28. Public vs internal match information

The only hidden gameplay information is unresolved Charge card identity.

Design public-facing state/results so that future presentation/networking can inspect match state without accidentally revealing stored Charge cards.

Do not over-engineer a full networking serialization layer.

Simply avoid exposing hidden Charge face information through normal public action results/state snapshots where practical.

---

# 29. No bot gameplay yet

BotController must NOT start performing actual turn actions in this task.

Bots will use this engine in the next task.

Do not put:

- Attack logic
- Shield logic
- Charge logic
- damage math
- turn advancement

inside BotController.

Bots should later choose a request and submit it to the same engine as humans.

---

# 30. No Unity gameplay controls yet

Do NOT implement:

- keyboard 1 = Attack
- keyboard 2 = Replace Shield
- keyboard 3 = Charge
- crosshair player targeting
- turn HUD
- action text

Those belong to the presentation task after the engine is proven.

Do not modify CrosshairInteraction unless absolutely necessary.

---

# 31. Tests — turn system

Add focused EditMode tests.

At minimum validate:

### Starting state

- starting player is living
- direction is either Clockwise or CounterClockwise
- deterministic seed reproduces the same start state

### Turn advancement

- Clockwise advances correctly
- CounterClockwise advances correctly
- dead players are skipped
- only current player may act
- rejected invalid action does not advance turn

---

# 32. Tests — normal Attack

Test:

- self attack is rejected before drawing
- dead target rejected
- invalid target rejected
- valid target draws exactly one card
- Attack > Shield deals difference
- Attack == Shield deals zero
- Attack < Shield deals zero
- Shield remains unchanged
- Attack card ends in discard unless consumed by a waiting Health marker
- successful damage interrupts stored Charge
- zero damage does not interrupt Charge
- turn advances after resolution

Use deterministic deck arrangements / test helpers where necessary.

Do not make tests probabilistic.

---

# 33. Tests — Shield replacement

Test:

- self-target replacement works
- other living target works
- dead target rejected
- old Shield is discarded
- new Shield becomes target Shield
- weaker Shield still replaces stronger Shield
- stored Charge is unaffected
- turn advances

---

# 34. Tests — Charge

Test:

### First Charge

- requires no target
- draws exactly one card
- stores it
- result does not reveal card identity publicly
- turn advances

### While Charged

- normal Attack preserves stored Charge if actor is not damaged
- ReplaceShield preserves stored Charge

### Second Charge

- target required
- self target rejected before draw
- dead target rejected before draw
- second card drawn only after valid target
- combined value calculated correctly
- Shield applied once to combined attack
- equality deals zero
- both Charge cards discarded after resolution
- stored Charge clears
- positive damage interrupts target Charge
- zero damage does not interrupt target Charge

---

# 35. Tests — Health redistribution

Test the confirmed examples and edge cases.

### Example

King + Jack = 24

Take 8 damage.

Expected:

CurrentHealth = 16

King discarded.

Jack retained.

Physical value 5 obtained.

Representation = Jack + 5.

### Below lower card

King + Jack = 24

Health becomes 9.

Expected:

King discarded.
Jack discarded.

Representation becomes one physical 9 if available,
otherwise Virtual 9.

### Equal cards

10 Left + 10 Right

After damage where one 10 can remain:

LEFT 10 discarded.
RIGHT 10 remains.

### Lookup priority

Verify:

DiscardPile match is used before DrawPile match.

### DrawPile search

Removing matching value from DrawPile:

- removes exactly one card
- preserves all other cards' relative order
- does not shuffle

### Virtual marker

If no physical card exists:

Virtual marker created with correct value.

---

# 36. Tests — FIFO Virtual Health replacement

Test:

Player A waits for value 5.

Then Player B waits for value 5.

Discard first real 5:

- A receives it
- B remains virtual

Discard second real 5:

- B receives it

Verify the transferred cards no longer remain in DiscardPile.

Also test removing an eliminated player's waiting marker from the queue.

---

# 37. Tests — death and winner

Test:

- Health <= 0 eliminates player
- all physical player cards are released
- stored Charge released
- virtual markers removed from waiting queues
- eliminated player skipped in turns
- death cards can satisfy waiting Virtual Health markers
- last living player becomes Winner
- MatchOver becomes true
- additional actions are rejected after MatchOver

---

# 38. State/card accounting tests

At important points verify no physical PlayingCard identity exists in two logical locations simultaneously.

Conceptually each physical card must be in exactly one place:

- DrawPile
- DiscardPile
- player Health
- player Shield
- stored Charge
- temporary action resolution while being moved

Do not create duplicate PlayingCard identities.

For representative tests, verify total physical-card accounting remains:

52 for one deck

or

104 for two decks.

Virtual Health Markers do NOT count as physical cards.

---

# 39. Existing tests

All existing tests must continue passing.

Run:

- existing CardDeck tests
- existing MatchSetup tests
- all new Shields gameplay tests

Do not weaken/remove old assertions just to make the new code pass.

---

# 40. Runtime/editor safety

This task should not require meaningful changes to GameScene.

Do not dirty or redesign the scene unnecessarily.

Do not modify:

- room
- lighting
- player layout
- camera layout
- Crosshair interaction presentation
- CardView visuals

unless required to resolve a real compile dependency.

---

# 41. Final report

When complete report:

1. Files created
2. Files modified
3. Core active-match types introduced
4. How action requests work
5. How turn order works
6. How Attack works
7. How Shield replacement works
8. How Charge works
9. How Health redistribution works
10. How Virtual Health FIFO works
11. How hidden Charge information is protected
12. Test count and results
13. Any unresolved rule ambiguity discovered

If you encounter an actual gameplay rule ambiguity not covered by SHIELDS_RULES.md:

STOP and ask.

Do not invent the rule.

Do not continue into bot gameplay, UI controls, networking, or presentation.

Stop when TASK 006 is complete.s