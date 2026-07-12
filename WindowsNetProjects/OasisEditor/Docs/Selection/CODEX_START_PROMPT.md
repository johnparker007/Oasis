# Codex Start Prompt - Task 02 Panel2D Multi-Selection and Group Move

You are working in the connected Oasis repository.

Repository:

`johnparker007/Oasis`

Focus area:

`WindowsNetProjects/OasisEditor`

## Current State

Task 01 has already been implemented and merged.

The current architecture includes:

- `DocumentSelectionState` as the authoritative document-scoped selection state
- `EditorSelectionItem` identity-based selection entries
- ordered selected items
- a primary selected item
- a hierarchy range anchor
- selection-change notifications
- selection reconciliation
- transform-lock semantics

A temporary bridge from `HierarchySelectedPanelSelection` to `DocumentSelectionState` may still exist. This is a phased implementation bridge only. Do not remove it during Task 02 unless its removal is strictly necessary and does not expand scope. Its planned final removal is Task 06.

## Compatibility Direction

There is intentionally no backward compatibility requirement for previous Oasis asset schemas, old selection implementations, or the old lock implementation.

Do not:

- preserve old `IsLocked` serialization
- add schema migrations
- add compatibility aliases
- add fallback deserializers
- add tests for reopening old Oasis assets
- introduce any new migration or compatibility layer

The only intended lock concept is:

- runtime/model property: `IsTransformLocked`
- Inspector label: `Lock Transform`

Remove obsolete code when it is directly encountered within Task 02 scope rather than wrapping it in compatibility logic.

## First Step

Read these files in full:

- `WindowsNetProjects/OasisEditor/Docs/Selection/MULTI_SELECTION_CONTEXT.md`
- `WindowsNetProjects/OasisEditor/Docs/Selection/TASK_02_PANEL2D_MULTI_SELECTION_AND_GROUP_MOVE.md`
- `WindowsNetProjects/OasisEditor/Docs/Selection/TASK_01_SELECTION_FOUNDATION_AND_TRANSFORM_LOCK.md`

Then inspect the merged Task 01 implementation and current Panel2D interaction paths, especially:

- `WindowsNetProjects/OasisEditor/OasisEditor/DocumentSelectionState.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/ViewModels/DocumentTabViewModel.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/Views/SkiaPanel2DEditView.xaml.cs`
- Panel2D hit-testing services
- rectangle-selection services
- resize-handle services
- preview mutation services
- `CanvasMutationCommands.cs`
- transform-lock eligibility services
- selection rendering helpers
- relevant OasisEditor tests discovered from those entry points

Inspect the current implementation before deciding exact type and service names. Reuse established patterns where practical.

## Task

Implement only:

`TASK_02_PANEL2D_MULTI_SELECTION_AND_GROUP_MOVE.md`

Do not begin Task 03 or any later task.

Stop after Task 02 so the user can review and manually test it before Face parity work starts.

## Required Panel2D Behaviour

### Click Selection

- Clicking an unselected Panel2D element replaces the selection.
- Clicking an already selected element preserves the complete selection so a group drag can begin.
- Ctrl-clicking an unselected element adds it and makes it primary.
- Ctrl-clicking a selected element removes only that element.
- Clicking empty space without Ctrl clears selection.
- Ctrl-clicking empty space preserves selection.

Use `DocumentSelectionState` for all selection mutations. Do not create a second authoritative collection in the view.

### Rectangle Selection

- Rectangle without Ctrl replaces selection with all eligible enclosed Panel2D elements.
- Ctrl+rectangle adds enclosed eligible elements.
- Ctrl+rectangle never removes or toggles existing selections.
- Use full-enclosure bounds matching.
- Do not implement Shift viewport behaviour.
- Do not make Panel Face Source Shapes ordinary rectangular component selections unless the current specialised architecture explicitly requires it.

### Gesture Priority

Preserve this order:

1. Panel Face Source Shape corner handle
2. single-selection resize handle
3. drag on a selected transform-unlocked element
4. click candidate
5. rectangle selection after the drag threshold

A selected transform-locked element must not start group movement. Dragging over a selected locked background must still permit rectangle selection to start.

### Rendering

- Draw an outline for every selected Panel2D element.
- Give the primary item a clearly stronger outline treatment.
- Show resize handles only when exactly one eligible transform-unlocked resizable Panel2D element is selected.
- Do not implement group resize.
- Preserve Panel Face Source Shape visuals and corner handles.

### Group Movement

Dragging any selected transform-unlocked Panel2D element moves every selected transform-unlocked Panel2D element by the same document-space delta.

Requirements:

- capture immutable original models or geometry at drag start
- compute every preview from the original drag-start state
- never accumulate deltas from already preview-mutated models
- keep selected transform-locked items selected but stationary
- restore every previewed item on cancellation or lost capture
- commit the complete move as one atomic undoable command
- one Undo restores the entire group
- one Redo reapplies the entire group move
- avoid one command per selected item

Extract shared move computation and bulk mutation infrastructure suitable for Task 03 Face reuse, but do not implement Face behaviour in this task.

## Scope Constraints

Do not implement:

- Face viewport multi-selection or group movement
- Hierarchy Ctrl/Shift multi-selection
- Inspector multi-edit or mixed values
- bulk delete
- group resize
- context-menu redesign
- viewport Shift selection
- alignment/distribution
- duplicate or copy/paste
- removal of the temporary hierarchy-selection bridge as a project-wide cleanup
- old-schema migration or serialization compatibility

Keep existing single-item resize, pan, zoom, hit-test cycling, and specialised Panel Face Source Shape editing working.

## Tests

Add focused tests for at least:

- click replace behaviour
- click on an already selected item preserving the group
- Ctrl-click add and remove
- empty-space clear and Ctrl-preserve behaviour
- rectangle replace
- Ctrl+rectangle additive behaviour
- full-enclosure rectangle matching
- locked/background drag eligibility
- group move delta computation
- exclusion of selected locked members from movement
- preview cancellation and lost-capture restoration
- one atomic undo/redo command for the complete move
- primary selection rendering logic where practical
- resize-handle eligibility for exactly one unlocked item

Update tests to use `DocumentSelectionState` as authoritative.

Do not add compatibility tests for old `IsLocked` storage or old Oasis schemas.

Follow `WindowsNetProjects/OasisEditor/AGENTS.md`. Run the checks permitted by the environment. If the Windows/.NET/WPF toolchain is unavailable or prohibited, do not claim tests were executed; report the exact tests added and the local commands the user should run.

## Completion Report

When Task 02 is complete, report:

- files added and modified
- selection interaction changes
- shared services or command infrastructure introduced for later Face reuse
- rendering changes
- tests added or updated
- commands and tests actually run, with results
- manual checks the user should perform
- deviations, risks, or known limitations
- confirmation that no migration or old serialization compatibility layer was introduced
- confirmation that Task 03 was not started

Stop after Task 02 for user review and testing.
