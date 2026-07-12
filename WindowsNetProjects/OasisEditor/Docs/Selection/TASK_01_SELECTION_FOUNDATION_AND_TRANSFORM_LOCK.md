# Task 01 - Selection Foundation and Transform Lock

## Status

Implemented and merged.

This document records the intended Task 01 scope and the current compatibility direction for later cleanup.

## Goal

Introduce the document-scoped selection foundation, fix selection refresh behaviour, and replace the old lock semantics with an explicit transform lock.

Task 01 must not implement user-facing multi-select gestures.

## Required Work

### Central Selection State

Create the authoritative document-scoped selection state described in `MULTI_SELECTION_CONTEXT.md`.

It must support:

- ordered selected items
- primary item
- hierarchy range anchor
- replace, add, remove, toggle, clear, and reconcile operations
- dedicated selection-change notifications
- per-document selection isolation

Use stable object identity and a selection domain. Do not use copied geometry as authoritative selection state.

### Temporary Phased Bridge

Existing code initially expected one `HierarchySelectedPanelSelection` / active selection.

A temporary bridge may represent the primary selection for consumers not yet migrated, but `DocumentSelectionState` is authoritative and the bridge must never become a second source of truth.

This bridge is not old-schema or persisted-data compatibility. It exists only to support phased implementation and must be removed in Task 06 after all consumers use `DocumentSelectionState`.

### Fix Panel2D Refresh

Selecting a Panel2D element from the Hierarchy must immediately redraw its viewport selection outline through the dedicated selection notification path.

### Transform Lock

Remove the behaviour where the old lock prevents an element from being selected.

The only intended lock concept is:

- runtime/model property: `IsTransformLocked`
- Inspector label: `Lock Transform`

Transform-locked elements remain selectable and inspectable. Non-transform properties remain editable. Viewport movement and resize are blocked.

Old `IsLocked` behaviour and storage may be removed completely.

No backward compatibility is required for old Oasis asset schemas or lock serialization. Do not add compatibility mapping, aliases, migrations, fallback deserializers, or old-asset reopening tests.

Newly created or imported background components should default to transform locked where appropriate.

### Selection Reconciliation

Remove stale selection identities after document structure changes while preserving surviving selections and the primary item when possible.

## Out of Scope

- Ctrl-click and rectangle multi-selection
- Hierarchy multi-selection
- Inspector multi-edit
- Group movement
- Bulk delete
- Group resize
- Context-menu redesign

## Tests

Cover:

- replace/add/remove/toggle/clear
- primary item updates
- anchor updates
- duplicate prevention
- per-document isolation
- stale-item reconciliation
- transform-locked elements remain selectable
- transform-locked elements are rejected by move/resize eligibility checks
- hierarchy-originated selection invalidates Panel2D rendering

Do not add tests for compatibility with old `IsLocked` storage or previous Oasis asset schemas.

## Manual Test Checklist

- Select a Panel2D item in the Hierarchy and confirm the outline appears immediately.
- Enable Lock Transform, click away, then reselect the component from viewport and Hierarchy.
- Confirm the component remains inspectable.
- Confirm a locked component cannot be moved or resized.
- Confirm a newly created or imported background starts transform locked where applicable.

## Completion Report

Report:

- files added and modified
- selection-state API introduced
- temporary phased bridges retained
- obsolete lock behaviour removed
- tests added and run
- manual checks requested
- deviations or risks

Stop after Task 01 for user testing and review.
