# TASK 004 — Initial Deal and Card Presentation

Read SHIELDS_RULES.md before implementing this task.

The card/deck foundation from TASK 003 is complete and tested.

Do NOT modify the existing PlayingCard/CardDeck behavior unless a genuine bug is discovered.

Do NOT implement:

- attacks
- Shield replacement during turns
- Charge
- turn order
- damage
- Health redistribution after damage
- death
- winner detection
- networking
- real bot AI

This task ends when a match can be configured, cards can be dealt, players can choose their initial Shield/Health roles, and those cards are correctly represented on the 3D table.

---

# 1. Match setup UI

Extend the existing temporary match setup UI.

Before starting the prototype match, allow selection of:

Player Count:
3 through 8

Deck Count:
1 or 2

The selected values should become match configuration.

Do not build the final lobby UI.

This is still prototype setup UI.

---

# 2. Start match setup

When setup begins:

1. Create a CardDeck using the selected deck count.
2. Shuffle it.
3. Create the requested players using the existing player/table foundation.
4. Deal exactly three cards to every player.

All dealt cards leave the DrawPile and are now considered active/in-play cards.

No dealt card may be duplicated by identity.

---

# 3. Pure gameplay setup state

Introduce only the minimum plain-C# state required to represent initial Shields setup.

Gameplay state must NOT depend on GameObjects.

A player setup state should conceptually know:

- player index
- three initially dealt cards
- selected Health cards
- selected Shield card
- numeric starting Health
- whether initial selection is complete

Names may differ if the architecture remains clear.

Prefer placing pure Shields gameplay/setup logic in its own assembly that does not depend on UnityEngine if practical.

Do not create a large generic card-game framework.

---

# 4. Bot initial selection

Bots do not need strategic AI.

For every bot:

randomly choose exactly one of its three dealt cards as Shield.

The remaining two automatically become Health.

The random choice should operate on gameplay state rather than scene objects.

Prefer allowing a deterministic random seed for tests/debugging.

Bot selection must not inspect other players' cards in order to make the choice.

It is intentionally random.

---

# 5. Local human initial selection

The local player receives three visible cards.

All three are face-up.

The player chooses exactly ONE card to be their Shield.

The remaining two automatically become Health.

Use direct interaction with the cards if practical.

Preferred prototype interaction:

- enable mouse cursor during initial selection
- player clicks one of their three visible cards
- clicked card becomes Shield
- other two become Health

Do not require selecting two Health cards separately.

After selection is complete, restore the normal first-person mouse-look input state.

---

# 6. Card presentation architecture

Create a CardView or equivalent presentation component.

Important:

PlayingCard remains authoritative game data.

CardView is ONLY presentation.

CardView must not determine:

- card value
- rank
- suit
- identity
- gameplay behavior

It receives a PlayingCard and renders it.

Do not put authoritative state into Unity GameObjects.

---

# 7. Prototype card visuals

Replace the current 1/2/3 pip placeholder cards with readable standard-card placeholders.

Final artwork is NOT required.

Each face-up card should visibly communicate at minimum:

- rank
- suit

Examples:

A ♠
7 ♥
Q ♦
K ♣

Use existing Unity text capabilities already available in the project.

Do not install a new package solely for card text.

Simple prototype visuals are sufficient.

The four suits should be distinguishable.

Do not spend time creating final card art.

---

# 8. Initial three-card presentation

Before the local player selects their Shield, their three dealt cards must be visible and clickable.

They may temporarily use the existing three table positions if that produces a clear interaction.

After role selection:

two cards occupy:

- VerticalCardSlotLeft
- VerticalCardSlotRight

These are Health cards.

The selected Shield occupies:

- HorizontalCardSlot

The Shield should remain visually distinguishable by its landscape orientation.

All cards remain face-up.

---

# 9. Bot table presentation

Bots make their Shield choice automatically.

After their random selection:

- two Health cards appear in their vertical Health slots
- their Shield appears in their horizontal Shield slot

All bot cards are public and face-up.

There is no hidden information during initial setup.

---

# 10. Numeric starting Health

Once a player's roles are selected:

Starting Health =
HealthCard1.Value + HealthCard2.Value

Store this as authoritative numeric gameplay state.

Do NOT calculate Health later by reading CardView objects.

The physical cards represent the state; they do not define it.

Example:

King + Jack

13 + 11 = 24

Starting Health = 24

---

# 11. Match setup completion

Initial setup is complete only when every player has:

- two Health cards
- one Shield card
- numeric starting Health
- completed initial selection

Bots should complete this automatically.

The local player's choice may complete last.

When every player is ready, expose/log a clear:

InitialSetupComplete

state or equivalent.

Do NOT begin implementing actual turns yet.

---

# 12. Deck presentation

The remaining DrawPile stays conceptually at CenterDeckAnchor.

A simple deck visual is sufficient.

Optionally display remaining card count for debugging.

Do not instantiate one hidden 3D GameObject for every card remaining in the deck.

The authoritative DrawPile remains the plain-C# CardDeck.

---

# 13. Input separation

Initial card-selection interaction and seated camera mouse-look must not fight each other.

During local Shield selection:

- cursor can interact with local cards
- camera look should not consume the same mouse interaction

After selection:

- selection interaction ends
- normal seated first-person camera control resumes

Keep this simple.

---

# 14. Do not disturb the working scene foundation

Do not redesign:

- room
- table
- player prefab
- seating layout
- lighting
- player-count placement
- camera architecture

Only make changes required to connect initial card gameplay/presentation.

---

# 15. Tests

Add focused tests for pure gameplay setup logic.

At minimum validate:

### Deal

For player counts 3 through 8:

- every player receives exactly 3 cards
- no card identity appears in two players' deals
- DrawPile count decreases by playerCount * 3

Test with one deck.

Also test a representative two-deck match.

### Selection

Given three known cards:

selecting one as Shield:

- selected card becomes Shield
- other two become Health
- Starting Health equals their value sum
- all three original identities are preserved

### Bot selection

- bot selects exactly one of its three cards
- two remaining cards become Health
- deterministic seed reproduces the same choice
- choice remains one of the actual dealt cards

Do not install new testing packages.

Use the existing EditMode test setup.

---

# 16. Runtime validation

Validate in GameScene with at least:

3 players / 1 deck
6 players / 1 deck
8 players / 2 decks

Confirm:

- setup UI accepts Player Count and Deck Count
- correct deck size is used
- exactly three cards are dealt per player
- bots choose roles automatically
- local player can choose Shield by clicking a card
- remaining cards become Health
- all rank/suit labels match the underlying PlayingCard
- Starting Health values are correct
- all players show exactly two Health cards and one Shield
- exactly one gameplay camera remains active
- first-person look resumes after local setup selection
- no compile errors
- no Console errors

---

# 17. Report

When complete, report:

1. Files created
2. Files modified
3. New pure gameplay state types
4. How CardView binds to PlayingCard
5. How local Shield selection works
6. How bot random selection works
7. Test results
8. Runtime validation results

Stop after InitialSetupComplete.

Do NOT implement turn gameplay.