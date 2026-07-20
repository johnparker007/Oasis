# Task 01 — Complete Panel2D-to-Face Semantic Component Conversion

## Objective

Complete Oasis Editor Face generation so a Face created from a Panel2D Face Source Shape contains all supported semantic components within that shape, not only artwork and lamps.

Implement conversion for:

- conventional reels
- 7-segment displays
- alphanumeric/segment-alpha displays
- buttons where applicable

Read first:

- `Docs/FaceSemanticComponents/FACE_SEMANTIC_COMPONENTS_CONTEXT.md`
- `FaceGenerationService`
- `FaceRegenerationService`
- `FaceSourceShapeTransformService`
- Panel2D element models and renderers
- Face element models, storage and renderers
- current Face generation/regeneration tests

## Required investigation

Before implementation, trace and report:

1. the exact Panel2D model type and `PanelElementKind` used for conventional reels
2. the exact Panel2D model types used for 7-segment and alpha displays
3. how machine references and input/display references are currently stored
4. which source properties already have matching Face model properties
5. how lamps transform source bounds into flattened Face bounds
6. how Face editor rendering handles each typed Face element
7. how Face serialization and regeneration currently preserve each type
8. whether any supported source component is currently represented by a generic Panel2D element rather than a dedicated type

Do not invent new duplicate models when an existing typed model already carries the required semantics.

## Shared conversion pipeline

Refactor or add focused helpers so all semantic component conversion uses one consistent pipeline.

The pipeline must:

1. enumerate supported source Panel2D elements
2. include elements whose centre lies inside the selected Face Source Shape
3. transform all four source rectangle corners into flattened Face coordinates
4. calculate an axis-aligned flattened bounds rectangle
5. reject non-finite, empty or unusable transformed bounds with a clear diagnostic
6. construct the correct typed Face element
7. preserve source linkage and semantic properties

Do not implement separate approximate translation formulas for reels and displays.

Use the existing panel-to-Face transformation service. If it currently exposes only Face-to-panel mapping, add the smallest clean inverse/helper needed and test it directly.

## Bounds conversion

For a Panel2D rectangle with corners:

```text
(x, y)
(x + width, y)
(x + width, y + height)
(x, y + height)
```

transform all four corners into flattened Face coordinates.

The generated Face bounds are:

```text
left   = min(transformed X)
top    = min(transformed Y)
right  = max(transformed X)
bottom = max(transformed Y)
```

Use:

```text
X      = left
Y      = top
Width  = right - left
Height = bottom - top
```

Keep sufficient floating-point precision. Do not round during model creation merely for display convenience.

Clamp only where required to keep small floating-point drift inside the generated Face bounds. Do not hide genuinely invalid mappings through aggressive clamping.

## Reel conversion

For each included conventional Panel2D reel, create a `FaceReelDisplayElement`.

Preserve:

- `LinkedPanel2DElementId`
- `LinkedMachineObjectReference`
- source name
- source visibility
- reel-band `AssetPath`
- `Stops`
- `VisibleScale`
- `BandOffset`
- `IsReversed`
- transformed bounds

Use a generated `ObjectId` on first creation according to existing Face conventions. Regeneration must preserve the existing Face object ID through the current typed identity-preservation path.

A reel with a machine reference but an unresolved/missing reel-band asset must still be represented in the Face asset if that matches existing authoring behaviour, but generation/export must surface the missing asset clearly rather than silently omitting it.

## 7-segment conversion

For each included Panel2D 7-segment display, create a `FaceSevenSegmentDisplayElement`.

Preserve every meaningful property already represented by the source and target models, including:

- source linkage
- machine/display reference
- name and visibility
- on colour
- off colour
- decimal-point setting
- transformed bounds

Do not collapse 7-segment and alpha displays into one generic element when typed Face models already exist.

## Alpha display conversion

For each included Panel2D alpha/segment display, create a `FaceAlphaDisplayElement`.

Preserve every meaningful property already represented by the source and target models, including:

- source linkage
- machine/display reference
- segment display type
- name and visibility
- on colour
- off colour
- decimal-point setting
- comma-tail setting
- reversal setting where supported
- transformed bounds

Handle the exact existing source display variants explicitly. Do not guess unsupported variants into the wrong Face type.

## Button conversion

For each included Panel2D button/input component, create a `FaceButtonElement` where the source model represents an actual input region.

Preserve:

- source linkage
- machine reference
- input reference
- name and visibility
- transformed bounds

Do not generate buttons from decorative lamp or artwork components merely because they have click metadata elsewhere.

## Face document construction

The initial generated element set should conceptually become:

```text
artwork
+ lamp windows
+ reels
+ 7-segment displays
+ alpha displays
+ buttons
```

Update `FaceGenerationResult` counts correctly.

Progress messages should describe semantic component conversion rather than only lamps.

Ensure generated layers and Face editor visibility make these elements discoverable and selectable.

## Regeneration

Verify and complete regeneration for every new generated type.

Repeated regeneration must:

- update transformed bounds when the source shape or source element moves
- update source-derived semantic properties
- preserve existing Face runtime identity according to the current policy
- remove generated elements no longer inside the source shape
- add newly included elements
- preserve unrelated manual Face elements
- avoid duplicates

Use the same conversion functions for initial generation and regeneration.

Review `PreserveRuntimeIdentity` carefully. Fix any missing fields rather than replacing typed elements with generic copies.

## Diagnostics

Generation should report concise counts, for example:

```text
Face generated: artwork=1, lamps=12, reels=3, sevenSegment=4, alpha=1, buttons=8
```

When a supported source element cannot be converted, report:

- source object ID
- source name
- component kind/type
- reason conversion failed

Do not silently discard a supported component after it passes the inclusion test.

## Tests

Add focused tests covering real model paths rather than only constructing target Face elements manually.

### Inclusion tests

For each supported component type, verify:

- centre inside shape is included
- centre outside shape is excluded
- boundary behaviour is deterministic

### Coordinate tests

Verify bounds conversion for:

- rectangular source shape
- trapezoidal/perspective source shape
- translated source shape
- non-uniform output aspect ratio
- components near each edge

Use expected transformed corners/bounds with tolerances.

### Semantic preservation tests

Verify reels preserve:

- asset path
- stops
- visible scale
- band offset
- reversal
- machine reference

Verify 7-segment displays preserve:

- type/reference
- colours
- decimal setting
- machine reference

Verify alpha displays preserve:

- type/reference
- colours
- decimal/comma settings
- reversal
- machine reference

Verify buttons preserve:

- input reference
- machine reference

### Generation integration test

Create one Panel2D document containing:

- background/artwork source
- lamp
- reel
- 7-segment display
- alpha display
- button

Place all component centres inside one Face Source Shape.

Generate a Face and verify the exact typed element counts and source linkage.

### Regeneration tests

Verify:

- no duplicate elements after repeated regeneration
- object IDs are preserved
- moved source components update bounds
- removed/outside components disappear
- new components appear
- manual Face elements remain

### Runtime-export bridge test

Using a generated Face containing a reel, verify the current machine runtime export produces a non-zero authoritative reel collection and copies the reel-band asset.

This test is a bridge assertion only. Do not extend this task into final Player reel placement.

## Schema policy

Support only the latest current Face schema.

If serialized Face data changes:

- increment the current schema version
- update writer and reader together
- update fixtures and tests
- reject obsolete versions
- remove superseded format code

Do not add backwards compatibility.

## Manual validation

Using a real imported Panel2D layout:

1. create a Face from a shape containing lamps, reels and displays
2. open the generated Face asset
3. verify typed elements appear in the Face element hierarchy
4. verify their boxes align with the flattened background
5. regenerate the Face and verify no duplicates
6. move or resize a source component, regenerate, and verify its bounds update
7. build the machine runtime package
8. verify the runtime reel manifest count is non-zero

Record any remaining alignment limitations, especially for strongly perspective source shapes.

## Non-goals

Do not implement:

- Unity rendering for 7-segment or alpha displays
- live reel movement
- final reel 3D placement
- skewed semantic meshes
- per-pixel perspective warping of semantic components
- Panel2D scanning from runtime export as a fallback
- legacy Face format support

## Deliverable

Report:

- source model types discovered
- shared conversion helpers added
- inclusion rule
- bounds transformation method
- fields preserved per component type
- generation counts
- regeneration behaviour
- files changed
- tests run
- manual Face editor results
- runtime reel export count after the fix
- remaining limitations
