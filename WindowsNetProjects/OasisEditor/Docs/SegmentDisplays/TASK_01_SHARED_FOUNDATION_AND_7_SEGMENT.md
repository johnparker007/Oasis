# Task 01 — Shared Segmented Display Foundation and 7-Segment Runtime

## Goal

Implement conventional 7-segment displays end to end:

```text
Oasis Editor Face element
    → runtime export
    → machine package
    → Oasis Player load
    → generated segment mesh
    → emissive shader rendering
    → emulator-driven segment updates
```

The implementation must establish reusable segmented-display infrastructure for the later 16-segment task.

## Prerequisites

- Start from current `main`.
- Reel runtime work should already be merged.
- Read `SEGMENTED_DISPLAYS_CONTEXT.md`.
- Inspect and obey repository `AGENTS.md` guidance.

## 1. Trace existing display behavior

Before changing code, inspect:

- `FaceSevenSegmentDisplayElement`;
- Face serialization;
- Face generation from Panel2D;
- Inspector editing;
- Editor preview drawing;
- `FaceRuntimeExportService`;
- Face runtime manifest classes;
- Machine runtime reference flattening;
- Oasis Player runtime loaders;
- emulator display updates and adapters;
- existing segment-mask/character utilities;
- tests and fixtures.

Document in the PR description what already exists, what is placeholder-only, the machine-reference convention, the authoritative digit-count source, and any established segment bit order.

Do not create a competing bit order.

## 2. Create canonical 7-segment geometry

Introduce renderer-independent normalized polygons for:

```text
A, B, C, D, E, F, G, decimal point
```

Requirements:

- stable indices and names;
- deterministic winding;
- normalized coordinates;
- no WPF, Skia, or Unity type dependency in the canonical definition;
- testable bounds;
- no raster textures.

If Editor preview already has suitable geometry mathematics, extract it rather than creating a visually different implementation.

If direct project sharing is impractical, establish one canonical serializable/data source and generate platform-specific representations from it.

## 3. Align Editor preview

Update the Editor 7-segment preview to use the canonical geometry or a generated equivalent.

Preserve authored:

- `OnColorHex`;
- `OffColorHex`;
- `ShowDecimalPoint`;
- Face bounds;
- linked machine reference.

Add tests for canonical segment count, indices, normalized bounds, decimal inclusion/exclusion, and geometry inputs used by the Editor renderer.

## 4. Define runtime manifest data

Inspect the existing seven-segment runtime entry and ensure the latest manifest contains all definition data Player needs, including at minimum:

```text
objectId
machine/display reference
display topology
Face placement bounds
on color
off color
show decimal point
digit count or deterministic equivalent
```

Do not serialize mesh vertices unless repository inspection proves a genuine need. Prefer a standard topology identifier plus current authoring parameters.

If the serialized shape changes, increment Face runtime schema, update Player to accept only the new version, update fixtures/tests, and delete obsolete parsing branches.

## 5. Resolve digit count and mapping

Determine how current Panel2D/Face/emulator data represents digit count. Use the existing authoritative source.

Do not infer digit count from pixel width when explicit semantic data exists.

If a necessary explicit field is missing, add the smallest clean current-format field and update:

- Panel2D conversion;
- Face generation;
- Face persistence;
- Inspector;
- runtime export;
- Player loading;
- tests.

Use the canonical mapping from the context document unless the repository already has an authoritative mapping. Centralize any required conversion.

## 6. Unity mesh factory

Add a shared segmented-display mesh factory.

Initial behavior:

- generate one flat digit mesh containing all enabled segment polygons;
- store segment index in a shader-readable vertex channel;
- produce correct deterministic bounds/triangles;
- optionally include decimal-point geometry;
- bake no color or runtime state into the mesh.

Cache meshes by geometry-affecting options only.

Tests must verify segment count, valid indices, deterministic output, bounds, per-segment vertex data, punctuation options, and that color/mask changes do not create another mesh.

## 7. Unity shader and material

Create one segmented-display shader compatible with the project’s current render pipeline.

Support:

```text
active segment mask
on color
off color
active emission intensity
inactive emission intensity
global brightness where already supported
```

Use one shared material. Apply per-digit values with `MaterialPropertyBlock`.

Active segments must output HDR emission suitable for bloom. Inactive segments use the authored off color.

Do not use textures to identify segments.

## 8. Runtime renderer

Implement shared runtime rendering infrastructure, with 7-segment as the first topology.

Responsibilities:

- create a display root under the resolved Cabinet Face target;
- map Face bounds using existing placement conventions;
- create digit cells in stable left-to-right order;
- fit canonical geometry into each cell;
- instantiate one renderer per digit initially;
- reuse mesh/material resources;
- apply visibility and initial off state;
- destroy instances cleanly during reload.

Keep placement, mesh generation, and state updates separate.

## 9. Emulator state routing

Connect existing emulator segment updates to runtime instances.

Requirements:

- route by existing machine display reference;
- update the correct digit;
- pass masks without rebuilding geometry;
- update through `MaterialPropertyBlock`;
- support decimal point;
- preserve existing ordering/reversal semantics;
- diagnose unknown references deterministically;
- avoid polling where incremental events exist.

Add focused tests around pure routing and mapping logic.

## 10. Machine package integration

Ensure machine runtime output includes or references every required seven-segment display.

Reuse current Face and Machine packaging conventions. Do not create duplicate unrelated display definitions unless the existing architecture requires it.

Verify builds work with source documents closed.

## 11. Validation and diagnostics

Add clear validation for:

- missing machine display reference;
- invalid digit count;
- unsupported topology;
- malformed colors;
- duplicate runtime object IDs;
- conflicting display references where prohibited;
- unsupported state-mask width.

Diagnostics should name Face asset, display name, object ID, machine reference, and reason.

Do not silently derive missing semantic data from rectangle size.

## 12. Tests

### Editor

Cover:

- serialization round-trip;
- generation from Panel2D;
- regeneration preservation;
- Inspector fields;
- manifest values;
- schema version;
- validation failures;
- canonical geometry.

### Unity EditMode

Cover:

- mesh generation;
- segment-index vertex data;
- bounds;
- decimal geometry;
- mesh caching;
- mask interpretation;
- property-block values through a testable abstraction;
- digit ordering/reversal;
- placement calculations.

### End-to-end fixture

Create a fixture with at least one four-digit display, decimal points, distinct on/off colors, a known display reference, and known masks exercising every segment.

Build through the production machine runtime build path and assert the package definition.

## 13. Manual verification

In Oasis Editor:

1. Open/import a machine with a conventional multi-digit display.
2. Confirm preview and Inspector data.
3. Save all assets.
4. Close source Face/Cabinet/Panel tabs.
5. Build runtime output.
6. Inspect Face and Machine runtime manifests.

In Oasis Player:

1. Load the generated machine.
2. Confirm placement and digit order.
3. Exercise every segment and decimal point.
4. Confirm inactive segments remain subtly visible.
5. Confirm active emission and bloom.
6. Confirm state changes do not create meshes/materials.
7. Reload and verify clean teardown/recreation.

## Completion criteria

Task 01 is complete when:

- Editor and Player use equivalent canonical geometry;
- runtime output contains sufficient explicit definition data;
- Player generates meshes without segment textures;
- one shared material is used;
- masks update through property blocks;
- emulator updates illuminate correct segments;
- production-path tests and manual verification pass;
- no compatibility code is added.

## Out of scope

Do not implement in this PR:

- 16-segment geometry;
- alpha character mapping;
- dot matrix;
- VFD glass simulation;
- structured-buffer batching;
- arbitrary custom segment polygons.
