# Task 03 - Face Asset Package

## Goal

Change Face creation from a Face Source Shape so the Face becomes an asset package under `Assets/Faces/<AssetName>/`.

The package contains the Face manifest plus user-editable authored Face files.

## Current behavior to change

The current Face generation code writes perspective-corrected artwork and masks to generated paths. This includes:

- the generated Face background/artwork from `FaceSourceShapeTransformService.TryGenerateBackground`
- the composite Face mask from `SaveSourceShapeMask`
- per-lamp transformed mask images from `TryGenerateTransformedElementAsset`

Only the first two are authored/project assets.

## Required layout

When the user creates a Face from a Face Source Shape:

1. Prompt for a Face asset name.
2. Create a package folder:

```text
Assets/Faces/<AssetName>/
```

3. Save the Face manifest:

```text
Assets/Faces/<AssetName>/asset.face
```

4. Save the generated editable artwork:

```text
Assets/Faces/<AssetName>/artwork.png
```

5. Save the generated editable Face mask:

```text
Assets/Faces/<AssetName>/mask.png
```

6. Store project-relative references to these files in the Face manifest.

Do not save new Face manifests as `<AssetName>.face`.

## Manifest contents

The Face manifest should contain or preserve a stable internal asset/document ID and a display name where practical.

Suggested shape:

```json
{
  "assetId": "...",
  "displayName": "Top Glass",
  "artworkPath": "Assets/Faces/Top Glass/artwork.png",
  "maskPath": "Assets/Faces/Top Glass/mask.png"
}
```

The exact property names may follow the existing model.

## Per-lamp transformed masks

Do not put per-lamp transformed mask PNGs in `Assets/Faces/<AssetName>/`.

These are deterministic intermediates, not user-authored graphics. Prefer removing persistent storage for them entirely and generating whatever data is required in memory during Face generation/regeneration/runtime texture export.

If removing them is too large for this task, keep them as disposable implementation details outside the authored asset package and clearly mark that they are not user-editable project assets.

## Regeneration

When explicitly regenerating a Face from its source shape, update the Face manifest from the current Panel2D source shape and update the authored `artwork.png` and `mask.png` only as part of that explicit operation.

Do not overwrite `artwork.png` or `mask.png` during preview, project open, runtime export, or unrelated document saves.

Initial implementation may overwrite `artwork.png` and `mask.png` for explicit regeneration. Later versions can add candidate files or regeneration modes.

## Cabinet target assignment

Existing Face-to-Cabinet target metadata should remain in the Face manifest or current related model.

The package layout does not change the rule that aspect ratio comes from the selected Cabinet target during generation.

## Acceptance criteria

- Creating a Face from a Face Source Shape prompts for a Face asset name.
- The created Face package is stored under `Assets/Faces/<AssetName>/`.
- The Face manifest is named `asset.face`.
- `artwork.png` and `mask.png` are stored under the same Face package folder.
- The Face manifest references those assets using project-relative paths.
- No user-editable Face artwork or composite mask is written to `Generated`.
- Per-lamp transformed mask PNGs are not introduced as authored package assets.
- Relevant unit tests are updated to expect the package layout.
