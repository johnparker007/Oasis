# Task 04 - Keep Runtime Output in Generated

## Goal

Ensure runtime/export output remains disposable and is written under `Generated`, while authored Face artwork and mask are read from `Assets`.

## Required layout

Runtime output for a Face should use:

```text
Generated/Faces/<FaceName>/runtime/
  face.runtime.json
  artwork.png
  mask.png
  tray-id.png
  lamp-ids-0.png
  lamp-weights-0.png
  tray-id-debug.png
  lamp-weights-debug.png
```

The `artwork.png` and `mask.png` files in this runtime directory are copies or flattened runtime-ready exports derived from the authored Face assets and document model. They are not the project source files.

## Update FaceRuntimeExportService

`FaceRuntimeExportService` should resolve its output directory through the centralized project path service from Task 01.

It should no longer use GUID-only Face IDs as user-facing runtime folder names when a stable Face name/path is available.

The service should read the current Face document and authored asset references, then produce runtime files under `Generated/Faces/<FaceName>/runtime/`.

## Important behavior

Runtime export must not modify authored files under `Assets/Faces/<FaceName>/`.

Runtime export may overwrite files under `Generated/Faces/<FaceName>/runtime/`.

Deleting `Generated/Faces/<FaceName>/runtime/` should not lose any authored project work.

## Acceptance criteria

- Runtime export writes under `Generated/Faces/<FaceName>/runtime/`.
- Runtime export does not write authored Face source files under `Assets`.
- The Face document's runtime asset references point to generated runtime files, not authored source files.
- The authored Face artwork and mask remain editable and are used as inputs to runtime export.
- Tests verify that runtime export and authored asset generation use different folders.
