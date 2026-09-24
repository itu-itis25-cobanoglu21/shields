# TASK 008 — Multiplayer Connection Vertical Slice

Read CLAUDE.md and SHIELDS_RULES.md completely before making changes.

The local/offline Shields game is complete enough for multiplayer integration.

The existing architecture already separates:

Human/Bot decision
    ->
TurnActionRequest
    ->
ShieldsMatch
    ->
TurnActionResult
    ->
Unity Presentation

Do NOT rewrite the Shields rules engine.

This task is NOT yet about networking the full Shields match.

The goal of TASK 008 is to prove the multiplayer connection and player-ownership foundation before authoritative gameplay synchronization is added.

At the end of this task:

- one machine/editor instance can HOST
- another instance can JOIN using a join code
- both are connected through Unity's current multiplayer stack
- both receive an explicit player/seat assignment
- both see the same seated player layout
- each human controls only their own first-person camera/head
- each client sees the other human player's head/look direction update
- offline mode remains functional

Do NOT network the Shields gameplay engine yet.

---

# 1. Official/current Unity multiplayer stack

Use the CURRENT Unity 6 multiplayer stack.

Preferred packages:

- Multiplayer Services SDK:
  com.unity.services.multiplayer

- Netcode for GameObjects:
  com.unity.netcode.gameobjects

- Multiplayer Play Mode:
  com.unity.multiplayer.playmode
  only if compatible/useful for local multi-client testing

Use Multiplayer Services Sessions and Relay.

Do NOT install or use deprecated standalone packages such as old standalone:

- Lobby SDK
- Relay SDK
- Matchmaker SDK

unless the currently installed Unity package/version absolutely requires something different.

Before installation, inspect the current project/package state.

You are explicitly authorized to install the official Unity packages listed above if they are missing.

Do not install unrelated packages.

If the Unity project must be linked to a Unity Cloud Project, or if authentication/service configuration requires an action only the project owner can perform:

STOP at that point and clearly tell me the exact action I must perform in the Unity Editor/dashboard.

Do not invent project IDs, service IDs, environment IDs, credentials, or configuration values.

---

# 2. Preserve offline mode

The current offline/local mode must continue working.

Do not delete or replace it.

Conceptually the prototype should now support:

Offline
Host Online
Join Online

Offline must continue to use the existing:

one local human + bots

flow.

Networking code must not contaminate ShieldsMatch with Unity networking dependencies.

---

# 3. Minimal connection UI

Create only a PROTOTYPE networking UI.

Do not build the final main menu or lobby art.

Provide:

OFFLINE

HOST ONLINE

JOIN ONLINE

For Host Online:

allow selecting:

- Target Player Count: 3–8
- Deck Count: 1 or 2

Then:

CREATE SESSION

After session creation show:

- Join Code
- connected human count
- assigned local seat
- basic connection status

Provide a prototype:

START NETWORK TEST

button for the host.

For Join Online:

provide:

Join Code input

and:

JOIN

After joining show:

- waiting for host
- assigned local seat
- connection status

No session browser.
No matchmaking.
No Steam invites.
No final UI.

Join Code is sufficient.

---

# 4. Unity Services initialization

Use the current supported Unity Gaming Services initialization flow.

For development/testing, anonymous authentication is acceptable.

Handle initialization failures cleanly.

Do not hard-code credentials.

Do not assume authentication succeeded.

Show/log a useful error if services cannot initialize or authenticate.

---

# 5. Sessions + Relay

Use Multiplayer Services Sessions.

Host creates a session using Relay networking.

Use the current API for the installed package version.

Prefer the current session abstraction rather than manually recreating legacy Lobby + Relay workflows.

Do not copy old tutorials blindly.

Inspect the installed package/API and current Unity documentation/samples available to the project.

The host must receive/share a join code.

The joining player must be able to join using that code.

---

# 6. Netcode for GameObjects

Use Netcode for GameObjects for the network transport/message layer.

Configure the project's network connection using the supported integration between:

Multiplayer Services Sessions
+
Relay
+
Netcode for GameObjects

Keep networking objects/components minimal.

Do not convert every existing scene object into a NetworkObject.

The room, table and deterministic static environment do NOT need network synchronization.

---

# 7. Explicit player ownership

This is critical.

Do NOT infer gameplay seat from:

- client ID
- GameObject name
- connection order alone inside presentation code
- Player 0 assumptions

Create an explicit authoritative mapping:

network participant/client
    ->
player/seat index

The host owns this mapping.

For the prototype it is acceptable for the host to assign available seats in connection order.

Example:

Host -> Player 0
First joining client -> Player 1
Second joining client -> Player 2

But this relationship must be explicitly stored and communicated.

Presentation should consume:

LocalPlayerIndex

rather than assuming local player = 0.

This is important because future clients will each have a different local seat.

---

# 8. Humans and bots are different concepts

A match seat may eventually represent:

- local human
- remote human
- bot

Keep these concepts explicit.

Do not mark every non-local player as a bot.

During TASK 008 we are testing human network participants.

When the host eventually starts a real match, unoccupied target seats will become bots.

Do NOT implement that gameplay behavior yet.

---

# 9. Network-test layout

When the host presses START NETWORK TEST:

all connected clients receive the same test configuration:

- target player count
- deck count
- human seat assignments

Create/rebuild the existing deterministic table/player layout accordingly.

The same seat positions must appear on every client.

The room/table are static local scene objects.

Player layout may be created locally from the shared configuration rather than synchronizing transforms that can already be deterministically calculated.

Do NOT start ShieldsMatch.

Do NOT deal cards.

Do NOT perform initial Shield selection.

This task is testing connectivity/ownership only.

---

# 10. Local first-person ownership

Each client must attach its one gameplay camera to its OWN assigned PlayerCharacter CameraAnchor.

Examples:

Host:
LocalPlayerIndex = 0
camera sits at Player 0

Client:
LocalPlayerIndex = 1
camera sits at Player 1

Each client controls only its own:

- HeadPivot
- first-person camera
- local head-look input

Remote human characters must not respond to local mouse input.

There must still be exactly one active gameplay camera per client.

---

# 11. Head/look synchronization

Synchronize human head/look direction.

When a human player looks:

left/right/up/down

the other connected clients should see that player's HeadPivot rotate correspondingly.

Do NOT synchronize full PlayerCharacter transforms every frame.

Players are seated and their body/seat transforms are deterministic.

Only synchronize the minimum changing pose information required for head look.

Keep bandwidth reasonable.

A small throttled/unreliable or otherwise appropriate current-NGO mechanism is acceptable.

Use the current NGO API for the installed version.

Do not use obsolete RPC syntax if the installed package has superseded it.

---

# 12. Host authority

The host is authoritative for:

- session/match configuration
- seat assignments
- which network participants are connected
- starting the network test

Clients must not assign themselves arbitrary seats.

Clients may send their local head/look input, but ownership must be validated.

Do not implement authoritative Shields gameplay yet.

---

# 13. Network architecture boundary

Do NOT add networking references inside:

- Shields.Cards
- Shields.Game
- Shields.Bots

Those assemblies should remain pure.

Create a networking/integration layer outside them.

Conceptually:

Presentation/Input
      ↓
Network Layer
      ↓
Authoritative host

and later:

Authoritative host
      ↓
ShieldsMatch

But TASK 008 stops before that final connection.

---

# 14. Do not serialize ShieldsMatch wholesale

Do NOT simply serialize or replicate the entire ShieldsMatch object.

This will be important later because unresolved Charge cards contain hidden information.

The future design will replicate explicit public state DTOs/snapshots.

TASK 008 should not implement those gameplay snapshots yet.

Do not expose internal Charge state while preparing networking.

---

# 15. Disconnect behavior for this task

Keep disconnect behavior minimal.

Before real gameplay exists:

If a client leaves:
- remove them from the connected-human/seat mapping
- update host prototype UI

If the host ends/leaves the session:
- joining clients should receive a clear disconnected/session-ended state
- return safely to networking setup UI if practical

Do NOT implement:

- host migration
- reconnect
- bot takeover
- mid-match rejoin

Those are outside TASK 008.

---

# 16. No late networking feature creep

Do NOT implement:

- full lobby browser
- Steam networking
- Steam invites
- matchmaking
- Vivox
- player accounts/profiles
- cosmetics
- chat
- full Shields gameplay replication
- networked cards
- networked bots
- NetworkVariables for every game property
- host migration
- reconnect

This task is only the multiplayer connection foundation.

---

# 17. Multiplayer Play Mode testing

If the compatible Unity Multiplayer Play Mode package is available, prefer using it to run at least two local editor players.

If that path is unavailable or unreliable:

use one Editor instance plus one development build.

Do not fake the network test in one process.

The validation must use two independently networked participants.

---

# 18. Required validation — connection

Validate an actual two-participant session.

HOST:

1. Initialize services.
2. Authenticate.
3. Create Relay-backed session.
4. Obtain join code.

CLIENT:

5. Initialize services independently.
6. Authenticate independently.
7. Join using the host's join code.

Confirm:

- both report connected
- host sees 2 human participants
- client receives an explicit seat assignment
- host and client agree on seat mapping

---

# 19. Required validation — cameras

Start the network test.

Confirm on host:

- exactly one active gameplay camera
- camera attached to host's assigned player

Confirm on client:

- exactly one active gameplay camera
- camera attached to client's DIFFERENT assigned player

The two clients must therefore see the table from different seats.

---

# 20. Required validation — head synchronization

Move host mouse/head:

client must see host character's head rotate.

Move client mouse/head:

host must see client character's head rotate.

Confirm:

- remote body does not rotate incorrectly
- remote camera is never activated locally
- head rotations do not fight local input
- no obvious feedback loop/jitter caused by both sides writing the same pose

---

# 21. Required validation — 3/6/8 layout compatibility

It is not necessary to connect 8 human machines.

But validate the network-test configuration/layout at:

- target 3
- target 6
- target 8

with at least two real network humans connected.

Human seat assignments must remain valid.

Remaining seat positions may simply contain passive placeholder players for this task.

Do not run bot gameplay.

---

# 22. Offline regression

After adding networking, run the existing OFFLINE path.

Confirm:

- initial deal works
- Shield selection works
- bots work
- one complete local match can still finish

The full existing EditMode suite must still pass.

Do not change existing gameplay tests merely to accommodate networking.

---

# 23. Testing

Run the complete current EditMode suite.

Expected baseline before networking changes:

166 passing tests.

Add focused pure/unit tests for newly extractable networking-independent logic such as:

- seat-assignment mapping
- duplicate assignment prevention
- release/reuse after pre-match disconnect

Do not attempt to unit-test Unity Relay itself.

Runtime connection testing is required for the actual service/network flow.

---

# 24. Error handling

The prototype UI/logging must make failures understandable.

Examples:

- Unity Services initialization failed
- authentication failed
- session creation failed
- invalid join code
- session full
- disconnected
- host ended session

Do not silently fail.

Do not expose secrets/tokens in normal UI/logs.

---

# 25. Scene/project safety

Inspect the existing scene before modifying it.

Do not redesign:

- room
- table
- card presentation
- player visuals
- HUD gameplay presentation

Only add what is needed for connection/session/network test.

Prefer dedicated objects/components such as conceptually:

NetworkBootstrap
SessionController
NetworkSeatRegistry
NetworkHeadPoseBridge

Exact names are your choice.

Keep responsibilities small.

---

# 26. Final report

When complete, report:

1. Packages installed/versions
2. Whether the Unity project required cloud/service linking
3. Files created
4. Files modified
5. Session/Relay flow
6. Seat-assignment design
7. How LocalPlayerIndex is communicated
8. How head-look synchronization works
9. How host authority is enforced
10. Disconnect behavior
11. Automated test results
12. Actual two-participant runtime test results
13. Offline-regression result
14. Any Unity-service setup I still need to perform manually
15. Any networking issue/blocker discovered

If external service configuration requires my action:

STOP and ask me to perform it.

Do not guess credentials or project/service configuration.

If a networking API differs from assumptions in this task because the installed/current Unity package uses a newer API:

use the CURRENT supported API and explicitly report the difference.

Stop after TASK 008.

Do not proceed into networked Shields gameplay.


NOTE: ## Important — BotController is NOT authoritative identity

The existing `BotController` MonoBehaviour is only a Unity-side seat/presentation marker.

It is NOT authoritative proof that a player is a bot.

Do NOT determine human/bot/network ownership using:

- presence of BotController
- absence of local input
- prefab components
- GameObject names

The authoritative source for bot/human identity remains gameplay/session state, such as:

- MatchPlayerState.IsBot
- explicit network participant -> seat mapping
- explicit local/remote ownership information

A PlayerCharacter prefab/component layout must not determine gameplay ownership.

Do not remove BotController merely because of this warning unless it becomes genuinely unnecessary during the task.