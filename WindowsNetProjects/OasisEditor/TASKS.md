# TASKS.md

## Current Focus — MFME Extract Import into Panel2D

Goal: allow the WPF editor to import an existing MFME extract into an empty/new Panel2D document and create WPF editor equivalents for the first supported MFME component set: Background, Lamp, Reel, Seven Seg, and Alpha.

Complete these tasks in order. Keep each task small enough for one Codex pass where practical. Build after every implementation task and fix compile, binding, resource, and test errors before moving on.

### MFME Import Guardrails
- [ ] Only modify files under `Oasis/WindowsNetProjects/OasisEditor` unless a task explicitly says otherwise
- [ ] Read/reference legacy code from `UnityProjects/LayoutEditor` and `WindowsNetProjects/MfmeTools`, but do not modify those projects in this feature track
- [ ] Keep import/domain logic UI-agnostic; no WPF controls, brushes, dependency properties, or dialogs in importer/model classes
- [ ] All document mutations must go through document-scoped commands and preserve undo/redo behavior
- [ ] Keep `.panel2d` schema version 1 loading correctly; add explicit migration/validation before introducing schema version 2
- [ ] Prefer placeholder-but-editable WPF visuals first; do not attempt final runtime-accurate lamp/reel/segment rendering in this track
- [ ] Do not automate MFME or depend on the closed-source MFME executable for this first WPF import milestone
- [ ] Do not add a direct dependency from the .NET 9 WPF editor to the old .NET Framework `MfmeTools` WinForms app unless a dedicated compatibility task proves it is safe
- [ ] Copy imported image assets into the active Oasis project rather than permanently referencing the source extract folder
- [ ] Add/update tests where practical under the existing OasisEditor test project

### Phase K — Legacy Import and Extract Format Reconnaissance
- [x] Inspect the Unity importer entry point at `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MFME/ExtractImporter.cs`
  - [x] Document the exact field mappings currently used for Background, Lamp, Reel, SevenSegment, Alpha, AlphaNew, and MatrixAlpha
  - [x] Note existing Unity importer quirks that should be preserved initially, such as Reel number `+ 1` and MatrixAlpha importing as Alpha
  - [x] Note behavior that should be deferred, such as runtime-only editor components, complex lamp inputs, and temporary reel overlay compositing
- [x] Inspect MFME extract DTOs/helpers under `WindowsNetProjects/MfmeTools/MfmeTools/Shared`
  - [x] Identify the minimum DTO fields needed for Background, Lamp, Reel, SevenSegment, Alpha, AlphaNew, and MatrixAlpha
  - [x] Identify how extract image folders and filenames are represented
  - [x] Identify the extract manifest/layout file format that the WPF editor should read first
- [x] Add `Docs/MfmeImportPlan.md` under `WindowsNetProjects/OasisEditor`
  - [x] Summarise the source extract contract for the first milestone
  - [x] Summarise component mapping rules
  - [x] Summarise asset-copy conventions
  - [x] List deferred behavior explicitly
  - [x] Include a short note that legacy projects are reference-only for this track
- [x] Build the WPF solution after the reconnaissance/documentation-only changes if project files changed; otherwise no build is required

### Phase L — MFME Import Domain Boundary
- [x] Add a focused MFME import feature folder under the WPF editor project
  - [x] Suggested location: `OasisEditor/Features/MfmeImport/`
  - [x] Keep classes internal unless they must be public for tests
- [x] Add importer result/diagnostic types
  - [x] `MfmeImportResult` with imported elements, copied assets, skipped components, and warnings/errors
  - [x] `MfmeImportWarning` or equivalent structured warning type
  - [x] Ensure warnings are useful for output-log display
- [x] Add import options/context types
  - [x] Source extract path
  - [x] Active project root/assets root
  - [x] Whether to copy assets
  - [x] Optional layout/display name
- [x] Add a first-pass extract reader abstraction
  - [x] Keep parsing independent from WPF UI
  - [x] Support loading an already-created MFME extract from disk
  - [x] Return a neutral in-editor representation rather than old Unity component types
- [x] Add tests for invalid/missing extract paths and basic warning/error reporting
- [x] Build and run tests

### Phase M — Minimal MFME Extract DTOs for the WPF Editor
- [x] Add minimal WPF-editor-owned DTOs for MFME extract data needed by this feature
  - [x] Layout/import root DTO
  - [x] Shared component base data: type, position, size, name/source identity if available
  - [x] Background DTO
  - [x] Lamp DTO including first lamp element data needed by the Unity importer mapping
  - [x] Reel DTO
  - [x] SevenSegment DTO
  - [x] Alpha/AlphaNew/MatrixAlpha DTOs or one normalised Alpha DTO
- [x] Add parser/normaliser from the real extract layout format into these DTOs
  - [x] Keep the parser tolerant of unsupported components
  - [x] Unsupported components should be skipped with warnings, not hard failures
  - [x] Missing optional images should produce warnings and usable placeholder elements
- [x] Add tests using small hand-written fixture JSON/data for each supported component type
- [x] Build and run tests

### Phase N — Panel2D Schema and Model Expansion
- [x] Design the Panel2D schema extension for imported MFME components before editing storage code
  - [x] Decide whether this feature requires schema version 2
  - [x] Preserve ability to open schema version 1 files
  - [x] Define a migration path or explicit version rejection behavior for future schemas
- [x] Add new `PanelElementKind` values for imported/editable MFME elements
  - [x] Background
  - [x] Lamp
  - [x] Reel
  - [x] SevenSegment
  - [x] Alpha
- [x] Extend `PanelElementModel`/storage DTOs with the minimum metadata needed for MFME imports
  - [x] Project-relative asset path or paths
  - [x] Source MFME component type
  - [x] Source MFME component identifier/index if available
  - [x] Display/runtime number where applicable, such as lamp/reel/segment number
  - [x] Common visual properties needed for placeholders, such as colors/text/reversed/stops
- [x] Update validation and normalisation for the new kinds
  - [x] Reject invalid dimensions consistently
  - [x] Preserve stable object IDs and names
  - [x] Ensure malformed or unsupported files fail with useful messages
- [x] Update storage/model round-trip tests
  - [x] Existing rectangle/image schema version 1 fixtures still load
  - [x] New imported component kinds round-trip correctly
  - [x] Unsupported future schemas produce explicit errors
- [x] Build and run tests

### Phase O — Component Mapping from MFME Extract to Panel2D Elements
- [ ] Add `MfmeComponentMapper` or equivalent pure mapping service
  - [ ] Input: normalised MFME extract DTOs
  - [ ] Output: Panel2D model/storage elements ready for command insertion
  - [ ] No WPF dependencies
- [ ] Implement Background mapping
  - [ ] Position `(0, 0)`
  - [ ] Size from MFME background
  - [ ] Color from MFME background
  - [ ] Optional image path from the extract Background folder
  - [ ] Name `Background`
- [ ] Implement Lamp mapping
  - [ ] Position and size from MFME lamp
  - [ ] Number from the first lamp element, matching the Unity importer for milestone 1
  - [ ] Optional image path from the extract Lamps folder
  - [ ] On/off/text colors
  - [ ] Text/font fields where available
  - [ ] Input metadata stored only if straightforward; no runtime behavior yet
  - [ ] Name `Lamp` or `Lamp <number>` consistently
- [ ] Implement Reel mapping
  - [ ] Position and size from MFME reel
  - [ ] Number as MFME number `+ 1`, matching the Unity importer
  - [ ] Band image path from the extract Reels folder
  - [ ] Stops and reversed fields
  - [ ] Visible scale using the Unity importer's first-pass calculation
  - [ ] Overlay handling deferred or stored as metadata only
  - [ ] Name `Reel <number>`
- [ ] Implement SevenSegment mapping
  - [ ] Position and size from MFME seven-segment component
  - [ ] Number from MFME component
  - [ ] Segment/on color
  - [ ] Name `7 Segment <number>`
- [ ] Implement Alpha mapping
  - [ ] Map Alpha and AlphaNew to the same WPF Alpha element kind
  - [ ] Map MatrixAlpha to Alpha for now, matching the Unity importer's temporary behavior
  - [ ] Position, size, and reversed where available
  - [ ] Name `Alpha`
- [ ] Add mapper tests for each supported component type
- [ ] Add tests that unsupported MFME component types are skipped with warnings
- [x] Build and run tests

### Phase P — Project Asset Copy and Relative Path Handling
- [ ] Add an asset-copy service for imported MFME extract images
  - [ ] Copy into `Assets/MfmeImport/<layout-or-extract-name>/Background/`
  - [ ] Copy into `Assets/MfmeImport/<layout-or-extract-name>/Lamps/`
  - [ ] Copy into `Assets/MfmeImport/<layout-or-extract-name>/Reels/`
  - [ ] Generate safe folder/file names and prevent path traversal
  - [ ] Avoid overwriting distinct files accidentally; use deterministic collision handling
- [ ] Store project-relative asset paths in Panel2D elements
- [ ] Handle missing source images gracefully
  - [ ] Import the element anyway when possible
  - [ ] Add a warning listing the missing file
  - [ ] Use placeholder visuals later in the visual projection phase
- [ ] Refresh the Assets pane after successful import/copy
- [ ] Add tests for root containment, duplicate names, missing files, and project-relative path generation
- [x] Build and run tests

### Phase Q — Panel2D Visual Projection for Imported Elements
- [ ] Update `PanelElementFactory` or introduce focused visual factories for new Panel2D kinds
  - [ ] Keep WPF-specific visual creation separate from import/domain mapping
  - [ ] Preserve existing Rectangle/Image behavior
- [ ] Add placeholder visual projection for Background
  - [ ] Use imported image when available
  - [ ] Fall back to a filled rectangle using imported color when no image is available
- [ ] Add placeholder visual projection for Lamp
  - [ ] Use imported lamp image when available
  - [ ] Fall back to a colored rectangle with optional text
  - [ ] Keep it selectable/model-backed like existing elements
- [ ] Add placeholder visual projection for Reel
  - [ ] Use imported band image when available, clipped/scaled into the element bounds
  - [ ] Fall back to a labeled placeholder rectangle
- [ ] Add placeholder visual projection for SevenSegment
  - [ ] Use a labeled/tinted placeholder visual for now
  - [ ] Preserve number/color metadata in the model
- [ ] Add placeholder visual projection for Alpha
  - [ ] Use a labeled placeholder visual for now
  - [ ] Preserve reversed metadata in the model
- [ ] Update hierarchy grouping/display for the new element kinds
  - [ ] Backgrounds
  - [ ] Lamps
  - [ ] Reels
  - [ ] Seven Segments
  - [ ] Alphas
- [ ] Verify selection, hierarchy selection, inspector display, save/load, and undo/redo still work for new kinds
- [x] Build and run tests

### Phase R — Undoable MFME Import Command
- [ ] Add `ImportMfmeExtractCommand` or equivalent document-scoped command
  - [ ] Target a specific document ID at creation time
  - [ ] Insert all imported Panel2D elements as one undoable operation
  - [ ] Record copied assets/import warnings outside undo only if necessary and documented
  - [ ] Mark the document dirty only when elements are actually added
  - [ ] Do not record no-op imports in undo history
- [ ] Ensure undo removes all imported elements from the active document only
- [ ] Ensure redo restores the same logical imported elements without reusing stale command state incorrectly
- [ ] Ensure object IDs remain stable through undo/redo of the same command
- [ ] Add command tests for import, undo, redo, no-op import, and wrong-document safety
- [x] Build and run tests

### Phase S — Import UI Entry Point
- [ ] Add a user-facing import command to the WPF editor shell
  - [ ] Suggested menu: `File > Import > MFME Extract...`
  - [ ] Enable only when a project is open and a Panel2D document is active or can be created intentionally
  - [ ] Use a folder picker or file picker appropriate to the extract contract identified in Phase K
- [ ] Route UI command through ViewModel/service code, not direct import logic in code-behind
- [ ] Display import results in the output log
  - [ ] Count imported components by kind
  - [ ] Count skipped unsupported components
  - [ ] List warnings for missing images or unsupported fields
- [ ] Ensure import into an empty Panel2D document is the first supported flow
- [ ] Add a clear message for unsupported active document types
- [x] Build and run tests

### Phase T — End-to-End Validation and Cleanup
- [ ] Create or identify a small MFME extract fixture for manual smoke testing
  - [ ] Must include at least one Background, Lamp, Reel, SevenSegment, and Alpha if possible
  - [ ] Keep fixture out of git if it is large or not redistributable
- [ ] Smoke test importing into an empty project/document
  - [ ] Imported assets are copied under the project's `Assets/MfmeImport/...` folder
  - [ ] Panel2D elements appear at expected positions/sizes
  - [ ] Hierarchy groups show imported elements
  - [ ] Selection and inspector work for imported elements
  - [ ] Save/reopen preserves imported elements and asset paths
  - [ ] Undo/redo of the import works document-locally
- [ ] Compare WPF imported output against the Unity importer behavior for the supported first-pass components
- [ ] Update `Docs/MfmeImportPlan.md` with any intentional differences from Unity behavior
- [ ] Add follow-up tasks for deferred rendering/runtime behavior
  - [ ] Accurate lamp rendering and masks
  - [ ] Accurate reel viewport/overlay handling
  - [ ] Seven-segment renderer
  - [ ] Alpha renderer
  - [ ] Button/input mapping
  - [ ] Runtime/export mapping
- [ ] Final build and test run

---

## Active Carryover — Previous Track Smoke Test

These are the only unfinished tasks from the previous context-menu/assets-pane track. Complete them after the MFME import milestone or when directly relevant.

### Previous Phase J — Follow-up Refactor Checks
- [ ] Smoke test full editor flow
  - [ ] Blocked in current container; see `PhaseJ.SmokeTestAttempt.md`
  - [ ] Create/open project
  - [ ] Refresh assets
  - [ ] Navigate folders in Assets pane
  - [ ] Open a `.panel2d` asset
  - [ ] Use hierarchy context menu rename/delete/duplicate/copy/paste/cut
  - [ ] Use undo/redo after hierarchy mutations
  - [ ] Save/reopen and verify panel contents

---

## Completed — Context Menus and Unity-Style Assets Pane

These tasks came from the latest Editor code review and are retained here as completed history.

### Feature Guardrails
- [x] Only modify files under `Oasis/WindowsNetProjects/OasisEditor`
- [x] Keep the app runnable after every task
- [x] Build after every task and fix compile/binding/resource errors before continuing
- [x] Prefer small diffs and behaviour-preserving refactors unless the task explicitly changes behaviour
- [x] Do not start Blender, cabinet import, machine assembly, or Unity export work in this track
- [x] Do not introduce new frameworks or dependency injection yet
- [x] Do not place command logic in WPF views/code-behind when a ViewModel or focused command service can own it
- [x] Reuse existing command paths for keyboard/menu/context-menu entry points instead of duplicating behavior
- [x] Add tests where practical; keep tests under the Editor solution only

### Phase F — Shared Context Menu Command Foundation
- [x] Review current hierarchy keyboard handlers and asset double-click handlers
- [x] Add a reusable command pattern for pane item context menus
- [x] Add shared menu item styling/resources if useful

### Phase G — Hierarchy Entity Context Menu
- [x] Ensure right-clicking a hierarchy entity selects it before showing the context menu
- [x] Add initial hierarchy entity context menu items: Cut, Copy, Paste, Rename, Duplicate, Delete
- [x] Route Rename through the existing rename behavior
- [x] Route Delete through the existing delete behavior
- [x] Implement Duplicate for Panel2D hierarchy entities
- [x] Implement Copy/Paste for Panel2D hierarchy entities
- [x] Implement Cut for Panel2D hierarchy entities
- [x] Verify hierarchy context menu behavior

### Phase H — Asset Browser Model Refactor Toward Unity Project Window
- [x] Replace the flat all-files asset list model with a tree/content model
- [x] Preserve existing asset open behavior during the model refactor
- [x] Add folder navigation behavior
- [x] Add folder/file icon state
- [x] Update `AssetBrowserView.xaml` to a two-column layout
- [x] Verify Unity-style asset browsing flow

### Phase I — Assets Pane Context Menu
- [x] Ensure right-clicking an asset item selects it before showing the context menu
- [x] Add initial asset item context menu items: Show In Explorer, Open, Delete, Rename
- [x] Implement Show In Explorer
- [x] Implement Open
- [x] Implement Rename for assets/folders
- [x] Implement Delete for assets/folders
- [x] Verify asset context menu behavior

### Phase J — Follow-up Refactor Checks
- [x] Review `MainWindowViewModel` after context menu and asset browser changes
- [x] Review document mutation command coverage
- [x] Add or update tests for new behavior where practical

---

## Completed — Code Review Fixes and Panel2D Model Stabilisation

These tasks came from the Editor code review and are retained here as completed history.

### Review Fix Guardrails
- [x] Only modify files under `Oasis/WindowsNetProjects/OasisEditor`
- [x] Keep the app runnable after every task
- [x] Build after every task and fix compile/binding/resource errors before continuing
- [x] Prefer small diffs and behaviour-preserving changes unless the task explicitly changes behaviour
- [x] Do not start Blender, cabinet import, machine assembly, or Unity export work in this track
- [x] Do not introduce new frameworks or dependency injection yet
- [x] Add tests where practical; keep tests under the Editor solution only

### Phase A — Correctness Fixes Before Larger Refactors
- [x] Ensure Panel2D canvas mutations mark the owning document dirty
- [x] Prevent no-op document commands from entering undo/redo history
- [x] Preserve command-history integrity when closing and undoing a closed document tab
- [x] Replace duplicated add-element command implementations

### Phase B — Canvas Behaviour Split Without Changing UX
- [x] Move canvas command dispatch out of `CanvasPanBehavior`
- [x] Move panel tool placement logic out of `CanvasPanBehavior`
- [x] Remove or isolate hardcoded MVP canvas sample visuals

### Phase C — Panel2D Live Model Preparation
- [x] Create a domain-facing Panel2D model separate from JSON storage
- [x] Move Panel2D mutation commands to operate on the live model first
- [x] Make hierarchy and inspector read from the live Panel2D model where practical

### Phase D — Storage, Validation, and Migration
- [x] Add explicit Panel2D schema validation
- [x] Add Panel2D schema migration hooks
- [x] Add tests for Panel2D storage/model behaviour

### Phase E — Deferred Planning Only
- [x] Plan layer ordering support only
- [x] Plan object locking/visibility support only
- [x] Plan copy/paste/duplicate support only

---

## Completed — Editor Stability and Document Context

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
- [x] Show Panel2D objects grouped by type: Images, Rectangles, Anchors, Zones
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

---

## Backlog — Panel Editor Usability and Future Planning

- [x] Snapping
- [ ] Layer ordering
- [ ] Object locking
- [ ] Object visibility
- [x] Duplicate/copy/paste
- [ ] Begin cabinet import MVP planning
- [ ] Begin machine assembly MVP planning

---

## Completed — Refactor Track: WPF Maintainability

These tasks were behavior-preserving unless explicitly stated otherwise.

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

## Completed — Startup, Project, Shell, Document, Theme, Command, and Panel MVP

### Startup Flow Refactor
- [x] Create dedicated Launcher window class and view model
- [x] Make Launcher window the application startup window
- [x] Move New Project UI into Launcher window
- [x] Move Open Project UI into Launcher window
- [x] Move Recent Projects list UI into Launcher window
- [x] Refactor project creation/open flow so Launcher opens editor only after success
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
- [x] Verify project creation/open/recent/cancel/close-project startup flows

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
- [x] Add panels: Asset browser, Inspector, Output/log

### Phase 3 — Document System
- [x] Define base document model
- [x] Implement open/save document
- [x] Implement document dirty state
- [x] Implement document tabs integration
- [x] Stub document types: `.panel2d`, `.cabinet3d`, `.machine`

### Phase 3A — .NET 9 Upgrade and Theme Foundations
- [x] Update solution target frameworks from .NET 8 to .NET 9
- [x] Verify solution builds and runs cleanly in Visual Studio 2022
- [x] Add built-in WPF Fluent theme resources
- [x] Define application theme service and theme preference enum
- [x] Persist/apply editor theme preference
- [x] Add Preferences and Project Settings windows/menu items
- [x] Add theme selector to Preferences window
- [x] Define and use semantic editor brushes
- [x] Ensure main window, menu, toolbar, document tabs, and panels respond to theme changes

### Phase 4 — Command System
- [x] Define ICommand interface
- [x] Implement command history
- [x] Implement undo
- [x] Implement redo
- [x] Ensure documents update via commands only

### Phase 5 — Panel Editor MVP
- [x] Render panel canvas
- [x] Implement pan
- [x] Implement zoom
- [x] Implement selection
- [x] Add rectangle tool
- [x] Add image placement
- [x] Add basic inspector editing
- [x] Save/load panel document
