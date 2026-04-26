# Layer Ordering Support Plan (Plan Only)

This document captures a **planning-only** design for introducing layer ordering in the Panel2D editor. It intentionally avoids implementation details that would change runtime behavior.

## Goals

- Allow users to change draw/order position of persisted Panel2D elements.
- Keep ordering stable across save/load.
- Preserve current undo/redo design by expressing ordering changes as document mutations.

## Non-Goals

- No locking/visibility behavior in this task.
- No copy/paste/duplicate behavior in this task.
- No UI redesign beyond minimal command entry points.

## Required Model Fields

Add ordering as explicit model state rather than relying on incidental collection order.

### `PanelElementModel`

- `int LayerOrder`
  - Lower value renders behind higher value.
  - Must remain unique per document after normalization.

Rationale:
- A dedicated field is easier to validate, migrate, and inspect than implicit list index semantics.
- Enables deterministic sorting even if collection order is changed by unrelated operations.

## Storage / DTO Alignment

Current `.panel2d` schema is version 1; ordering additions should be planned for version 2 migration work.

For future implementation:
- Add optional `layerOrder` to storage DTO for forward compatibility planning.
- Migration should populate missing values from current visual/list order.
- Validation should normalize duplicate or sparse layer values into a contiguous sequence.

## Command Surface (UI + Document Commands)

Introduce focused mutation commands (document-aware, undoable):

- `BringToFront` (set selected element to max layer)
- `SendToBack` (set selected element to min layer)
- `BringForward` (swap with next higher element)
- `SendBackward` (swap with next lower element)

Command behavior expectations:
- No-op if there is no selection.
- No-op if movement is impossible (already front/back).
- No-op commands must not enter command history.
- Dirty state should update only on real order changes.

## UI Entry Points

Minimal command access points for later implementation:

- Main menu: `Edit` or `Arrange` submenu with the four ordering actions.
- Canvas context menu: same four actions for current selection.
- Optional hierarchy context menu integration if hierarchy already supports object commands.

## Projection / Rendering Expectations

- Canvas projection should apply sorted order consistently when rebuilding visuals.
- Hierarchy should optionally display order for user clarity, but this can be deferred.
- Selection identity remains object-ID based; changing order must not change IDs.

## Validation Rules (for later implementation)

- `LayerOrder` must be non-negative.
- Orders should be unique within a document after normalization.
- Unsupported/missing order fields in older files handled by migration hook.

## Testing Plan (for implementation task)

- Unit tests: ordering command semantics and no-op history behavior.
- Round-trip tests: save/load preserves effective element order.
- Interaction smoke tests:
  - reorder selected rectangle/image
  - undo/redo reorder
  - close/reopen document keeps ordering

## Suggested Implementation Sequence

1. Add model/storage fields + normalization helpers.
2. Add mutation commands + undo/redo wiring.
3. Add menu/context-menu bindings.
4. Add tests and smoke test flows.

