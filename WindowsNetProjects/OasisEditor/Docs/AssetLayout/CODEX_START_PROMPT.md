# Codex Start Prompt - Face Asset Layout Redesign

You are working in the `johnparker007/Oasis` repository, inside:

```text
WindowsNetProjects/OasisEditor
```

Implement the Face asset layout redesign described in these docs:

```text
WindowsNetProjects/OasisEditor/Docs/AssetLayout/FACE_ASSET_LAYOUT_CONTEXT.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_01_PROJECT_ASSET_PATH_SERVICE.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_02_FACE_AUTHORED_ASSETS.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_03_DOCUMENT_FOLDER_SAVE_FLOW.md
WindowsNetProjects/OasisEditor/Docs/AssetLayout/TASK_04_RUNTIME_OUTPUT_REMAINS_GENERATED.md
```

Important project assumptions:

- Oasis Editor is early in development.
- Backwards compatibility with old project/generated layouts is not required.
- Do not add migration code unless it makes tests or current code clearer.
- Preserve the architecture split between Panel2D, Face, Cabinet3D, and runtime/generated output.

Core rule:

```text
Assets     = user-editable, project-owned source assets
Generated  = disposable runtime/cache/export assets
```

Main implementation goals:

1. Add a centralized project asset path service.
2. Change Face creation from Face Source Shape so it prompts for a Face name and creates:

```text
Assets/Faces/<FaceName>/<FaceName>.face
Assets/Faces/<FaceName>/artwork.png
Assets/Faces/<FaceName>/mask.png
```

3. Treat `artwork.png` and `mask.png` as authored/user-editable Face source assets.
4. Do not treat per-lamp warped mask PNGs as authored project assets. Prefer removing persistent storage for those intermediates; otherwise keep them as disposable implementation details outside `Assets/Faces/<FaceName>/`.
5. Keep runtime export output under:

```text
Generated/Faces/<FaceName>/runtime/
```

6. Ensure runtime export does not overwrite authored files under `Assets`.
7. Update or replace tests that currently expect GUID-based or `Generated/Faces/*.png` authored output.

Suggested order:

1. Implement and test the path service from Task 01.
2. Refactor Face generation path construction to use that service.
3. Update Face Source Shape creation flow to request a Face name and create the named folder.
4. Move authored Face artwork/mask writes to `Assets/Faces/<FaceName>/`.
5. Remove or isolate persistent per-lamp transformed mask PNGs.
6. Update `FaceRuntimeExportService` to write only runtime outputs under `Generated/Faces/<FaceName>/runtime/`.
7. Update tests.
8. Run the OasisEditor test suite.

Pay special attention to these existing files:

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
