# TASKS — Sixteen-Segment Display Rendering

This task list is for adding vector-based sixteen-segment display rendering support to the WPF OasisEditor project.

## Goal

Use a preprocessed segment definition file generated from the source SVG instead of RGB-channel bitmap masks. The editor should render each segment as WPF geometry, allowing clean scaling, hit-testing, selection, styling, and eventual export support.

## Proposed Asset Location

Recommended path for the generated JSON definition once added to the repo:

```text
WindowsNetProjects/OasisEditor/Assets/SegmentDisplays/oasis_16_segment_display_definition.json
```

## Phase BA — Add Segment Display Definition Asset

- [ ] Create `Assets/SegmentDisplays/` under `WindowsNetProjects/OasisEditor` if it does not already exist
- [ ] Add `oasis_16_segment_display_definition.json`
- [ ] Treat the JSON file as editor source geometry, not as a runtime bitmap/image
- [ ] Preserve the original SVG separately only if useful for provenance/reference

## Phase BB — Add Segment Definition Models

- [ ] Add model classes for the JSON schema:
  - [ ] Segment display definition
  - [ ] Cell definition
  - [ ] Segment definition
  - [ ] Decimal point definition
  - [ ] Bounds/size records
- [ ] Include fields for:
  - [ ] `schema`
  - [ ] `name`
  - [ ] `units`
  - [ ] `cell.size`
  - [ ] `cell.recommendedPitch`
  - [ ] `cell.segments[].index`
  - [ ] `cell.segments[].id`
  - [ ] `cell.segments[].pathData`
  - [ ] `cell.decimalPoint.pathData`
- [ ] Keep the model independent from any specific WPF control

## Phase BC — Add Segment Definition Loader

- [ ] Add a loader service that reads the JSON asset
- [ ] Deserialize using the project’s existing JSON conventions
- [ ] Validate required fields at load time
- [ ] Validate that exactly 16 segment definitions are present
- [ ] Validate that each segment has non-empty path data
- [ ] Surface clear errors if the asset is malformed

## Phase BD — Convert Path Data to WPF Geometry

- [ ] Convert each SVG/WPF-compatible `pathData` string to `Geometry`
- [ ] Cache parsed geometries after loading
- [ ] Do not parse path strings every render frame
- [ ] Ensure the geometry can be transformed/scaled per rendered cell instance
- [ ] Include decimal point geometry as an optional part

## Phase BE — Add WPF Renderer for Segment Cells

- [ ] Add a renderer/control for one cell of a sixteen-segment display
- [ ] Inputs:
  - [ ] Segment bitmask/state
  - [ ] Lit brush
  - [ ] Unlit brush
  - [ ] Optional decimal point state
  - [ ] Scale/transform
- [ ] Draw each segment using `DrawingContext.DrawGeometry`
- [ ] Avoid `Shape` elements per segment unless needed for editing handles; prefer retained/cached geometries for preview rendering

## Phase BF — Add Multi-Cell Display Rendering

- [ ] Render multiple cells using `cell.recommendedPitch`
- [ ] Support configurable cell count
- [ ] Support per-cell segment state
- [ ] Support optional decimal point per cell if needed by future layout data
- [ ] Confirm three-cell rendering matches the original SVG spacing

## Phase BG — Add Hit Testing and Selection Support

- [ ] Add hit-testing against segment geometries
- [ ] Return cell index and segment index for a hit
- [ ] Support decimal point hit-testing separately
- [ ] Highlight hover/selected segment without mutating source geometry

## Phase BH — Integrate with OasisEditor Element Model

- [ ] Decide whether sixteen-segment display is:
  - [ ] a new panel element kind, or
  - [ ] an editor-only helper/template at first
- [ ] Add persistence fields only after the rendering model is stable
- [ ] Preserve existing save/load behavior for current panel elements
- [ ] Do not couple this work to the current Inspector/Hierarchy performance refactor

## Phase BI — Local Testing for John

John must verify locally:

- [ ] JSON asset loads without errors
- [ ] One cell renders all sixteen segments correctly
- [ ] Three cells render with expected spacing
- [ ] Lit/unlit segment colors update correctly
- [ ] Scaling remains crisp
- [ ] Hit-testing selects the intended segment
- [ ] Existing OasisEditor save/load behavior is unchanged

## Notes

- The source SVG contains three decimal point paths, but they overlap exactly. The generated JSON stores one reusable decimal point normalized to the right side of a single cell.
- Segment path data has been converted to absolute SVG/WPF-compatible path data.
- The JSON should be treated as the canonical editor geometry source for this display style.
