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
  - TODO: record concrete files/classes here.
- Old Rectangle/Image add test methods.
  - TODO: record concrete files/classes here.
- Existing tests around Panel2D document mutation.
  - TODO: record concrete files/classes here.

## Investigation checklist

Before implementation, answer these questions in this document:

- [ ] What is the canonical in-memory collection for elements on a Panel2D?
- [ ] What IDs/properties are required for a new element to be valid?
- [ ] What command type, service, or pattern is used for undoable Panel2D edits?
- [ ] How does redo preserve identity and state?
- [ ] How is the selected element set after an edit?
- [ ] How do hierarchy and inspector views learn about model changes?
- [ ] How does the Skia editor convert mouse coordinates to Panel2D coordinates?
- [ ] Which defaults make each new element visible immediately?
- [ ] Which tests can cover the model/command path without brittle UI automation?

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

- TODO

## Open questions

Record any decisions needed from John here:

- TODO
