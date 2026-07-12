# Task 02 - Panel2D Multi-Selection and Group Move

## Goal

Implement multi-selection interaction, rendering, and atomic group movement in the Panel2D viewport using the selection foundation from Task 01.

## Required Behaviour

### Click Selection

- Click an unselected element: replace selection.
- Click a selected element: preserve the full selection.
- Ctrl-click an unselected element: add it and make it primary.
- Ctrl-click a selected element: remove only it.
- Click empty space without Ctrl: clear selection.
- Ctrl-click empty space: preserve selection.

### Rectangle Selection

- Rectangle without Ctrl replaces the selection.
- Ctrl+rectangle adds matching elements.
- Ctrl+rectangle never removes or toggles existing elements.
- Use full-enclosure bounds matching for the initial implementation.
- Do not add Shift viewport behaviour.

### Gesture Priority

Preserve Face Source Shape corner editing and single-element resize behaviour.

Resolve pointer gestures in this order:

1. Face Source Shape corner handle
2. Single-selection resize handle
3. Drag on a selected transform-unlocked element
4. Click candidate
5. Rectangle selection after threshold

A selected transform-locked background must not capture a move drag. Dragging over it should still allow rectangle selection to begin.

### Rendering

- Draw an outline for every selected Panel2D element.
- Give the primary selection a stronger visual treatment.
- Show resize handles only when exactly one transform-unlocked resizable element is selected.
- Do not implement group resize.
- Keep Face Source Shape visuals and handles working.

### Group Move

Dragging any selected transform-unlocked element moves all selected transform-unlocked Panel2D elements by the same document-space delta.

Requirements:

- Capture immutable originals at drag start.
- Preview all moved elements from those originals.
- Cancel/lost capture restores every previewed element.
- Commit one atomic undoable command.
- One undo restores the entire group.
- Selected locked elements remain selected but do not move.
- Dragging a locked selected element does not initiate group movement.

Extract shared move computation and bulk mutation infrastructure suitable for later Face reuse.

## Selection Eligibility

Retain existing hit-test ordering and cycling semantics where they remain useful, but adapt them to selection sets.

Do not include Panel Face Source Shapes in ordinary rectangular component multi-selection unless the existing architecture makes that safe and explicit. Preserve their specialised single-selection editing path.

## Out of Scope

- Face viewport parity
- Hierarchy Ctrl/Shift selection
- Inspector multi-edit
- Bulk delete
- Group resize
- Context-menu redesign

## Tests

Add tests for:

- Click replace/add/remove behaviour
- Ctrl-click toggle behaviour
- Empty-space behaviour
- Rectangle replace and Ctrl-add
- Full-enclosure rectangle rule
- Background/locked element drag eligibility
- Group move delta calculation
- Locked member exclusion
- Preview cancellation
- Atomic command undo/redo
- Primary outline and resize-handle eligibility logic where practical

## Manual Test Checklist

- Select several Panel2D components using Ctrl-click.
- Remove one using Ctrl-click.
- Replace the set by clicking another element.
- Draw a rectangle to replace selection.
- Ctrl-draw a rectangle to add elements.
- Drag any selected unlocked element and confirm the unlocked group moves together.
- Undo once and confirm the whole group returns.
- Include a locked background in the selection and confirm it remains stationary.
- Start a rectangle drag over a selected locked background.
- Confirm single-selection resize still works and multi-selection resize handles are absent.
- Confirm Face Source Shape corner editing still works.

## Completion Report

Report files changed, shared services introduced, tests run, manual tests requested, and any interaction deviations.

Stop after Task 02 for user testing and review.
