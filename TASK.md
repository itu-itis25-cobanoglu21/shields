Use option C for the discard pile.

For now, the DiscardPile is a presentation-only FACE-DOWN stack with a visible count.

Do NOT expose or replicate TopDiscard.
Do NOT add TopDiscard to PublicMatchSnapshot.
Do NOT make any decision yet about whether previously hidden Charge cards become public after entering the discard pile.

This means the only new replicated/public discard information needed for this task is:

DiscardCount

The visual pile should:
- live at a new CenterDiscardAnchor beside CenterDeckAnchor
- use a clearly different placeholder color/material from the DrawPile
- show a small face-down stack when DiscardCount > 0
- hide/appear empty when DiscardCount == 0
- optionally display "Discard: N"
- have no gameplay authority
- have no selectable/interactable collider
- never contain or infer actual PlayingCard identities

For the camera reset, proceed with the root-cause fix you identified:

- local look binding/rest initialization must occur only when the layout is genuinely built/rebuilt for a new local seat
- repeated EnsureLayout / ApplySeatRoles calls caused by gameplay snapshots must NOT call ResetLook, Bind in a way that resets look, or RestLookOnTable
- preserve current local yaw/pitch across every normal gameplay snapshot/action
- preserve existing head-pose networking
- do not implement a rotation-saving workaround

Implement TASK 010 now with those decisions.

Add/update tests for DiscardCount in PublicMatchSnapshot/PublicStateBuilder, including:
- normal discards increase the public count correctly
- health-card retrieval from discard updates the count correctly
- reshuffle clearing the discard pile is reflected correctly
- unresolved Charge identity remains structurally absent from all public network DTOs

Then run the complete EditMode suite.

Runtime-validate:
1. offline camera persistence after Attack, Replace Shield, Charge and bot turns
2. online Host + Client camera persistence
3. head synchronization still works
4. Host and Client show the same Draw and Discard counts
5. discard pile appears/disappears correctly
6. no Console errors

Do not change gameplay rules or networking architecture beyond what TASK 010 requires.

Stop after TASK 010 and report the results.