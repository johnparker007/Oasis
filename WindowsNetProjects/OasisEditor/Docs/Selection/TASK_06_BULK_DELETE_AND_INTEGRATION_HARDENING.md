# Task 06 - Bulk Delete and Integration Hardening

## Goal

Complete the core multi-selection feature by making Delete operate atomically on selected components and hardening selection behaviour across undo/redo, document changes, and existing editor workflows.

## Bulk Delete

Implement one shared bulk-delete path for selected deletable components.

Requirements:

- Delete removes all selected deletable components.
- One user action creates one undo entry.
- One undo restores the complete set.
- Restore original ordering and hierarchy placement deterministically.
- Redo removes the same set again.
- Reconcile selection after deletion.
- Do not delete unsupported special selection domains accidentally.
- If a selection mixes deletable and non-deletable domains, handle this explicitly and consistently.

Use the same command path from:

- Hierarchy Delete key
- Any existing viewport Delete key handling
- Existing delete command bindings that represent component deletion

Do not add a new context-menu redesign.

## Integration Hardening

Review and fix selection behaviour for:

- Undo and redo after group move
- Undo and redo after Inspector multi-edit
- Undo and redo after bulk delete
- Document reload or structure refresh
- Hierarchy rebuild
- Switching between open documents
- Closing a selected document
- Deleting the primary item
- Deleting the hierarchy anchor
- Selection changes during active preview gestures
- Lost mouse capture and cancelled drags
- Locked and hidden components

## Compatibility Cleanup

Review temporary single-selection compatibility members introduced in Task 01.

- Remove them where all consumers have migrated.
- Retain only compatibility that is still required for persisted data or clearly documented external behaviour.
- Ensure there is still one authoritative selection state.

Review old `IsLocked` names and behaviour:

- Remove obsolete selection-exclusion code.
- Keep only intentional persisted-data compatibility.
- Ensure UI and runtime terminology consistently use `Lock Transform` / `IsTransformLocked` where applicable.

## Behaviour Consistency Review

Confirm Panel2D and Face agree on:

- Click selection
- Ctrl-click selection
- Empty-space behaviour
- Rectangle replace/add
- Primary selection rendering
- Group move
- Transform-lock handling
- Selection clearing after deletion

Confirm Hierarchy and Inspector remain synchronized with both views.

## Performance Review

Test representative larger documents.

Avoid:

- Rebuilding the full hierarchy on every mouse move
- Rebuilding all Inspector rows on every preview frame
- Creating one command per selected object
- Repeatedly resolving the same object identities inside inner rendering loops when a local snapshot would suffice

Do not introduce speculative caching unless profiling or obvious hot paths justify it.

## Out of Scope

- Group resize
- Alignment/distribution
- Duplicate/copy/paste
- Full context-menu redesign
- Shift modifiers in viewports
- Relative Inspector expressions
- Group-node selection

## Tests

Add or update tests for:

- Atomic bulk delete
- Ordering restoration on undo
- Redo
- Mixed deletable/non-deletable handling
- Primary and anchor reconciliation
- Selection state after undo/redo
- Document switching and closing
- Lost-capture preview restoration
- Compatibility cleanup
- Panel2D/Face behaviour parity

Run the full OasisEditor test suite.

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

## Completion Report

Report:

- Files changed
- Bulk delete command design
- Compatibility members removed or retained
- Full test results
- Manual regression results requested
- Remaining known limitations
- Deferred follow-up recommendations

Stop after Task 06 for final user testing and review.
