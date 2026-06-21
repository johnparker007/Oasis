# Cabinet Model Viewer Plan

## Purpose

Add the first stage of 3D cabinet support to Oasis Editor.

The immediate goal is not full cabinet editing. The first milestone is a simple editor window/document that can load a `.glb` cabinet model and display it in a usable 3D viewport. Later work will add editable flat glass/face mapping surfaces on top of the model.

This feature is part of the longer-term workflow:

1. Users author/export common cabinet models as `.glb` files.
2. Oasis Editor loads the `.glb` for preview and setup.
3. Users define named flat quad surfaces where existing Oasis face documents will be mapped.
4. Oasis saves surface metadata separately from the `.glb`.
5. The later Unity runtime/player loads the same `.glb` plus Oasis metadata and renders the faces/lamps onto those surfaces.

## Non-goals for the first milestone

Do not implement these in the first task unless explicitly requested:

- Surface/quad editing.
- Face document assignment to cabinet surfaces.
- Lamp rendering on the 3D model.
- Runtime Unity export.
- Full PBR material parity with Unity.
- Complex asset packaging or online loading.
- Editing/saving data into the `.glb` itself.

## Core principle

The `.glb` is the cabinet geometry/material asset. Oasis-specific data must be stored separately.

Do not mutate or re-export the `.glb` just to store Oasis metadata.

Preferred future file pairing:

```text
Assets/Cabinets/GenesisCabinet.glb
Assets/Cabinets/GenesisCabinet.cabinet3d
```

The `.cabinet3d` file should eventually reference the `.glb` and contain Oasis-specific metadata such as named face surfaces.

## First milestone scope

Implement a basic Cabinet Model Viewer that can:

- Open/select a `.glb` from the project/assets workflow or a simple file picker, depending on existing editor patterns.
- Display the model in a dock/document/window area consistent with the current editor architecture.
- Provide basic camera controls:
  - orbit
  - pan
  - zoom
  - reset view
- Show basic scene helpers where practical:
  - model bounds
  - origin/grid
  - simple XYZ axes indicator or equivalent orientation cue
- Fail gracefully when the file cannot be loaded.
- Avoid blocking the UI on large model load errors where practical.

Keep the implementation minimal and testable.

## Rendering technology guidance

Pick the lowest-risk approach that works inside the existing WPF editor.

Important requirements:

- Must run inside a WPF editor window/document.
- Must load `.glb`/glTF 2.0 binary files.
- Should be suitable as a foundation for later quad overlays and picking.
- Should not require launching Unity for the first milestone.
- Should not put rendering/domain logic directly in WPF code-behind.

Acceptable first-stage compromises:

- Basic material display is enough initially.
- Perfect Unity/PBR parity is not required yet.
- If full texture/material support is difficult, still keep the architecture capable of being improved later.

Before adding a dependency, inspect the existing project package style and choose a dependency that is compatible with the current target framework and WPF setup.

## Suggested architecture

Keep a clear split between UI, document state, and model data.

Suggested feature area:

```text
OasisEditor/Features/CabinetEditor/
```

Possible structure:

```text
Features/CabinetEditor/
  Models/
    CabinetDocument.cs
    CabinetModelReference.cs
  ViewModels/
    CabinetModelDocumentViewModel.cs
    CabinetViewportViewModel.cs
  Views/
    CabinetModelDocumentView.xaml
    CabinetModelDocumentView.xaml.cs
  Services/
    ICabinetModelLoader.cs
    CabinetModelLoadResult.cs
```

Adapt names and paths to existing project conventions.

WPF view code-behind should remain thin glue only. Put load state, commands, and user-facing state in ViewModels/services.

## Cabinet document model direction

The first task may only need an in-memory document/viewer state. However, design it so it can evolve into `.cabinet3d` persistence.

Future `.cabinet3d` shape:

```json
{
  "version": 1,
  "model": {
    "path": "GenesisCabinet.glb",
    "scale": 1.0,
    "upAxis": "Y"
  },
  "surfaces": []
}
```

Do not implement the full persistence format unless the task specifically asks for it. Keep this as direction only.

## Future surface metadata direction

A cabinet surface should be a named quad in cabinet-local/model-local coordinates.

Future example:

```json
{
  "id": "mainGlass",
  "name": "Main Glass",
  "corners": [
    [-0.42, 1.85, -0.03],
    [ 0.42, 1.85, -0.03],
    [ 0.42, 1.20, -0.03],
    [-0.42, 1.20, -0.03]
  ],
  "frontSide": "normal"
}
```

Corner order defines texture mapping:

```text
top-left
top-right
bottom-right
bottom-left
```

The quad normal is derived from corner winding. `frontSide` controls whether the visible/rendered side is the derived normal side or the inverted side.

Do not add `uvRotation`, `uvFlipX`, or `uvFlipY` in the first design. If a real use case appears later, a minimal mapping flag can be added then. For now, the intended model is:

- corner order defines orientation
- `frontSide` defines physical facing direction

## Future cabinet surface edit modes

Later work should add:

- create surface
- select surface
- move/rotate/scale surface
- edit individual corners
- flip front/back
- show normal arrow
- front/side/top orthographic views
- selected-surface orthographic view
- snap quad/corners to model surface
- preview assigned face document texture

These are future tasks. Do not implement them in the first GLB viewer task.

## Camera/view guidance

The first milestone should support perspective orbit view.

Design the camera layer so these can be added later:

- front orthographic
- left/right orthographic
- top orthographic
- selected-surface orthographic

Avoid hard-coding assumptions that prevent orthographic view modes later.

## UX expectations

The viewer should feel like part of the editor, not an external utility.

Expected first-stage UI:

- A command/menu/button to open a cabinet model viewer.
- A visible loaded file name or load status.
- Simple camera reset command.
- Clear error message for invalid/unreadable `.glb` files.
- No hard-coded theme colors.

## Testing guidance

Codex cannot run the WPF build/tests in its environment. Add automated tests only where practical and UI-independent.

John will test locally:

- solution builds
- app launches
- project open/create still works
- cabinet model viewer can be opened
- valid `.glb` loads and renders
- invalid file shows a safe error
- camera orbit/pan/zoom/reset work
- existing panel editor workflow is unaffected

## Implementation caution

Do not refactor unrelated editor systems as part of this task.

Do not scan every Markdown file. Start with:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. this file
4. the task-specific prompt/document, if one is supplied

Then inspect only source files needed to integrate the viewer into the existing shell/document workflow.
