# Task 04 - Hierarchy Multi-Selection

## Goal

Implement component multi-selection in the Hierarchy and synchronize it bidirectionally with Panel2D and Face viewports.

## Required Behaviour

### Mouse Selection

- Click a component row: replace selection.
- Ctrl-click a component row: toggle that item.
- Shift-click: select the continuous visible range from the hierarchy anchor.
- Ctrl+Shift-click: add the continuous visible range.
- Clicking a group row must not add the group to the component selection set.
- Preserve expand/collapse behaviour.

### Visible Range

Range selection must use the flattened list of currently visible hierarchy rows.

- Include expanded descendants.
- Exclude descendants hidden by collapsed ancestors.
- Exclude non-selectable group rows from the resulting component selection.
- Keep ordering deterministic.

### WPF TreeView Handling

Do not use native `TreeViewItem.IsSelected` as the authoritative multi-selection state.

Use `HierarchyItemViewModel.IsSelected` or an equivalent explicit visual-state property for every selected row.

The native TreeView selection may remain as keyboard focus/navigation support, but it must not clear the document selection set unexpectedly.

### Synchronization

- Viewport multi-selection updates all matching hierarchy row visuals.
- Hierarchy multi-selection immediately redraws Panel2D or Face selection outlines.
- Switching documents restores each document's own hierarchy selection visuals.
- Hierarchy refresh/rebuild preserves surviving selections and expansion state.
- Deleted or missing items are removed from selection through reconciliation.

### Keyboard Navigation

Preserve existing Delete and F2 behaviour where applicable, but do not implement bulk delete in this task. Ensure keyboard focus changes do not collapse the multi-selection accidentally.

## Context Menus

Do not redesign Hierarchy context menus.

Minimal preservation rule:

- Right-click an already-selected component: preserve the current selection.
- Right-click an unselected component: replace selection with that component before opening the existing menu.

Only implement this rule if required to prevent regressions; do not add new multi-selection menu commands.

## Out of Scope

- Inspector multi-edit
- Bulk delete
- Group nodes as selectable component collections
- Drag-and-drop reordering of multiple hierarchy items
- Context-menu command redesign

## Tests

Add tests for:

- Visible-row flattening
- Shift range selection
- Ctrl+Shift additive range
- Collapsed descendant exclusion
- Group-row exclusion
- Anchor and primary updates
- Viewport-to-hierarchy synchronization
- Hierarchy-to-viewport synchronization
- Selection preservation across hierarchy refresh
- Per-document selection restoration
- Right-click preservation rule if implemented

## Manual Test Checklist

- Ctrl-select separate Panel2D hierarchy rows.
- Shift-select a continuous visible range.
- Collapse a group and confirm hidden descendants are not selected by a range.
- Use Ctrl+Shift to add another range.
- Repeat in a Face document.
- Select several elements in a viewport and confirm all hierarchy rows highlight.
- Select several hierarchy rows and confirm all viewport outlines appear immediately.
- Switch tabs and confirm each document restores its own selection.
- Confirm expand/collapse, F2, and existing context menus still behave sensibly.

## Completion Report

Report files changed, TreeView strategy, range algorithm, tests run, manual tests requested, and any focus/navigation compromises.

Stop after Task 04 for user testing and review.
