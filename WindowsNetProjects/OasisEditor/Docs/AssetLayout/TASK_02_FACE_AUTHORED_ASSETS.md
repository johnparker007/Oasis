# Task 02 - Move Face Artwork and Mask into Assets

## Goal

Change Face creation from a Face Source Shape so the user-editable Face artwork and Face mask are saved under `Assets/Faces/<FaceName>/` instead of `Generated/Faces/`.

## Current behavior to change

The current Face generation code writes perspective-corrected artwork and masks to generated paths. This includes:

- the generated Face background/artwork from `FaceSourceShapeTransformService.TryGenerateBackground`
- the composite Face mask from `SaveSourceShapeMask`
- per-lamp transformed mask images from `TryGenerateTransformedElementAsset`

Only the first two are authored/project assets.

## New behavior

When the user creates a Face from a Face Source Shape:

1. Prompt for a Face name.
2. Create a folder:

```text
Assets/Faces/<FaceName>/
```

3. Save the Face document:

```text
Assets/Faces/<FaceName>/<FaceName>.face
```

4. Save the generated editable artwork as:

```text
Assets/Faces/<FaceName>/artwork.png
```

5. Save the generated editable Face mask as:

```text
Assets/Faces/<FaceName>/mask.png
```

6. Store project-relative references to these files in the Face document.

## Per-lamp transformed masks

Do not put per-lamp transformed mask PNGs in `Assets/Faces/<FaceName>/`.

These are deterministic intermediates, not user-authored graphics. Prefer removing persistent storage for them entirely and generating whatever data is required in memory during Face generation/regeneration/runtime texture export.

If removing them is too large for this task, keep them as disposable implementation details outside the authored asset folder and clearly mark that they are not user-editable project assets.

## Regeneration

When explicitly regenerating a Face from its source shape, update the Face document from the current Panel2D source shape and update the authored `artwork.png` and `mask.png` only as part of that explicit operation.

Do not overwrite `artwork.png` or `mask.png` during preview, project open, runtime export, or unrelated document saves.

## Acceptance criteria

- Creating a Face from a Face Source Shape prompts for a Face name.
- The created Face document is stored under `Assets/Faces/<FaceName>/`.
- `artwork.png` and `mask.png` are stored under that same Face folder.
- The Face document references those assets using project-relative paths.
- No user-editable Face artwork or composite mask is written to `Generated`.
- Per-lamp transformed mask PNGs are not introduced as authored assets.
- Relevant unit tests are updated to expect the new asset layout.
