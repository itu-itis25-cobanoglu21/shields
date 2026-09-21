# SHIELDS — Authoritative Game Rules

This document contains the confirmed rules of Shields.

Do not invent, infer, or modify rules that are not written here.
If implementation requires an unspecified gameplay rule, stop and ask.

---

# 1. Players

Shields supports:

- Minimum: 3 players
- Maximum: 8 players

The game continues until only one player remains alive.

Eliminated players are skipped in turn order.

---

# 2. Cards

The game uses standard 52-card playing card decks.

There are no Jokers.

Ranks and gameplay values:

- Ace = 1
- 2 = 2
- 3 = 3
- 4 = 4
- 5 = 5
- 6 = 6
- 7 = 7
- 8 = 8
- 9 = 9
- 10 = 10
- Jack = 11
- Queen = 12
- King = 13

Suits have no gameplay effect.

The match can use either:

- 1 deck
- 2 decks

Deck count is selected before the match.

With two decks, duplicate card faces are expected.

---

# 3. Public and hidden information

Normal player information is public.

Players may see:

- all Health cards
- all Shield cards
- all initial dealt cards
- all resolved attack cards
- all resolved Shield replacement cards
- current Health values
- all other normal match state

The only hidden cards are unresolved Charge cards.

A face-down Charge card is unknown to everyone, including the player who owns it.

---

# 4. Initial deal

At the beginning of the match every player receives exactly 3 random cards.

Each player chooses:

- 2 cards as Health
- 1 card as Shield

Since exactly one card becomes Shield, selecting the Shield card implicitly makes the other two cards Health.

All three cards are visible during this choice.

For bots:

- choose one of the three cards randomly as Shield
- use the other two cards as Health

---

# 5. Initial Health

Initial Health equals the sum of the values of the two Health cards.

Example:

King + Jack

13 + 11 = 24 Health

The two cards remain physically visible in front of the player.

The authoritative Health value is numeric game state.

The physical Health cards are a representation of that numeric value.

---

# 6. Shield

A player's Shield is represented by one visible card.

Its rank value is the Shield strength.

Example:

Shield card = 5

Shield strength = 5

A Shield is persistent.

Being attacked does NOT consume or weaken the Shield.

The Shield continues working at full value on every attack until a Shield replacement action changes it.

---

# 7. Starting player and turn direction

At match start:

- starting player is selected randomly
- turn direction is selected randomly

Direction is either:

- clockwise
- counter-clockwise

The direction remains active while turns proceed.

Eliminated players are skipped.

---

# 8. Turn action commitment

At the beginning of a player's turn, the player must choose their action BEFORE seeing the card that will be drawn for that action.

The available actions are:

1. Attack
2. Replace Shield
3. Charge

Players cannot draw a card, inspect its value, and then decide what action to perform.

Any target required by the action must be selected before the relevant card is revealed.

---

# 9. Normal Attack

The active player chooses:

- Attack
- target player

Then one card is drawn from the DrawPile and revealed.

Attack Power = drawn card value.

The attack succeeds only when:

Attack Power > Target Shield

If:

Attack Power <= Target Shield

the attack fails and deals zero damage.

If successful:

Damage = Attack Power - Target Shield

The target loses that amount of Health.

Example:

Target Health = 14
Target Shield = 5
Attack card = Queen = 12

12 > 5

Damage = 12 - 5 = 7

New Health = 7

The attack card goes to the DiscardPile after resolution.

The Shield remains unchanged.

---

# 10. Shield replacement

The active player may replace:

- their own Shield
- another living player's Shield

The target is selected before drawing.

Then one card is drawn and revealed.

The newly drawn card MUST replace the target's existing Shield.

This replacement is mandatory regardless of whether the new Shield is stronger or weaker.

Example:

Old Shield = 10
New card = 2

New Shield becomes 2.

The old Shield card goes to the DiscardPile.

The newly drawn card becomes the new Shield card.

---

# 11. Charge — first Charge action

If a player has no stored Charge card and selects Charge:

- draw one card
- do NOT reveal it
- place it face-down in that player's Charge area

The card remains unknown to every player.

The Charge card stays there across turns until:

- the player completes a Charged Attack
- or the player takes successful damage
- or the player dies

---

# 12. Turns while already Charged

Having one stored Charge card does NOT force the player to complete the Charge on their next turn.

On later turns they may still choose:

- normal Attack
- Shield replacement
- Charge

If they choose normal Attack or Shield replacement:

the stored Charge card remains face-down and stays in front of them.

---

# 13. Completing a Charge

If a player already has one stored Charge card and chooses Charge again:

- they select a target before seeing any Charge card values
- a second card is drawn face-down
- both Charge cards are then revealed
- the two values are added together
- the combined value is used as one attack

Charged Attack Power:

ChargeCard1.Value + ChargeCard2.Value

Damage:

Charged Attack Power - Target Shield

The attack succeeds only when:

Charged Attack Power > Target Shield

If equal to or lower than the Shield, it fails.

Example:

Charge card 1 = 8
Charge card 2 = Queen = 12
Target Shield = 5

Attack Power = 8 + 12 = 20

Damage = 20 - 5 = 15

After the Charged Attack resolves, both Charge cards go to the DiscardPile whether the attack succeeded or failed.

The player no longer has an active Charge.

---

# 14. Charge interruption

If a player currently has a stored face-down Charge card and receives a successful attack that deals damage:

their stored Charge is destroyed.

The stored Charge card goes to the DiscardPile.

This applies to successful damage from attacks.

An attack that fails against the Shield does NOT destroy the Charge.

---

# 15. Taking damage and Health representation

Current Health is authoritative numeric state.

Health cards visually represent that value.

Initially a player normally has two Health cards.

When damage changes Health, the visible Health cards are adjusted.

Example:

Health cards:

King + Jack

13 + 11 = 24

Player receives 8 damage.

New Health:

24 - 8 = 16

The higher Health card, King, is discarded.

Jack remains.

A card with value 5 is needed:

Jack 11 + 5 = 16

The game attempts to obtain a physical 5 card.

---

# 16. Finding a Health representation card

When a specific value is required to visually represent remaining Health:

First:

search the DiscardPile for a card with that value.

If one exists:

remove it from the DiscardPile and use it as the Health card.

If none exists:

search the DrawPile for a card with that value.

If one exists:

remove that specific card from the DrawPile and use it as the Health card.

Removing a specific card from the DrawPile does NOT reorder or reshuffle the remaining DrawPile.

If the required value exists in neither place:

create a temporary Virtual Health Marker displaying the required numeric value.

The Virtual Health Marker is NOT a real PlayingCard.

---

# 17. Virtual Health Marker replacement

Players waiting for a specific physical Health card are queued FIFO for that card value.

Example:

Player A needs a physical 5.
Later Player B also needs a physical 5.

Player A is first in the queue.

When a real card with value 5 later becomes available in the DiscardPile:

the earliest waiting Virtual Health Marker for value 5 receives it.

The Virtual Health Marker is replaced by the real card.

---

# 18. Health dropping below the remaining card

If the player's new Health can no longer be represented while keeping the lower existing Health card:

discard both existing Health cards.

Then represent the remaining Health using a single card or Virtual Health Marker.

Example:

Health cards:

King + Jack = 24

Player's Health falls to 9.

Jack alone is already greater than 9.

Therefore:

- King is discarded
- Jack is discarded
- a 9 is obtained using the normal Health-card lookup rules
- if no physical 9 exists, use a Virtual Health Marker showing 9

---

# 19. Player death

A player is eliminated when their Health reaches zero or below.

When a player dies, all cards currently belonging to that player go to the DiscardPile, including:

- Health cards
- Shield card
- stored Charge card, if any

The player is removed from active turn rotation.

---

# 20. Discard and deck exhaustion

Resolved cards go to the DiscardPile unless another rule explicitly places them somewhere else.

When a normal draw is requested and the DrawPile is empty:

- move the DiscardPile into the DrawPile
- shuffle it
- continue drawing

Cards currently being used as:

- Health
- Shield
- Charge
- other active gameplay state

are NOT part of the reshuffle.

Virtual Health Markers are not cards and are never shuffled.

---

# 21. Win condition

The game ends when exactly one living player remains.

That player is the winner.

---

# 22. Currently unspecified edge cases

Do not invent rules for these.

They must be resolved before implementing the relevant mechanic.

### Equal Health cards

If both currently displayed Health cards have the same value and one must be discarded during Health redistribution, the exact physical-card tie-breaking rule has not yet been specified.

Gameplay Health math is unaffected, but physical card identity may matter.

Ask before implementing this edge case if necessary.