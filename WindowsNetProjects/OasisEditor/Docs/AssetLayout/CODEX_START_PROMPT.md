# Codex Start Prompt - Asset Package Layout Redesign

You are working in the `johnparker007/Oasis` repository, inside:

```text
WindowsNetProjects/OasisEditor
```

Implement the folder-as-asset package redesign described in these docs:

```text
WindowsNetProjects/OasisEditor/Docs/AssetLayout/ASSET_PACKAGE_CONTEXT.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_01_ASSET_PACKAGE_INFRASTRUCTURE.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_02_PANEL2D_ASSET_PACKAGE.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_03_FACE_ASSET_PACKAGE.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_04_CABINET3D_ASSET_PACKAGE.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_05_RUNTIME_GENERATED_OUTPUT.md
```

Important project assumptions:

- Oasis Editor is early in development.
- Backwards compatibility with old project/generated layouts is not required.
- Do not add migration code unless it makes tests or current code clearer.
- Preserve the architecture split between Panel2D, Face, Cabinet3D, and runtime/generated output.
- The codebase may still use document-model type names. The storage model should become asset-package based.

Core model:

```text
The asset folder is the asset.
The manifest file inside the folder describes the asset.
```

Use fixed manifest filenames:

```text
Assets/Panel2D/<AssetName>/asset.panel2d
Assets/Faces/<AssetName>/asset.face
Assets/Cabinet3D/<AssetName>/asset.cabinet3d
```

Do not use `<AssetName>.panel2d`, `<AssetName>.face`, or `<AssetName>.cabinet3d` for new package manifests.

Core authored/generated rule:

```text
Assets     = user-editable, project-owned source assets
Generated  = disposable runtime/cache/export assets
```

A package contains everything required to author that asset. Anything that can be regenerated without losing user work does not belong in the package.

Main implementation goals:

1. Add centralized asset package/path infrastructure.
2. Change new Panel2D creation/first-save to create:

```text
Assets/Panel2D/<AssetName>/asset.panel2d
```

3. Change Face creation from Face Source Shape so it prompts for a Face asset name and creates:

```text
Assets/Faces/<AssetName>/asset.face
Assets/Faces/<AssetName>/artwork.png
Assets/Faces/<AssetName>/mask.png
```

4. Treat Face `artwork.png` and `mask.png` as authored/user-editable Face source assets.
5. Do not treat per-lamp warped mask PNGs as authored project assets. Prefer removing persistent storage for those intermediates; otherwise keep them as disposable implementation details outside `Assets/Faces/<AssetName>/`.
6. Change new Cabinet3D creation/first-save to create:

```text
Assets/Cabinet3D/<AssetName>/asset.cabinet3d
```

7. Preserve the current GLB protection/reference behavior. Cabinet3D must not modify the GLB.
8. Keep Face runtime export output under:

```text
Generated/Faces/<AssetName>/runtime/
```

9. Ensure runtime export does not overwrite authored files under `Assets`.
10. Update or replace tests that currently expect GUID-based folders, loose document files, `<Name>.<type>` manifests, or `Generated/Faces/*.png` authored output.

Suggested order:

1. Implement and test the package/path service from Task 01.
2. Refactor path construction in touched save/generation/export code to use that service.
3. Update first-save/new-asset flows for Panel2D and Cabinet3D.
4. Update Face Source Shape creation flow to request a Face asset name and create the package.
5. Move authored Face artwork/mask writes to `Assets/Faces/<AssetName>/`.
6. Remove or isolate persistent per-lamp transformed mask PNGs.
7. Update `FaceRuntimeExportService` to write only runtime outputs under `Generated/Faces/<AssetName>/runtime/`.
8. Update tests.
9. Run the OasisEditor test suite.

Pay special attention to these existing files and nearby code:

```text
OasisEditor/EditorProject.cs
OasisEditor/DocumentModel.cs
OasisEditor/FaceGenerationService.cs
OasisEditor/FaceSourceShapeTransformService.cs
OasisEditor/FaceRegenerationService.cs
OasisEditor/FaceRuntimeExportService.cs
OasisEditor/FaceDocumentStorage.cs
OasisEditor/MainWindowViewModel.cs
OasisEditor/ViewModels/DocumentWorkspaceViewModel.cs
OasisEditor.Tests/*Face*Tests.cs
```

Deliver the implementation as normal code changes with tests. Keep the docs updated if the final implementation intentionally differs from the proposed API names.
