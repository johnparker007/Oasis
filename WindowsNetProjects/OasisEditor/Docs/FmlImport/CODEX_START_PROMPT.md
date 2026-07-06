# Codex Start Prompt - MFME FML Import

You are working in:

```text
WindowsNetProjects/OasisEditor
```

Implement the first-pass MFME `.fml` import integration for the Oasis Editor.

## Read First

Read these files before editing code:

```text
AGENTS.md
Docs/FmlImport/FML_IMPORT_CONTEXT.md
Docs/FmlImport/TASK_01_FML_IMPORT_INTEGRATION.md
```

Do not scan every Markdown file in the repo. Open additional docs only if directly relevant to this task.

## Goal

Add a new menu option:

```text
File > Import > MFME .FML...
```

It should sit alongside the existing:

```text
File > Import > MFME Extract...
```

The new import path should decode an MFME `.fml` file through the newly copied `OasisEditor/FmlDecoder/` source, stage the decoded layout in a temporary folder, and reuse the existing MFME Extract import machinery to import into the active `Panel2D` document.

## Key Constraint

Treat this folder as vendor-copied source from another repo:

```text
OasisEditor/FmlDecoder/
```

Keep it copy/paste friendly. Do not scatter Oasis-specific modifications throughout it.

Prefer adding Oasis-specific integration code under:

```text
OasisEditor/Features/FmlImport/
```

Only make tiny decoder-side changes if needed to expose a stable public decode API that the Editor can call without using `Console.Out`.

## Relevant Existing Code

Existing MFME Extract import path:

```text
OasisEditor/MainWindow.xaml
OasisEditor/MainWindowViewModel.cs
OasisEditor/Automation/MfmeImportAndDocumentSaveServices.cs
OasisEditor/Features/MfmeImport/MfmeImportService.cs
OasisEditor/Features/MfmeImport/FileSystemMfmeExtractReader.cs
OasisEditor/Features/MfmeImport/ImportMfmeExtractCommand.cs
```

New decoder source:

```text
OasisEditor/FmlDecoder/Application/Application.cs
OasisEditor/FmlDecoder/Decoder/Component/Core/ComponentParser.cs
OasisEditor/FmlDecoder/Model/Layout.cs
```

## Implementation Requirements

1. Add an Editor-callable FML decode service/API that returns decoded JSON or a result object.
2. Add `Features/FmlImport` services to coordinate `.fml` decode -> temp staging folder -> existing MFME import service.
3. Add `ImportMfmeFmlCommand` to `MainWindowViewModel`.
4. Add the new menu item in `MainWindow.xaml`.
5. Enable the command only when a project is loaded and the active document is `Panel2D`.
6. Reuse the existing MFME import result handling, document insertion command, input definition update, progress reporting, and output logging where practical.
7. Keep existing MFME Extract import working.
8. Keep temporary decoded files outside project `Assets/` and `Generated/`.
9. Add tests where practical.

## Important Notes

The decoder is still WIP and may not generate image files yet. The first pass should import whatever decoded layout/component data is available and tolerate missing generated images where the existing importer can handle it.

Do not attempt to fully replace the old MFME Extract workflow in this task.

Do not run builds/tests in Codex unless explicitly requested. John will run builds/tests locally.
