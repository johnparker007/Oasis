# TASKS.md

## Current Focus — Unity-Style Inspector Editing for Panel2D Elements

PRIMARY IMPLEMENTATION REFERENCE:
- Docs/InspectorEditingPlan.md

Codex MUST read and follow that document before implementing any Inspector work.

Goal: replace the current read-only/summary-style Inspector with a Unity-style property editor for the selected Panel2D element.

---

## Execution Rules (READ FIRST)

- Do NOT attempt to build or run tests in Codex.
- Do NOT create Build/Test attempt logs.
- Implement code only.
- After each task, state what John must test locally.
- Follow Docs/InspectorEditingPlan.md for all architectural decisions.
- Exception to the existing "No new frameworks" rule: Phase Z explicitly allows adding the `PixiEditor.ColorPicker` NuGet package to `OasisEditor` only. Do not add any other UI/control framework for this work.

---

## Phase U — Foundation (DO FIRST)

### U1 — Element Update Infrastructure
- [x] Add `PanelElementModelUpdate` (or equivalent updater structure)
- [x] Ensure all editable properties can be represented
- [x] Do NOT break existing cloning logic

### U2 — Generic Update Command
- [x] Add `CreateUpdateElementCommand(...)`
- [x] Must:
  - [x] target documentId
  - [x] target objectId
  - [x] store previous + new snapshot
  - [x] skip no-op updates
  - [x] mark document dirty only when needed

### U3 — Validation Layer
- [x] Add numeric validation helpers
- [x] Width/Height > 0 enforced
- [x] Invalid edits must not execute commands

### U4 — Tests (logic only)
- [x] Update command tests
- [x] Undo/redo tests
- [x] No-op prevention tests

---

## Phase V — Inspector Property System

### V1 — Property Row ViewModels
- [x] Add base property row VM
- [x] Add:
  - [x] string
  - [x] double
  - [x] int
  - [x] bool
  - [x] read-only/info

### V2 — Binding Strategy
- [x] Property rows must:
  - [x] read from selected element
  - [x] issue commands on change
  - [x] NOT mutate model directly

### V3 — Commit Behavior
- [x] Text/numeric commit on Enter or focus loss
- [x] Bool commit immediately
- [x] Avoid flooding undo stack

---

## Phase W — Inspector UI

### W1 — Replace Summary UI
- [x] Replace current InspectorView layout
- [x] Add grouped layout:
  - [x] Transform
  - [x] Common
  - [x] Type-specific
  - [x] Metadata

### W2 — Common Fields
- [x] Name
- [x] ObjectId (read-only)
- [x] Kind (read-only)
- [x] X/Y
- [x] Width/Height
- [x] Locked
- [x] Visible

### W3 — Canvas Integration
- [x] X/Y changes move element
- [x] Width/Height changes resize element
- [x] Name updates hierarchy

---

## Phase X — Element-Specific Fields

- [x] Lamp fields
- [x] Reel fields
- [x] SevenSegment fields
- [x] Alpha fields
- [x] Image/Background asset fields

- [x] Hide irrelevant fields

---

## Phase Y — Integration & Stability

- [x] Selection change refreshes Inspector
- [x] Undo/redo refreshes Inspector
- [x] Deleting selection clears Inspector
- [x] Save/load preserves edits

---

## Phase Z — Inspector Color Picker Integration

Purpose: replace Inspector color fields that are currently editable hex-code text rows with an MVVM-friendly popup color picker. Use `PixiEditor.ColorPicker`, specifically `PortableColorPicker`, so the Inspector shows a compact swatch/control that opens the picker when clicked.

### Z0 — Read Existing Inspector Work First
- [ ] Read `Docs/InspectorEditingPlan.md` before making changes.
- [ ] Inspect the current completed Inspector implementation, especially:
  - [ ] `OasisEditor/ViewModels/InspectorViewModel.cs`
  - [ ] existing `InspectorPropertyRowViewModel` subclasses
  - [ ] `InspectorView.xaml`
  - [ ] existing Inspector tests
- [ ] Preserve the existing command-based mutation pattern: color edits must still update elements through `PanelElementModelUpdate` and `CanvasMutationCommands.CreateUpdateElementCommand(...)` or the existing equivalent update path.
- [ ] Do not directly mutate `PanelElementModel`.
- [ ] Do not add code-behind except minimal WPF event glue if unavoidable; prefer binding/commands.

### Z1 — Add Color Picker Dependency
- [ ] Add `PixiEditor.ColorPicker` to `WindowsNetProjects/OasisEditor/OasisEditor/OasisEditor.csproj`.
- [ ] Keep existing AvalonDock package references unchanged.
- [ ] Do not add Extended WPF Toolkit, MahApps, MaterialDesign, or any other control framework for this task.
- [ ] Use the current stable package version available to NuGet at implementation time. If Codex cannot query NuGet, use `3.4.2.3` as the known-good baseline.

### Z2 — Add a Color Property Row ViewModel
- [ ] Add an `InspectorColorPropertyViewModel` or equivalent alongside the existing row ViewModels.
- [ ] It must remain UI-agnostic except for using `System.Windows.Media.Color` if that is consistent with the WPF ViewModel layer.
- [ ] Expose at minimum:
  - [ ] `DisplayName`
  - [ ] `GroupName`
  - [ ] `Color Value` / `SelectedColor` as a bindable two-way color property
  - [ ] original/current hex string if useful for validation, copy/paste, diagnostics, or tests
  - [ ] `ErrorText` consistent with existing row validation patterns
- [ ] On a committed color change, convert the selected color back to canonical hex and call the same update command path used by the existing text color rows.
- [ ] Avoid creating undo commands for no-op color selections.
- [ ] Prefer canonical `#AARRGGBB` storage if alpha is enabled and safe for existing data. If the existing model/import data expects `#RRGGBB`, preserve `#RRGGBB` for fully opaque colors and only emit `#AARRGGBB` when alpha is non-opaque.

### Z3 — Add Color Parsing and Formatting Helpers
- [ ] Add focused helpers for parsing and formatting color hex strings.
- [ ] Accept existing imported/editor values without breaking older data:
  - [ ] `#RRGGBB`
  - [ ] `RRGGBB`
  - [ ] `#AARRGGBB`
  - [ ] `AARRGGBB`
- [ ] Reject invalid color strings safely without changing the model.
- [ ] For blank optional color fields, preserve the existing null/blank behavior if that field is optional.
- [ ] Add tests for parsing, formatting, invalid input, null/blank optional values, and no-op conversions.

### Z4 — Replace Text Rows for Model-Backed Color Fields
- [ ] In `InspectorViewModel.AddTypeSpecificRows(...)` or the current field builder equivalent, replace text rows with color rows for:
  - [ ] `OnColorHex`
  - [ ] `OffColorHex`
  - [ ] `TextColorHex`
- [ ] Apply this to all element kinds currently exposing those fields:
  - [ ] Lamp: On Color, Off Color, Text Color
  - [ ] SevenSegment: On Color
  - [ ] Alpha: On Color and/or Text Color as currently model-backed
- [ ] Keep non-color text fields as text rows:
  - [ ] Asset Path
  - [ ] Secondary Asset
  - [ ] Display Text
- [ ] Do not invent new color fields that are not already model-backed.

### Z5 — Render Color Rows in the Inspector UI
- [ ] Add a DataTemplate or equivalent XAML rendering path for `InspectorColorPropertyViewModel`.
- [ ] Use `ColorPicker:PortableColorPicker` from `PixiEditor.ColorPicker`.
- [ ] Bind the picker selected color two-way to the color row ViewModel.
- [ ] Configure the control for compact Inspector usage:
  - [ ] popup opens from the compact color swatch/control when clicked
  - [ ] alpha enabled if existing fields or rendering can support alpha safely
  - [ ] compact width/height consistent with existing Inspector row layout
- [ ] Keep the row aligned with the existing Inspector labels and grouping.
- [ ] Preserve keyboard focus behavior and avoid breaking Enter/focus-loss commit behavior for existing text and numeric rows.

### Z6 — Copy/Paste and Manual Hex Entry
- [ ] Confirm whether `PortableColorPicker` already provides copy/paste and hex entry for selected colors.
- [ ] If it does, use the package-provided behavior and do not duplicate it.
- [ ] If it does not expose copy/paste in the compact control, add a minimal context menu or adjacent hex field only for color rows:
  - [ ] Copy Hex
  - [ ] Paste Hex
  - [ ] optional visible hex text for diagnostics/user clarity
- [ ] Paste must validate with the same parsing helper from Z3.
- [ ] Invalid paste must not update the model and must show the row error state.

### Z7 — Rendering/Preview Consistency
- [ ] Confirm that changing color fields through the picker updates the selected element through the same command path as text edits.
- [ ] Confirm that the canvas redraws/reprojects after color changes where the selected element's renderer already uses those color fields.
- [ ] Confirm save/load preserves the edited color hex values exactly or in the chosen canonical format.
- [ ] Do not implement new element rendering behavior unless needed to keep existing color rendering working.

### Z8 — Tests
- [ ] Add or update tests for `InspectorColorPropertyViewModel`.
- [ ] Add or update `InspectorViewModel` tests proving color-backed elements produce color rows, not generic text rows.
- [ ] Test color commits for:
  - [ ] Lamp On Color
  - [ ] Lamp Off Color
  - [ ] Lamp Text Color
  - [ ] SevenSegment On Color
  - [ ] Alpha color fields that are currently exposed
- [ ] Test invalid color input/paste does not execute an update command.
- [ ] Test no-op color selection does not execute an update command.
- [ ] Test undo/redo still works for color edits through the generic update command path.

### Z9 — Local Testing for John
After Codex completes this phase, John must test locally:

- [ ] Build `WindowsNetProjects/OasisEditor/OasisEditor.sln` or the appropriate OasisEditor solution in Visual Studio.
- [ ] Run the OasisEditor test project.
- [ ] Open OasisEditor and load/create a project.
- [ ] Open a `.panel2d` asset.
- [ ] Select a Lamp and verify On Color, Off Color, and Text Color show popup color picker controls rather than plain editable hex text fields.
- [ ] Select a SevenSegment and verify On Color shows a popup color picker.
- [ ] Select an Alpha element and verify all currently model-backed color fields show popup color pickers.
- [ ] Click a color swatch/control and verify the picker opens.
- [ ] Change a color and verify the canvas/preview updates if existing rendering supports that field.
- [ ] Copy a color value and paste it into another color field.
- [ ] Undo and redo at least one color edit.
- [ ] Save, close, reopen, and verify edited colors persist.
- [ ] Verify non-color fields such as Asset Path and Display Text still behave as before.

---

## Local Testing (John)

After each phase:

- Build solution
- Run tests
- Open project
- Edit element properties
- Verify:
  - movement
  - resizing
  - undo/redo
  - save/load persistence

---

## DO NOT DO

- No direct mutation of PanelElementModel
- No WPF logic in model/domain
- No large refactors outside this scope
- No new frameworks except the explicit `PixiEditor.ColorPicker` dependency allowed by Phase Z

---

## Backlog

(unchanged, handled later)
