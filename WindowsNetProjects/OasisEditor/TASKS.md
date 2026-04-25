# TASKS.md

## Current Focus — Editor Stability and Document Context

### Undo/Redo Robustness
- [x] Audit current command history ownership
- [x] Ensure each open document has its own command history
- [x] Ensure commands are bound to the document they were created for
- [x] Ensure undo only affects the active document
- [x] Ensure redo only affects the active document
- [x] Clear redo stack only for the affected document when a new command is executed
- [x] Prevent commands from applying to a different active document after tab switching
- [x] Add document identity checks before command execute/undo/redo
- [x] Ensure closing a document clears or safely discards its command history
- [x] Ensure switching tabs updates undo/redo menu enabled state
- [x] Ensure undo/redo menu labels reflect active document command names where possible

### Undo/Redo Verification
- [x] Verify undo/redo works with one Panel2D document
- [x] Verify undo/redo works independently across two Panel2D documents
- [x] Verify undo in one tab does not affect another tab
- [x] Verify redo in one tab does not affect another tab
- [x] Verify adding a new command in one document does not clear redo history in another document
- [x] Verify closing a document prevents its commands from being reused accidentally
- [x] Verify Ctrl+Z and Ctrl+Y route through the active document only

### Active Document Context
- [x] Define active document context service if not already present
- [x] Ensure selection state is document-specific
- [x] Ensure inspector displays selection from active document only
- [x] Ensure asset/editor commands target active document explicitly
- [x] Ensure tab switching refreshes hierarchy, inspector, and command state
- [x] Add safe empty states for no active document

### Hierarchy Panel
- [x] Rename or repurpose existing panel area as Hierarchy if needed
- [x] Create document-aware hierarchy provider interface
- [x] Add Panel2D hierarchy provider
- [x] Show Panel2D objects grouped by type:
  - [x] Images
  - [x] Rectangles
  - [x] Anchors
  - [x] Zones
- [x] Update hierarchy when active document changes
- [x] Update hierarchy when document content changes
- [x] Selecting an item in hierarchy selects the object on the canvas
- [x] Selecting an object on the canvas selects the item in hierarchy
- [x] Support rename from hierarchy if object naming exists
- [x] Support delete selected hierarchy item through command system
- [x] Add empty hierarchy state for unsupported document types

### Panel Editor Object Model Cleanup
- [x] Ensure every editable panel object has a stable object ID
- [x] Ensure every editable panel object has a display name
- [x] Ensure object type is explicit and queryable
- [ ] Ensure image and rectangle objects share common selectable-object contract
- [ ] Ensure hierarchy and inspector use the same selected object identity
- [ ] Ensure save/load preserves object IDs and names

## Next Up
- [ ] Improve panel editor usability:
  - [ ] snapping
  - [ ] layer ordering
  - [ ] object locking
  - [ ] object visibility
  - [ ] duplicate/copy/paste
- [ ] Begin cabinet import MVP planning
- [ ] Begin machine assembly MVP planning


---

## Refactor Track — WPF Maintainability

These tasks should be completed one at a time. They are behavior-preserving unless explicitly stated otherwise.

### Refactor Guardrails
- [ ] Keep behavior and UI appearance unchanged unless a task explicitly says otherwise
- [ ] Build after each refactor task
- [ ] Fix compile, binding, and resource errors before moving on
- [ ] Prefer small diffs over large rewrites
- [ ] Do not introduce new frameworks or major architecture changes without a separate task
- [ ] If a task says “plan only”, do not edit code

### XAML / View Refactors
- [x] Extract Menu styles from `MainWindow.xaml` into `Styles/Menu.xaml`
- [x] Extract Asset Browser UI into `Views/AssetBrowserView.xaml`
- [x] Extract Inspector UI into `Views/InspectorView.xaml`
- [x] Extract Output Log UI into `Views/OutputLogView.xaml`
- [x] Extract Panel 2D canvas/tab UI into `Views/PanelCanvasView.xaml`
- [x] Clean up `MainWindow.xaml` so it acts mainly as the application shell

### ViewModel Refactors
- [x] Move `DocumentTabViewModel` into `ViewModels/DocumentTabViewModel.cs`
- [x] Move `AssetBrowserItemViewModel` into `ViewModels/AssetBrowserItemViewModel.cs`
- [x] Review `MainWindowViewModel.cs` and propose a split plan only
- [x] Extract Asset Browser logic into `ViewModels/AssetBrowserViewModel.cs`
- [x] Extract Inspector logic into `ViewModels/InspectorViewModel.cs`
- [x] Extract Output Log logic into `ViewModels/OutputLogViewModel.cs`
- [x] Extract document/workspace logic into `ViewModels/DocumentWorkspaceViewModel.cs`

### Canvas / Behavior Refactors
- [x] Review `CanvasPanBehavior.cs` and propose a split plan only
- [x] Extract canvas selection logic into `CanvasSelectionBehavior.cs`
- [x] Extract pan/zoom logic into `CanvasPanZoomBehavior.cs`
- [x] Extract canvas element creation logic into `PanelElementFactory.cs`
- [x] Extract canvas layout serialization/mapping into `PanelLayoutMapper.cs`
- [x] Extract canvas mutation commands into `CanvasMutationCommands.cs`
- [x] Reduce `CanvasPanBehavior.cs` to coordination/glue code only

### Refactor Cleanup
- [x] Remove unused XAML resources
- [x] Remove unused C# usings
- [x] Remove dead code discovered during refactor
- [x] Ensure all extracted views preserve existing bindings
- [x] Ensure project builds cleanly
- [x] Smoke test main editor flows

### Optional Future Refactors
- [ ] Consider DataTemplates for view switching
- [ ] Consider dependency injection
- [ ] Consider undo/redo service abstraction
- [ ] Consider separating services from ViewModels

---

## Completed

### Startup Flow Refactor
- [x] Create dedicated Launcher window class and view model
- [x] Make Launcher window the application startup window
- [x] Move New Project UI into Launcher window
- [x] Move Open Project UI into Launcher window
- [x] Move Recent Projects list UI into Launcher window
- [x] Refactor project creation flow so Launcher opens editor only after success
- [x] Refactor project open flow so Launcher opens editor only after success
- [x] Refactor recent project selection flow so Launcher opens editor only after success
- [x] Ensure cancel/failure keeps user in Launcher window
- [x] Ensure failed project load shows error without opening editor shell
- [x] Remove startup project-selection UI from editor shell
- [x] Ensure editor shell requires a valid loaded project at construction/open time
- [x] Ensure editor shell initializes correctly from an already-loaded project
- [x] Prevent editor shell from opening when no active project exists
- [x] Add File > Close Project action
- [x] Close editor shell and return to Launcher window
- [x] Ensure closing a project clears active document/session state
- [x] Ensure Launcher refreshes recent projects after returning from editor shell
- [x] Verify New Project opens editor correctly
- [x] Verify Open Project opens editor correctly
- [x] Verify Recent Project opens editor correctly
- [x] Verify cancel from project selection keeps Launcher open
- [x] Verify Close Project returns user to Launcher
- [x] Verify app startup no longer exposes editor UI before project load

### Phase 1 — Project System
- [x] Create solution structure
- [x] Implement project create flow
- [x] Implement project open flow
- [x] Implement recent projects list
- [x] Generate project directory layout
- [x] Load project into editor shell

### Phase 2 — Editor Shell
- [x] Create main window
- [x] Add menu bar
- [x] Add toolbar
- [x] Implement basic dock layout
- [x] Implement document tab system
- [x] Add panels:
  - [x] Asset browser
  - [x] Inspector
  - [x] Output/log

### Phase 3 — Document System
- [x] Define base document model
- [x] Implement open/save document
- [x] Implement document dirty state
- [x] Implement document tabs integration
- [x] Stub document types:
  - [x] .panel2d
  - [x] .cabinet3d
  - [x] .machine

### Phase 3A — .NET 9 Upgrade and Theme Foundations
- [x] Update solution target frameworks from .NET 8 to .NET 9
- [x] Verify solution builds and runs cleanly in Visual Studio 2022
- [x] Add built-in WPF Fluent theme resources
- [x] Define application theme service
- [x] Define theme preference enum:
  - [x] System
  - [x] Light
  - [x] Dark
- [x] Persist editor theme preference
- [x] Apply theme preference on startup
- [x] Add Edit > Preferences menu item
- [x] Add Edit > Project Settings menu item
- [x] Create non-modal Preferences window
- [x] Create non-modal Project Settings window
- [x] Add theme selector to Preferences window
- [x] Define semantic editor brushes:
  - [x] EditorBackgroundBrush
  - [x] PanelBackgroundBrush
  - [x] InspectorBackgroundBrush
  - [x] ToolBarBackgroundBrush
  - [x] TextPrimaryBrush
  - [x] TextSecondaryBrush
  - [x] BorderSubtleBrush
  - [x] SelectionBrush
- [x] Replace shell-level hard-coded colors with semantic theme resources
- [x] Ensure main window, menu, toolbar, document tabs, and panels respond to theme changes

### Phase 4 — Command System
- [x] Define ICommand interface
- [x] Implement command history
- [x] Implement undo
- [x] Implement redo
- [x] Ensure documents update via commands only

### Phase 5 — Panel Editor (MVP)
- [x] Render panel canvas
- [x] Implement pan
- [x] Implement zoom
- [x] Implement selection
- [x] Add rectangle tool
- [x] Add image placement
- [x] Add basic inspector editing
- [x] Save/load panel document
