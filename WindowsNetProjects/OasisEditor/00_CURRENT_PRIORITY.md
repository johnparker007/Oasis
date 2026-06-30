# Current Priority for Codex

## Read First

Read only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. `Docs/AssetLayout/CODEX_START_PROMPT.md`

Do not scan all Markdown files in this directory.

Open additional AssetLayout task documents only as directed by `Docs/AssetLayout/CODEX_START_PROMPT.md` or when directly relevant to the requested work.

## Current Focus

Priority workstream:

- Asset package layout redesign
- Folder-as-asset storage model
- Fixed asset manifest filenames
- Authored assets under `Assets/`
- Disposable runtime/cache/export files under `Generated/`
- Panel2D, Face, and Cabinet3D first-save/package creation flow

## Immediate Direction

Implement the folder-as-asset package model described in:

```text
Docs/AssetLayout/CODEX_START_PROMPT.md
```

Core target layout:

```text
Assets/Panel2D/<AssetName>/asset.panel2d
Assets/Faces/<AssetName>/asset.face
Assets/Faces/<AssetName>/artwork.png
Assets/Faces/<AssetName>/mask.png
Assets/Cabinet3D/<AssetName>/asset.cabinet3d
Generated/Faces/<AssetName>/runtime/
```

Do not add migration code for old generated/document layouts unless it makes tests or current code clearer.

## Architectural Goals

- The asset folder is the asset.
- The manifest file inside the folder describes the asset.
- Use stable internal asset IDs in manifests where practical.
- Keep authored/user-editable files under `Assets/`.
- Keep disposable runtime/export/cache files under `Generated/`.
- Keep Cabinet3D GLB reference/protection behavior intact.
- Do not silently overwrite user-edited Face `artwork.png` or `mask.png` outside explicit regeneration.

## Testing Direction

Prefer tests around:

- asset package path service behavior
- path sanitization and collision handling
- first-save/package creation paths for Panel2D, Face, and Cabinet3D
- Face Source Shape generation writing authored Face files under `Assets/Faces/<AssetName>/`
- runtime export writing only under `Generated/Faces/<AssetName>/runtime/`
- old GUID/generated authored-output assumptions being removed

Do not attempt to run builds/tests in Codex. John will run builds/tests locally.
