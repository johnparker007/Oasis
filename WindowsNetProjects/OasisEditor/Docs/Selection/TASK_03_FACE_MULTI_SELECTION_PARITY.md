# Task 03 - Face Multi-Selection Parity

## Goal

Bring the Face edit viewport onto the shared selection and transform infrastructure established by Tasks 01 and 02.

The user-facing selection model should be consistent between Panel2D and Face wherever the asset types allow it.

## Required Behaviour

Implement in the Face viewport:

- Click replacement selection
- Ctrl-click add/remove selection
- Empty-space clear/preserve behaviour
- Rectangle replacement selection
- Ctrl+rectangle additive selection
- Full-enclosure rectangle matching
- Multi-selection outlines with a distinct primary item
- Atomic group movement
- Transform-lock enforcement
- Single-selection resize only, if Face resize is already supported or can be added without broad unrelated work

If Face does not currently have a reliable resize implementation, do not add group resize and do not force resize into this task. Preserve the agreed scope and report the limitation.

## Shared Infrastructure

Reuse the shared services introduced for Panel2D:

- Selection mutation operations
- Modifier interpretation
- Rectangle bounds matching
- Group move computation
- Bulk update commands
- Preview cancellation patterns
- Selection outline styling helpers where practical

Do not copy the Panel2D interaction implementation into Face under different names.

Do not force both views into one large base control. Keep WPF event handling view-specific and thin.

## Face-Specific Considerations

- Preserve artwork rendering.
- Preserve runtime display rendering.
- Preserve mask-layer and special Face selection behaviour.
- Ensure large artwork/background elements can be transform locked and remain selectable.
- Ensure a locked selected artwork/background does not block starting a rectangle selection.
- Keep right-click add behaviour working; do not redesign context menus.

## Out of Scope

- Hierarchy Ctrl/Shift selection
- Inspector multi-edit
- Bulk delete
- Group resize
- Context-menu redesign
- Alignment/distribution

## Tests

Add tests for Face equivalents of:

- Click and Ctrl-click transitions
- Rectangle replace/add
- Group move computation and atomic undo
- Locked item exclusion
- Selection rendering eligibility
- Selection state synchronization
- Panel2D/Face shared-service parity

## Manual Test Checklist

- Select multiple Face elements with Ctrl-click.
- Replace and clear selections.
- Select with a rectangle and add with Ctrl+rectangle.
- Move a selected group and undo it in one step.
- Include a transform-locked artwork/background and confirm it remains stationary and selectable.
- Begin rectangle selection over a locked artwork/background.
- Confirm selection changes from elsewhere redraw immediately.
- Confirm existing Face artwork and runtime display rendering remains correct.

## Completion Report

Report files changed, reused shared services, any Face-specific divergence, tests run, and manual tests requested.

Stop after Task 03 for user testing and review.
