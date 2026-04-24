# TASKS.md

## Current Focus — Dockable Editor Shell

### Docking Framework Integration (AvalonDock)

#### Phase 1 — Minimal Integration
- [x] Add AvalonDock via NuGet
- [x] Replace main window layout with DockingManager
- [x] Create central document pane
- [x] Add one tool window (Inspector) docked to the right
- [x] Ensure app builds and runs

#### Phase 2 — Tool Window Expansion
- [ ] Add Hierarchy tool window (left dock)
- [ ] Add Asset Browser tool window
- [ ] Add Output/Log tool window (bottom dock)
- [ ] Allow tool windows to be tabbed together
- [ ] Ensure tool windows can be dragged and docked

#### Phase 3 — Document Integration
- [ ] Ensure Panel2D documents open in central document area
- [ ] Ensure switching document tabs updates active document
- [ ] Ensure command routing follows active document
- [ ] Ensure inspector updates based on active document

#### Phase 4 — Layout Behavior
- [ ] Allow tool windows to float and re-dock
- [ ] Ensure layout remains stable on resize
- [ ] Verify tab dragging between dock areas

#### Phase 5 — Layout Persistence
- [ ] Save dock layout to user settings
- [ ] Restore dock layout on startup
- [ ] Add Window > Reset Layout command
- [ ] Ensure invalid layout falls back to default

#### Verification
- [ ] Verify documents appear in central tab area
- [ ] Verify panels dock and move correctly
- [ ] Verify layout persists after restart
- [ ] Verify reset layout works

---

## Next Up (after docking)
- [ ] Implement Hierarchy panel (document-aware)
- [ ] Improve panel editor usability:
  - [ ] snapping
  - [ ] layer ordering
  - [ ] object locking
  - [ ] object visibility
  - [ ] duplicate/copy/paste
- [ ] Begin cabinet import MVP planning

---

## Refactor Track — WPF Maintainability
(unchanged)

## Completed
(unchanged — keep your existing completed section exactly as-is)