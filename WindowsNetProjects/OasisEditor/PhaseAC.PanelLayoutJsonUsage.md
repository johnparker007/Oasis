# Phase AC — PanelLayoutJson Live-Trigger Removal Verification

Date: 2026-04-29

This note records the code-level verification for `TASKS.md` Phase AC.

## Findings

- `MainWindowViewModel.OnSelectedDocumentPropertyChanged(...)` no longer responds to `PanelLayoutJson` changes.
  - It now handles only `HierarchySelectedPanelSelection` updates.
  - Hierarchy/Inspector refreshes for panel edits are driven by `OnSelectedDocumentPanelChanged(PanelChangeEvent)`.
- `DocumentTabViewModel.PanelChanged` is the active fanout mechanism for panel edit notifications.
- Remaining `PanelLayoutJson` change notifications are confined to document/canvas projection synchronization:
  - `DocumentTabViewModel.SetPanelElements(...)` updates projection JSON and raises `PropertyChanged(nameof(PanelLayoutJson))`.
  - `DocumentTabViewModel.PanelLayoutJson` setter updates model projection when external JSON is applied.
  - `CanvasPanBehavior` and `PanelLayoutMapper` use `PanelLayoutJson` for canvas/layout persistence mapping.

## Phase AC conclusion

`PanelLayoutJson` is no longer used as the MainWindow-level live trigger for inspector/hierarchy updates. Those pane updates are now scoped through `PanelChangeEvent` metadata.

## Local verification John should run

- Edit X/Y/Visible and confirm no full hierarchy/inspector rebuild occurs from a JSON property change path.
- Confirm save/load persistence still works and panel visuals remain synchronized with model edits.
