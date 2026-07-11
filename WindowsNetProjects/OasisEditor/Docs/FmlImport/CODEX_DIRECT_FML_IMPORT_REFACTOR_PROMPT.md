# Codex Prompt: Direct FML Import Refactor

Work in:

`WindowsNetProjects/OasisEditor`

Repository:

`johnparker007/Oasis`

Read these documents first:

1. `Docs/FmlImport/FML_DIRECT_IMPORT_REFACTOR_CONTEXT.md`
2. `Docs/FmlImport/TASK_DIRECT_FML_IMPORT_REFACTOR.md`

Then inspect the current implementation before editing.

## Goal

Replace the temporary FML-to-MFME-Extract compatibility pipeline with direct mapping from decoded FML components into Oasis editor models.

The old `Import MFME Extract` workflow is superseded and should be removed.

The completed production flow must be:

```text
MFME .fml
    -> FmlDecoderService
    -> decoded FML model/components
    -> direct FmlToOasisMapper
    -> source-neutral import result
    -> generic undoable Panel2D insertion
```

It must no longer be:

```text
MFME .fml
    -> decoded JSON
    -> fabricated legacy extract JSON
    -> legacy manifest parser
    -> legacy DTOs
    -> legacy-to-Oasis mapper
```

## Why this is required

The compatibility layer is now losing valid decoder data, especially for text-based lamps:

- lamp colors are replaced or omitted;
- per-sublamp `OnColor` is not preserved;
- lamp `OffText`/on-state text does not become `DisplayText`;
- non-graphical lamps lose their lamp numbers because legacy lamp elements are only built through image handling;
- the decoder’s associated number/color/image data is broken apart and reconstructed through multiple schemas.

Do not fix this by expanding `FmlDecodedLayoutAdapter`. Remove that adapter from the production path.

## Required implementation

### 1. Inventory before edits

Trace and document in your working notes:

- current FML import call chain;
- old MFME Extract import call chain;
- all references to legacy extract DTOs, parser, mapper, services, UI and automation;
- reusable source-neutral code currently located under `Features/MfmeImport`;
- current tests and fixtures for graphical and text-heavy FML layouts.

Run the relevant tests before changing code.

### 2. Add a direct mapper

Add an editor-side mapper under `OasisEditor/Features/FmlImport/`, using a suitable project-convention name such as:

```text
FmlToOasisMapper
```

Consume the typed decoded layout/component model directly wherever practical.

Do not serialize decoded components to JSON merely to parse them again.

If decoder type visibility is a problem, introduce the smallest stable decoder contract needed. Do not create a new legacy-style compatibility DTO hierarchy.

Map all component families currently supported by the old mapper:

- Background/Bitmap/Frame;
- Lamp/PrismLamp;
- Button/Checkbox;
- Reel/BandReel/DiscReel/FlipReel;
- SevenSeg/SevenSegBlock;
- Alpha variants;
- Label.

Preserve existing successful behavior for geometry, dimensions, numbering, colors, overlays, reversed state, reel scale, input definitions, source identity and warnings.

### 3. Correct lamp mapping

Lamp mapping must preserve association by sublamp index.

For each defined sublamp, keep together:

- lamp number;
- sublamp on color;
- main image;
- mask image;
- source element index;
- shared source set identity.

Ignore MFME undefined lamp number `-2`.

Create one Oasis lamp element per defined sublamp number.

Text/shape lamps with no bitmap must still import.

For an image-less lamp:

- set `DisplayNumber` from the decoded lamp table;
- set `OnColorHex` from the matching decoded sublamp color;
- set `DisplayText`;
- map font name/style/size where decoded;
- leave primary and secondary asset paths null;
- do not classify it as graphical.

For lamp text:

1. prefer non-empty `OffText`;
2. otherwise use the first non-empty on-state text beginning with `On1Text`;
3. use the matching state font when possible;
4. fall back to the primary lamp font.

For graphical lamps:

- retain main and mask asset behavior;
- retain the real decoded sublamp color;
- classify as graphical only when a usable main image exists;
- do not treat a mask or unrelated image as sufficient.

Preserve alpha and channel ordering when converting colors. Add tests with non-symmetric channel values.

Do not globally alter the editor lamp fallback color to conceal absent imported colors.

### 4. Refactor FmlImportService

Change the production FML service so it:

1. validates paths;
2. decodes the FML;
3. exports decoded images to OS temp staging;
4. calls the direct mapper;
5. copies/post-processes imported authored assets into project `Assets/`;
6. returns a source-neutral import result.

Remove production dependence on:

- `FmlDecodedLayoutAdapter`;
- fabricated `layout.json` legacy manifest;
- legacy extract manifest parsing;
- legacy DTO construction;
- `MfmeToOasisComponentMapper`.

Keeping decoded JSON as optional diagnostic output is acceptable, but it must not drive mapping.

### 5. Generalize reusable infrastructure

Retain but rename/generalize infrastructure that is not genuinely extract-specific, including:

- import result/warning types;
- undoable document insertion command;
- progress and logging coordination;
- input-definition updates;
- editor refresh behavior;
- asset copying/post-processing;
- FML automation import-and-save support.

Use source-neutral names following existing conventions. Update DI, command bindings and tests.

### 6. Remove MFME Extract support

After direct mapping is working and tested, remove:

- `Import MFME Extract...` menu/UI;
- extract file/folder dialogs and commands;
- extract-specific reader/service interfaces;
- legacy extract manifest parser;
- legacy component DTOs;
- legacy-to-Oasis mapper;
- obsolete `MfmeImportService` code;
- `FmlDecodedLayoutAdapter`;
- fabricated legacy manifest generation;
- obsolete DI registrations;
- obsolete automation paths;
- tests that only validate removed extract compatibility;
- outdated documentation that presents the legacy bridge as current architecture.

Before deleting files, move any reusable image, asset or command behavior into suitable source-neutral/FML-specific locations.

Do not leave both workflows supported after the task is complete.

## Required tests

Add or update focused tests for:

### Architecture

- production FML import does not generate or parse a legacy extract manifest;
- direct mapper consumes decoded components;
- no production FML service depends on legacy extract DTOs/parser/mapper.

### Text-only single lamp

Use a lamp with:

- known geometry;
- lamp number `42`;
- known non-white/non-symmetric color;
- `OffText = "HOLD"`;
- no images.

Assert:

- one lamp is produced;
- `DisplayNumber == 42`;
- `DisplayText == "HOLD"`;
- `OnColorHex` is exact;
- asset paths are null;
- it is non-graphical.

### Multi-sublamp text lamp

Use multiple defined numbers and distinct colors.

Assert number/color pairing, one output element per defined sublamp, source grouping and skipping of `-2` entries.

### Graphical lamp regression

Assert number, color, main image, mask image and graphical classification all remain correct.

### Text/font fallback

Assert:

- off text preferred;
- first non-empty on text used as fallback;
- empty values ignored;
- matching font selected;
- primary font fallback works.

### Other component families

Add regression coverage for each component family previously supported by the legacy mapper.

### Editor and automation

Assert:

- generic undoable insertion works;
- project inputs are updated;
- failed imports do not mutate documents;
- FML automation import-and-save still works;
- old MFME Extract menu/command is gone.

## Validation

Run focused tests and the broader Oasis Editor suite.

Search the complete solution for:

```text
FmlDecodedLayoutAdapter
MfmeExtract
LegacyExtract
MfmeLegacy
MfmeToOasisComponentMapper
MfmeExtractManifestParser
ImportMfmeExtract
ExtractComponent
```

Every remaining result must be justified or removed.

Inspect at least one real graphical FML and one text-heavy FML using the direct path.

## Completion report

At completion, report:

- final architecture and call chain;
- new mapper/result/command names;
- files removed;
- reusable code moved or renamed;
- exact lamp association logic;
- asset staging and copying behavior;
- tests added/updated;
- exact test commands and results;
- remaining unsupported decoded fields;
- any remaining legacy/extract references and why they remain.

Implement the refactor, do not stop after producing a plan.
