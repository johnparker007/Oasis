# Task: Replace Legacy MFME Extract Mapping with Direct FML Import

## Objective

Replace the temporary FML-to-legacy-extract compatibility pipeline with direct mapping from decoded FML components into Oasis editor models.

Retire the MFME Extract import workflow completely.

Read first:

- `Docs/FmlImport/FML_DIRECT_IMPORT_REFACTOR_CONTEXT.md`
- current files under `OasisEditor/Features/FmlImport/`
- current files under `OasisEditor/Features/MfmeImport/`
- current FML decoder model and component parser types
- relevant import, automation and editor command tests

## Constraints

- Work in `WindowsNetProjects/OasisEditor`.
- Preserve existing project asset-package rules.
- Keep decoder parsing responsibilities separate from editor mapping responsibilities.
- Prefer typed decoder models over JSON round-tripping.
- Do not add a replacement compatibility DTO layer.
- Do not leave both MFME Extract and FML import as supported user-facing workflows.
- Do not weaken tests merely to make the refactor pass.

## Phase 1: Inventory and establish a baseline

1. Trace the current production FML import from menu command through document insertion.
2. Trace the old MFME Extract import path and list every referenced type/file.
3. Classify each legacy item as:
   - delete;
   - retain and rename/generalize;
   - retain unchanged because it is genuinely source-neutral.
4. Identify all current component mappings and import behaviors that must survive.
5. Run the relevant existing tests before changing code and record the baseline.
6. Identify representative graphical and text-heavy FML fixtures already in the repository or tests.

Do not start by deleting the old mapper. First establish direct-path coverage.

## Phase 2: Introduce source-neutral import contracts

Create or rename source-neutral contracts for downstream editor application.

Expected concepts:

- import result;
- import warning;
- imported elements;
- copied asset paths;
- project input definitions;
- diagnostics and errors.

Rename the undoable document command if it is still called `ImportMfmeExtractCommand` but only inserts already-mapped elements.

Candidate names:

```text
LayoutImportResult
ImportWarning
ImportLayoutCommand
ImportPanelElementsCommand
```

Use names that fit current project conventions.

Update DI, view models, automation and tests as needed.

## Phase 3: Add direct FML-to-Oasis mapping

Add a mapper under `OasisEditor/Features/FmlImport/`, for example:

```text
FmlToOasisMapper.cs
```

The mapper should consume the decoded layout/components and exported image lookup directly.

Map all currently supported component families:

- backgrounds;
- lamps and prism lamps;
- buttons and checkboxes;
- reels;
- seven-segment displays;
- alpha displays;
- labels.

Preserve current successful behavior for:

- geometry and dimensions;
- component numbering;
- reel visible-scale calculation;
- reversed state;
- overlays;
- colors;
- text and fonts;
- input definitions;
- source component identity;
- shared-source grouping;
- import-source diagnostics.

Unsupported components should produce warnings and be skipped safely.

## Phase 4: Implement correct lamp mapping

Lamp mapping is the highest-risk area and needs explicit tests.

### Sublamp association

For every defined sublamp, associate by sublamp index:

- lamp number;
- sublamp color;
- main image;
- mask image;
- source element index.

Ignore the undefined MFME lamp number `-2`.

Create one Oasis lamp element per defined sublamp number.

When a lamp does not expose a usable table but has another trustworthy single-lamp number, preserve the existing fallback behavior where appropriate.

### Text-only lamps

A lamp must not require an image to import.

For an image-less text/shape lamp:

- create the lamp element;
- set `DisplayNumber`;
- set `OnColorHex` from the decoded sublamp color;
- set `DisplayText`;
- set text/font properties;
- leave graphic asset paths null;
- do not mark it as graphical.

### Text selection

For lamp display text:

1. prefer non-empty `OffText`;
2. otherwise use the first non-empty on-state text beginning with `On1Text`;
3. preserve a more explicit decoded generic caption/text only when it is genuinely applicable.

Map the matching state font where possible, using the primary lamp font as fallback.

### Graphical lamps

Preserve main and mask image behavior.

A usable main image controls whether the mapped lamp is graphical. A mask by itself must not make a lamp graphical.

Retain the decoded sublamp color even when a bitmap exists.

### Color correctness

Use the actual decoder color representation and preserve alpha/channel order.

Add a test with non-symmetric channel values so ARGB/RGBA/BGRA mistakes are visible.

Do not substitute text color for lamp illumination color.

## Phase 5: Integrate assets directly

Refactor `FmlImportService` so it no longer:

- serializes decoded layout JSON solely for compatibility;
- creates a fabricated MFME Extract manifest;
- parses that manifest through the old reader/parser.

The service should:

1. decode FML;
2. export images to OS temp staging;
3. invoke the direct mapper;
4. copy/post-process required authored assets into project `Assets/`;
5. return the source-neutral import result.

Keep optional decoded JSON output only if it is useful for diagnostics. It must not be part of the production mapping path.

Do not write temporary import data under project `Assets/` or `Generated/`.

## Phase 6: Switch production flow

Change all production FML entry points to use the direct mapper.

Cover:

- main editor menu command;
- progress and logging flow;
- document insertion;
- input definition updates;
- asset browser/hierarchy/inspector refresh;
- automation import-and-save commands;
- error and cancellation behavior.

At this point, no production FML import should call the old extract parser or mapper.

## Phase 7: Remove MFME Extract support

Once direct-path tests pass, remove the obsolete workflow.

Remove or update:

- `Import MFME Extract...` UI/menu bindings;
- extract file dialogs and commands;
- extract-specific service interfaces and implementations;
- file-system extract reader;
- legacy manifest parser;
- legacy extract DTOs;
- legacy-to-Oasis mapper;
- `FmlDecodedLayoutAdapter`;
- fabricated `layout.json` generation;
- obsolete DI registrations;
- obsolete automation arguments/commands;
- tests that only validate removed extract JSON compatibility;
- outdated documentation.

Retain reusable image processing, asset copying and import-application behavior by moving or renaming it before deleting its old container.

## Phase 8: Tests

Add or update tests covering at least the following.

### Direct-path architecture

- importing an FML does not create or parse a legacy extract manifest;
- production services do not depend on legacy extract DTOs or parser;
- decoder output is passed directly to the new mapper.

### Text-only single lamp

Fixture/component contains:

- valid geometry;
- one defined lamp number, for example `42`;
- known non-white sublamp color;
- `OffText = "HOLD"`;
- no images.

Assert:

- one lamp element is imported;
- `DisplayNumber == 42`;
- `DisplayText == "HOLD"`;
- `OnColorHex` matches the decoded color;
- no primary or secondary asset path exists;
- the element is not treated as graphical.

### Multi-sublamp text lamp

Use multiple numbers and distinct colors.

Assert:

- one Oasis element per defined sublamp;
- number/color pairing remains correct by index;
- all share the expected source grouping;
- undefined entries are skipped.

### Graphical lamp regression

Assert:

- number mapping still works;
- main/mask image paths still work;
- decoded color survives;
- graphical state is based on a real main image.

### Text/font fallback

Assert:

- `OffText` wins over `On1Text`;
- `On1Text` is used when off text is empty;
- empty strings are ignored;
- matching font is selected, with primary font fallback.

### Other component regression

Cover at least one case for each currently supported family, preserving existing expected output.

### Editor integration

Assert:

- imported elements are inserted through the undoable generic command;
- project inputs are updated;
- failures do not mutate the document;
- automation still imports an FML and saves output;
- MFME Extract command/menu no longer exists.

## Phase 9: Validation and cleanup

Run:

1. focused FML decoder/import tests;
2. mapper tests;
3. automation tests;
4. view-model/menu tests;
5. the broader Oasis Editor test suite.

Search the full solution for obsolete names and ensure remaining references are intentional:

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

Inspect at least one real graphical FML and one text-heavy FML through the editor or an integration harness.

## Required completion report

Report:

- final direct-import architecture;
- new mapper and contract types;
- files deleted;
- types retained and renamed;
- how lamp number/color/text/font association is implemented;
- asset staging/copy flow;
- tests added or changed;
- exact test commands and results;
- any decoded MFME component fields still unsupported;
- any remaining references to MFME Extract and why they remain.
