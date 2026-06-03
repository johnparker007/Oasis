# Add Panel2D Element Context Menu - Investigation

## Goal

Add a first-pass right-click context menu to the Panel2D editor so real Oasis elements can be added directly to a Panel2D at the mouse pointer location.

## User-facing behaviour

- Right-clicking on a Panel2D edit surface opens an add-element context menu.
- The initial menu contains:
  - Add Lamp
  - Add Reel
  - Add 7 Segment Display
  - Add Segment Alpha
- Selecting one item creates that element on the clicked Panel2D.
- The new element is positioned at the Panel2D coordinate under the mouse pointer.
- The add operation participates in normal undo/redo.

## Current context

At the time this work is being started, Panel2D elements are mainly populated through MFME extraction/import. There were older test flows for adding `Rectangle` and `Image` elements, with undo/redo support per Panel2D. Treat those old flows as useful implementation references only; the new UI should expose real Oasis element types instead.

The right mouse button has intentionally been kept free on the Panel2D edit surface and is now intended for this context menu.

## Existing code paths to inspect

Start by inspecting these files/classes and update this section with concrete findings before broad implementation changes:

- `OasisEditor/Views/SkiaPanel2DEditView.xaml.cs`
  - Right-click / pointer handling.
  - Existing coordinate conversion from screen/view coordinates into Panel2D document coordinates.
  - Existing selection and refresh behaviour after edits.
- `OasisEditor/PanelElementFactory.cs`
  - Existing factory/default construction paths.
  - Whether real element types already have default constructors or helper creation methods.
- `OasisEditor/Rendering/Panel2DRenderer.cs`
  - How the supported element types are rendered.
  - Whether any default properties are required for visible output.
- `OasisEditor/Features/MfmeImport/MfmeToOasisComponentMapper.cs`
  - How MFME-imported components map into real Oasis elements.
  - Useful defaults for Lamp, Reel, 7 Segment Display, and Segment Alpha.
- Existing undo/redo command infrastructure.
  - `OasisEditor/Commands/CommandService.cs` executes commands, validates document ownership for `IDocumentCommand`, records commands in `CommandHistory`, and drives undo/redo. It respects `IExecutionTrackedCommand.WasExecuted` so no-op commands are not recorded.
  - `OasisEditor/Commands/CommandHistory.cs` stores the linear undo/redo stack. Redo re-executes the same command instance, which means command instances must retain identity/state needed for stable redo.
  - `OasisEditor/CanvasMutationCommands.cs` contains the current Panel2D mutation commands, including `AddPanelElementMutationCommand`, `DeleteElementMutationCommand`, and update/reorder/visibility/lock commands.
  - `CanvasMutationCommands.CreateAddRectangleCommand` and `CreateAddImageCommand` wrap a `PanelElementFile` in `AddPanelElementMutationCommand`; this command converts the storage element to a `PanelElementModel`, inserts it, raises a structure change, and marks the document dirty.
- Old Rectangle/Image add test methods.
  - `OasisEditor/PanelToolPlacementController.cs` still contains the old rectangle/image placement flow. It uses `PanelElementFactory.CreateRectangleElement`/`CreateImageElement` and `CanvasMutationCommands.CreateAddRectangleCommand`/`CreateAddImageCommand`. This remains a useful reference for factory + command wiring only.
  - `OasisEditor.Tests/Panel2DRoundTripTests.cs` has coverage around rectangle add and hierarchy refresh, plus existing undo/redo mutation tests.
- Existing tests around Panel2D document mutation.
  - `OasisEditor.Tests/ImportMfmeExtractCommandTests.cs` verifies import add mutations, history tracking, and stable undo/redo identity for imported elements.
  - `OasisEditor.Tests/Panel2DRoundTripTests.cs` covers storage round trips, hierarchy provider behaviour, and many mutation commands. The new add service/command tests can live in a focused new test file without UI automation.

## Investigation checklist

Before implementation, answer these questions in this document:

- [x] What is the canonical in-memory collection for elements on a Panel2D?
  - `DocumentTabViewModel` owns a `Panel2DDocumentModel`; its immutable-style `Elements` list is exposed through `GetPanelElements()` and replaced through `SetPanelElements(...)`. `SetPanelElements` also rebuilds runtime lookup caches, updates `PanelLayoutJson`, raises `PropertyChanged`, and optionally raises `PanelChanged`.
- [x] What IDs/properties are required for a new element to be valid?
  - New elements need a non-empty `ObjectId`, a user-visible `Name`, a real `PanelElementKind`, `X`, `Y`, `Width`, `Height`, and should remain `IsVisible` by default. Storage uses `PanelElementFile.Kind` string values produced by `Panel2DDocumentStorage.SerializeElementKind(...)`.
  - Element-specific useful defaults are needed for visible rendering: lamps need dimensions and colors; reels need dimensions and a placeholder-visible state if no band image exists; seven-segment and alpha displays need dimensions and on colors, with alpha using a known `SegmentDisplayType` such as `led16seg`.
- [x] What command type, service, or pattern is used for undoable Panel2D edits?
  - Panel2D edits are `OasisEditor.Commands.ICommand` instances, normally document-scoped via `IDocumentCommand`, executed through `DocumentTabViewModel.CommandService`. The existing add command is `CanvasMutationCommands.AddPanelElementMutationCommand`; it already raises `PanelChangeEvent` with canvas/hierarchy/inspector/persistence flags and marks dirty.
- [x] How does redo preserve identity and state?
  - `CommandHistory` stores the same command object; redo calls `Execute()` on that object. `AddPanelElementMutationCommand` stores the original `PanelElementFile` and previous insert index, so redo reconverts the same stored file with the same `ObjectId`/properties and restores it at the same index.
- [x] How is the selected element set after an edit?
  - Selection is held in `DocumentTabViewModel.HierarchySelectedPanelSelection`. Skia selection clicks call `Panel2DSelectionNotificationService.NotifySelection(...)`, which routes through `MainWindowViewModel.UpdateDocumentPanelSelection(...)` when a shell view model is available, otherwise sets `HierarchySelectedPanelSelection` directly.
- [x] How do hierarchy and inspector views learn about model changes?
  - `DocumentTabViewModel.SetPanelElements(...)` raises `PanelChanged` when passed a `PanelChangeEvent`. Existing structure changes set `AffectsCanvas`, `AffectsHierarchy`, `AffectsInspectorRows`, and `AffectsPersistence` to `true`, which is the correct event shape for an add. `PanelLayoutJson` property changes also notify persistence/projection observers.
- [x] How does the Skia editor convert mouse coordinates to Panel2D coordinates?
  - `SkiaPanel2DEditView` constructs `PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY)` and uses `ScreenToDocument(Point)` for selection, drag selection, move, and resize computations. The context menu should reuse that transform with `eventArgs.GetPosition(EditSkiaSurface)`.
- [x] Which defaults make each new element visible immediately?
  - Renderer findings: `LampElementRenderer` draws a solid rectangle when no `DisplayText` or asset exists, using `OnColorHex`/`OffColorHex` and current lamp intensity; because default runtime intensity is normally 0, a visible dark/off color is required. `ReelElementRenderer` draws a labelled placeholder when no asset exists. `SevenSegmentElementRenderer` draws a lit default mask when runtime masks are absent, using `OnColorHex`. `AlphaElementRenderer` similarly draws default lit cells when a loadable display definition is available; `SegmentDisplayDefinitionLoader` supports `led16seg`.
  - First-pass defaults: Lamp 80x40 with red/off-red colors and text font defaults; Reel 120x180 with display number 1, stops 24, visible scale 3/24; 7 Segment 80x120 with red on color; Segment Alpha 220x60 with `SegmentDisplayType = led16seg`, amber on color, and reversed false. Positions should use the clicked document coordinate as top-left, per the requested behaviour.
- [x] Which tests can cover the model/command path without brittle UI automation?
  - Add focused unit tests for a new creation path and command wrapper: create every supported addable type, assert required defaults and requested top-left position, execute through `DocumentTabViewModel.CommandService`, assert history count, selection, `PanelChanged` flags, undo removal, and redo restoration of the same identity/properties/position. UI context-menu automation is not necessary for this pass.

## Proposed implementation shape

Prefer a thin UI layer and a tested model/command layer.

A suitable implementation may look like this, adjusted to match the existing architecture discovered above:

1. Add a small descriptor/enum for supported addable element types.
2. Add or extend a factory method that creates supported real elements with sensible defaults:
   - Lamp
   - Reel
   - 7 Segment Display
   - Segment Alpha
3. Add or reuse an undoable command that adds a supplied element to a supplied Panel2D.
4. Wire the Panel2D edit view right-click handler to:
   - capture mouse position,
   - convert it to Panel2D coordinates,
   - display a context menu,
   - invoke the command for the selected element type.
5. After command execution, ensure normal editor state updates happen:
   - render invalidation,
   - selected element update if consistent with existing behaviour,
   - hierarchy update,
   - inspector update,
   - dirty state / document changed notification.

## Initial defaults guidance

Use existing imported/default element data as the source of truth where possible.

If no default exists yet, choose small visible defaults and keep them centralised in the factory so they can be tuned later without changing UI code.

Do not duplicate MFME import mapping logic into the UI. Shared construction defaults should live in an appropriate factory/model service.

## Tests to add or update

Prefer tests at the command/factory/model level. UI tests are optional unless the project already has reliable infrastructure for this.

Minimum desired automated coverage:

- Creating each supported element type through the new creation path returns a valid element.
- Adding a real element to a Panel2D increases the element count and stores the requested position.
- Undo removes the added element.
- Redo restores the same element with the same identity/properties/position.

At least one real element type must be covered by add + undo + redo. Cover all four if that is practical without excessive fixture setup.

## Non-goals for this pass

- No palette or toolbox UI.
- No drag/drop from a palette.
- No generic plugin system.
- No asset picker.
- No full schema redesign.
- No MFME import behaviour changes, except where harmless shared factory code avoids duplication.
- No large visual redesign of the editor.

## Implementation notes

Record final code touch points here once implemented:

- `OasisEditor/PanelElementFactory.cs` — add a real-element creation method/enum with centralized defaults.
- `OasisEditor/CanvasMutationCommands.cs` — expose a generic real-element add command and update add command selection behaviour.
- `OasisEditor/Views/SkiaPanel2DEditView.xaml.cs` — add thin right-click context menu wiring that converts pointer coordinates and executes add commands.
- `OasisEditor.Tests/AddPanel2DElementCommandTests.cs` — new focused factory/command undo/redo tests.
- `Docs/AddPanel2DElement.ContextMenu.Investigation.md` and `Docs/AddPanel2DElement.ContextMenu.Verification.md` — record findings/results.

## Open questions

Record any decisions needed from John here:

- None for this first pass. Default sizes/colors are intentionally centralized in `PanelElementFactory` so they can be tuned later.
