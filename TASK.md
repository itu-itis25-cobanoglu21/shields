# TASK 026 — Rematch Voting, Full Match Reset, Random Seating & Clockwise Turns

Read `CLAUDE.md`, `SHIELDS_RULES.md`, and this entire TASK.md before modifying anything.

TASK 025 completed the waiter delivery system.

Before implementing drinking, we need to make repeated matches inside the SAME online session reliable.

This task introduces:

1. multiplayer-safe REMATCH voting
2. a clean full match reset without leaving the session
3. fresh randomized seat assignment for EVERY match
4. offline random seating too
5. clockwise-only turn direction
6. a proper fix for the existing second-online-match snapshot/revision bug

This is a gameplay/session architecture task.

Do not treat it as a simple UI restart button.

---

# 1. Important architecture rule

A PARTICIPANT is not a SEAT.

A participant/client may occupy a different seat in every match.

Never assume:

`clientId == seat`

and do not introduce any new assumptions that:

`participant identity == permanent seat`

The existing explicit participant/client → seat mapping remains authoritative.

For every new match, the host assigns seats again.

---

# 2. Match-scoped vs session-scoped state

REMATCH must keep the existing online SESSION alive.

Keep session-scoped systems such as:

- Multiplayer Services session
- Relay connection
- NGO connection
- NetworkManager
- connected participants

Do NOT:

- disconnect everyone
- destroy/recreate NetworkManager
- leave and rejoin the Session
- create a new room code
- create a new Relay allocation solely for rematch

Instead, completely reset/recreate MATCH-SCOPED state.

---

# 3. Rematch UI

When a match reaches MatchOver, show a small clear post-match rematch control.

Suggested presentation:

`REMATCH?`

button.

After the local human votes yes:

`REMATCH ✓`
`WAITING 2 / 3`

or equivalent.

The exact visual style should fit the current gameplay HUD.

Do not redesign the whole post-match UI.

---

# 4. Who votes

ONLINE:

Every currently connected HUMAN participant in the match/session must vote REMATCH.

Bots do not vote.

Bots are implicitly ready for the next match.

Example:

3 humans + 3 bots

required votes:

`3 / 3 humans`

not:

`6 / 6`

Host also has to vote if the host is a human participant.

---

# 5. Offline rematch

Offline has only one human decision maker.

At MatchOver:

local human presses REMATCH
→ immediately proceed into the same rematch pipeline

Bots are implicitly ready.

Do not create a separate incompatible offline restart implementation.

Reuse the same logical match-reset path where practical.

---

# 6. Vote authority

Rematch voting must be host-authoritative online.

Clients only send intent such as:

`RequestRematchVote(true)`

The host determines:

- sender identity
- sender's participant/seat mapping
- whether match is actually over
- whether that participant may vote
- current connected-human voter set
- whether unanimity has been reached

Clients do not directly start a new match.

---

# 7. Vote behavior

Each connected human gets at most one current vote.

Preferred behavior:

press REMATCH
→ vote becomes YES

Allow the player to cancel their vote before unanimity starts the match if easy and clean.

For example:

`CANCEL REMATCH`

This is optional if existing UI architecture makes it awkward.

Once unanimity is reached and restart begins:

lock the vote state.

Do not allow duplicate votes.

---

# 8. Disconnects while waiting

The required voter set should be based on CURRENT connected human participants.

If a player disconnects while everyone is waiting:

remove that participant from the required voter set.

Then re-evaluate unanimity.

Example:

3 humans
A = yes
B = yes
C disconnects

→ remaining humans are A + B
→ unanimity is satisfied
→ rematch may begin

Do not keep a disconnected participant blocking the lobby forever.

Do not invent automatic reconnection handling in this task.

---

# 9. Join during MatchOver

Do not introduce complicated mid-rematch join behavior.

Inspect current session rules.

If joining a session after the match has already started/ended is not currently supported, preserve that restriction.

Do not redesign matchmaking or late-join.

Report the existing behavior.

---

# 10. Rematch status broadcast

All clients should see authoritative rematch status.

At minimum broadcast enough state to display:

- local voted / not voted
- number of YES votes
- number of currently required human votes
- restart beginning

Do not rely on each client independently counting UI state.

---

# 11. Unanimous restart

When:

`yesVotes == connectedHumanParticipants`

and there is at least one connected human:

host begins ONE rematch transition.

Protect against starting twice because multiple votes/RPCs arrive close together.

Use an authoritative restart-in-progress guard/state.

---

# 12. Full match reset

A rematch means a genuinely NEW match.

Do not reuse previous `ShieldsMatch` gameplay state.

Reset/recreate all match-specific state including:

- authoritative match instance
- deck(s)
- shuffle
- draw pile
- discard pile
- initial cards
- Health values
- Shield cards
- stored Charge cards
- turn state
- current action
- target-selection state
- death state
- winner state
- starting player
- turn counter
- initial Shield selection
- physical/public card presentation
- temporary action/reveal cards

The new match should look and behave like a fresh first game.

---

# 13. Deck/configuration behavior

Keep the SAME match configuration for a rematch unless existing project architecture clearly says otherwise.

For example:

- same number of total participants
- same configured deck count (1 or 2)
- same bot count

Do not reopen the entire host setup screen merely to rematch.

But:

the deck itself must be freshly created and shuffled.

No previous card order may carry over.

---

# 14. Initial setup happens again

The new match must repeat normal SHIELDS starting flow.

That means:

- each participant receives 3 fresh face-up cards
- human chooses exactly 1 as Shield again
- remaining 2 become Health
- bots choose Shield through the existing bot behavior

Do not carry the previous Shield/Health selection into rematch.

---

# 15. Social/presentation reset

A new match must also clear previous-match social/presentation state.

At rematch transition:

- abort/remove an active waiter
- clear pending BeerOrder queue
- clear BeerOrder cooldowns
- remove tray mugs
- remove delivered mugs from all DrinkAreas
- clear Beer Order UI state
- clear rematch UI/votes from the previous match

Do not let beers from Match 1 remain on the table in Match 2.

---

# 16. Other presentation cleanup

Inspect the existing GameScene presentation and clean all match-scoped visuals.

Examples include:

- discard presentation
- card hands
- charge flames
- weapon/action state
- hit/reaction state
- temporary VFX
- selected-target state

Do not blindly destroy environment objects.

The tavern, table model, lighting and static scene remain.

The PLAY STATE around them is rebuilt.

---

# 17. Random seating — applies to every match

Seat assignments must be freshly randomized for:

- first offline match
- offline rematch
- first online match
- online rematch
- later rematches too

Do not special-case only rematches.

The host/authority generates the assignment.

---

# 18. Offline seating

Currently the local offline human often ends up in the same seat.

Fix this.

For an offline match with:

1 human + N bots

randomly assign ALL match participants to the available seats.

The local human may therefore start in any valid seat.

Bots fill the remaining seats randomly.

All local systems must follow the actual local human seat.

Do not assume the human is seat 0.

---

# 19. Online seating

For online matches:

randomly assign all current match participants to the available seats.

This includes:

- host human
- remote humans
- bots

Preserve participant identity and network ownership while changing only seat assignment.

The mapping must be published authoritatively before match presentation starts.

---

# 20. Fresh shuffle every match

Generate a new random seat permutation for every match.

If player count > 1 and the generated COMPLETE assignment is identical to the previous match's complete assignment:

reroll so the whole table is not exactly identical again.

Individual participants are still allowed to coincidentally remain in the same seat.

Do NOT attempt to guarantee that every single player moves.

That would no longer be a normal shuffle.

---

# 21. Seat mapping ordering

Rematch startup must have a safe ordering.

Conceptually:

MatchOver
→ unanimous rematch
→ enter Restarting state
→ teardown old match/presentation
→ generate new seat assignment
→ publish/update authoritative participant → seat mapping
→ clients apply the new mapping
→ rebuild seat/player presentation
→ create/start fresh match
→ initial deal
→ Shield selection
→ Playing

Do not start dealing cards using stale seat mappings.

---

# 22. Local camera after seat shuffle

The local gameplay camera must follow the local participant's NEW seat.

After every seat reassignment:

- correct local PlayerCharacter owns/uses the gameplay camera
- only local gameplay camera is active
- view begins from the new chair/seat
- crosshair and head-look still work
- remote players do not gain a local camera

This applies both offline and online.

---

# 23. Player character rebuilding

Inspect whether current player characters can safely be rebound to new seats or should be rebuilt.

Choose the cleaner existing architecture-compatible solution.

A rematch must NOT leave:

- old characters at previous seats
- duplicate characters
- duplicated chairs
- stale ownership markers
- old bot/human visual identities attached to wrong seats

Report whether you rebuild or rebind them.

---

# 24. Bot identity after shuffle

`MatchPlayerState.IsBot` remains authoritative.

Random seating must not accidentally turn:

- a human seat into bot control
- a bot seat into human local control

Participant type follows the participant, not the old seat.

Do not use presence of `BotController` as authoritative identity.

---

# 25. Clockwise-only turns — rules change

SHIELDS should no longer choose a random turn direction.

From now on:

**every match always proceeds clockwise around the table.**

This applies to:

- offline
- online
- first match
- every rematch
- 3–8 players

The starting player may STILL be chosen randomly from living players at the beginning of each new match.

Only the DIRECTION becomes fixed.

---

# 26. Determine actual clockwise direction correctly

Do NOT assume numeric seat `+1` or `-1` is clockwise without inspecting the real seat layout.

Inspect:

- `TableLayoutController`
- seat-angle generation
- current turn-direction representation
- camera/table orientation

Determine which seat progression is VISUALLY clockwise.

Then make the authoritative match use that direction consistently.

Add tests that verify the actual intended seat order.

---

# 27. Update SHIELDS_RULES.md

This is an intentional game-rule change.

Update `SHIELDS_RULES.md`.

Replace the old rule that chooses a random fixed direction.

The rule should now state the equivalent of:

- choose a random living starting player
- turn direction is always clockwise
- dead players are skipped

Keep the document terminology/style consistent.

Do not leave documentation disagreeing with runtime behavior.

---

# 28. Existing online second-match bug

There is an existing bug discovered during TASK 025:

On a second online match inside the same session:

`NetworkMatchController.StartMatch`

resets host-side snapshot/revision counters, while clients retain previous values such as:

- `_appliedSetupRevision`
- `_appliedMatchRevision`

The clients then treat new-match snapshots as stale and discard them.

This prevented the second online match from reaching Shield selection.

Fix this as part of the rematch architecture.

---

# 29. Robust match generation / revision handling

Do not just patch one counter blindly.

Inspect the existing snapshot architecture.

Preferred robust concept:

introduce or reuse an authoritative per-match generation/epoch identifier.

For example:

`MatchGeneration`

Host increments it for every new match.

Snapshots/results belong to:

`generation + revision`

Client behavior:

- newer generation:
  - reset per-match applied-revision tracking
  - accept the new match's snapshots
- same generation:
  - normal revision ordering
- older generation:
  - reject as stale

This protects the system against delayed messages from a previous match.

If the existing architecture already has an equivalent match-instance concept, reuse it instead of adding another.

If a simpler reset mechanism is genuinely safe because of existing reliable ordering, explain why.

---

# 30. Match generation consistency

The same generation identity should cover all network state whose revisions restart per match.

Audit at least:

- setup snapshot revisions
- match snapshot revisions
- match-start state
- public presentation refresh triggers

Do not fix only ShieldSelection and leave another revision channel broken on Match 3.

---

# 31. Multiple rematches

The system must support:

Match 1
→ rematch
→ Match 2
→ rematch
→ Match 3
→ rematch
→ Match 4

without reconnecting.

Do not implement a one-rematch-only solution.

Counters/epochs, votes and seat assignments must continue working indefinitely within the session.

---

# 32. Random starting player

At each new match:

choose a fresh random living starting player through authoritative match setup.

Seat randomization and starting-player randomization are separate.

Do not always make seat 0 start.

---

# 33. Offline first-match validation

Start offline multiple times.

Use at least:

- 3 players
- 6 players
- 8 players

Confirm:

- local human is not hard-coded to seat 0
- participant seating is generated through the new shuffle
- bots fill remaining seats correctly
- camera follows local human
- clockwise order is correct

Because randomness can repeat by chance, inspect the actual generated seat mappings rather than expecting a different local seat on every launch.

---

# 34. Offline rematch validation

Complete or safely force MatchOver through existing development/test mechanisms.

Press REMATCH.

Confirm:

- no setup/session teardown
- old table play state clears
- Beer state clears
- fresh seating assignment generated
- fresh deck generated/shuffled
- fresh initial deal
- Shield selection repeats
- local human camera moves to their new seat
- bots still work
- clockwise play begins
- random starting player is respected

Do this for at least two consecutive rematches if practical.

---

# 35. Online voting validation

With multiple human participants if practical:

MatchOver
→ Human A votes REMATCH
→ nothing starts yet
→ Human B votes REMATCH
→ if all required humans have voted:
restart begins exactly once

Confirm all clients show the same vote count.

---

# 36. Online rematch validation

This task strongly prefers a REAL second client test.

Validate:

- same Session remains connected
- same Relay/NGO connection remains
- no new NetworkManager
- seat mappings are randomized
- host/client both rebuild correct local/remote players
- both receive new MatchGeneration
- both reach initial deal
- both reach Shield selection
- both enter Playing
- clockwise turns agree
- public snapshots no longer get rejected as stale
- Beer/waiter state from previous match is gone

---

# 37. Three-match online validation

If tooling allows:

Play Match 1
→ rematch Match 2
→ rematch Match 3

At minimum force/end matches through a safe developer path if playing them fully is impractical.

Confirm no accumulated:

- revision issue
- duplicate players
- stale seats
- stale cards
- stale waiter
- stale mugs
- stale rematch votes

---

# 38. If real second client cannot be run

If the environment cannot launch a genuine second client:

do NOT claim remote multiplayer validation.

Still:

- wire the complete real network path
- unit-test participant IDs that differ from seats
- run host/local-loopback
- test at least multiple match generations
- explicitly report that a real remote second client remains manual validation

---

# 39. Leave / disconnect behavior

If someone leaves during MatchOver voting:

update the required voter set.

If host/session shutdown occurs:

cancel restart cleanly.

If a participant disconnects DURING restart:

do not crash or create duplicate/stale seats.

Use the project's existing disconnect/session policy rather than inventing major reconnection behavior.

Report the resulting behavior.

---

# 40. Rematch during waiter/order activity

Normally MatchOver can occur while social presentation is active.

At restart:

- active waiter presentation aborts
- pending waiter delivery state cancels
- host Beer queue clears
- clients remove waiter
- all mugs clear

Do not allow an old MatchGeneration waiter callback to affect the new match.

---

# 41. Input/UI reset

At rematch:

- Beer Order panel must close
- rematch panel disappears when restart begins
- cursor state returns to normal gameplay/setup state
- head-look suppression clears
- target-selection UI clears
- old winner/end UI clears

No stuck cursor.

No duplicate UI.

---

# 42. No scene reload dependency

Prefer rebuilding match-scoped objects/state inside the existing session/GameScene.

Do not rely on disconnect/reconnect.

Avoid reloading GameScene unless the existing networking architecture already has a safe synchronized scene-reload pattern and it is clearly superior.

The required behavior is seamless SAME-SESSION rematch.

---

# 43. Tests — seating

Add pure tests where practical for:

- random participant → seat permutation contains each seat exactly once
- no participant duplicated
- no seat duplicated
- human/bot identity preserved
- initial and rematch use the same seating algorithm
- exact complete previous permutation is rerolled when player count > 1
- sender/client ID is not assumed equal to seat

Keep randomness injectable/deterministic for tests if appropriate.

---

# 44. Tests — rematch votes

Add tests for:

- no restart before unanimity
- all humans yes → restart once
- bots don't count as voters
- duplicate yes vote doesn't increment count
- disconnected voter is removed from requirement
- votes reset after new match begins
- Match 2 can later collect a fresh set of votes

---

# 45. Tests — revisions/generation

Add tests for:

- MatchGeneration 1 accepts revisions normally
- Generation 2 resets generation-scoped revision tracking
- low revision in a NEW generation is accepted
- stale messages from Generation 1 are rejected after Generation 2 begins
- Match 3 works too

Do not only test one transition.

---

# 46. Tests — clockwise

Replace/update tests that depended on random direction.

Verify:

- 3 players clockwise
- 6 players clockwise
- 8 players clockwise
- dead players are skipped while maintaining clockwise direction
- random starting seat still works independently

---

# 47. Regression

Preserve:

- Attack
- Replace Shield
- Charge
- charge privacy
- death
- bots
- discard
- targeting
- player animation
- Beer ordering
- waiter delivery

Do not weaken existing tests to make the new rule pass.

---

# 48. Full EditMode suite

Run the full EditMode test suite.

Previous baseline:

`291 / 291`

All previous tests plus new tests should pass.

Update intentional tests only where the game rule legitimately changed from random direction to clockwise.

Explain those changes.

---

# 49. Console

Final validation should have:

- 0 project-code errors
- 0 missing references
- 0 duplicate NetworkManager
- 0 stale-revision spam
- 0 duplicate-seat warnings
- 0 orphan-player/waiter errors

Report unrelated third-party warnings separately.

---

# 50. Final report

Report:

1. Rematch architecture
2. Rematch UI behavior
3. Vote RPC/state flow
4. Who counts as a voter
5. Disconnect handling during voting
6. Unanimity/start-once guard
7. Match-scoped teardown path
8. Session-scoped objects preserved
9. New-match startup sequence
10. Seat randomization algorithm
11. Offline local-human seat behavior
12. Online participant → seat remapping
13. Whether complete identical seating is rerolled
14. Local-camera reassignment
15. Player prefab rebuild/rebind behavior
16. Bot identity preservation
17. Clockwise direction implementation
18. Actual seat progression that represents clockwise
19. Random starting-player behavior
20. SHIELDS_RULES.md change
21. Root cause of old second-match revision bug
22. Exact revision/generation fix
23. Handling of stale prior-match messages
24. Beer/waiter/mug cleanup
25. Initial offline random-seat validation
26. Offline rematch validation
27. Consecutive offline rematch validation
28. Online vote validation
29. Online Match 2 validation
30. Online Match 3 validation if performed
31. Real second-client test status
32. Disconnect-during-rematch result
33. Seat tests
34. Vote tests
35. generation/revision tests
36. clockwise tests
37. final full EditMode count
38. Console result
39. exact files created/modified
40. any remaining manual validation required

Do not commit anything.

Stop when REMATCH + RANDOM SEATING + CLOCKWISE TURN FLOW is complete.

Do not begin drinking animation.