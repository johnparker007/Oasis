# TASKS.md

## Phase 1 — Project System
- [x] Create solution structure
- [x] Implement project create flow
- [x] Implement project open flow
- [x] Implement recent projects list
- [x] Generate project directory layout
- [x] Load project into editor shell

## Phase 2 — Editor Shell
- [x] Create main window
- [x] Add menu bar
- [x] Add toolbar
- [x] Implement basic dock layout
- [x] Implement document tab system
- [x] Add panels:
  - [x] Asset browser
  - [x] Inspector
  - [x] Output/log

## Phase 3 — Document System
- [x] Define base document model
- [x] Implement open/save document
- [x] Implement document dirty state
- [x] Implement document tabs integration
- [x] Stub document types:
  - [x] .panel2d
  - [x] .cabinet3d
  - [x] .machine
  
## Phase 3A — .NET 9 Upgrade and Theme Foundations
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
- [x] Add smoke test checklist for:
  - [x] System theme
  - [x] Light theme
  - [x] Dark theme
  - [x] Theme persistence after restart

## Phase 4 — Command System
- [x] Define ICommand interface
- [x] Implement command history
- [x] Implement undo
- [x] Implement redo
- [x] Ensure documents update via commands only

## Phase 5 — Panel Editor (MVP)
- [x] Render panel canvas
- [x] Implement pan
- [x] Implement zoom
- [x] Implement selection
- [ ] Add rectangle tool
- [ ] Add image placement
- [ ] Add basic inspector editing
- [ ] Save/load panel document
