# MFME Import Plan (Phase K Reconnaissance)

Date: 2026-04-27

This document captures reconnaissance for **Phase K** in `TASKS.md` and defines the first MFME extract import contract for the WPF Oasis editor.

Legacy source references used for this plan:

- Unity importer entry: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MFME/ExtractImporter.cs`
- Unity extract loader: `UnityProjects/LayoutEditor/Assets/OasisPackages/MFMEExtract/Extractor/Extractor.cs`
- MFME shared extract DTOs/helpers under: `WindowsNetProjects/MfmeTools/MfmeTools/Shared/**`

> For this feature track, Unity/MfmeTools projects are **reference-only**. Do not modify them.

---

## 1) First-milestone source extract contract

The WPF importer should initially target an **already-created extract folder** produced by legacy tooling.

### Expected extract layout on disk

- Extract root directory name: `.extract`
- Component image subfolders used by milestone 1:
  - `background/`
  - `lamps/`
  - `reels/`
- Other known folders (deferred for milestone 1):
  - `buttons/`, `bitmaps/`, `misc/`
- Optional ROM ident file in `misc/romident.txt`

### Expected manifest file format

- Layout manifest filename pattern: `<ASName>.json` in the extract root.
- JSON is written with `TypeNameHandling.Auto`, with component polymorphism represented by runtime type metadata.
- Unity loader currently deserializes this JSON with `TypeNameHandling.Auto`.
- Unity loader also performs assembly-name replacement (`"MfmeTools"` -> `"Assembly-CSharp"`) before deserialization; the WPF importer must not rely on this exact replacement, and should instead parse into WPF-owned DTOs.

### Minimum root fields to read

From `Shared/Extract/Layout.cs`:
- `ASName`
- `MameRomIdent` (optional for this milestone)
- `Components[]` (polymorphic component list)

---

## 2) Minimum DTO fields needed for milestone-1 component support

The following fields are sufficient for first-pass WPF import of:
**Background, Lamp, Reel, SevenSegment, Alpha/AlphaNew/MatrixAlpha**.

### Common base data (all supported components)
From `ExtractComponentBase`:
- `Position` (x, y)
- `Size` (width, height)
- `TextBoxText`
- `TextBoxFontName`
- `TextBoxFontStyle`
- `TextBoxFontSize`
- `ZOrder`

### Background
From `ExtractComponentBackground`:
- `BmpImageFilename`
- `Color`

### Lamp
From `ExtractComponentLamp`:
- `LampElements[0]` first element only for milestone 1:
  - `NumberAsText` / parsed `Number`
  - `OnColor`
  - `BmpImageFilename`
- Component-level fields used by current Unity mapping:
  - `OffImageColor`
  - `TextColor`
  - `NoOutline`
  - `ButtonNumberAsString`, `CoinNote`, `Inverted`, `Shortcut1` (input metadata retained if simple)
  - `TextBox*` font/text fields (from base)

### Reel
From `ExtractComponentReel`:
- `Number` (with Unity quirk `+1` during mapping)
- `Stops`
- `Reversed`
- `BandBmpImageFilename`
- `HasOverlay` and `OverlayBmpImageFilename` (defer full visual compositing)

### SevenSegment
From `ExtractComponentSevenSegment`:
- `Number`
- `SegmentOnColor`
- (optional metadata to retain later: `SegmentOffColor`, `SegmentBackgroundColor`)

### Alpha / AlphaNew / MatrixAlpha
From `ExtractComponentAlpha`:
- `Number`
- `Reversed`
- `Color`
- `BmpImageFilename` (if present)

From `ExtractComponentAlphaNew`:
- `Number`
- `Reversed`
- `OnColor`

From `ExtractComponentMatrixAlpha`:
- `Number`
- `OnColor`, `OffColor`, `BackgroundColor`

For milestone 1, normalize all three source types into one WPF Alpha import shape.

---

## 3) Unity importer mapping rules to preserve initially

These behaviors in `ExtractImporter.cs` should be matched for parity in milestone 1 where practical:

1. **Background**
   - Name fixed to `Background`
   - Position `(0,0)`
   - Size from extract component size
   - Color from extract background color
   - Optional background image loaded from `background/<BmpImageFilename>`

2. **Lamp**
   - Uses component position/size directly
   - Uses first lamp element (`LampElements[0]`) only
   - Number set from first lamp element number
   - Name currently fixed `Lamp`
   - On/Off/Text colors imported
   - Text/font fields imported from textbox fields
   - Image path uses `lamps/<LampElements[0].BmpImageFilename>` when present

3. **Reel**
   - Uses component position/size directly
   - **Number mapping quirk:** `componentReel.Number = extract.Number + 1`
   - Name format: `Reel <number>`
   - Band image from `reels/<BandBmpImageFilename>`
   - Stops and reversed imported
   - Visible symbol scale in Unity derived from:
     - `symbolHeight = (bandHeight / Stops)`
     - `visibleRows = reelHeight / symbolHeight`

4. **SevenSegment**
   - Position/size from extract
   - Number copied directly
   - Segment/on color imported
   - Name format: `7 Segment <number>`

5. **Alpha / AlphaNew / MatrixAlpha**
   - All map to same runtime component (`ComponentAlpha`)
   - Name fixed `Alpha`
   - `Reversed` set where source provides it
   - Color from source (`Color` or `OnColor`)
   - **Unity quirk:** MatrixAlpha currently imported as Alpha

---

## 4) Known quirks to preserve for first pass

- Reel number offset: **`MFME Reel Number + 1`**.
- MatrixAlpha treated as Alpha.
- Lamp import uses only lamp element index 0 (not all 12 elements).
- Component-type dispatch in legacy importer is strict runtime-type matching and unsupported types are logged and skipped.

---

## 5) Deferred behavior (explicitly out of milestone scope)

These behaviors exist in legacy code but should be deferred in WPF milestone 1:

- Runtime/editor-only component types outside first supported set.
- Complex lamp input mapping beyond straightforward metadata capture (coin/note, effect, platform-specific input port derivation).
- Temporary reel overlay compositing into background imagery.
- Final runtime-accurate lamp/reel/segment rendering fidelity.
- Any dependency on launching/automating MFME executable.

---

## 6) Asset-copy conventions for WPF importer

For WPF project safety and portability, imported images should be copied under the active Oasis project rather than referenced in-place.

Proposed destination convention:

- `Assets/MfmeImport/<layout-or-extract-name>/Background/`
- `Assets/MfmeImport/<layout-or-extract-name>/Lamps/`
- `Assets/MfmeImport/<layout-or-extract-name>/Reels/`

Rules:
- Store project-relative asset paths in imported Panel2D elements.
- Sanitize folder and file names.
- Prevent path traversal.
- Handle collisions deterministically.
- Missing source image should produce a warning but still import an editable placeholder element where possible.

---

## 7) Initial parser/importer boundary guidance

- Keep extract parsing and normalization in UI-agnostic classes under a focused `Features/MfmeImport` area.
- Parse legacy manifest into **WPF-owned minimal DTOs**, not Unity runtime components.
- Unsupported component types should be skipped with structured warnings (not hard failure).
- Any document mutation must occur via document-scoped undoable commands in later phases.
