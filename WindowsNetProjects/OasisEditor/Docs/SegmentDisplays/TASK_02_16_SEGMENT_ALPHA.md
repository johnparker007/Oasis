# Task 02 — 16-Segment / Alphanumeric Runtime

## Goal

Extend the shared foundation from Task 01 to support conventional 16-segment/alphanumeric displays without duplicating runtime infrastructure.

```text
FaceAlphaDisplayElement
    → latest runtime manifest
    → shared Player segmented-display loader
    → generated 16-segment mesh
    → direct segment-mask updates
```

## Prerequisites

- Task 01 is merged and manually verified.
- Shared mesh factory, material, shader, renderer lifecycle, placement, and state-routing abstractions already exist.
- Read `SEGMENTED_DISPLAYS_CONTEXT.md`.

## 1. Inspect current alpha-display semantics

Trace:

- `FaceAlphaDisplayElement.SegmentDisplayType`;
- decimal/comma flags;
- `IsReversed`;
- Panel2D/import conversion;
- backend alpha/VFD update representation;
- existing character or segment maps;
- digit count;
- current Editor geometry.

Determine which variants are genuinely present, such as 14-segment, 16-segment, decimal, or comma-tail layouts. Do not implement speculative variants with no current source data.

## 2. Define canonical topology

Add one explicit canonical 16-segment topology with stable names, indices, normalized polygons, punctuation indices, deterministic winding, and bounds.

Document the exact bit mapping in code and tests.

If a backend already has an authoritative mask, adapt geometry indices to it or add one explicit conversion layer. Do not scatter conversion logic.

## 3. Reuse shared infrastructure

Extend the geometry registry/factory so topology selects either:

```text
SevenSegment
SixteenSegment
```

Do not create a second mesh factory, material manager, placement service, lifecycle system, or state router.

Mesh cache keys must include topology and optional punctuation geometry.

## 4. Runtime export

Ensure alpha-display entries export:

```text
objectId
machine/display reference
topology
digit count
Face bounds
on color
off color
show decimal point
show comma tail
is reversed
```

Prefer explicit current-format enum values over loose strings where appropriate.

If serialized shape changes, update schema directly and support only the latest version.

## 5. Rendering

Use the shared shader and material. Each digit receives a segment mask through its property block.

Support all canonical segments, punctuation, colors, emission/brightness, reversal, and clean reload.

Do not render characters with fonts.

## 6. State interpretation

Prefer direct per-digit masks.

If a backend supplies characters:

- centralize character-to-mask mapping;
- make it topology-specific;
- test digits, letters, punctuation, blank, and unknown input;
- retain direct masks when available.

Route separate brightness through the shared brightness path.

## 7. Editor parity

Update Editor preview to use canonical 16-segment geometry or a generated equivalent.

Add parity tests for topology, indices, bounds, decimal point, comma tail, and reversal.

## 8. Validation

Diagnose unknown display type, invalid digit count, unsupported punctuation, oversized masks, missing machine reference, and ambiguous/duplicate references.

Do not fall back to 7-segment rendering for unknown alpha-display types.

## 9. Tests

### Editor

- serialization;
- generation/import mapping;
- regeneration preservation;
- runtime export;
- schema;
- validation;
- geometry.

### Unity

- 16-segment mesh topology;
- segment-index data;
- punctuation geometry;
- cache separation from 7-segment meshes;
- mask application;
- reversal;
- character mapping where required;
- shared material reuse.

### End-to-end

Use a multi-digit alpha fixture exercising letters, digits, diagonals, punctuation, reversal, brightness, and off-state appearance.

## 10. Manual verification

Confirm:

- Editor and Player geometry agree;
- diagonals and split segments are correctly ordered;
- direct masks illuminate intended segments;
- letters and digits are legible;
- punctuation appears only when authored;
- reversal matches source behavior;
- no material is created per digit;
- 7-segment displays remain unchanged.

## Completion criteria

Task 02 is complete when 16-segment displays use the same loading, placement, shader, material, lifecycle, and update infrastructure as 7-segment displays, differing only in topology and optional character mapping.
