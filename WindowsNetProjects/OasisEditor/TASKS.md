# TASKS.md

## Current Focus — Context Menus and Unity-Style Assets Pane

These tasks come from the latest Editor code review. Complete them in order. Build and smoke test after each checked task. Keep each task as a small, focused Codex change.

### Feature Guardrails
- [ ] Only modify files under `Oasis/WindowsNetProjects/OasisEditor`
- [ ] Keep the app runnable after every task
- [ ] Build after every task and fix compile/binding/resource errors before continuing
- [ ] Prefer small diffs and behaviour-preserving refactors unless the task explicitly changes behaviour
- [ ] Do not start Blender, cabinet import, machine assembly, or Unity export work in this track
- [ ] Do not introduce new frameworks or dependency injection yet
- [ ] Do not place command logic in WPF views/code-behind when a ViewModel or focused command service can own it
- [ ] Reuse existing command paths for keyboard/menu/context-menu entry points instead of duplicating behavior
- [ ] Add tests where practical; keep tests under the Editor solution only

### Phase F — Shared Context Menu Command Foundation
- [x] Review current hierarchy keyboard handlers and asset double-click handlers
  - [x] Identify commands already implemented for rename/delete/open
  - [x] Identify command gaps for cut/copy/paste/duplicate/show-in-explorer/assets rename/assets delete
  - [x] Do not change behavior in this review step
- [x] Add a reusable command pattern for pane item context menus
  - [x] Keep View code-behind limited to selecting the right-clicked item before opening the menu, if needed
  - [x] Put command execution and CanExecute logic on the relevant ViewModel or focused service
  - [x] Ensure unavailable operations are disabled in the menu
  - [x] Keep keyboard shortcuts and context menus routed through the same command methods
- [x] Add shared menu item styling/resources if useful
  - [x] Use semantic theme resources
  - [x] Ensure context menus work in System, Light, and Dark theme modes

### Phase G — Hierarchy Entity Context Menu
- [x] Ensure right-clicking a hierarchy entity selects it before showing the context menu
  - [x] Do not select non-entity group rows as editable objects
  - [x] Preserve existing left-click selection behavior
- [x] Add initial hierarchy entity context menu items
  - [x] Cut
  - [x] Copy
  - [x] Paste
  - [x] Rename
  - [x] Duplicate
  - [x] Delete
- [x] Route Rename through the existing rename behavior
  - [x] Preserve F2 rename behavior
  - [x] Avoid `Microsoft.VisualBasic.Interaction.InputBox` long-term if a simple editor-owned dialog can be added without scope creep
  - [x] Mark the document dirty only when the name actually changes
  - [x] Preserve undo/redo behavior
- [x] Route Delete through the existing delete behavior
  - [x] Preserve Delete key behavior
  - [x] Do not record no-op delete commands
  - [x] Clear selection after successful delete
  - [x] Preserve undo/redo behavior
- [x] Implement Duplicate for Panel2D hierarchy entities
  - [x] Duplicate the selected model-backed element only
  - [x] Generate a new stable object ID
  - [x] Generate a useful display name such as `<name> Copy`
  - [x] Offset the duplicate slightly on the canvas if practical
  - [x] Execute through the document command system
  - [x] Preserve undo/redo behavior
- [ ] Implement Copy/Paste for Panel2D hierarchy entities
  - [ ] Use an editor-owned clipboard format for Panel2D element data
  - [ ] Paste must create new stable object IDs
  - [ ] Paste must target the active document only
  - [ ] Disable Paste when clipboard data is missing or incompatible
  - [ ] Execute Paste through the document command system
  - [ ] Preserve undo/redo behavior
- [ ] Implement Cut for Panel2D hierarchy entities
  - [ ] Copy selected entity to the editor clipboard first
  - [ ] Delete through the document command system only after copy succeeds
  - [ ] Preserve undo/redo behavior for the delete portion
- [ ] Verify hierarchy context menu behavior
  - [ ] Right-click selects the intended entity
  - [ ] Group rows do not expose invalid entity actions
  - [ ] Rename/Delete keyboard shortcuts still work
  - [ ] Duplicate/Copy/Paste/Cut preserve object IDs correctly
  - [ ] Undo/redo affects only the active document

### Phase H — Asset Browser Model Refactor Toward Unity Project Window
- [ ] Replace the flat all-files asset list model with a tree/content model
  - [ ] Add an asset directory tree ViewModel rooted at the project's Assets directory
  - [ ] Add a selected asset directory property
  - [ ] Add a right-pane collection containing the selected directory's child folders and files
  - [ ] Keep asset paths relative to the Assets root for display and identity where practical
  - [ ] Ensure all full paths are contained inside the project's Assets directory
- [ ] Preserve existing asset open behavior during the model refactor
  - [ ] Double-clicking an asset file opens/loads it through the existing `OpenAsset` flow
  - [ ] Unsupported document types should continue to fail gracefully with output-log/error messages
  - [ ] Refresh should rebuild tree/content state safely
- [ ] Add folder navigation behavior
  - [ ] Selecting a directory in the left tree updates the right pane
  - [ ] Double-clicking a folder in the right pane navigates into that folder
  - [ ] Preserve or restore selection where possible after refresh
- [ ] Add folder/file icon state
  - [ ] Empty folder icon/template
  - [ ] Closed folder icon/template
  - [ ] Open/selected folder icon/template
  - [ ] File icon/template
  - [ ] Use simple built-in glyphs or lightweight templates first; do not add image asset dependencies unless requested
- [ ] Update `AssetBrowserView.xaml` to a two-column layout
  - [ ] Left column: directory tree
  - [ ] Right column: selected directory contents
  - [ ] Keep Refresh available
  - [ ] Use semantic theme resources
  - [ ] Avoid hard-coded colors
- [ ] Verify Unity-style asset browsing flow
  - [ ] Empty Assets folder shows an empty state and root folder
  - [ ] Nested folders appear in the tree
  - [ ] Selected folder contents appear on the right
  - [ ] Double-click folder navigation works
  - [ ] Double-click file open still works

### Phase I — Assets Pane Context Menu
- [ ] Ensure right-clicking an asset item selects it before showing the context menu
  - [ ] Support both left-tree directory items and right-pane directory/file items where practical
  - [ ] Do not break double-click open/navigation
- [ ] Add initial asset item context menu items
  - [ ] Show In Explorer
  - [ ] Open
  - [ ] Delete
  - [ ] Rename
- [ ] Implement Show In Explorer
  - [ ] For files, show/select the file in Windows Explorer
  - [ ] For folders, open the folder in Windows Explorer
  - [ ] Handle missing paths gracefully and log a useful output message
- [ ] Implement Open
  - [ ] Files: use the existing asset document open flow
  - [ ] Folders: navigate into the folder
  - [ ] Disable Open where it is not meaningful
- [ ] Implement Rename for assets/folders
  - [ ] Validate non-empty names
  - [ ] Prevent path traversal and paths outside the Assets root
  - [ ] Prevent duplicate target names
  - [ ] Refresh tree/content after successful rename
  - [ ] Preserve selection on the renamed item where practical
  - [ ] Log success/failure to output
- [ ] Implement Delete for assets/folders
  - [ ] Prevent deletion outside the Assets root
  - [ ] Handle missing paths gracefully
  - [ ] Decide whether non-empty folders are blocked or deleted recursively; prefer blocking first unless explicitly requested
  - [ ] Refresh tree/content after successful delete
  - [ ] Clear or move selection safely after delete
  - [ ] Log success/failure to output
- [ ] Verify asset context menu behavior
  - [ ] Right-click selects the intended item
  - [ ] Show In Explorer works for files and folders
  - [ ] Open works for files and folder navigation
  - [ ] Rename handles invalid/duplicate names cleanly
  - [ ] Delete handles files, empty folders, missing paths, and non-empty folders safely

### Phase J — Follow-up Refactor Checks
- [ ] Review `MainWindowViewModel` after context menu and asset browser changes
  - [ ] Move any new cohesive asset-browser logic back into `AssetBrowserViewModel` or focused services
  - [ ] Move any new hierarchy command logic into hierarchy/document command helpers where practical
  - [ ] Keep MainWindowViewModel as orchestration, not a dumping ground
- [ ] Review document mutation command coverage
  - [ ] Ensure Duplicate/Paste use the same command safety contracts as add/delete/rename
  - [ ] Ensure no-op commands are not recorded
  - [ ] Ensure dirty state changes only for real mutations
- [ ] Add or update tests for new behavior where practical
  - [ ] Duplicate creates a new object ID
  - [ ] Paste creates new object IDs
  - [ ] Rename/delete no-op cases are not recorded
  - [ ] Asset browser tree excludes paths outside Assets root
  - [ ] Asset rename/delete refreshes expected collections
- [ ] Smoke test full editor flow
  - [ ] Create/open project
  - [ ] Refresh assets
  - [ ] Navigate folders in Assets pane
  - [ ] Open a `.panel2d` asset
  - [ ] Use hierarchy context menu rename/delete/duplicate/copy/paste/cut
  - [ ] Use undo/redo after hierarchy mutations
  - [ ] Save/reopen and verify panel contents

---

## Current Focus — Code Review Fixes and Panel2D Model Stabilisation

These tasks come from the Editor code review. Complete them in order. Build and smoke test after each checked task. Keep each task as a small, focused Codex change.

### Review Fix Guardrails
- [ ] Only modify files under `Oasis/WindowsNetProjects/OasisEditor`
- [ ] Keep the app runnable after every task
- [ ] Build after every task and fix compile/binding/resource errors before continuing
- [ ] Prefer small diffs and behaviour-preserving changes unless the task explicitly changes behaviour
- [ ] Do not start Blender, cabinet import, machine assembly, or Unity export work in this track
- [ ] Do not introduce new frameworks or dependency injection yet
- [ ] Add tests where practical; if the project has no test project yet, create the smallest suitable test project under the Editor solution only

### Phase A — Correctness Fixes Before Larger Refactors
- [x] Ensure Panel2D canvas mutations mark the owning document dirty
  - [x] Add a focused API on `DocumentTabViewModel` or the workspace to mark the document dirty without replacing unrelated state
  - [x] Ensure add rectangle marks the document dirty
  - [x] Ensure add image marks the document dirty
  - [x] Ensure delete element marks the document dirty only when an element was actually deleted
  - [x] Ensure rename element marks the document dirty only when the name actually changed
  - [x] Verify the tab title gains `*` after each real canvas mutation
  - [x] Verify saving clears the dirty marker
- [x] Prevent no-op document commands from entering undo/redo history
  - [x] Introduce a minimal success/no-op contract for commands, or an equivalent command-service guard
  - [x] Do not record delete when no matching element exists
  - [x] Do not record rename when no matching element exists
  - [x] Do not record rename when the new name equals the current name after normalisation
  - [x] Verify undo/redo menu labels do not change after no-op commands
- [x] Preserve command-history integrity when closing and undoing a closed document tab
  - [x] Review whether clearing a document command history inside close-tab execution makes undo of tab close unsafe
  - [x] Adjust close-tab behaviour so undoing a close either restores a usable document safely or the close operation is not undoable
  - [x] Verify closing a tab cannot leave stale document commands executable against a different tab
- [x] Replace duplicated add-element command implementations
  - [x] Replace separate rectangle/image add commands with a single `AddPanelElementMutationCommand`
  - [x] Preserve existing public factory methods if that keeps callers stable
  - [x] Verify add rectangle/image undo/redo still works

### Phase B — Canvas Behaviour Split Without Changing UX
- [x] Move canvas command dispatch out of `CanvasPanBehavior`
  - [x] Create a small canvas command adapter/service responsible for routing commands to the active document shell
  - [x] Keep `Window.GetWindow(...)` usage contained in one place if it cannot be removed yet
  - [x] Preserve current canvas behaviour and bindings
- [x] Move panel tool placement logic out of `CanvasPanBehavior`
  - [x] Extract rectangle/image placement decisions into a focused tool/controller class
  - [x] Keep click-to-place behaviour unchanged
  - [x] Verify placement works with current pan/zoom transforms
- [x] Remove or isolate hardcoded MVP canvas sample visuals
  - [x] Ensure non-persisted instructional visuals are not selectable editor objects
  - [x] Prefer an overlay/help layer rather than selectable placeholder rectangles/text
  - [x] Verify hierarchy contains only persisted panel elements
  - [x] Verify save/load does not include sample visuals

### Phase C — Panel2D Live Model Preparation
- [x] Create a domain-facing Panel2D model separate from JSON storage
  - [x] Add `Panel2DDocumentModel` with panel title/summary and a collection/list of `PanelElementModel`
  - [x] Add `PanelElementModel` fields for object ID, name, kind, X, Y, width, and height
  - [x] Keep the model UI-agnostic: no WPF types, controls, brushes, or dependency properties
  - [x] Add conversion between the new model and existing `PanelElementFile` storage DTOs
  - [x] Keep existing `.panel2d` file format working
- [x] Move Panel2D mutation commands to operate on the live model first
  - [x] Add element, delete element, and rename element should mutate the model
  - [x] Update JSON/layout sync as a projection of the model, not as the canonical state
  - [x] Preserve undo/redo behaviour
  - [x] Verify save/open round-trips existing panel files
- [x] Make hierarchy and inspector read from the live Panel2D model where practical
  - [x] Keep binding-facing property names stable unless a task explicitly allows a rename
  - [x] Verify selection identity remains object-ID based
  - [x] Verify hierarchy refreshes after add/delete/rename

### Phase D — Storage, Validation, and Migration
- [x] Add explicit Panel2D schema validation
  - [x] Validate `SchemaVersion`
  - [x] Validate required element IDs, names, kinds, and dimensions
  - [x] Normalise recoverable issues and report unrecoverable ones cleanly
  - [x] Ensure malformed JSON fails gracefully with a clear output-log/error message
- [x] Add Panel2D schema migration hooks
  - [x] Keep schema version 1 as the current format
  - [x] Add a small migration entry point even if there are no migrations yet
  - [x] Ensure future schema versions fail with an explicit unsupported-version message
- [x] Add tests for Panel2D storage/model behaviour
  - [x] Round-trip rectangle and image elements
  - [x] Preserve object IDs and names
  - [x] Normalise missing IDs/names only when intended
  - [x] Reject or report unsupported schema versions

### Phase E — Deferred Planning Only
- [x] Plan layer ordering support only
  - [x] Identify required model fields and UI commands
  - [ ] Do not implement yet
- [x] Plan object locking/visibility support only
  - [x] Identify required model fields, canvas hit-test changes, and hierarchy UI changes
  - [ ] Do not implement yet
- [x] Plan copy/paste/duplicate support only
  - [x] Identify ID-generation and undo/redo requirements
  - [ ] Do not implement yet

---

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
- [x] Ensure image and rectangle objects share common selectable-object contract
- [x] Ensure hierarchy and inspector use the same selected object identity
- [x] Ensure save/load preserves object IDs and names

## Next Up
- [ ] Improve panel editor usability:
  - [x] snapping
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
