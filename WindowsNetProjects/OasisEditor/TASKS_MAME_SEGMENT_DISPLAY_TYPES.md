# TASKS — MAME-compatible 14/16 Segment Display Types

## Priority

Next priority task for OasisEditor.

## Goal

Extend the WPF OasisEditor segment display support so it can render the same four segment display component types supported by MAME layouts:

- `led14seg`
- `led14segsc` — 14 segment plus semicolon/decimal punctuation
- `led16seg`
- `led16segsc` — 16 segment plus semicolon/decimal punctuation

The immediate target is to stop treating the existing OasisEditor 16-segment JSON as the only extended segment-display format and introduce a reusable definition/rendering path that can support all four MAME-compatible variants.

## Context already found

### MAME source of truth

MAME defines/registers these layout element component names in:

```text
mamedev/mame/src/emu/rendlay.cpp
```

Look for:

```cpp
layout_element::s_make_component
```

The registered names include:

```cpp
"led14seg"
"led14segsc"
"led16seg"
"led16segsc"
```

MAME declares the component classes in:

```text
mamedev/mame/src/emu/rendlay.h
```

Look for these class declarations:

```cpp
class led14seg_component;
class led16seg_component;
class led14segsc_component;
class led16segsc_component;
```

The render geometry is implemented in `rendlay.cpp` in the corresponding `draw_aligned(...)` methods.

### Important MAME bit mapping

#### `led14seg`

- Bits `0..13`: the 14 main segments
- `maxstate() == 16383`

#### `led14segsc`

- Bits `0..13`: the 14 main segments
- Bit `14`: decimal point
- Bit `15`: comma/semicolon tail
- `maxstate() == 65535`

#### `led16seg`

- Bits `0..15`: the 16 main segments
- `maxstate() == 65535`

#### `led16segsc`

- Bits `0..15`: the 16 main segments
- Bit `16`: decimal point
- Bit `17`: comma/semicolon tail
- `maxstate() == 262143`

For the `sc` variants, the semicolon is represented by two independently addressable punctuation pieces: decimal point and comma tail.

### OasisEditor files already involved

Current JSON asset:

```text
WindowsNetProjects/OasisEditor/Assets/SegmentDisplays/oasis_16_segment_display_definition.json
```

Current model:

```text
WindowsNetProjects/OasisEditor/OasisEditor/SegmentDisplayDefinition.cs
```

Current loader:

```text
WindowsNetProjects/OasisEditor/OasisEditor/SegmentDisplayDefinitionLoader.cs
```

Current 16-segment task/history doc:

```text
WindowsNetProjects/OasisEditor/TASKS_16_SEGMENT_DISPLAY.md
```

The current model supports:

- `cell.segments[]`
- `cell.decimalPoint`

It does **not** yet support a comma/semicolon tail geometry.

The current loader has hard-coded lazy loaders for:

- `oasis_16_segment_display_definition.json`
- `oasis_7_segment_display_definition.json`

and validates only an expected segment count.

## Proposed asset layout

Add or migrate toward these files:

```text
WindowsNetProjects/OasisEditor/Assets/SegmentDisplays/oasis_14_segment_display_definition.json
WindowsNetProjects/OasisEditor/Assets/SegmentDisplays/oasis_14_segment_sc_display_definition.json
WindowsNetProjects/OasisEditor/Assets/SegmentDisplays/oasis_16_segment_display_definition.json
WindowsNetProjects/OasisEditor/Assets/SegmentDisplays/oasis_16_segment_sc_display_definition.json
```

Keep the existing 16-segment asset path stable if existing code depends on it, but add a distinct `16_segment_sc` asset for MAME `led16segsc` compatibility.

## JSON schema changes

Extend the definition schema so `cell` can optionally include:

```json
"decimalPoint": {
  "id": "DP",
  "bitIndex": 16,
  "pathData": "..."
},
"commaTail": {
  "id": "CT",
  "bitIndex": 17,
  "pathData": "..."
}
```

For non-`sc` variants, omit both punctuation fields unless the renderer intentionally supports a separate DP mode.

For `led14segsc`, use:

```json
"decimalPoint.bitIndex": 14
"commaTail.bitIndex": 15
```

For `led16segsc`, use:

```json
"decimalPoint.bitIndex": 16
"commaTail.bitIndex": 17
```

Consider adding a top-level or cell-level metadata field:

```json
"mameComponentType": "led16segsc"
```

so renderer/editor code can map MAME layout component names directly to a definition.

## Implementation tasks

### 1. Update definition models

File:

```text
WindowsNetProjects/OasisEditor/OasisEditor/SegmentDisplayDefinition.cs
```

Tasks:

- Add optional `MameComponentType` field if useful.
- Add optional `CommaTail` field under `SegmentDisplayCellDefinition`.
- Add `BitIndex` to segment/punctuation models, or introduce a shared punctuation model.
- Preserve backward compatibility with the existing JSON, which may not have `bitIndex` values.
- Keep parsed `Geometry` cached and frozen as currently done.

Suggested model direction:

```csharp
internal sealed class SegmentDisplayPunctuationDefinition
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("bitIndex")]
    public int? BitIndex { get; set; }

    [JsonPropertyName("pathData")]
    public string? PathData { get; set; }

    [JsonIgnore]
    public Geometry? Geometry { get; set; }
}
```

Then use it for both `DecimalPoint` and `CommaTail` if practical.

### 2. Generalize the loader

File:

```text
WindowsNetProjects/OasisEditor/OasisEditor/SegmentDisplayDefinitionLoader.cs
```

Tasks:

- Replace hard-coded `TryGetSixteenSegmentDefinition(...)`-style expansion with a small lookup by display type or MAME component type.
- Continue supporting existing callers for 7-seg and 16-seg if needed.
- Validate expected main segment count separately from expected total bit count.
- Parse/freeze segment geometries, decimal point geometry, and comma tail geometry.
- Validate punctuation `bitIndex` for `sc` variants.

Suggested mapping:

```text
led14seg   -> oasis_14_segment_display_definition.json      -> 14 main segments, no required punctuation
led14segsc -> oasis_14_segment_sc_display_definition.json   -> 14 main segments, DP bit 14, comma tail bit 15
led16seg   -> oasis_16_segment_display_definition.json      -> 16 main segments, no required punctuation
led16segsc -> oasis_16_segment_sc_display_definition.json   -> 16 main segments, DP bit 16, comma tail bit 17
```

### 3. Generate or hand-author MAME-style geometry assets

Use MAME `rendlay.cpp` geometry as the source of truth for the four definitions.

The existing Oasis 16-segment JSON was generated from `108870-16-segment-led.svg`; it may not visually match MAME’s renderer exactly. For MAME compatibility, prefer adding new MAME-derived assets rather than silently changing the visual meaning of the existing asset without review.

MAME canonical drawing constants used in `rendlay.cpp`:

```text
bmwidth = 250
bmheight = 400
segwidth = 40
skewwidth = 40
```

`sc` variants allocate extra vertical space:

```text
bitmap height = bmheight + segwidth
```

MAME applies skew before drawing punctuation:

```cpp
apply_skew(tempbitmap, 40);
```

For vector JSON, either:

1. Approximate each MAME segment as WPF/SVG path data using the same line segment coordinates and cap semantics, or
2. Add a small generator script/tool that outputs the JSON from MAME-like constants.

Prefer option 2 if this will be maintained long term.

### 4. Renderer changes

Find the OasisEditor renderer that currently consumes `SegmentDisplayDefinition` and `SegmentDisplayDefinitionLoader`.

Tasks:

- Render `segments[]` by bit index.
- Render optional `decimalPoint` using its own `bitIndex`.
- Render optional `commaTail` using its own `bitIndex`.
- Ensure `led14segsc` and `led16segsc` can light decimal point and comma tail independently.
- Keep unlit/lit brush behavior consistent with current segment rendering.
- Keep hit-testing aware of comma tail if segment hit-testing exists.

### 5. MAME layout import/export integration

Search for MAME layout parsing/export code and component-name handling.

Tasks:

- Map XML component names `led14seg`, `led14segsc`, `led16seg`, `led16segsc` to the correct display definition.
- Ensure state masks are not truncated to 16 bits for `led16segsc`; it needs up to bit 17.
- Ensure any UI that labels display type exposes all four options.

### 6. Tests/manual checks

Minimum checks:

- Existing 7-seg and existing 16-seg rendering still load.
- `led14seg` renders 14 main segments.
- `led14segsc` renders 14 main segments plus DP and comma tail.
- `led16seg` renders 16 main segments.
- `led16segsc` renders 16 main segments plus DP and comma tail.
- All-on states render correctly:
  - `led14seg`: `0x3fff`
  - `led14segsc`: `0xffff`
  - `led16seg`: `0xffff`
  - `led16segsc`: `0x3ffff`
- Existing save/load behavior is unchanged.

## Notes for Codex

Do not remove the existing SVG-derived 16-segment JSON unless there is a clear reason. Prefer additive support first.

When implementing `sc` support, decimal point and comma tail should be separate geometries with separate bit indices, not a single combined semicolon path. This matches MAME’s bit/state behavior.

If exact vector geometry conversion from MAME is too large for the first pass, implement the model/loader/renderer plumbing first, then add placeholder JSON assets with clearly named TODO provenance. However, do not wire placeholders into production defaults without making the TODO obvious.
