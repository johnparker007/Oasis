# Task 02 - Panel2D Asset Package

## Goal

Convert new Panel2D creation/first-save behavior to use an asset package folder under `Assets/Panel2D/`.

The Panel2D package is the source/import package for the 2D artwork, lamps, source elements, and Face Source Shapes.

## Required layout

Use this package layout for new Panel2D assets:

```text
Assets/
  Panel2D/
    <AssetName>/
      asset.panel2d
      background.png       optional/current import policy dependent
      lamps/               optional/future/current policy dependent
      masks/               optional/future/current policy dependent
```

The manifest filename must be fixed:

```text
asset.panel2d
```

Do not save new Panel2D manifests as `<AssetName>.panel2d`.

## Name prompt

When creating or first-saving a Panel2D asset, request a user-facing asset name if one is not already known.

The prompt should validate that the name:

- is non-empty after trimming
- can be converted into a safe path segment
- does not collide with an existing Panel2D package unless a deterministic suffix/rename policy is used

## Manifest contents

The manifest should contain or preserve a stable internal asset/document ID and a display name where practical.

Suggested shape:

```json
{
  "assetId": "...",
  "displayName": "Main Panel"
}
```

The current model names may remain document-oriented for now.

## Source files

This task does not require moving every existing Panel2D-related external file into the package if that would be too large.

However, new authored files that belong to the Panel2D source should naturally live inside the Panel2D package. For example, a copied/imported background can become:

```text
Assets/Panel2D/<AssetName>/background.png
```

If the current import model references an external source image, keep that behavior unless the implementation is straightforward. The important first step is that the Panel2D manifest itself is stored as an asset package manifest.

## Save behavior

Once a Panel2D asset has a package location, normal save should save back to:

```text
Assets/Panel2D/<AssetName>/asset.panel2d
```

Save As may continue to exist, but first-save/new-asset creation should default into the correct package path rather than a loose file dialog location.

## Acceptance criteria

- New Panel2D assets are stored under `Assets/Panel2D/<AssetName>/`.
- The manifest file is named `asset.panel2d`.
- Existing save behavior works after the package path is established.
- The UI no longer encourages first-saving Panel2D as a loose arbitrary file.
- Tests cover path decisions and name/collision behavior where practical.
