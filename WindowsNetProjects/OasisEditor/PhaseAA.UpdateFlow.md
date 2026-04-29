# Phase AA — Inspector Edit Update Flow Trace

Date: 2026-04-29

This note captures the current (pre-refactor) flow requested by `TASKS.md` Phase AA.

## 1) Inspector row -> command creation

- `InspectorViewModel.RebuildPropertyRows()` builds editable rows with `commit` delegates.
- Each row commit calls `TryApplyUpdate(...)` with an update description and `PanelElementModelUpdate` payload.
- `TryApplyUpdate(...)` clones the selected model, applies updates, then creates an update command through `CanvasMutationCommands.CreateUpdateElementCommand(...)`.
- The command is executed via `_executeCanvasCommand(...)` to keep document scope/undo behavior.

## 2) Command execution -> document mutation

- `CanvasMutationCommands.UpdateElementMutationCommand.Execute()` resolves the target element by object ID, replaces it in a copied list, then calls `_document.SetPanelElements(elements)`.
- On successful mutation, it calls `_document.MarkDirty()` and marks `WasExecuted = true` for no-op history filtering.

## 3) Where `SetPanelElements` is called

- Nearly all Panel2D mutations route through `CanvasMutationCommands` and call `DocumentTabViewModel.SetPanelElements(...)`.
- This includes add/delete/rename/duplicate/paste/reorder/lock/visibility/update flows.
- MFME import mutation command also calls `SetPanelElements(...)`.

## 4) Where `PanelLayoutJson` is regenerated

- `DocumentTabViewModel.SetPanelElements(...)` assigns `_panelDocumentModel` and immediately regenerates JSON with `GetPanelLayoutProjectionJson()`.
- `PanelLayoutMapper.SyncPanelLayout(...)` also serializes layout and writes `PanelLayoutJson` back to the document.
- `PanelLayoutMapper.ApplyPersistedLayout(...)` can serialize and push normalized layout back to `PanelLayoutJson` after applying canvas visuals.

## 5) Where PropertyChanged events fire

- `DocumentTabViewModel.SetPanelElements(...)` raises `PropertyChanged(nameof(PanelLayoutJson))` after every mutation.
- Setting `DocumentTabViewModel.PanelLayoutJson` also rebuilds `_panelDocumentModel` and raises `PropertyChanged(nameof(PanelLayoutJson))`.

## 6) Where MainWindowViewModel reacts

- `MainWindowViewModel.OnSelectedDocumentPropertyChanged(...)` listens for `PanelLayoutJson` changes.
- On each `PanelLayoutJson` change it calls:
  - `RefreshHierarchy()`
  - `NotifyInspectorChanged()`

## 7) Where Hierarchy is refreshed

- `MainWindowViewModel.RefreshHierarchy()` calls `_hierarchy.Refresh()` and raises hierarchy-related property notifications.
- This is currently triggered from `OnSelectedDocumentPropertyChanged` on every `PanelLayoutJson` change.

## 8) Where Inspector rows are rebuilt

- `MainWindowViewModel.NotifyInspectorChanged()` calls `_inspector.NotifyContextChanged()`.
- `InspectorViewModel.NotifyContextChanged()` calls `RebuildPropertyRows()` unconditionally.
- Therefore, any `PanelLayoutJson`-driven refresh currently rebuilds all inspector rows.

## 9) Color picker path confirmation

- Color-related rows (`On Color`, `Off Color`, `Text Color`) are created in `InspectorViewModel.AddTypeSpecificRows(...)` and commit through the same `TryApplyUpdate(...)` path.
- This means color edits currently use the same `SetPanelElements -> PanelLayoutJson PropertyChanged -> RefreshHierarchy + RebuildPropertyRows` fanout.
- High-frequency color updates therefore amplify the same broad refresh path.

## Local verification John should run

- Open a `.panel2d` file and edit X/Y/Visible once; verify current behavior still follows the traced path.
- Drag color values quickly and confirm hierarchy/inspector refresh frequency matches this baseline trace before refactor.
