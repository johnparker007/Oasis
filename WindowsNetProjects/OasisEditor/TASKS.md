# TASKS.md

## Current Focus — Unity-Style Inspector Editing for Panel2D Elements

Goal: replace the current read-only/summary-style Inspector with a Unity-style property editor for the selected Panel2D element. Selecting an element in the Hierarchy or Panel2D canvas should populate the Inspector with editable controls. Editing a field must update the selected model-backed element, refresh the canvas/hierarchy/inspector, mark the document dirty, and participate in document-scoped undo/redo.

Codex environment note: do not run local builds or tests in the Codex workspace. The required Windows/.NET/WPF environment is not available there. Implement changes, keep them small and reviewable, then ask John to run the relevant build, tests, and manual smoke checks locally.

### Code Review Findings to Address
- [ ] `InspectorViewModel` currently exposes title/type/path/summary strings and an editable document summary, but it does not expose structured editable fields for selected Panel2D elements.
- [ ] `InspectorView.xaml` is currently a static summary layout. It needs a reusable property-row/control layout that can display text fields, numeric fields, checkboxes, and asset/path fields.
- [ ] `PanelElementModel` is immutable via `init` properties, so Inspector edits must not directly mutate object instances. Use command-driven replacement through `DocumentTabViewModel.SetPanelElements(...)` and existing document command infrastructure.
- [ ] `PanelElementModelCloner.Clone(...)` only supports a small subset of properties today. Inspector editing will need a more complete clone/update path for geometry and element-specific properties.
- [ ] `CanvasMutationCommands` already contains focused document-scoped commands for rename, duplicate, reorder, lock, and visibility. Add Inspector property edit commands to the same command/mutation style rather than introducing direct ViewModel mutation.
- [ ] Selection must remain object-ID based. Do not identify editable elements by display name or approximate canvas coordinates.

### Guardrails
- [ ] Only modify files under `Oasis/WindowsNetProjects/OasisEditor` unless explicitly instructed otherwise.
- [ ] Do not start cabinet import, Blender/3D model import, machine assembly, or Unity export work in this track.
- [ ] Do not add new frameworks or dependency injection in this track.
- [ ] Keep Core/domain/model code free of WPF controls, brushes, dependency properties, and dialogs.
- [ ] Do not put Inspector business logic in WPF view code-behind. Use ViewModels and document-scoped commands.
- [ ] Preserve existing selection synchronization between Hierarchy and Panel2D canvas.
- [ ] Preserve existing context-menu behavior and leave room for future right-click menus in canvas, inspector rows, document tabs, and other editor panes.
- [ ] Keep changes small enough for review. Prefer one cohesive task per Codex pass.
- [ ] Add/update automated tests where practical, but do not run them in Codex.
- [ ] After each implementation pass, report exactly what John should build/test locally.

### Phase U — Inspector Property Editing Foundation
- [ ] Add a small Inspector property model suitable for binding in WPF.
  - [ ] Text/string property rows.
  - [ ] Numeric `double` property rows for geometry.
  - [ ] Numeric `int` property rows for display numbers/stops.
  - [ ] Boolean property rows for lock/visibility/reversed.
  - [ ] Optional read-only/info rows for object ID, kind, and import source.
- [ ] Keep property row ViewModels UI-agnostic enough to unit test.
- [ ] Add validation behavior for numeric edits.
  - [ ] Reject invalid numbers without corrupting the selected element.
  - [ ] Reject non-positive width/height.
  - [ ] Preserve the last valid value or expose a validation error state.
- [ ] Add a generic document-scoped property update command for Panel2D elements.
  - [ ] Target a specific document ID at construction time.
  - [ ] Target a specific selected element by object ID.
  - [ ] Store previous and next element snapshots for undo/redo.
  - [ ] Treat no-op edits as no-ops and do not add them to undo history.
  - [ ] Mark the document dirty only when a real element change is applied.
- [ ] Extend `PanelElementModelCloner` or introduce a dedicated `PanelElementModelUpdater` so all editable properties can be replaced without hand-copying fields in multiple places.
- [ ] Add tests for property update command behavior.
  - [ ] X/Y/Width/Height edits update the correct element.
  - [ ] Undo/redo restores geometry correctly.
  - [ ] No-op edits are not recorded.
  - [ ] Wrong-document and missing-object cases fail safely.
  - [ ] Width/height validation prevents invalid dimensions.

### Phase V — Inspector UI for Common Panel2D Properties
- [ ] Replace the current selected-element summary-only Inspector with editable fields when a Panel2D element is selected.
- [ ] Show common properties for all selected Panel2D elements:
  - [ ] Name.
  - [ ] Object ID as read-only.
  - [ ] Kind/type as read-only.
  - [ ] X.
  - [ ] Y.
  - [ ] Width.
  - [ ] Height.
  - [ ] Locked checkbox.
  - [ ] Visible checkbox.
- [ ] Editing X/Y must move the element on the Panel2D canvas immediately after the command applies.
- [ ] Editing Width/Height must resize the element immediately after the command applies.
- [ ] Editing Name must update the Hierarchy and selected element display consistently.
- [ ] Editing Locked/Visible must use the same semantics as the existing hierarchy/context-menu commands.
- [ ] Keep asset/project/document summary behavior for non-element selections until a later dedicated task replaces it.
- [ ] Add/update Inspector ViewModel tests for selected-element field population and command invocation.

### Phase W — Inspector UI for Element-Specific Properties
- [ ] Add fields for Rectangle/Image where currently supported by the native model.
  - [ ] Image asset path for image-like elements/backgrounds where applicable.
  - [ ] Fill/visual color fields where currently represented in the model.
- [ ] Add fields for native imported component kinds:
  - [ ] Background: asset path, secondary asset path if relevant, color fields/import info where available.
  - [ ] Lamp: display/lamp number, asset path, on/off/text colors, display text.
  - [ ] Reel: reel number, band asset path, secondary asset path if relevant, stops, reversed, visible scale.
  - [ ] SevenSegment: display number and display/on color.
  - [ ] Alpha: display text/color fields where available and reversed.
- [ ] Hide unavailable fields instead of showing disabled noise for every possible property.
- [ ] Preserve unsupported/deferred runtime fields rather than inventing MFME-specific editor concepts.
- [ ] Add tests for at least Lamp, Reel, SevenSegment, Alpha, Rectangle, and Image property population.

### Phase X — Inspector Editing Integration and Manual Validation Notes
- [ ] Ensure active document changes refresh the Inspector field list.
- [ ] Ensure canvas selection changes refresh the Inspector field list.
- [ ] Ensure hierarchy selection changes refresh the Inspector field list.
- [ ] Ensure deleting the selected element clears or safely resets the Inspector.
- [ ] Ensure undo/redo after Inspector edits refreshes the Inspector, canvas, and hierarchy.
- [ ] Ensure save/reopen preserves Inspector-edited properties.
- [ ] Ensure imported MFME elements can be selected and edited through the Inspector as native Oasis elements.
- [ ] Document a local manual smoke-test checklist for John:
  - [ ] Create/open a project.
  - [ ] Open a `.panel2d` asset.
  - [ ] Select an element from canvas and hierarchy.
  - [ ] Edit X/Y and confirm the element moves.
  - [ ] Edit Width/Height and confirm the element resizes.
  - [ ] Edit Name and confirm hierarchy updates.
  - [ ] Toggle Locked/Visible and confirm behavior matches hierarchy context menu behavior.
  - [ ] Undo/redo each edit.
  - [ ] Save, close, reopen, and confirm edited values persist.
  - [ ] Import an MFME extract and edit at least one Background, Lamp, Reel, SevenSegment, and Alpha.

---

## Active Carryover — Local Verification Required

Codex cannot run these checks in its local workspace. John should run them locally after the next relevant implementation pass.

### MFME Import Track Verification
- [ ] Build and run tests for Phases L through T.
- [ ] Smoke test importing into an empty project/document.
- [ ] Verify imported assets are copied under `Assets/MfmeImport/...`.
- [ ] Verify imported native Oasis Panel2D elements appear at expected positions/sizes.
- [ ] Verify hierarchy groups show imported native elements.
- [ ] Verify selection and Inspector work for imported native elements.
- [ ] Verify save/reopen preserves imported native elements and asset paths.
- [ ] Verify undo/redo of import works document-locally.

### Previous Context Menu / Assets Pane Verification
- [ ] Create/open project.
- [ ] Refresh assets.
- [ ] Navigate folders in Assets pane.
- [ ] Open a `.panel2d` asset.
- [ ] Use hierarchy context menu rename/delete/duplicate/copy/paste/cut.
- [ ] Use undo/redo after hierarchy mutations.
- [ ] Save/reopen and verify panel contents.

---

## Completed History Summary

The detailed historical task list was consolidated during housekeeping. Completed work includes:

- Startup, project, shell, document, theme, command, and Panel2D MVP work.
- WPF maintainability refactors, including extracted views/ViewModels and canvas behavior splits.
- Editor stability and document-context work, including document-specific undo/redo and active document context rules.
- Panel2D model stabilization, storage validation, migration hooks, stable object IDs, hierarchy/inspector identity rules, and command-based mutations.
- Context menu and Unity-style Assets pane work, including hierarchy entity commands and asset item commands.
- MFME extract import planning and implementation through import boundary, DTOs, native component expansion, conversion, asset copy, visual projection, undoable import command, import UI entry point, and end-to-end cleanup planning.

## Backlog — Later Tracks

- [ ] Accurate lamp rendering and masks.
- [ ] Accurate reel viewport and overlay handling.
- [ ] Seven-segment renderer.
- [ ] Alpha renderer.
- [ ] Native button/input mapping.
- [ ] Runtime/export mapping for the Unity arcade app.
- [ ] Layer ordering polish.
- [ ] Object locking/visibility polish.
- [ ] More right-click context menus across editor panes.
- [ ] Cabinet import MVP planning.
- [ ] Machine assembly MVP planning.
