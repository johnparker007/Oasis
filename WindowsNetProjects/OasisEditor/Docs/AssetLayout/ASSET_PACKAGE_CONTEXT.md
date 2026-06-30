# Asset Package Layout Context

## Project context

Oasis Editor is early in development. Existing project files and generated asset layouts may be broken by this change. Do not add migration or backwards-compatibility code unless it is needed to keep current tests understandable.

The editor currently works with these primary authored asset types:

- `Panel2D`: source/import asset containing background artwork, lamps, source elements, and Face Source Shapes.
- `Face`: asset generated from a Panel2D Face Source Shape, then authorable/touch-up-able by the user.
- `Cabinet3D`: asset referencing a GLB and mapping Faces onto detected `OasisFace_*` target meshes.

The later runtime/player will be Unity. Cabinet models use GLB. The editor is WPF + HelixToolkit.

## Core model

Move the editor directly to a folder-as-asset model.

The asset folder is the asset. The manifest file inside the folder describes that asset.

Use fixed manifest filenames:

```text
Assets/
  Panel2D/
    Main Panel/
      asset.panel2d
      background.png
      ...

  Faces/
    Top Glass/
      asset.face
      artwork.png
      mask.png
      ...

  Cabinet3D/
    Vogue Cabinet/
      asset.cabinet3d
      cabinet.glb   optional/current policy dependent
      ...
```

Do not use `<AssetName>.face`, `<AssetName>.panel2d`, or `<AssetName>.cabinet3d` as the standard manifest filenames. The folder name is the user-facing asset name, so renaming an asset folder should not require renaming the manifest file.

The codebase may continue to use internal types such as `FaceDocumentModel`, `Panel2DDocumentModel`, and `Cabinet3DDocumentModel`. The terminology shift is conceptual and storage-level first: document models are manifests inside asset packages.

## Core design rule

Use this split everywhere:

```text
Assets     = user-editable, project-owned source assets
Generated  = disposable runtime/cache/export assets
```

A file belongs under `Assets` when it represents authored or touch-up-able project source data.

A file belongs under `Generated` when it can be deleted and recreated without losing user work.

A package contains everything required to author that asset. Anything that can be regenerated without losing user work does not belong in the package.

## Asset identity

Each asset package should have:

- a user-facing folder/display name
- a stable internal `assetId` stored in the manifest

Example:

```json
{
  "assetId": "9fd0c0d5-...",
  "displayName": "Top Glass"
}
```

Internal references should prefer the stable asset ID where practical. User-facing paths and UI should use the asset folder/display name.

This allows later rename/move support without making every reference depend on a visible folder name. Initial implementation can still use project-relative paths where that matches the current architecture, but the manifest schema should not assume the folder name is the only identity.

## Face asset package

When creating a Face from a Face Source Shape, prompt for a Face name and create:

```text
Assets/
  Faces/
    Top Glass/
      asset.face
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
    Top Glass/
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

These files are disposable. They should be derived from the current Face manifest and its current authored assets.

The `artwork.png` and `mask.png` files under runtime output are runtime-ready copies or flattened exports. They are not the editable source files.

## What not to treat as authored assets

Do not preserve the current per-lamp warped mask PNGs as user-editable project files.

The current pipeline can generate transformed lamp mask images for each Face lamp window. These are deterministic intermediates derived from:

```text
Panel2D lamp mask + Face Source Shape transform + Face output size
```

They are not intended to be painted or maintained by the user. They should either be generated transiently in memory or treated as disposable cache/runtime implementation details. Do not place them under `Assets/Faces/<FaceName>/`.

If they are still needed during implementation, keep them out of the authored asset folder and clearly mark that they are not user-editable project assets.

## Future extensibility

The asset package model should grow naturally as the editor gains more authored data.

Possible future Face package contents:

```text
Assets/Faces/Top Glass/
  asset.face
  artwork.png
  mask.png
  reflectivity.png
  roughness.png
  metallic.png
  normal.png
  emission-tint.png
```

Possible future Cabinet3D package contents:

```text
Assets/Cabinet3D/Vogue Cabinet/
  asset.cabinet3d
  cabinet.glb
  materials/
  previews/
```

These future files are not part of the initial implementation. The initial task should establish the package model and move current authored files into the correct packages.

## Naming

Use named folders for user-facing asset packages. Do not use GUID-only folder names for user-facing authored assets.

Names must be sanitized for file-system safety. The implementation should handle collisions, for example by requiring a unique name or adding a predictable suffix.

## Document/path references

Prefer project-relative paths in manifests for file references.

For example:

```text
Assets/Faces/Top Glass/artwork.png
Assets/Faces/Top Glass/mask.png
```

Avoid absolute paths unless the existing editor architecture has a strong reason to keep them.

## Regeneration behavior

Initial implementation may overwrite `artwork.png` and `mask.png` when the user explicitly regenerates a Face from its source shape.

However, do not silently overwrite user-edited assets as part of unrelated operations such as opening a project, switching views, exporting runtime textures, or live preview.

A later improvement may add regeneration modes such as:

- Regenerate runtime only
- Regenerate source artwork/mask
- Regenerate all
- Generate preview candidates such as `artwork.regenerated.png` and `mask.regenerated.png`

The current implementation should at minimum make the authored/runtime distinction clear in code.
