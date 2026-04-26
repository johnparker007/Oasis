# Object Locking & Visibility Support Plan (Plan Only)

This document defines a planning-only path for adding object locking and visibility controls to the Panel2D editor.

## Goals

- Allow users to lock elements to prevent accidental selection/move/resize.
- Allow users to hide elements from canvas rendering without deleting them.
- Keep locking/visibility persistent across save/load.
- Preserve undo/redo semantics through document mutation commands.

## Non-Goals

- No implementation in this task.
- No new framework/DI introduction.
- No changes to copy/paste/duplicate behavior in this task.

## Required Model Fields

Add explicit state to `PanelElementModel`:

- `bool IsLocked`
  - `true`: element cannot be selected for transform/move/edit interactions.
- `bool IsVisible`
  - `false`: element should not render in normal canvas projection.

Field defaults for existing content:
- `IsLocked = false`
- `IsVisible = true`

## Storage & Migration Considerations

Current file format is schema version 1. Lock/visibility persistence should be introduced with explicit migration support.

Future implementation expectations:
- Add optional storage fields (e.g., `isLocked`, `isVisible`).
- Migration of older files should inject defaults (`false`/`true`).
- Validation should normalize missing fields to defaults and reject invalid field types.

## Canvas Hit-Test / Interaction Changes (Planned)

Locking behavior expectations:
- Locked elements are skipped by editable hit-testing for drag/resize/mutation interactions.
- Locked elements may still be selectable for inspection, depending on UX choice; if selectable, mutation commands should remain blocked.
- Multi-select operations should exclude locked elements from transform sets.

Visibility behavior expectations:
- Hidden elements should not be rendered in primary canvas layer.
- Hidden elements should not be selectable via direct canvas hit-testing.
- Hidden state should not remove the element from the document model.

## Hierarchy UI Changes (Planned)

Hierarchy should remain the authoritative object list even when elements are hidden.

Planned additions:
- Lock toggle indicator/action per item.
- Visibility toggle indicator/action per item.
- Optional filtering controls (show hidden, show only unlocked) can be deferred.

Interaction notes:
- Hidden items remain present in hierarchy with clear hidden-state affordance.
- Locked items show locked-state affordance and disable rename/drag-reorder interactions where appropriate.

## Command Surface (Undoable Mutations)

Add focused commands that are document-aware:

- `SetElementLocked(objectId, isLocked)`
- `ToggleElementLocked(objectId)`
- `SetElementVisible(objectId, isVisible)`
- `ToggleElementVisible(objectId)`

Command rules:
- No-op when object does not exist.
- No-op when requested state matches current state.
- No-op commands must not enter undo/redo history.
- Real changes should mark document dirty.

## Inspector/Property Panel Expectations

- Show lock and visibility toggles for selected supported panel elements.
- Disable mutation-affecting controls when selected object is locked.
- Keep binding-facing property names stable unless explicitly approved by task scope.

## Testing Plan (for implementation task)

- Unit tests:
  - lock/visibility command no-op behavior
  - undo/redo for lock/visibility toggles
  - dirty-state changes only on real mutations
- Storage tests:
  - round-trip lock/visibility values
  - migration/default behavior for old files
- Smoke tests:
  - hidden elements not selectable on canvas
  - locked elements resist move/resize actions
  - hierarchy toggles update canvas and inspector consistently

## Suggested Implementation Sequence

1. Extend model + storage DTO + migration/default normalization.
2. Add lock/visibility mutation commands and history guards.
3. Update canvas selection/hit-test path for lock/visibility semantics.
4. Add hierarchy and inspector toggles.
5. Add tests and run smoke verification.

