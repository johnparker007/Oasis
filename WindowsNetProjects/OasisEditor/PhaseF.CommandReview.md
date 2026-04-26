# Phase F Command Surface Review

Review date: 2026-04-26

This is a behavior-preserving review pass for Task **Phase F / Review current hierarchy keyboard handlers and asset double-click handlers**.

## Hierarchy keyboard handlers (current state)

### Existing command paths

- **Delete** is wired in `HierarchyView.xaml.cs` `OnTreeViewPreviewKeyDown` and routes to `MainWindowViewModel.DeleteSelectedHierarchyItem()`.
- **Rename (F2)** is wired in `HierarchyView.xaml.cs` `OnTreeViewPreviewKeyDown`, currently prompts via `Interaction.InputBox`, then routes to `MainWindowViewModel.RenameSelectedHierarchyItem(string)`.

### What is already implemented

- Rename command path exists for selected Panel2D hierarchy entity.
- Delete command path exists for selected Panel2D hierarchy entity.
- Both paths are routed through document canvas mutation commands (`CanvasMutationCommands`) via `MainWindowViewModel.ExecuteDocumentCanvasCommand(...)`.

### Confirmed command gaps

- **Cut**: not implemented in hierarchy keyboard handling.
- **Copy**: not implemented in hierarchy keyboard handling.
- **Paste**: not implemented in hierarchy keyboard handling.
- **Duplicate**: not implemented in hierarchy keyboard handling.

## Asset handlers (current state)

### Existing command paths

- **Open (double-click)** is wired in `AssetBrowserView.xaml.cs` `OnAssetListMouseDoubleClick`, which executes `MainWindowViewModel.OpenAssetCommand`.
- `OpenAssetCommand` is provided by `AssetBrowserViewModel.OpenAssetCommand` and delegates to the existing app-level open flow via callback.

### What is already implemented

- Asset file open on double-click is implemented.

### Confirmed command gaps

- **Show in Explorer**: not implemented.
- **Asset Rename**: not implemented.
- **Asset Delete**: not implemented.

## Notes for next implementation task

- Keep keyboard/menu/context-menu entry points routed through the same ViewModel methods.
- Keep view code-behind limited to right-click selection glue where required by WPF.
