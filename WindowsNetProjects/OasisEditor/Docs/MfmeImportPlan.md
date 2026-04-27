# MFME Import Plan (WPF OasisEditor)

## MFME Is Import-Only (Boundary Statement)

MFME extract data is a **legacy source format only** for OasisEditor.

- MFME data is parsed and converted once at import time.
- The resulting document content must be represented as **native Oasis Panel2D elements/components**.
- Normal editor workflows (rename, duplicate, copy/paste, undo/redo, save/load) must operate on Oasis-native data only.
- MFME-specific metadata must not be required by normal editor behavior.
- If provenance is retained, it should be optional, generic, and isolated (for example import source format metadata only).

## Legacy Source Contract (First Milestone)

## Where extract data lives

MFME tools writes extract output under a sibling `.extract` folder next to the source layout path.

Expected folders/files:

- `.extract/<layoutName>.json` (layout + component manifest)
- `.extract/background/` (background bitmap files)
- `.extract/lamps/` (lamp bitmap files)
- `.extract/reels/` (reel band/overlay files)
- `.extract/buttons/` (legacy button images; currently not a milestone target)
- `.extract/bitmaps/` (legacy bitmap assets; currently not a milestone target)
- `.extract/misc/romident.txt` (MAME ROM ident)

## Manifest/layout format

The manifest is a JSON serialization of `Oasis.MfmeTools.Shared.Extract.Layout` containing:

- `ASName`
- `GamFile` (key-value metadata)
- `MameRomIdent`
- `Components` (polymorphic list of legacy extract component records)

Important parsing note:

- The writer uses Newtonsoft `TypeNameHandling.Auto` for serialization.
- The reader should expect type-discriminated component entries and map supported types, while skipping unsupported types with structured warnings.

## Minimum legacy fields required for native conversion

Shared base data for every supported component:

- `Position` (`X`, `Y`)
- `Size` (`X`, `Y`)
- optional base text/font fields where relevant (`TextBoxText`, `TextBoxFontName`, `TextBoxFontStyle`, `TextBoxFontSize`)

Per supported type:

- **Background**
  - `BmpImageFilename`
  - `Color`
- **Lamp**
  - `LampElements[0].NumberAsText`/`Number`
  - `LampElements[0].OnColor`
  - `LampElements[0].BmpImageFilename`
  - `OffImageColor`, `TextColor`
  - text/font fields from base data
  - `NoOutline`
- **Reel**
  - `Number`, `Stops`, `Reversed`
  - `Height` (used for visible-scale derivation)
  - `BandBmpImageFilename`
  - `HasOverlay`, `OverlayBmpImageFilename` (capture for deferred/optional handling)
- **SevenSegment**
  - `Number`
  - `SegmentOnColor`
- **Alpha**
  - `Reversed`
  - position/size from base data
- **AlphaNew**
  - `Reversed`
  - position/size from base data
- **MatrixAlpha**
  - position/size (mapped to native Alpha in milestone 1)

## Conversion Rules (Legacy MFME -> Native Oasis)

These rules mirror the current Unity importer behavior for the supported first-pass component set.

- **Background -> Native Background component**
  - Name: `Background`
  - Position forced to `(0, 0)`
  - Size from MFME background size
  - Color from MFME background color
  - Optional image from `background/<BmpImageFilename>`

- **Lamp -> Native Lamp component**
  - Position/size from MFME lamp
  - Use first lamp element (`LampElements[0]`) for milestone 1 number/color/image mapping
  - Number from first lamp element number
  - On color from first lamp element on color
  - Off color from lamp off image color
  - Text color from lamp text color
  - Text/font fields mapped where native model supports them
  - Outline derived from `NoOutline` (`Outline = !NoOutline`)
  - Name: `Lamp` (or consistently `Lamp <number>` if standardised during implementation)
  - Optional image from `lamps/<BmpImageFilename>`

- **Reel -> Native Reel component**
  - Position/size from MFME reel
  - Reel number mapped as `MFME Number + 1` (legacy parity behavior)
  - Stops and reversed mapped directly
  - Band image from `reels/<BandBmpImageFilename>`
  - Visible-scale first-pass mapping:
    - `visibleSymbols = Height / 50f`
    - `visibleScale = visibleSymbols / Stops`
  - Name: `Reel <number>`

- **SevenSegment -> Native SevenSegment component**
  - Position/size from MFME seven-segment component
  - Display number from MFME number
  - Segment/on color from MFME segment color
  - Name: `7 Segment <number>`

- **Alpha + AlphaNew -> Native Alpha component**
  - Position/size from source component
  - Reversed mapped where available
  - Name: `Alpha`

- **MatrixAlpha -> Native Alpha component (milestone 1 compatibility)**
  - Position/size mapped
  - Reversed default false in legacy implementation
  - Name: `Alpha`

## Unity Importer Quirks to Preserve Initially

- Reel numbering applies `+ 1` during import.
- MatrixAlpha is currently imported as Alpha for compatibility.
- Reel visible scale is a coarse first-pass calculation (`Height`, `Stops`, constant `50`).
- Lamp conversion currently uses only the first of up to 12 lamp elements.

## Deferred Behavior (Explicitly Out of Scope for First WPF Milestone)

- Runtime-only component types and non-target legacy types (e.g., checkbox/button/bandreel and broader MFME set).
- Complex lamp input mapping (coin/note/effect/shortcut routing beyond basic property carry-over).
- Reel overlay compositing into background imagery.
- Final runtime-accurate lamp/reel/segment render fidelity.
- Any dependency on automating MFME executable from the WPF editor.

Unsupported legacy component types should be skipped with warnings, not hard-fail the entire import when partial import is still possible.

## Asset Copy Conventions (WPF Import)

Imported image assets should be copied into the active Oasis project and referenced via project-relative paths.

Recommended destination layout:

- `Assets/MfmeImport/<LayoutOrExtractName>/Background/`
- `Assets/MfmeImport/<LayoutOrExtractName>/Lamps/`
- `Assets/MfmeImport/<LayoutOrExtractName>/Reels/`

Rules:

- Sanitize folder/file names.
- Enforce project-root containment.
- Use deterministic collision handling.
- Missing source files should produce warnings and still import placeholder-capable native components where possible.

## Legacy Projects Are Reference-Only

For this feature track:

- `UnityProjects/LayoutEditor` and `WindowsNetProjects/MfmeTools` are **reference sources** for mapping/parsing behavior.
- Do not modify those projects as part of OasisEditor MFME import milestones unless a future task explicitly requires it.
