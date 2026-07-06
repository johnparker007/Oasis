# MFME FML Import Context

## Goal

Add a new Editor import option for MFME `.fml` layout files:

```text
File > Import > MFME .FML...
```

This should initially sit alongside the existing:

```text
File > Import > MFME Extract...
```

The new FML path should ultimately replace the old MFME Extract workflow once the new decoder/exporter is complete.

## Current Editor Import Path

The current MFME Extract import path is already wired into the WPF Editor and imports into the active `Panel2D` document.

Important existing files:

```text
OasisEditor/MainWindow.xaml
OasisEditor/MainWindowViewModel.cs
OasisEditor/Automation/MfmeImportAndDocumentSaveServices.cs
OasisEditor/Features/MfmeImport/MfmeImportService.cs
OasisEditor/Features/MfmeImport/FileSystemMfmeExtractReader.cs
OasisEditor/Features/MfmeImport/ImportMfmeExtractCommand.cs
```

The current flow is roughly:

1. User selects an MFME Extract JSON manifest.
2. `MainWindowViewModel.ImportMfmeExtract()` validates that a `Panel2D` document is active.
3. `MfmeExtractImportService.ImportFromExtract(...)` calls `MfmeImportService.Import(...)`.
4. `FileSystemMfmeExtractReader` reads a JSON manifest/extract folder.
5. The import mapper produces Oasis `PanelElementModel` instances and input definitions.
6. `ImportMfmeExtractCommand` inserts imported elements into the active Panel2D document.
7. The asset browser, hierarchy, inspector, project input definitions, and output log are refreshed.

The FML import should reuse this downstream import/document insertion path as much as possible.

## New FmlDecoder Source

A new decoder source tree has been copied into:

```text
OasisEditor/FmlDecoder/
```

Important entry points found so far:

```text
OasisEditor/FmlDecoder/Application/Application.cs
OasisEditor/FmlDecoder/Decoder/Component/Core/ComponentParser.cs
OasisEditor/FmlDecoder/Model/Layout.cs
```

`Application.Run(string[] args)` currently behaves like a command-line entry point:

- parses args
- accepts `.dat` or `.fml`
- decrypts `.fml`
- walks TLV data
- optionally writes layout JSON to `Console.Out` when `--json` is passed
- writes errors/usage to console/log output
- returns an exit code

This is useful as a reference, but should not be the main WPF integration boundary.

## Vendor Source Rule

Treat `OasisEditor/FmlDecoder/` as vendor-copied source from another repo.

Avoid scattering Oasis-specific changes through the decoder tree. The aim is that updated decoder source can be copied in from the teammate's repo with minimal manual merge work.

Preferred approach:

- keep Editor-specific import code outside `FmlDecoder/`
- add new Oasis integration code under `OasisEditor/Features/FmlImport/`
- make only a tiny, stable API change inside `FmlDecoder/` if absolutely required
- document any local edits inside `FmlDecoder/` clearly

The ideal decoder-side integration surface is a small public API such as:

```csharp
public sealed class FmlDecoderService
{
    public string DecodeToJson(string inputPath, uint offset = 0);
}
```

or a result-based equivalent:

```csharp
public sealed class FmlDecodeResult
{
    public bool Succeeded { get; init; }
    public string? Json { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public IReadOnlyList<string> Warnings { get; init; }
}
```

If the decoder already grows an equivalent public API upstream, use that instead of inventing another one.

## Desired First Implementation

Add a first-pass FML import that:

1. Adds `File > Import > MFME .FML...` to the menu.
2. Enables it only when a project is loaded and the active document is `Panel2D`.
3. Prompts for an `.fml` file.
4. Decodes the FML into a temporary staging extract folder.
5. Writes decoded layout JSON into the staging folder in the format currently expected by the existing `MfmeImportService` path, or adds a narrow adapter if the JSON shape differs.
6. Reuses the existing MFME import machinery to map decoded components into Panel2D elements.
7. Inserts imported elements using the same command/undo path as existing MFME Extract import.
8. Updates project input definitions using the same behavior as MFME Extract import.
9. Refreshes asset browser, hierarchy, inspector, and output log.
10. Cleans up temporary files where safe, or keeps them only long enough for import diagnostics.

The decoder is currently WIP and may not emit/copy image files yet. The first implementation should be tolerant of missing generated images and should import whatever component/layout data is available.

## Temporary Decode Staging

Do not write FML decode output under `Assets/` or `Generated/` as authored project content.

Use an OS temp folder, for example:

```text
Path.Combine(Path.GetTempPath(), "OasisEditor", "FmlImport", <unique-id>)
```

The current project rule remains:

- authored/user-editable content goes under `Assets/`
- disposable runtime/cache/export files go under `Generated/`
- short-lived import staging should live under OS temp

When the existing import service copies assets into the project, it should continue to copy only real authored/imported assets into the project `Assets/` path.

## Suggested Editor-Side Structure

Create a new feature folder:

```text
OasisEditor/Features/FmlImport/
```

Possible classes:

```text
FmlImportService.cs
FmlImportDecodeService.cs
FmlImportStagingService.cs
FmlImportResult.cs
```

Keep this thin. It should coordinate decoder -> temp extract -> existing MFME import, not duplicate the existing MFME mapper.

Potential automation-facing service:

```csharp
internal interface IFmlImportService
{
    MfmeImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true);
}
```

Implementation can:

1. create temp staging directory
2. decode `.fml` to JSON
3. write compatible manifest JSON into staging
4. call existing `MfmeImportService.Import(...)` or `MfmeExtractImportService.ImportFromExtract(...)`

## Refactoring Opportunity

`MainWindowViewModel.ImportMfmeExtract()` currently contains reusable UI/import flow logic.

Avoid copying the whole method for FML import. Extract a shared private helper if practical, for example:

```csharp
private async Task ImportMfmeLikeLayoutAsync(
    string operationTitle,
    string progressTitle,
    Func<IEditorProgressReporter, Task<MfmeImportResult>> importFactory)
```

or a simpler local helper that handles:

- running the progress dialog
- logging warnings/errors
- updating input definitions
- executing `ImportMfmeExtractCommand`
- refreshing editor panels
- final output log summaries

The existing command name can stay `ImportMfmeExtractCommand` for now if it is generic enough, but consider a future rename to `ImportMfmeLayoutCommand` if this becomes broader than extract import.

## Testing Direction

Add or update tests where practical:

- menu command property exists and enablement matches active `Panel2D` document behavior
- FML import service creates staging output and delegates to existing MFME import service
- decoder failure returns/logs errors and does not mutate the active document
- import success inserts elements through the existing command path
- temp/staging behavior does not write decoded intermediate files under project `Assets/` or `Generated/`

Do not require fully generated image files for initial tests while the decoder is still WIP.

## Non-goals For First Pass

Do not attempt to fully replace MFME Extract yet.

Do not rewrite all FmlDecoder parser/model classes.

Do not move authored files into `Generated/`.

Do not make WPF import code depend on `Console.Out` as the normal integration path.

Do not add broad migration code for old project layouts as part of this task.
