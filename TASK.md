# TASK 003 — Playing Card and Deck Foundation

We are now beginning gameplay development for Shields.

Do NOT implement the Shields game rules yet.
Do NOT change the current room, table, player placement, camera, or scene architecture.
Do NOT implement networking yet.
Do NOT implement bots yet.
Do NOT install packages.

This task is only the pure playing-card and deck foundation.

The current GameScene and player/table foundation are working and should remain intact.

## 1. Important architecture requirement

The card/deck GAME LOGIC must be independent from Unity GameObjects and presentation.

Do not make the authoritative card model a MonoBehaviour.

Do not make 52 ScriptableObject card assets.

Do not make gameplay logic inspect card GameObjects to determine card values.

The logical card/deck system should be plain C# and usable later by:

- local gameplay
- bots
- authoritative multiplayer host/server
- automated tests

3D card visuals will be a separate presentation layer later.

---

## 2. Standard playing card model

A standard deck contains exactly 52 cards.

Suits:

- Clubs
- Diamonds
- Hearts
- Spades

Suits have NO gameplay effect in Shields.

Ranks and gameplay values:

Ace = 1
2 = 2
3 = 3
4 = 4
5 = 5
6 = 6
7 = 7
8 = 8
9 = 9
10 = 10
Jack = 11
Queen = 12
King = 13

There are no Jokers.

Create clear enums/types such as:

CardSuit
CardRank
PlayingCard

Names may differ slightly if there is a good reason.

---

## 3. Card identity

Each physical card instance must have a unique identity.

This is important because the game can use TWO decks.

With two decks there can therefore be:

King of Spades from deck 0

and

King of Spades from deck 1

They have the same rank, suit, and gameplay value but must still be two different card instances.

A card should therefore conceptually contain:

- unique card instance ID
- source deck index
- suit
- rank
- gameplay value

Do not rely on GameObject instance IDs for gameplay identity.

---

## 4. One-deck and two-deck support

The logical deck system must support:

deckCount = 1
or
deckCount = 2

One deck:
52 cards.

Two decks:
104 cards.

The second deck is another complete standard 52-card deck.

Do not change rank probabilities or introduce special cards.

Deck count will eventually be selected in the multiplayer lobby, but no lobby implementation is needed now.

---

## 5. Draw pile

Create a plain-C# deck implementation responsible for the draw pile.

It must support:

- creating the required 52 or 104 cards
- shuffling
- drawing the top card
- querying remaining draw count

Drawing must remove that card from the draw pile.

Use a proper shuffle such as Fisher-Yates.

Prefer a design where a deterministic random seed can optionally be supplied for testing.

Do not use deterministic deck generation as a security mechanism; the seed support is only for debugging/tests.

---

## 6. Discard pile

The deck system must also maintain a discard pile.

It must support moving previously drawn cards into discard.

Cards in player health, shield, charge, or other active gameplay locations are NOT considered discarded.

Only cards explicitly sent to the discard pile should exist there.

---

## 7. Deck exhaustion

When a draw is requested and the DrawPile is empty:

- take the current DiscardPile
- move those cards into DrawPile
- shuffle them
- continue the draw

The cards that are currently in active gameplay zones are NOT included.

If both DrawPile and DiscardPile are empty, fail gracefully instead of inventing a card.

Do not implement virtual health markers in this task.
That belongs to the Shields rules layer later.

---

## 8. No Shields mechanics yet

Do NOT implement:

- health
- shield behavior
- attacking
- charging
- turns
- player elimination
- card selection
- health replacement cards
- virtual health markers
- bots

Those will use this card system in later tasks.

---

## 9. Presentation separation

Do not replace the current placeholder table cards yet.

Do not create final card artwork.

If you create any CardView-related type at all, it must contain presentation only and must not own authoritative gameplay state.

Prefer NOT creating CardView in this task unless required for validation.

---

## 10. Project organization

Use a clean location such as:

Assets/Scripts/Cards/

Keep the amount of code small.

Avoid unnecessary:

- interfaces
- service locators
- dependency injection frameworks
- event buses
- generic card-game frameworks

This game only needs a clean standard playing-card model.

---

## 11. Validation

Before implementation, inspect the current project.

After implementation validate:

ONE DECK:

- exactly 52 cards created
- every suit contains 13 ranks
- values are Ace=1 through King=13
- all card instance IDs are unique

TWO DECKS:

- exactly 104 cards created
- every suit/rank combination appears twice
- all 104 card instance IDs are still unique
- cards correctly record which source deck they came from

DRAW:

- drawing decreases DrawPile count
- the drawn card no longer exists in DrawPile

DISCARD:

- discarding increases DiscardPile count
- discarded card identity remains unchanged

RESHUFFLE:

- when DrawPile becomes empty, DiscardPile is shuffled back into DrawPile
- cards currently outside the discard pile are not magically restored

If Unity Test Framework is ALREADY installed in this project, add focused EditMode tests for this logic.

If it is not installed, do NOT install it. Use another minimal validation method and report that automated Unity tests were skipped.

No compile errors.
No Console errors.

Report:

1. Files created
2. Files modified
3. How PlayingCard identity works
4. How one/two deck generation works
5. How shuffle/draw/discard/reshuffle work
6. Validation results

Do not continue into Shields gameplay.

Stop when this task is complete.