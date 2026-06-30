# Task 01 - Add Project Asset Path Service

## Goal

Create a single service responsible for resolving project asset and generated output paths. This prevents Face generation, runtime export, and document save code from manually constructing inconsistent paths such as `Generated/Faces/...`.

## Requirements

Add a service such as `ProjectAssetPathService` or equivalent.

It should provide helpers for:

- project-relative path normalization
- resolving project-relative paths to absolute file paths
- creating named document folders under `Assets`
- creating Face authored asset paths
- creating Face runtime/generated output paths
- sanitizing user-facing names into safe path segments
- handling name collisions predictably

Suggested helpers:

```csharp
string SanitizePathSegment(string name);
string ToProjectRelativePath(EditorProject project, string absolutePath);
string ResolveProjectRelativePath(EditorProject project, string relativePath);
string GetDocumentTypeAssetsDirectory(EditorProject project, EditorDocumentType documentType);
string GetNamedDocumentDirectory(EditorProject project, EditorDocumentType documentType, string name);
string GetFaceAssetsDirectory(EditorProject project, string faceName);
string GetFaceDocumentPath(EditorProject project, string faceName);
string GetFaceArtworkPath(EditorProject project, string faceName);
string GetFaceMaskPath(EditorProject project, string faceName);
string GetFaceRuntimeDirectory(EditorProject project, string faceNameOrStableFolderName);
```

The exact API may differ, but path construction should be centralized.

## Folder conventions

Use these conventions:

```text
Assets/Panel2D/<Name>/<Name>.panel2d
Assets/Faces/<Name>/<Name>.face
Assets/Cabinet3D/<Name>/<Name>.cabinet3d
Generated/Faces/<Name>/runtime/...
```

The existing `MachinesDirectory` can remain as-is unless the current code requires a change.

## Notes

- Do not use GUID-only folder names for user-facing authored assets.
- Stable document IDs may remain inside document JSON.
- Use project-relative references inside documents where practical.
- No legacy migration is required.

## Acceptance criteria

- New path service exists and is covered by unit tests.
- Path sanitization is deterministic.
- Face authored paths resolve under `Assets/Faces/<Name>/`.
- Face runtime paths resolve under `Generated/Faces/<Name>/runtime/`.
- Existing direct path construction in the touched Face generation/export code is replaced or isolated behind the new service.
