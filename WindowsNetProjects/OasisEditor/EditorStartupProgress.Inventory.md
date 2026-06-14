# Editor Startup Progress Inventory

This is the Step 1 inventory for `EDITOR_STARTUP_PROGRESS_PLAN.md`. It documents the current Launcher -> Editor startup path and proposes a concrete flow for a later implementation. It intentionally does **not** change runtime behavior.

## Scope reviewed

- `LauncherWindowViewModel.OpenEditor`
- `MainWindow` constructor
- `MainWindow.OnLoaded`
- `MainWindowViewModel` construction
- startup project loading
- document loading paths
- shell/tool-window creation
- first render and cache warmup paths

## Current Launcher -> Editor sequence

### High-level sequence today

```text
Launcher command
  -> LauncherWindowViewModel.OpenEditor(projectFilePath)
  -> validate project file by opening/parsing .oasisproj
  -> new MainWindow(..., projectFilePath)
       -> InitializeComponent()
       -> new EditorShellView()
       -> put shell in EditorShellHost
       -> register Loaded/Closing handlers and keyboard bindings
       -> new MainWindowViewModel(..., projectFilePath)
            -> create commands/services/child view models
            -> load preferences
            -> initialize MAME/debugger/emulation services
            -> create OpenDocuments collection and document workspace
            -> load startup project metadata
            -> refresh asset browser / diagnostics / hierarchy
            -> kick off async MAME preference validation
       -> subscribe tool-window events
       -> assign DataContext
       -> add command bindings
  -> launcher.Hide()
  -> mainWindow.Show()
  -> WPF raises Loaded/visual events
       -> MainWindow.OnLoaded applies saved placement
       -> EditorShellView.OnLoaded configures docking behavior
       -> EditorShellView rebuilds document tabs from OpenDocuments
       -> selected document views render if any documents are open
```

## What executes before `MainWindow.Show()`

### Launcher validation and construction

`OpenEditor` trims the project path, validates it, constructs `MainWindow`, hides the Launcher, attaches a `Closed` handler, and then calls `Show()`. Validation opens and parses the project JSON and verifies the `.oasisproj` extension and required `name` field.

Current pre-show work includes:

- `ValidateProjectFile(trimmed)`:
  - `File.Exists`
  - extension check
  - `File.OpenRead`
  - `JsonDocument.Parse`
  - required `name` validation
- `new MainWindow(...)`
- `_launcherWindow.Hide()` immediately before `mainWindow.Show()`

### MainWindow constructor

The constructor does substantial UI and view-model work before `Show()`:

- validates that a startup project path was provided;
- normalizes the startup project path for window placement persistence;
- calls `InitializeComponent()`, creating the main WPF visual tree declared in `MainWindow.xaml`;
- constructs `EditorShellView`;
- assigns the shell to `EditorShellHost.Content`;
- attaches `Loaded` and `Closing` handlers;
- registers keyboard shortcuts;
- constructs `MainWindowViewModel`;
- subscribes tool-window open/close events;
- assigns `DataContext`;
- adds undo/redo command bindings.

### EditorShellView constructor

`EditorShellView` also performs UI work before `Show()` because it is created in the `MainWindow` constructor:

- calls `InitializeComponent()`, creating the AvalonDock layout and its tool-window contents;
- attaches `Loaded`;
- immediately hides Preferences, Project Settings, Input Map, Play View, and debugger tool windows.

The XAML constructs visible startup panes for Hierarchy, Inspector, Assets, and Output, plus hidden but declared panes for Input Map, Play View, Project Settings, debugger panes, etc.

### MainWindowViewModel constructor

`MainWindowViewModel` performs most startup application work synchronously before `Show()`:

- creates the editor progress dialog service bound to the owner window dispatcher;
- creates all command objects;
- creates output log, active document context, machine runtime state store, asset browser, inspector, hierarchy, hierarchy commands, and document workspace;
- loads preferences synchronously from `EditorPreferencesStore`;
- applies preference values to many view-model fields;
- configures MAME download/catalog/validation services;
- constructs MAME stdout parsers/adapters, process runner, debugger service, debugger shell, and emulation service;
- attaches debugger and emulation event handlers;
- optionally syncs configured MAME version to the latest installed version;
- loads recent projects;
- creates `OpenDocuments`;
- exposes asset browser/output collections and commands;
- adds initial output entries;
- calls `LoadStartupProject(startupProjectFilePath.Trim())`;
- calls `RefreshHierarchy()`;
- starts `_ = ValidateMamePreferencesAsync()` without awaiting it.

### Startup project loading

`LoadStartupProject` is synchronous and runs before `Show()` as part of `MainWindowViewModel` construction. It:

- calls `LoadProjectFromFile`;
- assigns `LoadedProject`;
- copies project platform / ROM preferences into the view model;
- refreshes MAME ROM status;
- sets `PanelElementFactory.ProjectDirectoryPath`;
- sets `ProjectFilePath`;
- updates recent projects;
- refreshes the asset browser;
- writes status/output messages;
- refreshes input-map diagnostics.

`LoadProjectFromFile` performs synchronous file and JSON work:

- validates existence and extension;
- opens and parses the project JSON;
- reads `name`, `layout`, platform, ROM settings, and input definitions;
- resolves project directories;
- constructs `EditorProject`.

### Startup document loading

There does not appear to be automatic persisted document reopening in the reviewed startup path. `OpenDocuments` starts as an empty collection, and startup project loading does not call `OpenDocumentFromPath` or `DocumentWorkspaceViewModel.OpenOrSelectDocument`.

Document loading does exist for explicit user actions after the editor is running:

- `OpenDocumentFromPath` synchronously reads the whole file with `File.ReadAllText`;
- `DocumentWorkspaceViewModel.BuildOpenDocumentData` parses `.panel2d` and `.face` content, validates it, serializes model data back into the document tab format, and creates open-tab data;
- `OpenOrSelectDocument` creates/selects a `DocumentTabViewModel` and mutates `OpenDocuments`.

If a future startup feature reopens last documents, this path would become startup-critical and should be included in progress reporting.

## What executes after `MainWindow.Show()`

### MainWindow loaded handling

After `Show()`, WPF loads the window. `MainWindow.OnLoaded` calls `ApplyWindowPlacement()`, which:

- loads preferences again;
- looks up persisted bounds by normalized startup project path;
- applies width/height/left/top/window state, or maximizes by default.

Because this runs in `Loaded`, the first visible layout may begin with the XAML default size/state and then change to saved/maximized placement.

### EditorShellView loaded handling

`EditorShellView.OnLoaded` runs after the shell is loaded. It:

- configures several tool windows to hide instead of closing;
- checks for a `MainWindowViewModel` data context;
- calls `RebuildDocuments(viewModel)`;
- subscribes to `OpenDocuments.CollectionChanged` and `MainWindowViewModel.PropertyChanged` for selected-document activation.

`RebuildDocuments` clears `DocumentPane.Children`, creates `LayoutDocument` instances for each open document, assigns a new `DocumentEditorView` for each document, wires selection/close handlers, adds each layout document to AvalonDock, and activates the selected document.

With the current startup path, `OpenDocuments` is empty, so the document pane remains empty at first show. If startup later opens documents before shell `Loaded`, this is where document views would be created and first rendered.

### First selected document render and cache warmup

For explicit document opens after startup, `DocumentEditorView` creates the editor view for the selected document:

- `SkiaPanel2DEditView` requests a render on `Loaded` and queues rendering at dispatcher render priority.
- `SkiaFaceEditView` follows a similar loaded/render-queued pattern and loads/caches artwork images when rendering.
- `PlayView` is hidden during normal startup, but when shown and a Face document is selected it waits for active progress operations, opens an indeterminate progress operation, yields at application-idle priority, and calls `WarmFacePlayViewRenderCache` before rendering.

No reviewed code shows automatic Panel2D/Face render cache warmup during the initial Launcher -> Editor startup while no document is open.

### Async work started during construction

`MainWindowViewModel` starts `ValidateMamePreferencesAsync()` fire-and-forget before `Show()`. The task begins from the constructor, but its continuation and UI updates can overlap the first visible window render after `Show()`.

## Likely causes of visible white/blank editor regions

The reviewed code points to several likely contributors:

1. **The Launcher is hidden before the editor has completed first layout/render.** The user transitions directly from Launcher to a newly shown WPF/AvalonDock window that still needs to load, measure, arrange, apply placement, and render.

2. **Heavy synchronous UI construction happens on the UI thread before and around first show.** `MainWindow.InitializeComponent`, `EditorShellView.InitializeComponent`, AvalonDock layout construction, tool-window hide operations, child view creation, and view-model construction all run on the UI thread.

3. **Window placement is applied in `Loaded`, not before first show.** The first visible frame can race against applying saved bounds/maximized state.

4. **AvalonDock document/tool panes initialize after `Show()`.** `EditorShellView.OnLoaded` configures docking behavior and rebuilds documents only when the shell is loaded.

5. **The central document pane starts empty.** Since startup does not open a document, the AvalonDock document pane can naturally appear as a blank region. If AvalonDock defaults or theme resources paint white before app brushes/theme finish applying, that blank area will be visually obvious.

6. **Theme/resource application and third-party docking defaults may lag first paint.** Most shell backgrounds use dynamic resources, while AvalonDock uses its own VS2013 theme and layout controls. Any missing/default brush during initialization can present as white until resources/styles settle.

7. **Fire-and-forget validation can overlap first render.** MAME preference validation begins during view-model construction and may perform file/system work and later update UI state while the first visible render is happening.

8. **Future startup document reopening would add more risk.** The explicit document-loading path is synchronous and parsing/validation/render setup would add UI-thread delay if invoked before or immediately after `Show()`.

## Candidate readiness points

No single existing readiness event currently means “the editor is ready for first useful render.” Candidate points for a later implementation are:

1. **After `MainWindow` construction completes.** This covers project metadata load, view-model construction, shell construction, and synchronous startup service setup. It does not cover WPF loaded/layout/render work.

2. **After a new explicit `PrepareForFirstShowAsync` method yields through dispatcher layout/render priorities.** This is the recommended first readiness API. It can be called while the Launcher progress dialog is visible and before showing the editor. It should do minimal safe preparation first, then grow as needed.

3. **After `MainWindow.Loaded`.** Reliable for window/shell loaded events, but using it requires showing the window, unless the window is intentionally shown hidden/transparent/off-screen. That is less desirable because it can create taskbar/focus flash.

4. **After `ContentRendered`.** Stronger than `Loaded` for first visible render, but it also requires the window to be shown. It is useful only if a later design uses temporary invisibility/opacity or off-screen positioning.

5. **After `EditorShellView.OnLoaded` and document rebuild.** This marks that AvalonDock subscriptions and document containers are initialized. It currently also requires the shell to be loaded by WPF.

6. **After first selected document render completes.** This would be the best user-perceived readiness if startup begins reopening documents, but there is no current general event from `SkiaPanel2DEditView`/`SkiaFaceEditView` indicating first render completion.

Recommended readiness for the first implementation: **construct hidden/not-yet-shown MainWindow, perform all currently pre-show synchronous work under Launcher progress, then call a new `PrepareForFirstShowAsync` that applies placement earlier where possible and yields through dispatcher `Loaded`/`Render`/`ApplicationIdle` stages as far as WPF permits without showing. Show only after that preparation.** If WPF loaded/render events are needed, use the least-flashy fallback, such as temporary `ShowActivated=false` plus hidden/off-screen/opacity guarded by the progress dialog, and reveal after `ContentRendered`.

## UI-thread-only work

The following should be treated as UI-thread-only unless refactored with great care:

- WPF `Window`, `UserControl`, and XAML construction (`InitializeComponent`).
- Creating and mutating `EditorShellView`, AvalonDock layout objects, `LayoutDocument`, `DocumentEditorView`, tool windows, and WPF controls.
- Assigning `EditorShellHost.Content` and `DataContext`.
- Registering WPF command bindings and keyboard shortcuts.
- Reading/writing WPF-bound view-model properties from UI callbacks.
- Mutating `ObservableCollection` instances bound to the UI (`OpenDocuments`, `AssetBrowserItems`, `OutputEntries`).
- Showing/hiding/floating AvalonDock tool windows.
- Applying window placement (`WindowState`, `Width`, `Height`, `Left`, `Top`).
- Interacting with WPF dispatchers, dynamic resources, message boxes, and dialogs.
- Skia WPF control rendering callbacks and view `Loaded` handlers.
- `PanelElementFactory.ProjectDirectoryPath` assignment should remain serialized on the UI/startup path until its broader thread-safety assumptions are reviewed.

## Work that could later become async/background

The following are good candidates for later async/background work, provided results are marshalled back to the UI thread:

- project-file validation and project JSON parsing;
- preference file load/save operations;
- recent-projects store load/save;
- MAME install directory scanning and `SyncMameVersionToLatestInstalled`;
- MAME preference validation and version catalog/cache reads;
- asset browser file-system enumeration, with UI collection updates batched on the dispatcher;
- input-map diagnostics computation;
- document file reads (`File.ReadAllText` -> async read);
- `.panel2d` and `.face` JSON parse/validation/model conversion;
- Face runtime asset export/generation and render asset file checks;
- first-render cache warmups that do not touch WPF controls directly, such as pure Skia image/cache preparation guarded for thread safety.

## Concrete startup flow recommended for later implementation

### MVP flow

```text
Launcher Open/Create command
  -> validate the project path enough to fail fast
  -> show shared progress dialog owned by Launcher
       Title: Starting Editor
       Initial message: Opening project...
  -> inside progress operation on UI thread:
       progress: Preparing editor shell...
       construct MainWindow but do not show it
       progress: Loading project...
       allow current MainWindowViewModel startup project load to run as it does today
       progress: Preparing first render...
       await mainWindow.PrepareForFirstShowAsync(progress, token)
       progress: Finalizing editor...
  -> hide Launcher
  -> show MainWindow
  -> close progress dialog
```

For the first pass, keep progress indeterminate. Report coarse messages only:

- `Opening project...`
- `Preparing editor shell...`
- `Loading project...`
- `Preparing editor layout...`
- `Finalizing editor...`

### Proposed `MainWindow` readiness API

Add a small preparation method rather than moving all startup logic immediately:

```csharp
public Task PrepareForFirstShowAsync(IEditorProgressReporter progress, CancellationToken token)
```

Initial responsibilities:

- report `Preparing editor layout...`;
- apply window placement before the visible show if possible;
- allow queued dispatcher work to process at layout/render/application-idle priorities;
- optionally ask `EditorShellView` for a minimal readiness pass if needed later;
- avoid opening documents or changing runtime behavior in this task.

### Error handling flow

If any startup step fails:

- close any constructed-but-not-shown `MainWindow`;
- keep or restore the Launcher;
- close the progress dialog;
- show/log the existing warning message;
- do not leave hidden windows or active progress operations alive.

### Longer-term refinements

After the MVP hides partial rendering successfully:

1. Extract shared progress dialog/service so Launcher and Editor use the same visual implementation without `MainWindowViewModel` coupling.
2. Split `MainWindowViewModel` construction into cheap construction plus explicit startup phases, so progress can report accurately.
3. Move project metadata parse, asset enumeration, MAME validation, and document parsing off the UI thread.
4. Add optional first-document reopen support only after document loading has progress and readiness events.
5. Add first-render completion notifications from document editor views if startup ever needs to wait for a selected document to be visibly rendered.
