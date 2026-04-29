# TASKS.md

## Current Focus — Inspector/Hierarchy Performance Refactor (CRITICAL)

The current Inspector + color picker implementation is functionally correct but has a major performance issue:

- Editing a single property (X/Y/Visibility/Color) causes a noticeable delay
- Color picker slider drag is extremely laggy
- Hierarchy and document panes visibly flash during color edits

Root cause (must be assumed and verified in code):

- Element updates trigger full document replacement
- Full panel JSON serialization (`PanelLayoutJson`)
- MainWindowViewModel reacts by:
  - rebuilding Hierarchy
  - rebuilding ALL Inspector rows (`RebuildPropertyRows`)
- This happens for every small change, including high-frequency UI interactions

This phase must fix the architecture so that:

- small edits update only what changed
- high-frequency UI interactions remain smooth
- future features cannot reintroduce full-refresh behavior

---

## Execution Rules (READ FIRST)

- Do NOT attempt to build or run tests in Codex.
- Do NOT create Build/Test attempt logs.
- Implement code only.
- After each task, state what John must test locally.
- Preserve existing command/undo architecture.
- Do NOT break save/load or JSON persistence.

---

## Phase AA — Diagnose and Map Current Update Flow (DO FIRST)

- [ ] Trace full execution path for a simple Inspector edit (e.g. X position):
  - [ ] Inspector row -> command creation
  - [ ] command execution -> document mutation
  - [ ] where `SetPanelElements` or equivalent is called
  - [ ] where `PanelLayoutJson` is regenerated
  - [ ] where PropertyChanged events fire
  - [ ] where MainWindowViewModel reacts (see `OnSelectedDocumentPropertyChanged`)
  - [ ] where Hierarchy is refreshed
  - [ ] where Inspector rows are rebuilt
- [ ] Document this flow in code comments where appropriate
- [ ] Confirm that color picker uses the same path (multiple rapid updates)

---

## Phase AB — Introduce Scoped Panel Change Notifications

Create a new, explicit change notification mechanism for Panel2D documents.

- [ ] Add a `PanelChangeEvent` (or similar) structure containing:
  - [ ] DocumentId
  - [ ] ObjectId (nullable for document-level changes)
  - [ ] ChangedProperties (list or flags)
  - [ ] Flags:
    - [ ] AffectsCanvas
    - [ ] AffectsHierarchy
    - [ ] AffectsInspectorRows
    - [ ] AffectsPersistence
- [ ] Add an event or observable on `DocumentTabViewModel` to publish these changes
- [ ] Commands must emit a `PanelChangeEvent` after execution
- [ ] Ensure this works with undo/redo (events fire on undo/redo as well)

---

## Phase AC — Stop Using PanelLayoutJson as a Live Update Trigger

- [ ] Identify all code paths reacting to `PanelLayoutJson` changes (MainWindowViewModel, others)
- [ ] Remove or reduce reliance on `PanelLayoutJson` for UI updates
- [ ] Ensure `PanelLayoutJson` is only used for:
  - [ ] persistence
  - [ ] save/load
- [ ] If necessary, defer JSON regeneration until:
  - [ ] save
  - [ ] explicit export
  - [ ] or mark dirty and lazily rebuild

---

## Phase AD — Incremental Inspector Updates

- [ ] Modify `InspectorViewModel` so that:
  - [ ] it does NOT call `RebuildPropertyRows()` for every property change
- [ ] Rebuild rows ONLY when:
  - [ ] selected element changes
  - [ ] element kind changes
  - [ ] property set (row structure) changes
- [ ] For simple property updates:
  - [ ] update only the affected row value
- [ ] Ensure bindings reflect live updates without full rebuild

---

## Phase AE — Incremental Hierarchy Updates

- [ ] Modify hierarchy refresh logic so that:
  - [ ] full `RefreshHierarchy()` is NOT called for simple property changes
- [ ] Only refresh hierarchy when:
  - [ ] element added/removed
  - [ ] reorder/z-index change
  - [ ] parent/group change
  - [ ] name change
  - [ ] visibility/lock IF shown in hierarchy UI
- [ ] For non-structural changes:
  - [ ] skip hierarchy rebuild entirely

---

## Phase AF — Canvas Update Optimization

- [ ] Ensure canvas updates do NOT rely on full document reload
- [ ] Update only affected element visuals where possible
- [ ] Verify existing pan/zoom performance remains unchanged

---

## Phase AG — High-Frequency Interaction Handling (Color Picker)

- [ ] Modify color picker behavior:
  - [ ] During slider drag:
    - [ ] update preview (canvas + row) without committing undo command each tick
  - [ ] On drag end / picker close:
    - [ ] issue ONE command
- [ ] Ensure no flooding of command stack
- [ ] Ensure no full Inspector/Hierarchy rebuild during drag

---

## Phase AH — Command Metadata and No-Op Filtering

- [ ] Ensure update commands include metadata about changed properties
- [ ] Ensure no-op updates are ignored (already partly implemented)
- [ ] Ensure command execution triggers only necessary UI updates via PanelChangeEvent

---

## Phase AI — Regression Tests (Logic-Level)

- [ ] Add tests verifying:
  - [ ] simple property change does NOT trigger full Inspector rebuild
  - [ ] simple property change does NOT trigger hierarchy rebuild
  - [ ] undo/redo still works
  - [ ] no-op commands are ignored
- [ ] Add tests for color change batching behavior (one command after drag)

---

## Phase AJ — Local Testing (John)

John must verify:

- [ ] Changing X/Y updates immediately with no visible delay
- [ ] Toggling visibility responds instantly
- [ ] Double-click checkbox works reliably (no UI blocking)
- [ ] Color picker sliders are smooth (no stutter)
- [ ] No flashing of hierarchy or document panes during color edits
- [ ] Undo/redo behaves correctly
- [ ] Save/load unchanged

---

## Existing Phases (Completed Work)

(keep for reference)

---

## DO NOT DO

- No direct mutation of PanelElementModel
- No reintroduction of full-refresh patterns for simple edits
- No reliance on PanelLayoutJson for UI updates
- No new frameworks

---

## Backlog

(unchanged)
