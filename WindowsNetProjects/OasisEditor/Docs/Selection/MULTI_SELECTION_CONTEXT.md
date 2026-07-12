# Multi-Selection and Shared Selection Architecture Context

## Purpose

This document defines the planned selection-system refactor for Oasis Editor.

The goal is to introduce a consistent Unity-style multi-selection workflow across:

- Panel2D edit views
- Face edit views
- Hierarchy tree views
- Inspector editing
- Move and delete commands
- Undo and redo

The work should be implemented in numbered phases. Each phase should be completed, tested by the user, and reviewed before the next phase begins.

## Current Problem

The Editor currently treats selection as one nullable `PanelSelectionInfo`.

That single-selection assumption is embedded in:

- `DocumentTabViewModel.HierarchySelectedPanelSelection`
- `ActiveDocumentContextService.ActivePanelSelection`
- `HierarchyViewModel`
- `InspectorViewModel`
- Panel2D selection outlines
- Face selection outlines
- Panel2D move and resize handling
- Hierarchy delete and context commands

Panel2D and Face selection behaviour has also drifted apart.

Panel2D currently supports click selection, rectangle selection, movement, and resize. Face currently has a more limited click-selection path. Both views should eventually present the same core selection behaviour.

There is also a current Panel2D bug: selecting an element from the Hierarchy updates the logical selection but does not immediately redraw the Panel2D selection outline. The selected element can still be dragged, confirming that the state changed but the viewport was not invalidated.

## Core Architectural Decision

Introduce one authoritative, document-scoped selection state.

The Hierarchy, Inspector, Panel2D view, and Face view must all read and update the same selection state. Do not maintain separate parallel selection collections for different UI surfaces.

Suggested conceptual shape:

```csharp
public sealed class DocumentSelectionState
{
    public IReadOnlyList<EditorSelectionItem> Items { get; }
    public EditorSelectionItem? PrimaryItem { get; }
    public EditorSelectionItem? AnchorItem { get; }
}
```

The exact names may change to fit project conventions.

### Selection Identity

Selection items should identify objects using stable identity rather than copied bounds.

Suggested conceptual shape:

```csharp
public readonly record struct EditorSelectionItem(
    EditorSelectionDomain Domain,
    string ObjectId);
```

Possible domains include:

- Panel element
- Face element
- Panel Face Source Shape
- Face mask layer

Do not rely on copied X, Y, Width, or Height values as the authoritative selection state. Resolve the current model when geometry or properties are needed.

### Primary Selection

The selection state must track a primary item separately from the selected set.

The primary item is normally the last item clicked or focused. It is useful for:

- Inspector titles and type decisions
- Hierarchy keyboard focus
- Context-menu behaviour later
- Single-item resize handles
- Distinguishing one selection outline from the rest

### Hierarchy Range Anchor

The selection state or hierarchy selection controller must track an anchor item for Shift-range selection.

The anchor is distinct from the primary item, although they will often be the same.

## Document Scope

Selection belongs to a document.

Each open document should retain its own selection state. Switching tabs must restore that document's selection rather than transferring IDs from another document.

When document content changes:

- Remove selections whose objects no longer exist.
- Preserve surviving selections.
- Preserve the primary item when it still exists.
- Choose a deterministic replacement primary item when necessary.

## Required Selection Operations

Selection changes should be expressed as explicit operations rather than scattered collection mutations.

At minimum, support:

- Replace selection
- Add item
- Add range or collection
- Remove item
- Toggle item
- Clear selection
- Set primary item
- Set hierarchy anchor
- Reconcile selection after model changes

Selection changes must raise a dedicated notification that causes all relevant UI surfaces to refresh.

## Viewport Selection Behaviour

### Click

- Click an unselected component: replace the selection with that component.
- Click a selected component: keep the complete selection.
- Ctrl-click an unselected component: add it and make it primary.
- Ctrl-click a selected component: remove only that component.
- Click empty space without Ctrl: clear selection.
- Ctrl-click empty space: preserve the selection.

Keeping the full selection when clicking an already-selected component is required so a group drag can begin without collapsing the group.

### Rectangle Selection

- Rectangle without Ctrl: replace the selection with all matching eligible components.
- Ctrl+rectangle: add matching components.
- Ctrl+rectangle never toggles or removes already-selected components.
- Shift modifiers in the viewport are deferred.

Use a shared, tested geometry rule. For the initial implementation, select components whose bounds are fully enclosed by the rectangle. This avoids a large background or artwork element being selected merely because the rectangle crosses it.

Special non-rectangular editing objects such as Panel Face Source Shapes may retain their existing specialised selection behaviour unless a task explicitly includes them.

### Gesture Priority

Pointer interaction should resolve in this order:

1. Special editing handle, such as a Face Source Shape corner
2. Single-selection resize handle
3. Drag on a selected, transform-unlocked component
4. Potential click selection
5. Rectangle selection after the drag threshold is exceeded

## Multi-Selection Rendering

- Draw an outline around every selected component.
- Draw the primary item's outline with a visually stronger treatment.
- Show resize handles only when exactly one transformable component is selected.
- Do not implement group resizing in this work.
- Ensure selection changes from the Hierarchy immediately invalidate Panel2D and Face viewports.

## Group Movement

Dragging any selected, transform-unlocked component should move the selected transform-unlocked group by the same document-space delta.

Implementation requirements:

- Capture immutable original models or bounds at drag start.
- Calculate all previews from the original state.
- Do not accumulate preview deltas from already-mutated preview models.
- Commit the group move as one undoable command.
- Cancel or lost-capture handling must restore all previewed models.
- Locked items may remain selected but do not move.
- Dragging a locked selected item must not initiate group movement.

## Transform Lock

The old `IsLocked` behaviour is not wanted.

Current observed behaviour prevents a locked component from being selected after the user clicks away. Remove that old selection-exclusion behaviour.

Replace the concept with an explicit transform lock:

- Prefer a model/property name such as `IsTransformLocked`.
- Inspector label: `Lock Transform`.
- A transform-locked component remains selectable.
- It remains inspectable.
- Non-transform properties remain editable.
- It cannot be moved or resized in the viewport.
- Transform fields in the Inspector should be disabled or rejected for locked items according to the multi-edit rules.

Preserve asset compatibility where practical. If persisted data currently uses `IsLocked`, introduce a deliberate migration or serialization compatibility mapping rather than silently breaking existing assets.

Background components should default to transform locked when newly created or imported. Do not automatically rewrite every existing background on each load.

## Hierarchy Behaviour

The WPF `TreeView` is single-selection by default, so multi-selection must be represented by view-model state rather than relying on native `TreeViewItem.IsSelected` as the authoritative source.

Required behaviour:

- Click: replace selection.
- Ctrl-click: toggle one component.
- Shift-click: select the continuous visible range from the anchor to the clicked item.
- Ctrl+Shift-click: add the continuous visible range.
- Range selection uses currently visible hierarchy rows.
- Descendants hidden inside collapsed groups are not included.
- Group nodes do not become component selections in the initial implementation.
- Programmatic selection from a viewport must update all matching hierarchy row visuals.

The native TreeView selection may be retained only for keyboard focus/navigation if useful.

## Inspector Multi-Edit

The Inspector should aggregate selected component properties.

A property has one of these conceptual states:

- Common value
- Mixed value
- Unavailable

Do not store the dash character as a value. The UI may display `-` for a mixed numeric or text field, but the internal state must explicitly represent a mixed value.

For boolean properties, use an indeterminate three-state checkbox for mixed values.

### Same-Type Selection

When all selected elements have the same concrete type:

- Show common/base properties.
- Show meaningful type-specific properties.
- Show a common value when all values match.
- Show mixed state when values differ.
- Entering a value applies it to every eligible selected item.

### Mixed-Type Selection

Initially show only safe common properties:

- X
- Y
- Width
- Height
- Visible
- Lock Transform

Do not bulk-edit Name in the initial mixed-type implementation.

### Transform Editing

X, Y, Width, and Height edits are absolute assignments.

For example, if selected items have different Y values and the user enters `10`, all eligible selected items receive Y = 10.

Do not add relative-expression syntax in this work.

A multi-object Inspector edit must be committed as one undoable command.

## Bulk Delete

Delete should operate on all selected deletable components.

Requirements:

- One delete action removes the selected set.
- One undo restores the complete set.
- Preserve deterministic ordering when restoring.
- Ignore or reject non-deletable selection domains explicitly.
- After deletion, reconcile the selection state.
- Hierarchy Delete-key behaviour and any existing viewport Delete-key path should use the same bulk-delete command.

## Shared Logic and View-Specific Logic

Share domain logic between Panel2D and Face, including:

- Selection state
- Selection mutation operations
- Modifier interpretation
- Rectangle bounds matching
- Group move computation
- Bulk mutation commands
- Inspector aggregation
- Selection outline styling helpers where practical

Do not force the complete Panel2D and Face WPF controls into one inheritance hierarchy during this refactor. Their current gesture and rendering responsibilities differ. Keep view-specific event adapters thin and route them into shared services.

## Undo and Preview Requirements

Multi-object operations must be atomic from the user's perspective.

One user action should create one undo entry for:

- Group move
- Multi-object Inspector edit
- Bulk delete

Preview mutations must not become separate command-history entries.

## Deferred Work

Do not include the following in this core selection work:

- Group resize or scale
- Alignment and distribution tools
- Duplicate or copy/paste workflows
- Full context-menu redesign
- Multi-selection context-menu command matrices
- Shift selection in Panel2D or Face viewports
- Rectangle-selection toggle behaviour
- Relative Inspector expressions such as `+=10`
- Selecting hierarchy group nodes as component groups
- Cross-document selections

A minimal right-click preservation rule may be added only if required to prevent the new selection state being accidentally collapsed, but context-menu redesign remains separate.

## Testing Expectations

Add focused unit tests for:

- Replace, add, remove, toggle, and clear operations
- Primary and anchor behaviour
- Selection reconciliation after deletion
- Rectangle enclosure matching
- Modifier interpretation
- Group move calculations
- Locked-item movement exclusion
- Inspector common and mixed value aggregation
- Atomic bulk update commands
- Atomic bulk delete and undo restoration
- Hierarchy visible-range calculation

Add view-model or integration tests where practical for:

- Hierarchy-to-viewport selection refresh
- Viewport-to-hierarchy multi-selection sync
- Document tab selection isolation
- Panel2D and Face parity

## Delivery Process

Implement one numbered task at a time.

For each task:

1. Read this context file and the current numbered task.
2. Inspect the current implementation before changing code.
3. Keep the change focused on that task.
4. Run relevant targeted tests and the full test suite where practical.
5. Report files changed, tests run, deviations, risks, and manual test instructions.
6. Stop and wait for user testing and review before starting the next task.
