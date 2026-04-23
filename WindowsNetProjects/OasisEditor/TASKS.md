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
- [ ] Implement document tabs integration
- [ ] Stub document types:
  - [ ] .panel2d
  - [ ] .cabinet3d
  - [ ] .machine

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
