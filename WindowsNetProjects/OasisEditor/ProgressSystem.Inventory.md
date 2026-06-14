# Progress System Inventory

This inventory covers Step 1 of `MODAL_PROGRESS_SYSTEM_PLAN.md`: identify current long-running editor operations, likely progress hooks, progress mode recommendations, and threading risks before implementing any UI or service code.

## Summary recommendations

- Add UI-independent progress abstractions first, then thread the reporter through feature services without creating WPF dependencies in domain-style services.
- Treat the first integration pass as mostly non-cancellable. Add cancellation only where the underlying operation can safely stop before mutating editor state.
- Prefer step-based determinate progress for operations that already have clear phases, even when exact byte/item counts are not available.
- Use indeterminate progress for initial document opening and Play View rendering/cache paths until those code paths expose meaningful counts.
- Do not move WPF-bound document/tab/view-model mutations off the UI thread in the first pass. Only file I/O, parsing, asset copying, and texture generation are candidates for incremental background work.

## Operation inventory

| Operation | Entry point(s) | Estimated progress stages | Recommendation | UI-thread/blocking notes |
| --- | --- | --- | --- | --- |
| MFME extract import | `MainWindowViewModel.ImportMfmeExtract()` opens the manifest dialog, calls `IMfmeExtractImportService.ImportFromExtract(...)`, then applies `ImportMfmeExtractCommand`; automation calls also use `MfmeExtractImportService` and `MfmeImportService.Import(...)`. | 1. Validate selected Panel2D and chosen manifest. 2. Read extract path / manifest. 3. Parse manifest JSON. 4. Map legacy components to Oasis elements. 5. Copy background/lamp/reel assets. 6. Bake display overlays/cutouts into backgrounds. 7. Apply imported elements command. 8. Update input definitions, metadata, asset browser, hierarchy and inspector. | Step-based determinate progress is realistic. Counts are available or can be derived for manifest components, mapped elements, copied asset paths, and element insertion. Start with coarse determinate stages, then add nested item counts in `MfmeImportAssetCopier`. | Current UI command path is synchronous and blocks the UI thread while `ImportFromExtract`, asset copying, image processing, metadata save, command execution, and refreshes run. Core import services are mostly UI-agnostic and good candidates for a reporter parameter and later background file work. Applying `ImportMfmeExtractCommand` and refreshing WPF state must remain on the UI thread. |
| MFME import command insertion | `ImportMfmeExtractCommand.Execute()` materializes imported elements, resolves IDs, inserts elements, sends display-like elements to the back, updates the active document and marks it dirty. | 1. Materialize imported elements. 2. Insert elements. 3. Reorder imported display/cutout elements. 4. Raise document change and mark dirty. | Determinate but very short; probably report as the final MFME import stage rather than show a separate operation. | Runs synchronously as a document command and mutates `DocumentTabViewModel`; keep on UI thread. Progress hook should be outside or at the command boundary unless command interfaces gain optional progress later. |
| Document loading/opening | `MainWindowViewModel.OpenDocument()` and asset browser open paths call `OpenDocumentFromPath(...)`; it reads the entire file, calls `DocumentWorkspaceViewModel.BuildOpenDocumentData(...)`, then `OpenOrSelectDocument(...)` creates/selects the tab. | 1. Read file. 2. Detect document type. 3. Parse/validate `.panel2d` or `.face` JSON. 4. Serialize normalized live content back into tab JSON. 5. Create/select document tab. 6. Render selected editor/Play View if visible. | Use indeterminate initially. A coarse 4-5 stage determinate flow is possible, but exact parsing/render cost is opaque and document size does not reliably map to progress. | Current implementation is fully synchronous and blocks UI during file read, JSON validation/deserialization, serialization, tab mutation, and first render invalidation. File read/parse could later move off UI thread; tab creation and selection must stay on UI thread. |
| Face generation from Panel2D region | `MainWindowViewModel.GenerateFaceFromRegion()` collects settings/dialog input, then `DocumentWorkspaceViewModel.GenerateFaceFromSelectedPanel2DRegion(...)` calls `FaceGenerationService.GenerateFromPanelRegion(...)`, exports runtime assets for preview, serializes the face, and opens a new document tab. | 1. Validate source document/region. 2. Create artwork elements. 3. Convert lamps. 4. Convert reels. 5. Convert seven-segment displays. 6. Convert alpha displays. 7. Create button/input elements. 8. Generate mask layer. 9. Auto-author trays/emitters. 10. Export runtime preview assets. 11. Serialize/open Face document. | Step-based determinate progress is realistic. Exact element counts can be known from the source region before conversion, and runtime export has clear sub-steps. | Current path runs synchronously on the UI thread after dialogs close. `FaceGenerationService` is mostly model/file oriented, but uses WPF `Rect` types and mask/runtime export writes files and images. New document tab mutation and output logging must stay on UI thread. |
| Face regeneration | `MainWindowViewModel.RegenerateSelectedFace()` handles settings, then `DocumentWorkspaceViewModel.RegenerateSelectedFace(...)` finds the source Panel2D, calls `FaceRegenerationService.Regenerate(...)`, exports preview runtime assets, replaces face document contents, marks dirty, and logs diagnostics. | 1. Validate selected Face. 2. Locate source Panel2D among open documents. 3. Validate provenance/source region. 4. Generate replacement Face from source region. 5. Correlate regenerated elements with existing generated elements. 6. Preserve manual elements and runtime identity. 7. Auto-author trays/emitters. 8. Export runtime preview assets. 9. Replace open Face document content and refresh. | Step-based determinate progress is realistic. The regeneration merge can report based on regenerated/existing element counts. | Current path is synchronous and UI-blocking. Source document lookup and document replacement are view-model work and should remain on UI thread. Generation, merge computation, and runtime texture export can receive progress hooks and may later be moved off the UI thread if all input models are snapshotted first. |
| Face runtime preview/export during generate, regenerate and save | `DocumentWorkspaceViewModel.ExportRuntimeAssetsForPreview(...)` calls `FaceRuntimeExportService.Export(...)`; `DocumentSaveService.SaveDocument(...)` also exports Face runtime assets before writing a `.face` document. | 1. Resolve runtime dimensions/output directory. 2. Export artwork. 3. Copy mask. 4. Create runtime texture plan. 5. Generate tray ID texture. 6. Generate lamp ID/weight textures and debug textures. 7. Write runtime manifest. 8. Update face runtime asset references. | Determinate by stages. Texture generation internals may also support determinate progress by tray/emitter/pixel region later, but first pass should use coarse stages. | Currently synchronous. Preview export is invoked from UI workflows and can block. Save also blocks during export and file write. This is a high-value hook because failures are already caught/logged in preview but not shown with progress. |
| Face Play View creation/loading/rendering | `MainWindowViewModel.OpenPlayView()` requests the tool window; `EditorShellView` opens/shows it; `PlayView.OnLoaded`, `OnDataContextChanged`, and selected-document changes call `RefreshCanvasFromSelection()` and `RequestRender()`; `OnPlaySkiaSurfacePaintSurface(...)` calls `Face2DRenderer.Render(...)` or `Panel2DRenderer.Render(...)`. | 1. Show/open Play View pane. 2. Subscribe to selected document/runtime state. 3. Queue first render. 4. Renderer loads/uses artwork, mask, reel and runtime asset references as needed. 5. Continue render throttling for updates. | Indeterminate initially. The current render path is event-driven and does not expose reliable asset/cache counts. If renderers later expose asset load/cache warmup phases, convert to coarse determinate. | Opening the pane is quick, but first render and asset decode/cache work likely occurs on the WPF/Skia paint path and can make the UI appear frozen. Avoid modal progress inside paint callbacks. Prefer a future explicit `PreparePlayViewAsync`/renderer cache warmup service before showing or before first render. |
| Save document, especially Face documents | Save command path uses `DocumentSaveService.SaveDocument(...)`; Face saves export runtime assets before `DocumentWorkspaceViewModel.BuildDocumentContent(...)` and `File.WriteAllText(...)`. | 1. For Face, export runtime assets. 2. Build document content. 3. Write file. 4. Replace tab metadata/clean state. | Determinate by stages for Face saves; indeterminate or no modal for small non-Face saves unless slow files are reported. | Synchronous. File and texture export work can block UI. Save metadata/tab replacement must stay on UI thread. This was not in the plan's first integration list but is an obvious long-running path because it shares the Face runtime export pipeline. |
| Project open/create and asset browser refresh | Launcher/project services load project metadata and scaffold folders; `_assetBrowser.RefreshAssetBrowser()` is called after MFME import and likely scans project assets. | 1. Validate/read project metadata. 2. Ensure directories. 3. Scan assets. 4. Build/update browser items. | Indeterminate initially for project open and asset scanning unless item counts are introduced. | Not part of the first progress integration list, but asset refresh after MFME import can compound perceived stalls. Keep as later-phase scope unless users report project open stalls. |
| MAME/emulator setup and ROM/plugin work | `MameEmulationService`, plugin deployment, ROM download/setup validation and process start paths may perform file/network/process work. | Varies: validate setup, deploy plugin files, download/copy ROMs, start process, wait for debugger/plugin readiness. | Indeterminate for process waits; determinate for file copies/download bytes if exposed. | Some paths are async already, but UI commands can still await or react on UI. This should remain outside the first modal progress pass unless it conflicts with editor actions. |

## Proposed API/service design

### Placement

- Put UI-independent contracts/models in the main editor project namespace or a small `Progress` folder that has no WPF view dependency.
- Put WPF dialog/overlay implementation in the Shell/WPF layer later.
- Feature services should depend only on reporter abstractions, not on dialog types.

### Proposed contracts

```csharp
public interface IProgressDialogService
{
    bool IsOperationActive { get; }

    Task RunAsync(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsync<TResult>(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record EditorProgressRequest(
    string Title,
    string InitialMessage = "",
    EditorProgressMode InitialMode = EditorProgressMode.Indeterminate,
    bool CanCancel = false,
    TimeSpan? ShowDelay = null,
    TimeSpan? MinimumDisplayDuration = null);
```

```csharp
public enum EditorProgressMode
{
    Indeterminate,
    Determinate
}
```

```csharp
public sealed record EditorProgressState(
    string Title,
    string Message,
    EditorProgressMode Mode,
    double? Value,
    bool CanCancel,
    bool IsCancelling,
    string? ErrorMessage = null);
```

```csharp
public interface IEditorProgressReporter
{
    void Report(double normalizedProgress, string message);
    void ReportIndeterminate(string message);
    void ReportMessage(string message);
    IEditorProgressReporter CreateChild(double start, double end, string? defaultMessagePrefix = null);
}
```

```csharp
public interface IEditorProgressScope : IAsyncDisposable
{
    EditorProgressRequest Request { get; }
    IEditorProgressReporter Reporter { get; }
    CancellationToken CancellationToken { get; }
    Task SetErrorAsync(string message);
}
```

### Design notes

- `IProgressDialogService` owns modal lifecycle, nested-operation policy, UI-thread marshalling, and closing on success/failure.
- `IEditorProgressReporter` is the only dependency feature code should need. It supports determinate, indeterminate, and message-only updates.
- `CreateChild(start, end, ...)` lets callers map a sub-operation into a range without every lower-level service knowing global percentages.
- Clamp determinate progress to `[0, 1]` in the reporter/state layer.
- Start with non-cancellable requests. Only set `CanCancel = true` when the operation accepts and honors a `CancellationToken` before destructive mutations.
- Reject nested modal operations initially with a clear exception/log message; queuing can be added later if needed.
- Provide a no-op reporter/service for tests, automation, and paths where UI progress is not available.

## Step 2 implementation plan

1. Add UI-independent progress abstractions (`EditorProgressMode`, `EditorProgressRequest`, `EditorProgressState`, `IEditorProgressReporter`, `IProgressDialogService`, optional `IEditorProgressScope`).
2. Add a small default/no-op progress reporter and service for tests/headless automation.
3. Add unit tests for request defaults, progress clamping, indeterminate reporting, message-only reporting, child reporter range mapping, and no-op service success/failure behavior.
4. Do not add WPF UI in Step 2.
5. Do not wire feature commands to a modal in Step 2, except optional constructor parameters/default no-op reporters if needed to keep later integration low-risk.
6. Prepare future integration seams by preferring optional `IEditorProgressReporter? progress = null` parameters on feature services rather than referencing WPF services directly.

## Face progress integration note

- Face UI modal integration is intentionally deferred because the previous direct WPF modal wiring around Face generation/regeneration caused a fatal `PresentationFramework` `InvalidOperationException` after confirming the Face Generation dialog.
- The safe current step is limited to UI-independent, service-level progress hooks for Face generation, regeneration, and runtime export. These hooks must not reference WPF dialog/service types and must not change `MainWindowViewModel` behavior.
- Later UI integration should first split background-safe model/export work from UI-thread document/tab/view-model mutation, then connect a modal progress surface only around the safe portions of the workflow.

## Status-bar progress integration note

- The first visible progress integration uses the main shell status bar rather than the WPF modal progress dialog for workflows that can safely report UI progress without blocking document mutation behind a modal.
- MFME extract import now shows status-bar progress while the extract/import service runs off the UI thread, then applies document/project mutations back on the UI thread.
- Play View opening shows a short indeterminate status-bar progress indicator while the pane is requested. First-render/cache progress remains a later task because renderer preparation does not yet expose safe progress hooks.
- Face generation/regeneration modal progress remains deferred; those workflows should not be wrapped in the modal progress service until model/export work is split from UI-thread document and tab mutation.
