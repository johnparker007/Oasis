# TASKS.md

## Current Focus — Startup Flow Refactor

### Launcher Window
- [ ] Create dedicated Launcher window class and view model
- [ ] Make Launcher window the application startup window
- [ ] Move New Project UI into Launcher window
- [ ] Move Open Project UI into Launcher window
- [ ] Move Recent Projects list UI into Launcher window

### Project Opening Flow
- [ ] Refactor project creation flow so Launcher opens editor only after success
- [ ] Refactor project open flow so Launcher opens editor only after success
- [ ] Refactor recent project selection flow so Launcher opens editor only after success
- [ ] Ensure cancel/failure keeps user in Launcher window
- [ ] Ensure failed project load shows error without opening editor shell

### Editor Shell Separation
- [ ] Remove startup project-selection UI from editor shell
- [ ] Ensure editor shell requires a valid loaded project at construction/open time
- [ ] Ensure editor shell initializes correctly from an already-loaded project
- [ ] Prevent editor shell from opening when no active project exists

### Close Project Flow
- [ ] Add File > Close Project action
- [ ] Close editor shell and return to Launcher window
- [ ] Ensure closing a project clears active document/session state
- [ ] Ensure Launcher refreshes recent projects after returning from editor shell

### Verification
- [ ] Verify New Project opens editor correctly
- [ ] Verify Open Project opens editor correctly
- [ ] Verify Recent Project opens editor correctly
- [ ] Verify cancel from project selection keeps Launcher open
- [ ] Verify Close Project returns user to Launcher
- [ ] Verify app startup no longer exposes editor UI before project load

## Next Up
- [ ] Refine command system integration into editors
- [ ] Begin improving panel editor usability (selection, snapping, layering)
- [ ] Begin cabinet import MVP planning

## Completed

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
