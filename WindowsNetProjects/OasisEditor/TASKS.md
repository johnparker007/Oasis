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
- [ ] Define theme preference enum:
  - [ ] System
  - [ ] Light
  - [ ] Dark
- [ ] Persist editor theme preference
- [ ] Apply theme preference on startup
- [ ] Add Edit > Preferences menu item
- [ ] Add Edit > Project Settings menu item
- [ ] Create non-modal Preferences window
- [ ] Create non-modal Project Settings window
- [ ] Add theme selector to Preferences window
- [ ] Define semantic editor brushes:
  - [ ] EditorBackgroundBrush
  - [ ] PanelBackgroundBrush
  - [ ] InspectorBackgroundBrush
  - [ ] ToolBarBackgroundBrush
  - [ ] TextPrimaryBrush
  - [ ] TextSecondaryBrush
  - [ ] BorderSubtleBrush
  - [ ] SelectionBrush
- [ ] Replace shell-level hard-coded colors with semantic theme resources
- [ ] Ensure main window, menu, toolbar, document tabs, and panels respond to theme changes
- [ ] Add smoke test checklist for:
  - [ ] System theme
  - [ ] Light theme
  - [ ] Dark theme
  - [ ] Theme persistence after restart

## Phase 4 — Command System
- [ ] Define ICommand interface
- [ ] Implement command history
- [ ] Implement undo
- [ ] Implement redo
- [ ] Ensure documents update via commands only

## Phase 5 — Panel Editor (MVP)
- [ ] Render panel canvas
- [ ] Implement pan
- [ ] Implement zoom
- [ ] Implement selection
- [ ] Add rectangle tool
- [ ] Add image placement
- [ ] Add basic inspector editing
- [ ] Save/load panel document

