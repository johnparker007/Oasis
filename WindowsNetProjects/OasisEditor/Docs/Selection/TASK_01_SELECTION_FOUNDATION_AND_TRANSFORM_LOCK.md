# Task 01 - Selection Foundation and Transform Lock

## Goal

Introduce the document-scoped selection foundation, fix current selection refresh behaviour, and replace the unwanted old lock semantics with an explicit transform lock.

Do not implement user-facing multi-select gestures yet.

## Required Work

### Central Selection State

Create the authoritative document-scoped selection state described in `MULTI_SELECTION_CONTEXT.md`.

It must support:

- Ordered selected items
- Primary item
- Hierarchy range anchor
- Replace, add, remove, toggle, clear, and reconcile operations
- Dedicated selection-change notifications
- Per-document selection isolation

Use stable object identity and a selection domain. Do not use copied geometry as authoritative selection state.

### Compatibility Bridge

Existing code currently expects one `HierarchySelectedPanelSelection` / active selection.

Provide a temporary compatibility path where useful, but new code must treat the selection state as authoritative. The compatibility property should represent the primary item only and should not become a second source of truth.

Document any temporary compatibility members so they can be removed after later tasks migrate all consumers.

### Fix Current Panel2D Refresh Bug

Selecting a Panel2D element from the Hierarchy must immediately redraw its viewport selection outline.

Use the new dedicated selection notification rather than adding more ad hoc redraw conditions where practical.

### Replace Old Lock Semantics

Remove the behaviour where `IsLocked` prevents an element from being selected.

Introduce explicit transform-lock semantics:

- Prefer `IsTransformLocked` in runtime/model APIs.
- Inspector label should eventually be `Lock Transform`.
- Locked elements remain selectable and inspectable.
- Locked elements cannot be moved or resized.
- Non-transform properties remain editable.

Preserve persisted asset compatibility. If the serialized property is currently named `IsLocked`, add an intentional compatibility mapping or migration. Do not silently drop existing values.

Newly created/imported background components should default to transform locked. Existing assets must not be rewritten on every load.

### Selection Reconciliation

Add a service or method that removes stale selection identities after document structure changes while preserving surviving selections and the primary item when possible.

## Out of Scope

- Ctrl-click and rectangle multi-selection
- Hierarchy multi-selection
- Inspector multi-edit
- Group movement
- Bulk delete
- Group resize
- Context-menu redesign

## Tests

Add tests covering:

- Selection replace/add/remove/toggle/clear
- Primary item updates
- Anchor updates
- Duplicate prevention
- Per-document isolation
- Reconciliation after selected objects are removed
- Locked elements remain selectable
- Locked elements are rejected by move/resize eligibility checks
- Existing serialized lock data remains compatible
- Hierarchy-originated selection invalidates Panel2D rendering through the new notification path where practical

## Manual Test Checklist

- Select a Panel2D item in the Hierarchy and confirm the outline appears immediately.
- Enable Lock Transform, click away, then reselect the component from both viewport and Hierarchy.
- Confirm the component remains inspectable.
- Confirm a locked component cannot be moved or resized.
- Confirm a newly created/imported background starts transform locked.
- Reopen an existing asset containing old lock data and confirm the lock value is preserved.

## Completion Report

Report:

- Files added and modified
- New selection-state API
- Compatibility members retained
- Lock serialization/migration approach
- Tests added and run
- Manual tests requested
- Deviations or risks

Stop after Task 01 for user testing and review.
