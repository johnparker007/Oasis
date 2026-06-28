# Cabinet Target Mapping Overrides Task

## Read first

Read only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. `01_CABINET_MODEL_VIEWER_PLAN.md`
4. this file

Do not scan all Markdown files in this directory.

## Goal

Add Cabinet3D per-target mapping controls for detected `OasisFace_` targets.

This lets users correct common Blender authoring orientation issues without editing the source `.glb`.

## Current baseline

Recent work has added:

- `.cabinet3d` saveable documents referencing `.glb` assets.
- Protection so Oasis does not overwrite source `.glb` model files.
- Cabinet model rendering in the Editor.
- `OasisFace_` target detection.
- Face document assignment to target IDs.
- Static Face background/artwork preview on assigned targets.

Do not replace this architecture. Extend it.

## Design decision

Mapping correction belongs to the Cabinet3D document, not to the Face document and not to the `.glb`.

Reasoning:

- The Blender quad is cabinet-specific.
- The same Face document should not need to know how a particular cabinet's target plane was authored.
- The `.glb` should remain a source asset and should not be mutated by Oasis.
- The `.cabinet3d` document is now the correct place for target-specific metadata.

## Required override fields

Add persisted per-target override state to the Cabinet3D document.

Minimum fields:

```json
{
  "targetId": "topGlass",
  "frontSide": "normal",
  "textureRotation": 0
}
```

Valid values:

```text
frontSide: normal | inverted
textureRotation: 0 | 90 | 180 | 270
```

`frontSide` chooses which side of the detected target quad receives the static Face preview/rendering.

`textureRotation` rotates the Face preview/artwork on the target in 90-degree steps.

Do not add arbitrary UV editing in this task.

Do not add `uvFlipX` or `uvFlipY` in this task unless the existing implementation already has a clear, tested need for them. The intended first version is front/back plus rotation.

## UI direction

Use the Cabinet3D document UI.

Preferred design:

- Detected `OasisFace_` targets appear as selectable items in the Cabinet3D hierarchy/list/panel.
- Selecting a target populates the Inspector.
- Inspector shows:
  - display name
  - source name
  - stable target ID
  - valid/invalid state
  - Front Side: Normal/Inverted
  - Texture Rotation: 0/90/180/270
- Editing these properties updates the static preview immediately where practical.
- Editing marks the `.cabinet3d` document dirty.
- Save/reopen preserves the selected values.

If the current editor architecture does not yet have a proper Cabinet3D hierarchy selection path, implement the smallest compatible version: selection in the existing target side panel can populate inspector-like controls there. Prefer an architecture that can later move into the common Hierarchy/Inspector system.

## Command/undo direction

Follow existing document mutation patterns.

If Cabinet3D documents already have document-scoped commands, use them.

If they do not, introduce minimal Cabinet3D document-scoped commands for:

- setting target front side
- setting target texture rotation

No-op changes should not mark the document dirty or create undo entries.

Do not bypass existing dirty-state/save pathways.

## Rendering behavior

The static Face preview should respect the selected target override values.

Requirements:

- `textureRotation = 0` preserves current mapping behavior.
- `textureRotation = 90/180/270` rotates the Face preview/artwork on the target.
- `frontSide = normal` uses the current/derived normal side.
- `frontSide = inverted` renders/applies the Face preview on the opposite side.
- Existing target overlays and static previews must continue to work.
- If there is no assigned Face preview for a target, the controls should still be editable and persisted.

Implementation may adjust texture coordinates, quad vertex order, back material/material assignment, or generated preview geometry as appropriate for the current Helix/WPF implementation. Keep it isolated and simple.

## Persistence behavior

Persist overrides in the `.cabinet3d` document.

Requirements:

- Overrides are keyed by stable `targetId`.
- Missing override means defaults:
  - `frontSide = normal`
  - `textureRotation = 0`
- Save writes JSON metadata only to `.cabinet3d`.
- Save must never write to the referenced `.glb`.
- Reopen restores overrides and applies them to previews.
- If the GLB no longer contains a target for a saved override, preserve the metadata if existing document patterns allow it, or at least do not crash.

## Non-goals

Do not implement:

- moving/resizing/rotating target geometry in Oasis
- arbitrary UV editing
- texture flipping flags unless clearly needed
- dynamic lamp flashing on the cabinet
- Unity runtime export changes
- unrelated refactors

## Suggested model/API names

Use existing naming conventions if different.

Possible additions:

```csharp
public enum CabinetFaceTargetFrontSide
{
    Normal,
    Inverted
}

public enum CabinetFaceTargetTextureRotation
{
    Rotate0 = 0,
    Rotate90 = 90,
    Rotate180 = 180,
    Rotate270 = 270
}

public sealed record CabinetFaceTargetOverride(
    string TargetId,
    CabinetFaceTargetFrontSide FrontSide,
    CabinetFaceTargetTextureRotation TextureRotation);
```

Commands may be shaped like:

```text
SetCabinetFaceTargetFrontSideCommand
SetCabinetFaceTargetTextureRotationCommand
```

Adapt to the existing command pattern.

## Local testing checklist for John

After implementation, John should test locally:

- Build the solution.
- Launch Oasis Editor.
- Open/create a project.
- Open a `.cabinet3d` that references a `.glb` with `OasisFace_` targets.
- Assign a Face document to a target.
- Select the target in the Cabinet3D UI.
- Change Texture Rotation through 0/90/180/270 and confirm preview orientation changes.
- Change Front Side normal/inverted and confirm the preview appears on the expected side where visible.
- Save/reopen the `.cabinet3d` and confirm overrides persist.
- Confirm Save Document does not modify the referenced `.glb`.
- Confirm no-op changes do not dirty the document if existing command patterns support that.
- Confirm orbit/pan/zoom/reset still work.
- Confirm Panel2D and Face document workflows remain unaffected.

## Codex environment constraint

Do not run builds or tests in Codex. The environment does not have the required Windows/.NET/WPF toolchain. Summarize changes and provide the local test checklist instead.
