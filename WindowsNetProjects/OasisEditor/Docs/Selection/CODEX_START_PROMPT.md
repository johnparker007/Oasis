# Codex Start Prompt - Multi-Selection Refactor

You are working in the connected Oasis repository.

Repository:

`johnparker007/Oasis`

Focus area:

`WindowsNetProjects/OasisEditor`

## First Step

Read these files in full:

- `WindowsNetProjects/OasisEditor/Docs/Selection/MULTI_SELECTION_CONTEXT.md`
- `WindowsNetProjects/OasisEditor/Docs/Selection/TASK_01_SELECTION_FOUNDATION_AND_TRANSFORM_LOCK.md`

Then inspect the current implementation, especially:

- `OasisEditor/ViewModels/DocumentTabViewModel.cs`
- `OasisEditor/ActiveDocumentContextService.cs`
- `OasisEditor/ViewModels/HierarchyViewModel.cs`
- `OasisEditor/Views/HierarchyView.xaml`
- `OasisEditor/Views/HierarchyView.xaml.cs`
- `OasisEditor/ViewModels/InspectorViewModel.cs`
- `OasisEditor/Views/SkiaPanel2DEditView.xaml.cs`
- `OasisEditor/Views/SkiaFaceEditView.xaml.cs`
- `OasisEditor/Panel2DSelectionNotificationService.cs`
- `OasisEditor/CanvasMutationCommands.cs`
- `OasisEditor/FaceMutationCommands.cs`
- relevant selection, preview mutation, serialization, and test files discovered from those entry points

## Task To Start

Implement only:

`TASK_01_SELECTION_FOUNDATION_AND_TRANSFORM_LOCK.md`

Do not begin Task 02 or later tasks in the same PR.

## Core Requirements

- Replace the singular-selection foundation with a document-local ordered selection state capable of holding multiple stable object identities.
- Track selected items, a primary item, and a hierarchy range anchor separately.
- Keep compatibility shims only where needed to avoid unnecessary Task 01 churn.
- Make selection changes explicitly observable so Panel2D and Face canvases redraw when hierarchy-driven selection changes.
- Fix the current Panel2D hierarchy-selection outline refresh regression.
- Remove the old behaviour where `IsLocked` prevents hit-testing or selection.
- Replace the old ambiguous lock with clearly named transform-lock semantics.
- Transform-locked components must remain selectable and Inspector-visible.
- Transform lock must govern moving/resizing only; it must not make components unselectable.
- Newly created or imported background components should default to transform locked where appropriate.
- Preserve existing serialized assets safely. Do not silently reinterpret arbitrary existing locked elements without an explicit compatibility strategy.
- Keep Panel2D, Face, hierarchy, and Inspector single-selection behaviour working while establishing the new foundation.
- Keep selection document-local across tab switching.

## Constraints

- Do not implement viewport Ctrl-click or rectangle multiselect yet.
- Do not implement hierarchy Ctrl/Shift multiselect yet.
- Do not implement Inspector mixed values yet.
- Do not implement group move or group resize yet.
- Do not implement bulk delete yet.
- Do not redesign context menus.
- Do not make Face Source Shapes ordinary component multiselections.
- Do not use copied X/Y/Width/Height values as selection identity.
- Avoid reflection-driven Inspector or model mutation systems.
- Keep changes small enough to review and diagnose.
- Preserve undo/redo behaviour.
- Keep all existing tests passing and add focused tests for the new state and lock semantics.

## Expected Design Direction

Use stable selection identity such as domain/kind plus object ID. The exact type names may vary to fit repository conventions, but the architecture must provide the equivalent of:

- ordered selected item identities
- primary selected identity
- hierarchy anchor identity
- replace/add/remove/toggle/clear operations
- pruning of identities whose objects no longer exist
- one selection-changed notification path consumed by hierarchy, Inspector, and both canvases

Do not let views maintain independent authoritative selection collections.

## Verification

Run the relevant OasisEditor build and test suite available in the repository.

Add tests covering at least:

- replace/add/remove/toggle/clear selection state operations
- primary and anchor behaviour
- document-local selection retention
- stale-selection pruning
- transform-locked items remain selectable
- transform-locked items are rejected by move/resize eligibility
- legacy selection compatibility, if retained
- Panel2D redraw notification when selection changes from the hierarchy path

## Completion Report

When Task 01 is complete, report:

- files added
- files modified
- tests added or updated
- commands/tests run and results
- old `IsLocked` behaviour removed
- transform-lock compatibility or migration decisions
- any deviations from the task document
- manual checks the user should perform
- confirmation that Task 02 was not started

Stop after Task 01 so the user can test it and the PR can be reviewed before further work.