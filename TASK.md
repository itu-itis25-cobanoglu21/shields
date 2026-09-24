# TASK 009 — Full Host-Authoritative Online Shields Gameplay

Read CLAUDE.md and SHIELDS_RULES.md completely before editing.

TASK 008 implemented the multiplayer connection foundation:

- Unity Multiplayer Services Sessions
- Relay
- Netcode for GameObjects
- explicit network participant -> seat mapping
- per-client LocalPlayerIndex
- deterministic shared table/player layout
- synchronized head-look
- prototype Host / Join flow

TASK 008 has passed its automated tests but its final real two-participant runtime validation may still be performed manually by the project owner while this task is being implemented.

Preserve the TASK 008 connection architecture.

If a genuine TASK 008 runtime networking defect is discovered while implementing this task, fix it narrowly and report it.

Do not broadly rewrite the session layer.

The objective of TASK 009 is:

A complete host-authoritative online Shields match playable by multiple human clients plus bots.

---

# 1. Core authority model

The HOST is the only authority for Shields gameplay.

Only the host may own and execute:

ShieldsMatch

Clients must NOT maintain their own authoritative ShieldsMatch simulation.

Clients must NOT independently:

- shuffle
- draw cards
- determine Attack cards
- calculate damage
- replace Shields
- calculate Charge attacks
- redistribute Health
- eliminate players
- advance turns
- determine the winner

Clients only submit player INTENT.

Conceptually:

CLIENT
    |
    | TurnActionRequest
    v
HOST
    |
    | ShieldsMatch.Submit(...)
    v
Authoritative Result
    |
    v
ALL CLIENTS

Bots use the same host-side ShieldsMatch.

---

# 2. Keep pure assemblies pure

Do NOT add Unity networking dependencies to:

- Shields.Cards
- Shields.Game
- Shields.Bots
- Shields.Session

Do not make ShieldsMatch a NetworkBehaviour.

Do not put RPC attributes in the pure gameplay engine.

Create/adapt a Unity networking integration layer around the existing engine.

---

# 3. Session configuration

The host controls pre-match configuration:

- Target Player Count: 3–8
- Deck Count: 1 or 2

All connected session members must receive/display the same configuration.

Use the existing Session/host architecture appropriately.

Session properties may be used for low-frequency pre-match metadata if useful.

Do NOT use cloud/session properties for real-time turn gameplay.

Real-time gameplay synchronization belongs to NGO.

---

# 4. Human seat allocation

Continue using the existing authoritative SeatRegistry / NetworkSeatRegistry from TASK 008.

Each connected human has exactly one explicit seat.

Do NOT derive seat from:

- Player 0 assumptions
- GameObject names
- BotController
- presentation hierarchy

Every client resolves their own LocalPlayerIndex from the authoritative seat mapping.

---

# 5. Filling empty match seats with bots

When the host starts the match:

TargetPlayerCount determines the total number of gameplay players.

Connected human seats remain human.

Every unoccupied seat required to reach TargetPlayerCount becomes a bot.

Example:

TargetPlayerCount = 6

Connected humans:
Seat 0
Seat 1
Seat 2

Result:

Seat 0 = Human
Seat 1 = Human
Seat 2 = Human
Seat 3 = Bot
Seat 4 = Bot
Seat 5 = Bot

Bots exist only as HOST-side decision makers.

Remote clients must not run RandomBotPolicy.

BotController MonoBehaviour remains non-authoritative presentation scaffolding.

Authoritative bot/human identity comes from match/network state.

---

# 6. Starting the online match

Only the host may start the online match.

Before starting validate:

- target player count valid
- deck count valid
- all connected humans have valid unique seats
- there are not more humans than target player count
- network connection is active

When START MATCH is accepted:

- prevent new participants from joining if supported by current session API
- finalize gameplay seat occupancy
- mark empty required seats as bots
- create ONE authoritative MatchSetup on the host
- create ONE CardDeck on the host
- deal only on the host

Clients must NOT create their own CardDeck for online gameplay.

---

# 7. Networked initial deal

The host performs the initial deal.

Every gameplay seat receives three cards from the host's authoritative deck.

All initial dealt cards are PUBLIC according to SHIELDS_RULES.md.

Replicate enough public setup state so every client can render:

- player index
- each of the three dealt PlayingCards
- card identity
- rank
- suit
- value where needed for presentation
- whether that seat is human or bot
- whether initial selection is complete

Do not replicate a second logical CardDeck to clients.

Clients only need presentation/public state.

---

# 8. Initial Shield selection — human

Every connected human chooses their own Shield using the existing:

head-look
+
crosshair
+
left click

interaction.

The client's click must NOT directly mutate MatchSetup.

Instead:

CLIENT
    ->
InitialShieldChoiceRequest(card identity)
    ->
HOST

The host validates:

- sender corresponds to that seat
- setup phase is active
- card belongs to that player's original 3-card deal
- selection was not already finalized

Then host performs the authoritative selection.

A client must never be able to choose a Shield for another human seat.

Rejected requests must not mutate setup state.

---

# 9. Initial Shield selection — bots

Bot initial Shield choice is performed only by the host.

Use the existing confirmed random bot behavior:

- choose one of the three dealt cards randomly as Shield
- remaining two become Health

Remote clients do not run this random selection.

---

# 10. Initial setup synchronization

After every accepted setup choice:

replicate updated public setup state.

All clients should see:

- correct two Health cards
- correct Shield
- numeric starting Health
- selection-complete state

When every seat has completed selection:

the HOST transitions the existing authoritative MatchSetup into ONE ShieldsMatch.

Clients do NOT create their own ShieldsMatch.

---

# 11. Public network gameplay state

Create an explicit network-safe PUBLIC representation of the authoritative match.

Do NOT serialize ShieldsMatch wholesale.

Do NOT expose internal fields by reflection or serializer convenience.

Create narrowly scoped DTO/snapshot structures containing only public information required for presentation.

A public player state conceptually needs:

- PlayerIndex
- IsAlive
- IsBot
- CurrentHealth
- Health slot presentation:
  - physical PlayingCard if public
  - Virtual Health value if virtual
- current Shield card
- HasCharge
- NO unresolved Charge card identity

Public match state conceptually needs:

- CurrentPlayerIndex
- TurnDirection
- MatchOver
- Winner
- DrawPile remaining count if desired for existing deck UI
- public player states
- monotonically increasing state/revision number

Exact names/serialization form are your choice.

---

# 12. CRITICAL — hidden Charge privacy

An unresolved stored Charge is the only hidden gameplay card.

It is unknown to EVERY gameplay participant, including the player who owns it.

The host's authoritative ShieldsMatch internally knows its PlayingCard.

NO client may receive that card's:

- CardId
- DeckIndex
- Suit
- Rank
- Value

before resolution.

Not even the Charge-owning client.

Network state may only contain:

HasCharge = true

for an unresolved Charge.

Do NOT:

- include ChargeCard in public snapshot structs
- include it in NetworkVariables
- include it in NetworkLists
- send it in RPC payloads
- send it in debug replication
- bind it to a hidden client CardView
- log its value in client-visible debug messages

When the Charged Attack resolves:

the two Charge cards become public through the resolved action result.

Add tests specifically protecting this property.

---

# 13. Network action request format

Create a small network-safe action request.

Client sends only intent such as:

- actor is derived from sender ownership, NOT trusted from payload
- action type
- target player index when required

The client must NOT send:

- drawn card
- AttackPower
- damage
- Shield result
- Charge value
- Health result
- next player
- winner

The HOST derives actor PlayerIndex from the authenticated NGO sender/client -> seat mapping.

Never trust a client-provided actor index.

---

# 14. Human Attack online

Client local human:

press 1
->
Attack targeting mode
->
crosshair target
->
left click

Then send request to host:

Action = Attack
Target = X

Host validates through both:

network ownership/seat mapping
and
ShieldsMatch.Submit(...)

Only after host accepts does the authoritative engine draw the card.

Host then sends:

- public TurnActionResult
- updated public state

to all clients.

---

# 15. Replace Shield online

Same pattern:

Client chooses action and target.

Valid presentation targets remain:

- own Shield for self
- living players / Shields for others

Request sent to host.

Host derives actor from sender.

Host calls ShieldsMatch.

Host publishes result/state.

The client never pre-draws or predicts the replacement card.

---

# 16. Charge online — first Charge

Client presses 3 with no stored Charge.

Send only:

Action = Charge

No target.

Host executes ShieldsMatch.

The result sent to clients may reveal only:

Player X CHARGED
HasCharge = true

The result must NOT contain the hidden card identity.

All clients render one generic face-down Charge card.

---

# 17. Charge online — completion

If public state says local human HasCharge:

pressing 3 enters Charged Attack targeting.

Client selects another living player.

Client sends:

Action = Charge
Target = X

Host resolves second Charge.

Now BOTH Charge cards become public because the attack resolved.

The public action result may contain the revealed cards and calculated result.

After resolution:

HasCharge becomes false.

---

# 18. Host-side bots

Random bots act ONLY on the host.

When authoritative CurrentPlayerIndex is a bot:

use the existing RandomBotPolicy.

Submit the chosen TurnActionRequest directly to the host-owned ShieldsMatch.

Preserve existing bot pacing in presentation.

Do NOT network bot decision logic.

Do NOT have every client make the same random bot choice.

The host result/state is replicated just like a human action result.

---

# 19. Client action permissions

A client may submit a gameplay action only when:

- online match has started
- match is not over
- its mapped seat is alive
- authoritative public state says it is that player's turn

This client-side condition is UX only.

The HOST must still independently validate every request.

Malicious or stale requests must be rejected.

---

# 20. Sender validation

For every gameplay RPC from a client:

derive sender ClientId using current NGO RpcParams / current installed API.

Map:

SenderClientId
->
authoritative SeatRegistry
->
PlayerIndex

Do not accept an actor index from the caller as truth.

Reject if:

- sender has no seat
- sender's seat is bot
- sender does not correspond to current player
- match not active
- match over
- target invalid

Do not disconnect the client merely for an ordinary stale request.

---

# 21. State revision / ordering

Network messages may arrive over time.

Add a simple monotonically increasing authoritative revision/version to public gameplay state.

Clients must not apply an older state snapshot over a newer one.

Do not build a complex rollback/prediction system.

This game is turn-based and does not need prediction.

Host-confirmed state is sufficient.

---

# 22. Presentation synchronization

Reuse the existing presentation architecture where possible.

Offline currently follows:

ShieldsMatch
->
TurnActionResult
->
MatchPresenter

Online clients should consume public replicated state/result and drive the SAME visual concepts.

Do not duplicate the entire presentation stack.

Create an adapter/integration path so:

OFFLINE:
local ShieldsMatch state -> presentation

ONLINE:
network public state -> presentation

Avoid making CardView network-aware.

CardView remains presentation only.

---

# 23. Online Health presentation

Replicate enough public state for every client to show:

- physical Health cards
- one/two slots
- Virtual Health markers
- numeric Health

FIFO replacement remains HOST authority.

Clients do not perform:

- discard lookup
- DrawPile lookup
- virtual waiting queues

They simply render the resulting public state.

---

# 24. Online Shield presentation

Shield is always public.

Every client displays the host-authoritative current Shield PlayingCard for each living player.

Clients do not independently mutate Shield CardViews from their own action assumptions.

Wait for host-confirmed state/result.

---

# 25. Online deck presentation

Clients may receive:

RemainingDrawCount

for the prototype deck counter.

Do not replicate actual DrawPile contents/order.

Do not serialize the remaining deck.

Only the host knows the authoritative DrawPile.

---

# 26. Public action results

Define a network-safe public action result based on existing TurnActionResult.

It should support the existing presentation feedback:

Attack:
- actor
- target
- revealed attack card
- Shield value
- damage
- resulting Health
- blocked/success state
- Charge interrupted
- death if any

ReplaceShield:
- actor
- target
- old public Shield
- new public Shield

First Charge:
- actor
- NO hidden card information

Charged Attack:
- actor
- target
- both now-revealed Charge cards
- combined AttackPower
- Shield
- damage
- resulting Health
- interrupted Charge
- death

Match end:
- winner

Do not expose information beyond what SHIELDS_RULES.md permits.

---

# 27. Action feedback / pacing

Preserve the current readable gameplay pacing:

- existing botThinkDelay
- existing postActionDelay
- large screen-space action panel
- world-space labels

Online actions must produce the same presentation feedback as offline actions.

Clients should understand:

who acted
what action happened
target
drawn/revealed public cards
damage/result

Do not reveal unresolved Charge.

---

# 28. Turn input

Preserve current local controls:

1 = Attack
2 = Replace Shield
3 = Charge

Mouse remains first-person head look.

Crosshair remains centered.

Left click confirms world targets.

No free cursor during world gameplay.

Input must use each client's actual LocalPlayerIndex.

Never assume seat 0.

---

# 29. Camera and head synchronization

Preserve TASK 008:

- one active gameplay camera per machine
- camera mounted at that machine's assigned seat
- local head look
- remote synchronized head pose

Do not let gameplay snapshot synchronization overwrite HeadPivot networking.

These are separate concerns.

---

# 30. Session start locking

When the host commits START MATCH:

if supported by current Multiplayer Services Sessions API:

prevent new players from joining that active match.

Use current supported session lifecycle/locking API.

Do not invent legacy Lobby calls.

If session locking semantics differ in the installed MPS 2.3.3 API, use the current API and report how it works.

---

# 31. Online match phases

Represent online flow clearly.

Conceptually:

SessionLobby
->
InitialDeal
->
WaitingForShieldSelections
->
Playing
->
MatchOver

Clients must know which phase is active.

Do not use UI object existence as phase state.

Host owns phase transitions.

---

# 32. Prototype lobby state

Extend the existing network prototype UI only as needed.

Before host starts:

show:

- Join Code
- connected humans
- seat assignments
- Target Player Count
- Deck Count
- START MATCH host-only button

Client shows:

- join code/session status
- assigned seat
- Waiting for host

Do NOT build final lobby art.

---

# 33. Human readiness for start

For this prototype, explicit Ready buttons are NOT required.

Host may start when desired provided:

- at least one host/human exists
- connected humans do not exceed TargetPlayerCount
- all current participants have valid seats

Unfilled seats become bots.

Do not invent a ready-state system.

---

# 34. Mid-match joining

Do NOT support joining an already active match in TASK 009.

Once match starts, reject/lock late joins.

No spectator mode.

No replacing bots with late joiners.

---

# 35. Disconnects during active match

Do not implement reconnect or host migration.

HOST disconnect:

- session/match ends for clients
- clients return safely to network setup
- show understandable message

Non-host client disconnect during an active game:

For TASK 009, do NOT invent a new gameplay rule such as automatic bot takeover.

Instead:

- stop/pause/end the online match safely
- surface a clear "Player disconnected" state
- allow returning to networking setup

Do not leave ShieldsMatch softlocked waiting for a vanished human.

Bot takeover can be a later explicit product decision.

---

# 36. Offline mode regression

OFFLINE mode must continue working exactly as before:

one local human
+
bots
+
local ShieldsMatch

Do not route offline gameplay through Relay/NGO unnecessarily.

The existing 1/2/3 controls and complete local match must remain playable.

---

# 37. Network serialization

Use explicit compact serializable DTOs.

Do not serialize:

- arbitrary C# object graphs
- ShieldsMatch
- CardDeck
- MatchSetup internals

PlayingCard public DTO should contain only what is required to faithfully identify/render a PUBLIC physical card.

Be careful with:

- readonly structs
- nullable values
- enums
- lists

Use current NGO-supported serialization mechanisms for package 2.13.3.

Inspect the installed package/API rather than assuming an old tutorial.

---

# 38. Reliable vs unreliable messages

Gameplay actions/results/state are turn-based and correctness matters.

Use reliable delivery where appropriate for:

- action requests
- authoritative action results
- important state snapshots
- setup selections

Continue using the existing efficient/unreliable mechanism for frequent head-look pose updates.

Do not optimize prematurely beyond this.

---

# 39. Host-client duplicate execution

Host mode is both server and client.

Be especially careful that the host does NOT:

- apply an action twice
- show duplicate action feedback
- apply a snapshot twice incorrectly
- schedule a host bot twice

A host human action should result in exactly ONE ShieldsMatch.Submit.

Add regression coverage for host-mode paths where possible.

---

# 40. Match restart / cleanup

At the end of an online match:

stop gameplay input and bots.

Do not automatically begin a second match.

Ensure authoritative gameplay objects/state can be cleaned up.

For TASK 009, returning to the session/lobby prototype after match end may be simple.

Do not implement full rematch voting yet.

---

# 41. Automated pure tests — network DTO privacy

Add tests ensuring public network state cannot contain hidden Charge card identity.

At minimum:

- public player snapshot with unresolved Charge exposes HasCharge
- no Charge PlayingCard field is populated/exposed
- first-Charge public action result contains no card face/id/value
- Charged Attack resolved result DOES expose its two now-public cards

This privacy test is critical.

---

# 42. Automated tests — request authority

Add tests around extractable request-routing logic.

At minimum:

- sender seat is derived from mapping
- client cannot act as another seat
- client cannot submit for a bot seat
- out-of-turn sender rejected
- invalid target does not mutate state
- valid human action reaches ShieldsMatch exactly once
- host-local human also resolves exactly once

Prefer pure adapters/routers for testability rather than requiring Relay for all tests.

---

# 43. Automated tests — setup authority

Test:

- host deals once
- clients do not create decks
- human Shield choice can affect only sender's seat
- foreign card identity rejected
- repeat initial selection rejected
- bots select only on host
- match starts only after all setup selections complete

---

# 44. Card accounting

Preserve the existing physical-card identity guarantees.

In representative online-host simulations:

total physical cards remain:

52 for one deck
104 for two decks

No card identity may exist in two authoritative locations.

Network snapshots may contain copies of card DATA for presentation; those are not authoritative PlayingCard ownership.

---

# 45. Existing test suite

The current baseline after TASK 008 is:

198 EditMode tests passing.

All must continue to pass.

Do NOT weaken old tests.

Add new tests for TASK 009.

---

# 46. Runtime test strategy

TASK 008 real runtime validation may be performed manually by the project owner while this task is being implemented.

Do not falsely report TASK 008 runtime validation as complete if it has not actually been run.

Before calling TASK 009 complete, perform a genuine two-participant network gameplay test using:

- Multiplayer Play Mode
OR
- Editor + development build

Do NOT simulate both players inside one authoritative process and call that network validation.

---

# 47. Required online runtime scenario

With two actual independently networked participants:

HOST
+
CLIENT

configure:

TargetPlayerCount = 4
DeckCount = 1

Expected final occupancy:

2 human
2 bot

Validate:

1. Both connect.
2. Both receive different LocalPlayerIndex values.
3. Host starts match.
4. Both clients see identical 3-card deals for every seat.
5. Host and client each choose their own Shield.
6. Client cannot select host's Shield for setup.
7. Bots complete setup on host.
8. Both enter Playing phase.
9. Host human can take a turn.
10. Client human can take a turn.
11. Bots act only on host.
12. Attack result is identical on both screens.
13. ReplaceShield result is identical.
14. First Charge shows one face-down card on both.
15. No client can inspect its value through public replicated state.
16. Charged Attack reveals both cards on both.
17. Health/Shield/Virtual-marker state stays equal.
18. Current turn stays equal.
19. elimination stays equal.
20. winner stays equal.
21. no double-submit on host.
22. no rejected legitimate human actions.
23. head sync continues working.
24. exactly one camera per participant.
25. no project-code Console errors.

---

# 48. Additional runtime scenario

Also validate at least:

3 total players / 1 deck
with:
2 humans + 1 bot

and:

8 total players / 2 decks
with:
2 humans + 6 bots

You do not need to manually finish both long matches if the core replicated actions/state are already proven.

But validate:

- correct layout
- bot/human occupancy
- initial deal
- Shield selection
- several turns
- Charge privacy
- state agreement

---

# 49. Network consistency diagnostics

During development, add development-only diagnostics if useful:

- authoritative state revision
- local applied revision
- current turn
- phase
- local seat

Do not log hidden Charge identities on clients.

Avoid noisy permanent logs.

Development diagnostics must be easy to remove/disable later.

---

# 50. Failure behavior

If a client action request fails host validation:

do not corrupt state.

Return/show a useful rejection reason for debugging where safe.

Examples:

- Not your turn
- Invalid target
- Match not active

Do not expose secret state through rejection text.

---

# 51. Do not implement yet

Do NOT add:

- voice chat
- final character models
- final room art
- Steam networking
- Steam invites
- matchmaking browser
- host migration
- reconnect
- spectators
- mid-match joining
- advanced bot AI
- final lobby UI
- rematch voting
- anti-cheat beyond server authority/validation
- prediction/rollback

---

# 52. Preserve TASK 008 boundaries

Do not rewrite:

- ShieldsSessionController
- SeatRegistry
- head sync
- Relay/session creation

unless a genuine defect requires it.

If TASK 008 manual testing reveals a connection-level bug while this task is running:

fix the lowest layer first.

Do not work around a broken connection by duplicating networking code.

---

# 53. Final report

When complete report:

1. Files created
2. Files modified
3. Online match phase architecture
4. How host creates MatchSetup/ShieldsMatch
5. How connected humans become seats
6. How empty seats become bots
7. Initial deal replication
8. Human Shield-selection request validation
9. How client TurnActionRequest reaches host
10. How sender identity is validated
11. Public match snapshot design
12. Public action-result design
13. Hidden Charge privacy implementation
14. Host-side bot execution
15. Presentation reuse between offline/online
16. Disconnect behavior
17. Automated test count/results
18. Real two-participant runtime results
19. Offline regression result
20. Any TASK 008 runtime defects discovered/fixed
21. Any unresolved blocker

Do not proceed into art, voice, Steam, or final lobby polish.

If a gameplay rule ambiguity appears:

STOP and ask.

If a networking API assumption differs from the installed packages:

use the CURRENT supported API and report the difference.

Stop when TASK 009 is complete.