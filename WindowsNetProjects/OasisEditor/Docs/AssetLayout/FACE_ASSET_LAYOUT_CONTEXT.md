# Face Asset Layout Redesign Context

## Project context

Oasis Editor is early in development. Existing project files and generated asset layouts may be broken by this change. Do not add migration or backwards-compatibility code unless it is needed to keep current tests understandable.

The editor currently has three main document types:

- `Panel2D`: source/import document containing background artwork, lamps, source elements, and Face Source Shapes.
- `Face`: generated/authorable document created from a Panel2D Face Source Shape.
- `Cabinet3D`: document referencing a GLB and mapping Faces onto detected `OasisFace_*` target meshes.

The later runtime/player will be Unity. Cabinet models use GLB. The editor is WPF + HelixToolkit.

## Current problem

The current Face generation flow writes several files under the project `Generated` area. Some of those files are not true disposable runtime output. In particular, the perspective-corrected Face artwork and the composite Face mask are user-editable project assets.

Users should be able to open those files in external tools such as Photoshop, touch them up, save them, and see those edits reflected when the editor regenerates runtime output.

Therefore, the generated Face artwork and Face mask must move out of `Generated` and into the project `Assets` tree.

## Core design rule

Use this split everywhere:

```text
Assets     = user-editable, project-owned source assets
Generated  = disposable runtime/cache/export assets
```

A file belongs under `Assets` when it represents authored or touch-up-able project source data.

A file belongs under `Generated` when it can be deleted and recreated without losing user work.

## New Face folder structure

When creating a Face from a Face Source Shape, prompt for a Face name and create a named folder:

```text
Assets/
  Faces/
    TopGlass/
      TopGlass.face
      artwork.png
      mask.png
```

`artwork.png` is the perspective-corrected Face background generated from the Panel2D source background.

`mask.png` is the composite Face mask that determines where lamps illuminate through the glass.

Both are project assets and may be edited externally by the user.

## Runtime/generated output

Runtime export/build products remain under `Generated`:

```text
Generated/
  Faces/
    TopGlass/
      runtime/
        face.runtime.json
        artwork.png
        mask.png
        tray-id.png
        lamp-ids-0.png
        lamp-weights-0.png
        tray-id-debug.png
        lamp-weights-debug.png
```

These files are disposable. They should be derived from the current Face document and its current authored assets.

## What not to treat as authored assets

Do not preserve the current per-lamp warped mask PNGs as user-editable project files.

The current pipeline can generate transformed lamp mask images for each Face lamp window. These are deterministic intermediates derived from:

```text
Panel2D lamp mask + Face Source Shape transform + Face output size
```

They are not intended to be painted or maintained by the user. They should either be generated transiently in memory or treated as disposable cache/runtime implementation details. Do not place them under `Assets/Faces/<FaceName>/`.

If they are still needed during implementation, keep them out of the authored asset folder, and prefer removing persistent storage for them entirely.

## Future extensibility

The Face asset folder should be able to grow later without redesigning the layout. Possible future maps include:

```text
Assets/Faces/TopGlass/
  reflectivity.png
  roughness.png
  metallic.png
  normal.png
  emission-tint.png
```

These are not part of the initial task. The initial task should only establish the authored Face folder and move `artwork.png` and `mask.png` into it.

## Naming and IDs

Use named folders for user-facing assets. Do not use GUID-only folder names for user-facing Face asset folders.

Documents may still contain stable internal IDs. Internal IDs should not drive the visible project asset folder names.

Names must be sanitized for file-system safety. The implementation should handle collisions, for example by requiring a unique name or adding a short suffix.

## Document references

Prefer project-relative paths in documents.

For example:

```text
Assets/Faces/TopGlass/artwork.png
Assets/Faces/TopGlass/mask.png
```

Avoid absolute paths unless the existing editor architecture has a strong reason to keep them.

## Regeneration behavior

Initial implementation may overwrite `artwork.png` and `mask.png` when the user explicitly regenerates a Face.

However, do not silently overwrite user-edited assets as part of unrelated operations such as opening a project, switching views, exporting runtime textures, or live preview.

A later improvement may add regeneration modes such as:

- Regenerate runtime only
- Regenerate source artwork/mask
- Regenerate all
- Generate preview candidates such as `artwork.regenerated.png` and `mask.regenerated.png`

The current task should at minimum make the authored/runtime distinction clear in code.
