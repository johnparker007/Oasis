# Direct FML Import Refactor Context

## Decision

The MFME Extract import workflow is retired.

The only supported MFME layout import path should be:

```text
MFME .fml
    -> FmlDecoderService
    -> decoded FML model
    -> direct FML-to-Oasis mapper
    -> imported Panel2D elements and project inputs
```

The current FML import path still passes decoded data through the old MFME Extract compatibility layer:

```text
MFME .fml
    -> decoded layout model
    -> decoded JSON
    -> fabricated legacy extract JSON
    -> legacy manifest parser
    -> legacy extract DTOs
    -> legacy-to-Oasis mapper
    -> PanelElementModel
```

That compatibility route was useful for the first FML integration, but it is now obsolete. It creates duplicate schemas, unnecessary serialization and parsing, and places old MFME Extract limitations in the path of the new decoder.

The recent text-lamp problems are examples of this architectural issue:

- decoded sublamp colors are replaced or omitted during legacy adaptation;
- decoded lamp text is not represented by the generic legacy fields selected by the adapter;
- image-less lamps do not receive legacy `LampElements`, so their lamp numbers are lost;
- data that is naturally associated in the decoder model must be reconstructed after several conversions.

Do not extend `FmlDecodedLayoutAdapter` to solve these problems. Replace the adapter and legacy extract route with direct mapping.

## Scope

This refactor must:

1. Map decoded FML components directly to Oasis editor models.
2. Retain the existing successful graphical-layout behavior.
3. Correctly import text/shape-based lamps, including numbers, colors, text and fonts where decoded.
4. Retain image export, post-processing, project asset copying, diagnostics, warnings and import command/undo behavior.
5. Remove the obsolete MFME Extract UI and implementation.
6. Generalize downstream import infrastructure that is still useful but currently has MFME Extract-specific names.

## Architectural boundary

Keep a clean boundary between the decoder and the editor:

```text
FML decoder domain model
    -> Oasis FML import mapper
    -> Oasis editor/domain models
```

The decoder owns:

- FML decryption and parsing;
- MFME component interpretation;
- decoded geometry, values, colors, fonts and images;
- sublamp tables and decoded component relationships.

The Oasis FML importer owns:

- deciding how decoded MFME concepts map to Oasis `PanelElementModel` instances;
- exporting and copying decoded images into the project asset layout;
- creating project input definitions;
- producing import warnings and diagnostics;
- returning an import result for insertion into the active Panel2D document.

The WPF/editor layer owns:

- file selection;
- progress UI;
- logging;
- applying imported elements through undoable commands;
- refreshing hierarchy, inspector, asset browser and project state.

Avoid coupling editor UI code directly to low-level decoder parser implementation details.

## Target flow

Recommended target flow:

```text
FmlImportService.ImportFromFml(...)
    1. Validate source and project paths.
    2. Decode the FML through FmlDecoderService.
    3. Export any decoded images to temporary staging.
    4. Map the decoded layout directly through FmlToOasisMapper.
    5. Copy required imported assets into the project Assets tree.
    6. Return LayoutImportResult.
    7. Apply the result through a generic document import command.
```

A neutral result type is preferred:

```csharp
internal sealed class LayoutImportResult
{
    public IReadOnlyList<PanelElementModel> ImportedElements { get; init; }
    public IReadOnlyList<InputDefinitionModel> InputDefinitions { get; init; }
    public IReadOnlyList<string> CopiedAssetRelativePaths { get; init; }
    public IReadOnlyList<ImportWarning> Warnings { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public IReadOnlyList<string> DebugDiagnostics { get; init; }
}
```

Names may differ if an equivalent source-neutral type already exists.

## Direct mapper

Introduce an editor-side mapper under `OasisEditor/Features/FmlImport/`, for example:

```csharp
internal sealed class FmlToOasisMapper
{
    public FmlMapResult Map(
        Layout decodedLayout,
        IReadOnlyDictionary<FmlDecodedImageKey, string> exportedImages);
}
```

Use typed decoder objects directly where practical. Do not serialize to JSON merely to parse that JSON again.

If visibility of decoder types prevents clean integration, expose or add a small stable decoder-facing contract. Do not create another broad compatibility schema resembling the removed extract DTO layer.

## Lamp mapping requirements

Lamp and PrismLamp mapping must preserve sublamp association.

For each defined sublamp, keep these values aligned by sublamp index:

- lamp number;
- on color;
- main image;
- mask image;
- text/font state where applicable;
- source component identity and shared-source grouping.

Required behavior:

- create one Oasis lamp element per defined sublamp number;
- ignore MFME undefined sublamp value `-2`;
- allow non-graphical lamps with no images;
- set `DisplayNumber` from the decoded sublamp table;
- set `OnColorHex` from the corresponding decoded sublamp color;
- set `DisplayText`, preferring non-empty `OffText`, then the first non-empty on-state text;
- map the associated font where possible, with primary font as fallback;
- set graphic asset paths only when a usable main image exists;
- do not infer `Graphic = true` solely from a mask or unrelated image;
- preserve existing shared-source grouping for multi-sublamp components.

Do not globally change the default lamp rendering color merely to hide missing imported data. The direct mapper should provide the real decoded color.

## Components

Direct mapping must cover at least all component types currently supported through the legacy mapper:

- Background/Bitmap/Frame;
- Lamp/PrismLamp;
- Button/Checkbox and button-as-lamp behavior;
- Reel/BandReel/DiscReel/FlipReel;
- SevenSeg/SevenSegBlock;
- Alpha variants;
- Label.

Preserve current geometry, numbering, reel scaling, reversed state, overlays, colors and input-definition behavior unless the decoder provides more accurate information.

Unsupported decoded component types should be skipped with clear warnings rather than crashing the import.

## Assets and staging

Temporary decoded/exported files should remain under the OS temporary directory.

Do not place intermediate decoded JSON or staging files under project `Assets/` or `Generated/`.

Only actual imported authored assets should be copied into the project asset tree.

Retain reliable existing image extraction and post-processing code. Move it behind FML-specific or source-neutral services rather than deleting it with the legacy extract parser.

## Generic infrastructure to retain

Retain and generalize:

- import result handling;
- warning/error reporting;
- progress reporting;
- undoable insertion into Panel2D;
- project input-definition updates;
- document refresh behavior;
- asset copy/post-processing helpers;
- automation support for importing an FML and saving the resulting document.

Rename extract-specific types when they no longer describe their responsibility. Likely candidates include:

- `ImportMfmeExtractCommand` -> `ImportLayoutCommand` or `ImportPanelElementsCommand`;
- `MfmeImportResult` -> `LayoutImportResult`;
- `MfmeImportWarning` -> `LayoutImportWarning` or `ImportWarning`;
- extract-specific automation wrappers -> FML-specific or source-neutral names.

Do not perform broad renames without updating all tests, DI registrations, command bindings and automation entry points.

## Obsolete functionality to remove

After the direct path is working and covered by tests, remove:

- `FmlDecodedLayoutAdapter`;
- generation of the fabricated legacy `layout.json` manifest;
- MFME Extract file/folder reader code;
- legacy extract manifest parser;
- legacy extract component DTOs;
- legacy-to-Oasis component mapper;
- old `MfmeImportService` implementation if no reusable responsibilities remain;
- `Import MFME Extract...` menu item and command;
- tests whose only purpose is validating obsolete extract JSON compatibility;
- outdated documentation that says FML import should continue to route through MFME Extract.

Before deletion, search the full solution for references to:

```text
MfmeExtract
LegacyExtract
FmlDecodedLayoutAdapter
MfmeToOasisComponentMapper
MfmeImportService
ImportMfmeExtract
ExtractComponent
```

Classify each reference as obsolete, reusable-but-needing-rename, or still required.

## Migration rule

Do not maintain both implementations indefinitely.

A temporary side-by-side direct mapper is acceptable during implementation and testing, but the completed change should have one production FML import path and no user-facing MFME Extract import.

## Validation standard

The refactor is complete when:

- FML imports no longer create or parse a legacy extract manifest;
- no production FML path uses legacy extract DTOs or mapper classes;
- graphical FML fixtures import at least as well as before;
- text-based lamp fixtures preserve display number, display text and color;
- image-less lamps import successfully;
- input definitions still import correctly;
- project assets are copied to the correct asset package locations;
- undo/redo and editor refresh behavior still work;
- MFME Extract UI and obsolete code are removed;
- the full relevant test suite passes.

## Implementation status

The refactor is now implemented in the editor codebase:

- MFME Extract import is removed as a supported user-facing workflow.
- MFME `.fml` is the supported source format for importing MFME layouts.
- FML decoding produces a typed `Layout`; `FmlToOasisMapper` maps decoder components directly to Oasis panel elements and input definitions.
- Temporary staging is used only for decoded/exported image files and diagnostic paths under the OS temporary directory.
- Production FML import no longer creates, writes, reads, or parses a compatibility `layout.json` manifest.
- Legacy extract DTOs, parser, file-system reader, and legacy-to-Oasis mapper are obsolete and should not be reintroduced.
