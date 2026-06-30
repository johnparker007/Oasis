# Task 04 - Cabinet3D Asset Package

## Goal

Convert new Cabinet3D creation/first-save behavior to use an asset package folder under `Assets/Cabinet3D/`.

Cabinet3D packages store editor metadata for a GLB-backed cabinet asset. They must not modify the GLB itself.

## Required layout

Use this package layout for new Cabinet3D assets:

```text
Assets/
  Cabinet3D/
    <AssetName>/
      asset.cabinet3d
      cabinet.glb        optional/current reference policy dependent
```

The manifest filename must be fixed:

```text
asset.cabinet3d
```

Do not save new Cabinet3D manifests as `<AssetName>.cabinet3d`.

## GLB policy

Cabinet3D references a GLB and never modifies the GLB.

This task does not require changing whether the GLB is copied into the Cabinet3D package or referenced externally. Keep the current GLB reference semantics unless the existing code makes that awkward.

The important part is that the `.cabinet3d` manifest itself lives in a named asset package folder.

## Locator meshes

The existing GLB locator mesh conventions remain unchanged:

```text
OasisFace_TopGlass
OasisFace_BottomGlass
...
```

These meshes are detected and used as locator geometry only. The editor still generates its own render quads and stores target overrides in the Cabinet3D manifest/package.

## Manifest contents

The Cabinet3D manifest should contain or preserve a stable internal asset/document ID and a display name where practical.

Suggested shape:

```json
{
  "assetId": "...",
  "displayName": "Vogue Cabinet",
  "glbPath": "..."
}
```

The exact property names may follow the existing model.

## Name prompt

When creating or first-saving a Cabinet3D asset, request a user-facing asset name if one is not already known.

The prompt should validate that the name:

- is non-empty after trimming
- can be converted into a safe path segment
- does not collide with an existing Cabinet3D package unless a deterministic suffix/rename policy is used

## Save behavior

Once a Cabinet3D asset has a package location, normal save should save back to:

```text
Assets/Cabinet3D/<AssetName>/asset.cabinet3d
```

Save As may continue to exist, but first-save/new-asset creation should default into the correct package path rather than a loose file dialog location.

## Acceptance criteria

- New Cabinet3D assets are stored under `Assets/Cabinet3D/<AssetName>/`.
- The manifest file is named `asset.cabinet3d`.
- The current GLB protection/reference behavior remains intact.
- Existing save behavior works after the package path is established.
- The UI no longer encourages first-saving Cabinet3D as a loose arbitrary file.
- Tests cover path decisions and name/collision behavior where practical.
