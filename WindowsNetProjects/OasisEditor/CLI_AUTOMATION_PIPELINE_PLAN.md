# CLI Automation Pipeline Plan

This document defines the next architecture workstream: making Oasis Editor project/import/export operations automatable from a command-line/headless workflow.

The immediate target is to support future workflows such as:

```text
Create/open project container named "xxx.oasisproject"
Create Panel2D named "mfmeimport.panel2d"
Import MFME extract into active Panel2D
Save Panel2D document using existing document-save workflow
Export Panel2D to MAME .lay layout
```

The MAME `.lay` exporter is not written yet, but the architecture should leave a clean extension point for it.

## Important Clarification About Save Behavior

At the moment, Oasis Editor primarily has:

```text
File -> Save Document
```

for saving the currently focused document (such as a `.panel2d` document).

There is not currently a broad explicit `Save Project` workflow in the same sense as some traditional editors/IDEs.

Therefore:

- Codex should not invent a large new project-save system during this workstream;
- the automation architecture should initially reuse/extract the existing document-save behavior;
- future project-save/container-save behavior may be added later if the project format evolves.

When this document references project save concepts, treat them as future-facing placeholders unless an existing implementation already exists.

The immediate concrete automation target is:

```text
SavePanel2DDocument
```

not:

```text
FullProjectSaveSystem
```

## Key Decision

Do not build an in-app terminal first.

Instead build reusable command/pipeline services that can be used by:

- WPF menu/buttons/dialogs;
- future CLI/headless automation;
- tests;
- future command palette;
- optional future debug terminal.

Core direction:

```text
Reusable services + command runner first
CLI/headless entrypoint second
Terminal/debug console much later, if still useful
```

## Why Not Terminal First

A terminal would require:

- command language/parser;
- history/completion;
- quoting/path syntax;
- async progress display;
- active document/project context rules;
- undo integration rules;
- error formatting;
- help system.

That is too much surface area for the immediate converter use case.

The immediate need is deterministic automation and batch conversion.

## Desired Long-Term Project Shape

Eventually the solution should move toward:

```text
OasisEditor.Core
    project/document model
    import/export services
    command pipeline
    validation
    file IO
    non-WPF runtime abstractions

OasisEditor.Wpf
    launcher
    editor UI
    views/panes
    Skia/WPF interaction
    menus/dialogs

OasisEditor.Cli
    command-line conversion/import/export workflows
```

This split should be incremental.

Do not attempt a giant solution-wide project split immediately.

Prefer extracting UI-independent services first. Move to separate projects once seams are stable.

## Immediate Goal

Create enough infrastructure that a future CLI can run this workflow without depending on WPF views:

```text
CreateProjectContainer
CreatePanel2D
ImportMfmeExtract
SavePanel2DDocument
ExportMameLay
```

The first working automation can be implemented inside the existing WPF app as a `--headless` mode if that is faster, but the code should be shaped so it can later move into `OasisEditor.Cli`.

## Recommended Architecture

### Command Abstractions

Introduce a small command pipeline abstraction.

Suggested names:

```text
IOasisAutomationCommand
OasisAutomationCommandContext
OasisAutomationCommandResult
OasisAutomationCommandRunner
```

These are separate from existing undoable edit commands unless an existing command abstraction already fits.

Automation commands are workflow commands, for example:

```text
CreateProjectContainerAutomationCommand
CreatePanel2DAutomationCommand
ImportMfmeExtractAutomationCommand
SavePanel2DDocumentAutomationCommand
ExportMameLayAutomationCommand
```

They may internally call lower-level undoable/editor commands where appropriate, but they should not require a visible WPF view.

### Service Abstractions

Extract UI-independent services:

```text
IProjectContainerCreationService
IPanel2DCreationService
IDocumentSaveService
IMfmeExtractImportService
IMameLayExportService
IProjectLoadService
IAutomationLog
```

If equivalent services already exist, reuse/rename/refactor rather than duplicating.

### Automation Context

The command context should include:

```text
WorkingDirectory
CurrentProject
CurrentDocument
Logger
CancellationToken
ProgressReporter
FileSystem/Path helpers, if abstracted
```

It should not include WPF controls.

## CLI Shape

Preferred eventual CLI examples:

```text
OasisEditor.Cli.exe convert-mfme \
  --input "source.mfmeextract" \
  --project "xxx.oasisproject" \
  --panel "mfmeimport.panel2d" \
  --export-lay "output.lay"
```

or, interim inside WPF executable:

```text
OasisEditor.exe --headless convert-mfme \
  --input "source.mfmeextract" \
  --project "xxx.oasisproject" \
  --panel "mfmeimport.panel2d" \
  --export-lay "output.lay"
```

The exact executable shape can be decided after service extraction.

## CLI Exit Codes

Use deterministic exit codes:

```text
0 = success
1 = invalid arguments
2 = input file not found / unreadable
3 = import failed
4 = save failed
5 = export failed
10 = unexpected error
```

Add more specific codes later if needed.

## Automation Logging

CLI/headless workflows should log to:

- console/stdout/stderr;
- existing OutputLogService when running inside WPF;
- disk log if available.

Do not require the Output pane UI.

Suggested abstraction:

```text
IAutomationLog
    Info(string)
    Warning(string)
    Error(string, Exception?)
```

## Command Palette / Terminal Future

Do not implement now, but design should support future UI command entry.

Future command palette:

```text
Ctrl+Shift+P
> Import MFME Extract
> Export MAME Layout
> Undo
```

Future terminal/debug console:

```text
Undo()
CreatePanel2D("foo.panel2d")
```

Both should call the same command registry/runner rather than separate code.

## MFME Import Automation

Codex should inspect the current MFME import implementation and identify UI dependencies.

Goal:

```text
MFME import logic should be callable without opening import dialogs or relying on active WPF controls.
```

Suggested service:

```text
IMfmeExtractImportService.ImportIntoPanel2DAsync(project, panelDocument, inputPath, options, cancellationToken)
```

Import options might include:

```text
PreserveExistingElements
ClearPanelBeforeImport
ImportInputs
ImportRomSettings
ImportLayoutMetadata
```

Use current behavior as defaults.

## Project Container / Panel Creation Automation

Add services for:

```text
Create empty Oasis project container at path
Create Panel2D document in project container
Set active/current document in automation context
```

These should not require Launcher UI dialogs.

## Document Save Automation

Add reusable extraction/wrapping for the existing:

```text
File -> Save Document
```

workflow.

Suggested abstraction:

```text
IDocumentSaveService
```

with automation command:

```text
SavePanel2DDocumentAutomationCommand
```

The automation pipeline should initially save documents using the same underlying behavior as the current UI document-save workflow.

Do not invent a large new project-save system during this workstream.

Future project/container save behavior can be added later if the format evolves.

## MAME .lay Export Placeholder

The MAME `.lay` exporter does not exist yet.

Create an interface/placeholder only if useful:

```text
IMameLayExportService.ExportAsync(project, panelDocument, outputPath, options, cancellationToken)
```

Initial implementation may throw `NotImplementedException` or return a clear failure result.

Do not block the rest of the automation architecture on this exporter.

## Tests

Add tests around command/service logic without WPF.

Suggested tests:

- automation command runner executes commands in order;
- pipeline stops on failure;
- errors are logged;
- cancellation stops pipeline;
- create project container command creates expected model/path;
- create panel command adds document to project container;
- import command can be invoked through fake importer;
- document save command can be invoked through fake save service;
- export command placeholder returns clear not-implemented result;
- argument parser maps CLI args to workflow options;
- invalid missing input path returns deterministic failure.

Avoid tests that require visible WPF windows.

## Recommended Codex Steps

### Step 1 - Inventory Existing Project/Import/Save APIs

Document current APIs and UI dependencies for:

- project container creation;
- panel creation;
- MFME extract import;
- current File -> Save Document behavior;
- export placeholders;
- existing command/undo systems.

Deliverable:

```text
CliAutomation.Inventory.md
```

### Step 2 - Define Automation Command Models

Add small command/result/context abstractions.

Keep them UI-independent.

### Step 3 - Extract Project Container / Panel Creation Services

Create reusable services for project container and Panel2D creation.

Wire existing UI actions through them where practical.

### Step 4 - Extract MFME Import Service

Refactor MFME import so it can be called from automation without WPF dialogs.

Keep existing UI import path working.

### Step 5 - Extract Document Save Service

Refactor the current File -> Save Document behavior into a reusable service.

Keep existing UI save behavior working.

### Step 6 - Add Conversion Pipeline Command

Add a high-level workflow command such as:

```text
ConvertMfmeToOasisProjectCommand
```

It should orchestrate:

```text
create project container -> create panel -> import -> save document
```

Include export placeholder if requested.

### Step 7 - Add CLI/Headless Entry Point

Choose lowest-risk route:

- interim `--headless` mode in current app; or
- separate `OasisEditor.Cli` project if seams are ready.

Do not delay the pipeline service work on project splitting.

### Step 8 - Add Tests

Add unit tests around command runner and pipeline behavior.

### Step 9 - Manual Verification

John should manually verify:

- existing UI project creation still works;
- existing UI MFME import still works;
- existing UI File -> Save Document still works;
- automation pipeline can create/import/save without a visible edit workflow;
- failures produce useful logs/errors.

## Manual Future Target

Eventually this should work:

```text
OasisEditor.Cli.exe convert-mfme --input "C:\Layouts\Foo\extract" --project "C:\Layouts\Foo\foo.oasisproject" --panel "mfmeimport.panel2d"
```

And later:

```text
OasisEditor.Cli.exe convert-mfme --input "C:\Layouts\Foo\extract" --project "C:\Layouts\Foo\foo.oasisproject" --panel "mfmeimport.panel2d" --export-lay "C:\Layouts\Foo\foo.lay"
```

## Out Of Scope

Do not implement now:

- full in-app terminal;
- scripting language;
- command auto-completion/history;
- MAME `.lay` exporter internals;
- broad solution/project split before service seams are ready;
- cross-platform packaging.

This workstream is about making core editor operations reusable, automatable, and testable.
