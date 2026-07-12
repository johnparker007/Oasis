# Multi-Selection and Shared Selection Architecture Context

## Purpose

This document defines the phased Unity-style multi-selection refactor for Oasis Editor across:

- Panel2D
- Face
- Hierarchy
- Inspector
- Delete
- Undo and redo

Implement one numbered task at a time. Each task must stop for user testing and review before the next task begins.

## Current State

Task 01 has been implemented and merged.

The editor now has a document-scoped selection foundation built around:

- `DocumentSelectionState`
- `EditorSelectionItem`
- ordered selection items
- a primary item
- a hierarchy range anchor
- selection-change notifications
- selection reconciliation
- transform-lock semantics

`DocumentTabViewModel.HierarchySelectedPanelSelection` remains temporarily as a phased implementation bridge to the authoritative `DocumentSelectionState`. It is not a backward-compatibility feature and must be removed in Task 06 after all consumers have migrated.

## Compatibility Policy

No backward compatibility is required for previous Oasis asset schemas, old selection implementations, or old lock implementations.

This is a solo project with no customer asset base. Existing old layouts are temporary test data and may stop loading.

Therefore:

- Do not preserve the old `IsLocked` property or its serialized representation.
- Do not add schema migration code.
- Do not add compatibility aliases.
- Do not add fallback deserializers.
- Do not retain old selection APIs for persisted compatibility.
- Do not add tests whose purpose is reopening assets written by previous Oasis versions.
- Remove obsolete code rather than building migration layers around it.

The only lock concept should be:

- Runtime/model property: `IsTransformLocked`
- Inspector label: `Lock Transform`

Temporary bridges used solely to stage this six-task refactor may remain only until their planned removal task.

## Core Architectural Decision

There is one authoritative document-scoped selection state.

Hierarchy, Inspector, Panel2D, and Face must all read and update the same `DocumentSelectionState`. Do not create separate authoritative selection collections for different UI surfaces.

Conceptually:

```csharp
public sealed class DocumentSelectionState
{
    public IReadOnlyList<EditorSelectionItem> Items { get; }
    public EditorSelectionItem? PrimaryItem { get; }
    public EditorSelectionItem? AnchorItem { get; }
}
```

The exact API should follow repository conventions.

## Selection Identity

Selection is identity-based rather than geometry-based.

Conceptually:

```csharp
public readonly record struct EditorSelectionItem(
    EditorSelectionDomain Domain,
    string ObjectId);
```

Possible domains include Panel elements, Face elements, Panel Face Source Shapes, and Face mask layers.

Do not store copied X, Y, Width, or Height values as authoritative selection state. Resolve the current model whenever geometry or properties are needed.

## Primary Item and Hierarchy Anchor

The primary item is normally the last item clicked or focused. It drives Inspector context, hierarchy focus, primary rendering, and single-item resize handles.

The hierarchy anchor is tracked separately for Shift-range selection. It may often match the primary item but is not the same concept.

## Document Scope and Reconciliation

Each open document owns its own selection state. Switching tabs restores that document's selection.

After document mutations:

- remove identities whose objects no longer exist
- preserve surviving selections
- preserve the primary item when possible
- choose a deterministic replacement primary item when required
- reconcile the hierarchy anchor when required

## Required Selection Operations

Selection changes should use explicit operations rather than scattered collection mutation:

- replace
- add one or many
- remove
- toggle
- clear
- set primary
- set hierarchy anchor
- reconcile

Every meaningful selection change must raise a dedicated notification consumed by relevant UI surfaces.

## Viewport Selection Behaviour

### Click

- Clicking an unselected component replaces the selection.
- Clicking an already selected component preserves the complete selection.
- Ctrl-clicking an unselected component adds it and makes it primary.
- Ctrl-clicking a selected component removes only that component.
- Clicking empty space without Ctrl clears selection.
- Ctrl-clicking empty space preserves selection.

Preserving the full selection when clicking an already selected component allows group movement to begin without collapsing the group.

### Rectangle Selection

- Rectangle without Ctrl replaces the selection.
- Ctrl+rectangle adds matching components.
- Ctrl+rectangle never toggles or removes existing components.
- Shift viewport modifiers are deferred.
- Initially select components whose bounds are fully enclosed by the rectangle.

Special non-rectangular objects such as Panel Face Source Shapes may retain specialised selection behaviour unless a task explicitly includes them.

### Gesture Priority

Resolve pointer interaction in this order:

1. Special editing handle, such as a Face Source Shape corner
2. Single-selection resize handle
3. Drag on a selected transform-unlocked component
4. Potential click selection
5. Rectangle selection after the drag threshold

## Multi-Selection Rendering

- Draw an outline around every selected component.
- Give the primary item a stronger treatment.
- Show resize handles only when exactly one eligible transform-unlocked component is selected.
- Do not implement group resizing.
- Hierarchy-driven selection changes must immediately invalidate Panel2D and Face viewports.

## Group Movement

Dragging any selected transform-unlocked component moves all selected transform-unlocked components in that viewport by the same document-space delta.

Requirements:

- capture immutable originals at drag start
- calculate every preview from the original state
- never accumulate movement from already preview-mutated models
- commit one atomic undoable command
- restore every previewed model on cancellation or lost capture
- keep locked items selected but stationary
- do not begin group movement when the drag starts on a selected locked item

Shared computation and bulk mutation infrastructure should be reusable by Panel2D and Face.

## Transform Lock

The old `IsLocked` concept and storage may be removed completely.

`IsTransformLocked` means only that viewport transforms are blocked:

- the component remains selectable
- the component remains inspectable
- non-transform properties remain editable
- viewport move and resize are blocked
- Inspector transform editing is disabled or rejected according to multi-edit rules

Newly created or imported background components should default to transform locked where appropriate.

Do not preserve or migrate old `IsLocked` serialized data.

## Hierarchy Behaviour

The WPF `TreeView` native single-selection state is not authoritative. Multi-selection must be represented through `DocumentSelectionState` and row visual state.

Required behaviour:

- click replaces selection
- Ctrl-click toggles one component
- Shift-click selects the visible continuous range from the anchor
- Ctrl+Shift-click adds that visible range
- collapsed descendants are excluded
- group nodes are not component selections in the initial implementation
- viewport-originated changes update all corresponding hierarchy row visuals

Native `TreeViewItem.IsSelected` may remain only for keyboard focus or navigation if useful.

## Inspector Multi-Edit

Inspector properties have explicit common, mixed, or unavailable states.

Do not store a display dash as a value. Boolean mixed values use an indeterminate three-state checkbox.

For same-type selections, show common/base and meaningful type-specific properties. For mixed-type selections, initially expose only safe common properties such as X, Y, Width, Height, Visible, and Lock Transform.

Transform edits are absolute assignments. One multi-object edit creates one undo entry.

## Bulk Delete

Delete operates on all selected deletable components through one shared command path.

- one action removes the selected set
- one undo restores the complete set and deterministic ordering
- unsupported selection domains are handled explicitly
- selection is reconciled after deletion
- Hierarchy and viewport Delete paths use the same command

## Shared Logic and View-Specific Logic

Share domain logic for selection operations, modifier interpretation, rectangle matching, group movement, bulk mutations, Inspector aggregation, and outline styling where practical.

Do not force Panel2D and Face controls into one inheritance hierarchy. Keep view-specific event adapters thin and route them into shared services.

## Undo and Preview Requirements

One user action creates one undo entry for group movement, multi-object Inspector editing, or bulk deletion.

Preview mutations must never create command-history entries.

## Deferred Work

Do not include:

- group resize or scale
- alignment and distribution
- duplicate or copy/paste
- context-menu redesign
- Shift selection in Panel2D or Face viewports
- rectangle toggle behaviour
- relative Inspector expressions
- hierarchy group-node component selection
- cross-document selection

## Testing Expectations

Add focused tests for selection operations, primary and anchor behaviour, reconciliation, rectangle matching, modifier interpretation, group movement, transform-lock exclusion, Inspector aggregation, atomic mutation commands, atomic deletion, hierarchy visible ranges, document isolation, cross-surface synchronization, and Panel2D/Face parity.

Do not add compatibility or migration tests for obsolete Oasis asset schemas or `IsLocked` storage.

## Delivery Process

For each task:

1. Read this context file and the current numbered task.
2. Inspect the current implementation before changing code.
3. Keep the change focused on that task.
4. Run relevant targeted tests and the full suite where practical.
5. Report files changed, tests run, deviations, risks, and manual checks.
6. Stop for user testing and review before starting the next task.
