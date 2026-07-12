# Task 06 - Bulk Delete and Integration Hardening

## Goal

Complete the core multi-selection feature by making Delete operate atomically on selected components, hardening behaviour across editor workflows, and removing temporary phased compatibility code.

## Bulk Delete

Implement one shared bulk-delete path for selected deletable components.

Requirements:

- Delete removes all selected deletable components.
- One user action creates one undo entry.
- One undo restores the complete set.
- Restore original ordering and hierarchy placement deterministically.
- Redo removes the same set again.
- Reconcile selection after deletion.
- Do not accidentally delete unsupported special selection domains.
- Handle mixed deletable and non-deletable selections explicitly and consistently.

Use the same command path from:

- Hierarchy Delete key
- any existing viewport Delete key handling
- existing component-delete command bindings

Do not redesign context menus.

## Integration Hardening

Review and fix selection behaviour for:

- undo and redo after group move
- undo and redo after Inspector multi-edit
- undo and redo after bulk delete
- document reload or structure refresh
- hierarchy rebuild
- switching between open documents
- closing a selected document
- deleting the primary item
- deleting the hierarchy anchor
- selection changes during active previews
- lost mouse capture and cancelled drags
- locked and hidden components

## Compatibility Cleanup

Once all consumers use `DocumentSelectionState`, remove every temporary compatibility member introduced to support phased implementation.

Completely remove obsolete selection and lock code, including:

- `HierarchySelectedPanelSelection` and any equivalent primary-selection bridge
- old singular selection aliases
- old `IsLocked` properties and storage
- compatibility serializers
- compatibility aliases
- migration code
- fallback deserializers
- obsolete compatibility tests

No backward compatibility is required for previous Oasis asset schemas, old selection models, or old lock implementations.

Do not retain compatibility members for persisted data. Remove obsolete code rather than introducing or preserving migration layers.

After cleanup:

- `DocumentSelectionState` is the only authoritative selection state.
- `IsTransformLocked` is the only lock property.
- `Lock Transform` is the consistent UI terminology.
- There is no remaining old-schema selection or lock compatibility layer.

## Behaviour Consistency Review

Confirm Panel2D and Face agree on:

- click selection
- Ctrl-click selection
- empty-space behaviour
- rectangle replace/add
- primary selection rendering
- group move
- transform-lock handling
- selection clearing after deletion

Confirm Hierarchy and Inspector remain synchronized with both views.

## Performance Review

Test representative larger documents.

Avoid:

- rebuilding the full hierarchy on every mouse move
- rebuilding all Inspector rows on every preview frame
- creating one command per selected object
- repeatedly resolving the same object identities inside inner rendering loops when a local snapshot would suffice

Do not add speculative caching without profiling or an obvious hot path.

## Out of Scope

- group resize
- alignment/distribution
- duplicate/copy/paste
- full context-menu redesign
- Shift modifiers in viewports
- relative Inspector expressions
- group-node selection

## Tests

Add or update tests for:

- atomic bulk delete
- ordering restoration on undo
- redo
- mixed deletable/non-deletable handling
- primary and anchor reconciliation
- selection state after undo/redo
- document switching and closing
- lost-capture preview restoration
- complete temporary-bridge removal
- complete old `IsLocked` removal
- Panel2D/Face behaviour parity

Delete obsolete tests that assert old schema, old serializer, old lock, or old singular-selection compatibility.

Run the full OasisEditor test suite where the Windows/.NET environment permits it.

## Manual Regression Checklist

- Multi-select and move in Panel2D, then undo/redo.
- Multi-select and move in Face, then undo/redo.
- Multi-edit from Inspector, then undo/redo.
- Delete a multi-selection from Hierarchy and undo/redo.
- Delete a multi-selection while the primary item is not first in hierarchy order.
- Switch documents with different selections.
- Close and reopen documents.
- Test locked backgrounds and unlocked normal components.
- Test hidden components where supported.
- Confirm Face Source Shapes, mask layers, asset inspection, resize, pan, zoom, and existing add commands still work.
- Confirm no old lock behaviour makes an item unselectable.
- Confirm no consumer still uses the temporary singular-selection bridge.

## Completion Report

Report:

- files changed
- bulk-delete command design
- temporary compatibility members removed
- obsolete `IsLocked` and serializer/migration code removed
- full test results
- manual regression checks requested
- remaining known limitations
- deferred follow-up recommendations

Stop after Task 06 for final user testing and review.
