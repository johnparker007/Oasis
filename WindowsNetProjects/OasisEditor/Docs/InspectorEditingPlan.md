# Inspector Editing Plan

## Purpose

This document describes the next OasisEditor implementation track: making the Inspector pane behave like the Unity Inspector for Panel2D elements.

When a user selects an element in the Hierarchy or on the Panel2D canvas, the Inspector should show editable controls for that element. Editing a control should update the selected model-backed element, refresh the canvas, refresh the Hierarchy/Inspector, mark the document dirty, and support undo/redo.

This plan is intentionally limited to 2D Panel editing. Do not start Blender, 3D cabinet import, machine assembly, or Unity runtime export work in this track.

## Current State

The Inspector currently shows summary strings and document/asset/project details. For selected Panel2D elements, `InspectorViewModel` builds a descriptive string via `BuildSelectedElementSummary(...)`.

The useful existing foundations are:

- `PanelElementModel` is the live UI-agnostic element model.
- `PanelElementModel` is immutable via `init` properties.
- `DocumentTabViewModel.SetPanelElements(...)` replaces the document element list and updates the Panel2D layout JSON projection.
- `CanvasMutationCommands` already contains document-scoped, undoable commands for element add/delete/rename/duplicate/paste/reorder/lock/visibility.
- Selection is already document-scoped through `ActiveDocumentContextService` and object-ID-based where possible.

The Inspector must build on these patterns rather than inventing parallel mutation logic.

## Design Rules

### Mutations

- Do not directly mutate `PanelElementModel` instances.
- Do not bind WPF controls directly to mutable model properties.
- Use clone-and-replace semantics for all element changes.
- All edits that change document content must go through a document-scoped command.
- Commands must target a specific document ID and a specific object ID.
- Commands must not apply to whichever document happens to be active later.
- No-op edits must not enter undo history.
- Invalid edits must not corrupt the document.

### Inspector ViewModel

- The Inspector ViewModel should expose structured property rows for selected Panel2D elements.
- Property rows should be simple, testable ViewModels.
- Property row ViewModels should not depend on WPF controls, brushes, or dependency properties.
- The Inspector should rebuild its property rows when:
  - active document changes
  - selected element changes
  - the selected document's Panel2D element list changes
  - undo/redo changes the selected element
  - the selected element is deleted

### Inspector View

- `InspectorView.xaml` should render property rows using templates or a simple grouped layout.
- Avoid large one-off hardcoded forms that make every element kind difficult to maintain.
- Hide irrelevant fields rather than showing every possible field disabled.
- Keep visual styling consistent with existing semantic resources and theme rules.
- Code-behind should be avoided. If WPF event glue is unavoidable, it must immediately delegate to ViewModel commands/methods.

### Validation

- X and Y may be any valid finite `double` unless a future task defines canvas bounds.
- Width and Height must be finite and greater than zero.
- Display numbers, reel stops, and similar integer fields must parse as valid integers.
- Optional numeric properties may be blank only when the model supports `null` for that property.
- Hex color fields should preserve existing strings initially; add strict validation only if it can be done without blocking current imported data.
- Invalid text input must not update the model.
- Property rows should expose a validation/error state or restore the last valid value.

## Suggested Implementation Shape

### 1. Element Update Helper

Add either a richer `PanelElementModelCloner.Clone(...)` or a new `PanelElementModelUpdater`.

Recommended option: add a focused updater so broad property replacement does not make `PanelElementModelCloner.Clone(...)` unwieldy.

Example shape:

```csharp
internal sealed class PanelElementModelUpdate
{
    public string? Name { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public string? AssetPath { get; init; }
    public string? SecondaryAssetPath { get; init; }
    public int? DisplayNumber { get; init; }
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public string? TextColorHex { get; init; }
    public string? DisplayText { get; init; }
    public bool? IsReversed { get; init; }
    public int? Stops { get; init; }
    public double? VisibleScale { get; init; }
    public bool? IsLocked { get; init; }
    public bool? IsVisible { get; init; }
}
```

If null must mean "set property to null" for optional properties, use explicit optional wrappers rather than plain nullable values. Do not lose the ability to clear optional fields if the UI needs that later.

### 2. Generic Element Update Command

Add a generic document-scoped command, likely in `CanvasMutationCommands`, for replacing one selected element with an updated element.

Recommended factory shape:

```csharp
public static Commands.ICommand CreateUpdateElementCommand(
    Guid documentId,
    DocumentTabViewModel document,
    string objectId,
    PanelElementModel updatedElement,
    string description)
```

Command behavior:

- Resolve the existing element by object ID.
- Validate document ID by using the existing document-scoped command service pattern.
- Compare old and new snapshots. If equal, do not execute and do not enter undo history.
- Replace exactly one element in the list.
- Preserve the element's `ObjectId` and `Kind` unless a future migration task explicitly changes them.
- Store old and new snapshots for undo/redo.
- Mark document dirty only when replacement succeeds.
- Fail safely if the element no longer exists.

### 3. Inspector Property Row ViewModels

Add property rows that can be displayed by `InspectorView.xaml`.

Suggested minimal types:

- `InspectorPropertyRowViewModel` base/abstract class
  - `DisplayName`
  - `IsReadOnly`
  - `ErrorText`
  - optional `GroupName`
- `InspectorTextPropertyViewModel`
- `InspectorDoublePropertyViewModel`
- `InspectorIntPropertyViewModel`
- `InspectorBoolPropertyViewModel`
- optional `InspectorInfoPropertyViewModel`

Each editable row should know how to request a model update through the parent Inspector ViewModel, not through WPF controls.

Avoid a design where every keypress creates a new undo command unless that is explicitly wanted. Prefer one of these approaches:

- Commit on lost focus/Enter for text and numeric fields.
- Or use `Delay` plus no-op coalescing if immediate live editing is required.

For the first milestone, commit-on-lost-focus/Enter is safer and keeps undo history usable. Checkboxes may commit immediately.

### 4. Field Builder

Add a field builder in or near `InspectorViewModel` that maps a selected `PanelElementModel` into rows.

Common fields for all elements:

- Name: editable string.
- Object ID: read-only string.
- Kind: read-only string.
- X: editable double.
- Y: editable double.
- Width: editable double, > 0.
- Height: editable double, > 0.
- Locked: editable bool.
- Visible: editable bool.

Type-specific fields:

#### Rectangle

- Show currently model-backed visual fields only.
- If the model has no explicit fill/color field, do not invent one in this track.

#### Image

- Asset path: editable string initially.
- Consider browse/picker UX later.

#### Background

- Asset path.
- Secondary asset path if relevant.
- Color fields only where already represented by the native model.
- Optional import source as read-only.

#### Lamp

- Lamp/display number.
- Asset path.
- On color.
- Off color.
- Text color.
- Display text.
- Optional import source as read-only.

#### Reel

- Reel/display number.
- Band/asset path.
- Secondary asset path if relevant.
- Stops.
- Reversed.
- Visible scale.
- Optional import source as read-only.

#### SevenSegment

- Display number.
- On/display color.
- Optional import source as read-only.

#### Alpha

- Display text where supported.
- Text/on color where supported.
- Reversed.
- Optional import source as read-only.

## UI Layout Guidance

Use a simple Inspector layout similar to Unity:

- Header: selected element display name and kind.
- Transform/Geometry group: X, Y, Width, Height.
- Common group: Name, Locked, Visible.
- Type-specific group: fields for the selected kind.
- Metadata group: Object ID, import source, asset paths if read-only or advanced.

Reasonable first-pass controls:

- TextBox for strings.
- TextBox for numeric values with validation.
- CheckBox for booleans.
- TextBlock for read-only rows.

Do not introduce custom control libraries.

## Refresh Rules

After any command changes selected element data:

- `DocumentTabViewModel.SetPanelElements(...)` should raise `PanelLayoutJson` change.
- Canvas projection should redraw/reposition from the updated JSON/model.
- Hierarchy should refresh display names and groups where needed.
- Inspector should refresh selected rows from the updated model.

If existing notifications are insufficient, add focused notifications rather than broad global refresh hacks.

## Tests to Add or Update

Add tests where practical under the existing OasisEditor test project.

Recommended tests:

### Command Tests

- Updating X/Y changes only the targeted object.
- Updating Width/Height changes only the targeted object.
- Undo restores the previous snapshot.
- Redo restores the new snapshot.
- No-op update does not execute.
- Missing object does not execute.
- Wrong document target is rejected by existing command service/document command rules.
- Invalid width/height is rejected before command creation or command execution.

### Inspector ViewModel Tests

- Selected rectangle/image/lamp/reel/seven-segment/alpha produces expected common rows.
- Lamp produces lamp number and color/text rows.
- Reel produces reel number, stops, reversed, and visible scale rows.
- SevenSegment produces display number/color rows.
- Alpha produces reversed/text rows where model-backed.
- Non-element selection still shows existing asset/document/project summary behavior.
- Deleted selected object clears element property rows safely.

### Storage Round Trip Tests

Only add these if existing coverage does not already prove it:

- Inspector-edited native element properties save and load correctly.
- Optional fields remain optional.
- Existing schema version behavior is unchanged.

## Local Validation Checklist for John

Codex must not run these. John will run them locally.

- Build the OasisEditor solution in Visual Studio.
- Run the OasisEditor test project.
- Create/open a project.
- Open a `.panel2d` asset.
- Select a rectangle/image on the canvas.
- Select the same element from the hierarchy.
- Edit X and Y and confirm the element moves.
- Edit Width and Height and confirm the element resizes.
- Edit Name and confirm the hierarchy updates.
- Toggle Locked and Visible and confirm behavior matches the existing context-menu behavior.
- Undo/redo each kind of edit.
- Save, close, reopen, and confirm values persist.
- Import an MFME extract and edit at least one Background, Lamp, Reel, SevenSegment, and Alpha as native Oasis elements.

## Deferred Work

Do not implement these unless a later task explicitly says to:

- Multi-object Inspector editing.
- Drag-and-drop asset assignment into Inspector fields.
- Asset picker/browse buttons.
- Color picker dialogs.
- Numeric scrubber controls.
- Rich mask/lamp rendering.
- Accurate reel viewport/overlay rendering.
- Runtime/export mapping.
- 3D cabinet or Blender import work.
