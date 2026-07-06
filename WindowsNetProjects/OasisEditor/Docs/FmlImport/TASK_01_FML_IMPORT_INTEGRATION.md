# Task 01 - MFME FML Import Integration

## Objective

Implement a first-pass Editor import path for MFME `.fml` files that sits alongside the existing MFME Extract import and reuses the existing Panel2D import pipeline wherever possible.

Add:

```text
File > Import > MFME .FML...
```

The new command should import decoded FML layout/component data into the active `Panel2D` document.

## Read First

Read:

```text
Docs/FmlImport/FML_IMPORT_CONTEXT.md
```

Relevant existing code:

```text
OasisEditor/MainWindow.xaml
OasisEditor/MainWindowViewModel.cs
OasisEditor/Automation/MfmeImportAndDocumentSaveServices.cs
OasisEditor/Features/MfmeImport/MfmeImportService.cs
OasisEditor/Features/MfmeImport/FileSystemMfmeExtractReader.cs
OasisEditor/Features/MfmeImport/ImportMfmeExtractCommand.cs
OasisEditor/FmlDecoder/Application/Application.cs
OasisEditor/FmlDecoder/Decoder/Component/Core/ComponentParser.cs
OasisEditor/FmlDecoder/Model/Layout.cs
```

## Constraints

### Keep FmlDecoder Copy-Friendly

Treat this folder as vendor-copied source:

```text
OasisEditor/FmlDecoder/
```

Do not scatter Oasis-specific code through it.

Allowed inside `FmlDecoder/`:

- a tiny stable public decoder API if needed
- changing access modifiers only where required for that API
- small refactor so CLI-style code and Editor integration can share the same decode core

Preferred outside `FmlDecoder/`:

```text
OasisEditor/Features/FmlImport/
```

### Reuse Existing Import Path

Do not build a separate full FML-to-Oasis mapper if the existing MFME import path can be reused.

First-pass flow should be:

```text
.fml file
  -> FmlDecoder decode
  -> temporary extract/staging folder
  -> existing MfmeImportService/MfmeExtractImportService
  -> ImportMfmeExtractCommand or equivalent document command
  -> active Panel2D document
```

### Temporary Files

Use OS temp for decode staging. Do not write intermediate decoder output under project `Assets/` or `Generated/`.

## Implementation Steps

### 1. Add FML Decoder Integration API

Add or expose a small decoder-facing API that can be called from Editor code without relying on `Console.Out`.

Example shape:

```csharp
public sealed class FmlDecoderService
{
    public string DecodeToJson(string inputPath, uint offset = 0);
}
```

or:

```csharp
public sealed class FmlDecodeResult
{
    public bool Succeeded { get; init; }
    public string? Json { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
```

Use the existing logic from `Application.Run(...)`:

- `Path.GetFullPath`
- file existence validation
- `.fml` decryption through `FmlDecryptor.Decrypt(...)`
- `FileWalker` / `ComponentWalker` / `ComponentParser`
- `componentParser.ToLayout().ToJson(indented: true)`

Keep `Application.Run(...)` working by making it call the shared decode API if practical.

### 2. Add FML Import Feature Services

Create:

```text
OasisEditor/Features/FmlImport/
```

Suggested service:

```csharp
internal interface IFmlImportService
{
    MfmeImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true);
}
```

Implementation responsibilities:

1. validate source path
2. create unique temp staging directory
3. call decoder service to get layout JSON
4. write layout JSON into staging in the manifest shape required by the existing MFME extract reader, or add a narrow adapter if JSON shapes differ
5. call existing `MfmeImportService.Import(...)` or `MfmeExtractImportService.ImportFromExtract(...)`
6. return `MfmeImportResult`

If the decoded JSON from FmlDecoder does not match the legacy extract manifest shape, keep the adapter narrow and isolated under `Features/FmlImport/` or `Features/MfmeImport/`. Do not modify many decoder parser classes to match Oasis.

### 3. Add Main Window Command Wiring

In `MainWindowViewModel`:

- add `ImportMfmeFmlCommand`
- enable it under the same broad condition as MFME Extract import:

```csharp
LoadedProject is not null
&& !_isMfmeImportInProgress
&& SelectedDocument?.Document.DocumentType == EditorDocumentType.Panel2D
```

- add an import method that prompts for `.fml`:

```text
Title: Import MFME FML
Filter: MFME FML Layout|*.fml|All Files|*.*
InitialDirectory: LoadedProject.ProjectDirectory
```

- run the import through progress UI
- log warnings/errors to Output
- show message box on failure
- insert imported elements into the active Panel2D document
- update project input definitions
- refresh asset browser, hierarchy, inspector

Prefer extracting shared logic from `ImportMfmeExtract()` instead of duplicating the whole method.

### 4. Add Menu Item

In `MainWindow.xaml`, under:

```text
File > Import
```

add:

```xml
<MenuItem Header="MFME ._FML..."
          Command="{Binding ImportMfmeFmlCommand}" />
```

Keep existing `MFME Extract...` in place.

### 5. Logging / UX

Use distinct labels so users can tell which path ran:

- `Importing MFME FML`
- `MFME FML import completed...`
- `MFME FML import failed...`

Keep summary logs similar to existing MFME Extract:

- imported element count
- grouped element kinds
- skipped unsupported component count
- copied asset count
- input definition count

### 6. Tests

Add tests where existing test harness makes this practical.

Useful coverage:

- command enablement for active `Panel2D`
- FML import service delegates decoded staging output into MFME import flow
- failed decode returns/logs errors and does not insert elements
- successful import uses existing document command path and is undoable if the existing command supports it
- staging path is not inside project `Assets/` or `Generated/`

Do not require image generation from the decoder yet.

## Acceptance Criteria

- `File > Import > MFME .FML...` appears in the Editor menu.
- Command is only active when a project is loaded and the selected document is `Panel2D`.
- Selecting an `.fml` file runs the new decoder path.
- The import reuses existing MFME import machinery as far as practical.
- The active Panel2D receives imported elements when decoding/mapping succeeds.
- Existing MFME Extract import still works.
- `OasisEditor/FmlDecoder/` remains copy-friendly, with minimal local changes.
- Temporary decode output is not written under project `Assets/` or `Generated/`.
- Output log distinguishes MFME Extract import from MFME FML import.
