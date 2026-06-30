# Task 05 - Runtime Generated Output

## Goal

Ensure runtime/export output remains disposable and is written under `Generated`, while authored asset packages remain under `Assets`.

This task primarily affects Face runtime export.

## Required Face runtime layout

Runtime output for a Face should use:

```text
Generated/Faces/<AssetName>/runtime/
  face.runtime.json
  artwork.png
  mask.png
  tray-id.png
  lamp-ids-0.png
  lamp-weights-0.png
  tray-id-debug.png
  lamp-weights-debug.png
```

The `artwork.png` and `mask.png` files in this runtime directory are copies or flattened runtime-ready exports derived from the authored Face package and manifest. They are not the project source files.

## Update FaceRuntimeExportService

`FaceRuntimeExportService` should resolve its output directory through the centralized package/path service from Task 01.

It should no longer use GUID-only Face IDs as user-facing runtime folder names when a stable Face asset name/path is available.

The service should read the current Face manifest and authored asset references, then produce runtime files under:

```text
Generated/Faces/<AssetName>/runtime/
```

## Important behavior

Runtime export must not modify authored files under:

```text
Assets/Faces/<AssetName>/
```

Runtime export may overwrite files under:

```text
Generated/Faces/<AssetName>/runtime/
```

Deleting `Generated/Faces/<AssetName>/runtime/` must not lose any authored project work.

## Project references

Runtime asset references should point to generated runtime files where the runtime/player needs runtime-ready outputs.

Authored asset references should remain in the Face manifest and point to package files such as:

```text
Assets/Faces/<AssetName>/artwork.png
Assets/Faces/<AssetName>/mask.png
```

Do not blur these two roles.

## Acceptance criteria

- Runtime export writes under `Generated/Faces/<AssetName>/runtime/`.
- Runtime export does not write authored Face source files under `Assets`.
- The Face runtime output references generated runtime files, not editable source files.
- The authored Face artwork and mask remain editable and are used as inputs to runtime export.
- Tests verify that runtime export and authored asset generation use different folders.
