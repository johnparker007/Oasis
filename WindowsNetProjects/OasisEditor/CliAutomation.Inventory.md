# CLI Automation Inventory

## Scope
Step 1 inventory for the CLI/headless automation pipeline workstream.

## Project container creation APIs
- `ProjectScaffolder.CreateProject(string projectName, string rootLocation)` creates the folder structure and writes `<name>.oasisproj` metadata. It is UI-independent and file-system based, but currently consumed from launcher UI flow.  
  - File/folder creation is direct (`Directory.CreateDirectory`, `File.WriteAllText`).
  - Throws for invalid args and existing folder.

### Current UI coupling
- `LauncherWindowViewModel.CreateProject()` calls `ProjectScaffolder` directly and uses WPF-driven state and dialogs around it.

## Panel2D creation APIs
- `DocumentWorkspaceViewModel.OpenPanel2DStubDocument()` creates an in-memory untitled Panel2D tab via `EditorDocument.CreatePanel2DStub(...)` and `Panel2DDocumentStorage.SerializeLayout([])`.
- This does **not** create a persisted `.panel2d` asset path; persistence happens later via save.

### Current UI coupling
- Creation is tied to an open-document tab/session path and selection state.

## MFME extract import dependencies
- `MainWindowViewModel.ImportMfmeExtract()` contains WPF dialog and message-box handling.
- Core import mapping/copy logic itself is in `Features/MfmeImport/MfmeImportService` and is UI-independent.
- Import insertion into document currently depends on an active selected Panel2D document tab and executes `ImportMfmeExtractCommand` via document command service.
- Updating project input definitions and metadata save is currently orchestrated from `MainWindowViewModel`.

### WPF/editor-state dependencies to remove/extract
- `OpenFileDialog` selection of manifest file.
- `MessageBox` for warnings/errors.
- Reliance on `SelectedDocument` and `LoadedProject` properties on the main view model.

## File -> Save Document implementation
- `MainWindowViewModel.SaveSelectedDocument()` implements save behavior.
- Uses `DocumentWorkspaceViewModel.BuildDocumentContent(current)` to generate file content.
- Uses `File.WriteAllText(savePath, content)` and replaces the open document with clean saved state.
- For untitled docs it prompts via WPF `SaveFileDialog` (`PromptSavePath()`).

### Automation-relevant extraction seam
- Non-UI save core can be extracted as a service method taking:
  - source `DocumentTabViewModel`
  - explicit save path (no dialog)
  - content builder + file writer.

## Existing command/undo abstractions
- Undoable edit command stack uses `OasisEditor.Commands.ICommand` and `CommandService`.
- Document-scoped execution for canvas edits is in `DocumentWorkspaceViewModel.ExecuteDocumentCanvasCommand(Guid, ICommand)`.
- `ImportMfmeExtractCommand` already exists as an undoable command for inserting imported elements.
- These abstractions are edit-history oriented; they are distinct from planned workflow automation command runner abstractions.

## Initial extraction recommendations
1. Add UI-independent automation command pipeline abstractions (context/result/command/runner).
2. Add orchestrator command for first target flow: create project container -> create Panel2D document -> import extract -> save document.
3. Wrap current save logic in reusable document save service, keeping `File -> Save Document` behavior unchanged.
4. Wrap MFME import invocation in an automation-capable service that accepts explicit paths and document/project targets (no dialogs).

## Out-of-scope confirmations
- Do **not** build in-app terminal in this phase.
- Do **not** build scripting language.
- Do **not** invent large new save-project system; reuse existing document save path.
