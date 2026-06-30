# Task 01 - Asset Package Infrastructure

## Goal

Create a single infrastructure layer for resolving asset package paths, manifest paths, authored file paths, and generated output paths.

This replaces scattered construction of paths such as `Generated/Faces/...`, GUID-only user-facing folders, and loose document files.

## Required concepts

Introduce an asset package path/service abstraction such as `ProjectAssetPathService`, `ProjectAssetPackageService`, or equivalent.

The exact API may differ, but the service should centralize:

- project-relative path normalization
- resolving project-relative paths to absolute file paths
- asset package root directories by asset type
- asset package directory creation
- fixed manifest paths such as `asset.face`
- authored asset paths such as `artwork.png` and `mask.png`
- runtime/generated output paths
- path segment sanitization
- predictable collision handling

## Folder conventions

Use these conventions:

```text
Assets/Panel2D/<AssetName>/asset.panel2d
Assets/Faces/<AssetName>/asset.face
Assets/Cabinet3D/<AssetName>/asset.cabinet3d
Generated/Faces/<AssetName>/runtime/...
```

The asset folder is the asset. The manifest file is not the asset's user-facing identity.

## Suggested API shape

This is illustrative, not mandatory:

```csharp
string SanitizePathSegment(string name);
string ToProjectRelativePath(EditorProject project, string absolutePath);
string ResolveProjectRelativePath(EditorProject project, string relativePath);

string GetAssetTypeDirectory(EditorProject project, EditorAssetType assetType);
string GetAssetPackageDirectory(EditorProject project, EditorAssetType assetType, string assetName);
string GetAssetManifestPath(EditorProject project, EditorAssetType assetType, string assetName);

string GetPanel2DManifestPath(EditorProject project, string assetName);
string GetFaceManifestPath(EditorProject project, string assetName);
string GetCabinet3DManifestPath(EditorProject project, string assetName);

string GetFaceArtworkPath(EditorProject project, string assetName);
string GetFaceMaskPath(EditorProject project, string assetName);
string GetFaceRuntimeDirectory(EditorProject project, string assetNameOrStableFolderName);
```

If the current codebase already has document-type enums but not asset-type enums, it is acceptable to adapt the existing enum. Keep the storage model package-based even if type names still say document.

## Asset IDs

Manifest models should gain or preserve a stable internal asset ID.

If a model already has a stable document ID, it may be reused or renamed later. Do not use GUID-only folder names as user-facing package names.

Store a display name in the manifest where practical:

```json
{
  "assetId": "...",
  "displayName": "Top Glass"
}
```

## Name validation and collision handling

The infrastructure should reject or sanitize invalid path characters and reserved/empty names.

Collision behavior must be deterministic. Either:

- reject duplicate package names and ask the user to choose another name, or
- create a predictable unique suffix.

Prefer asking the user to choose a unique name where the UI already supports validation.

## Acceptance criteria

- A centralized package/path service exists and is covered by unit tests.
- Fixed manifest names are used: `asset.panel2d`, `asset.face`, `asset.cabinet3d`.
- Face authored paths resolve under `Assets/Faces/<AssetName>/`.
- Face runtime paths resolve under `Generated/Faces/<AssetName>/runtime/`.
- Path sanitization is deterministic.
- Touched generation/export/save code no longer manually constructs incompatible package paths.
- No legacy migration is added.
