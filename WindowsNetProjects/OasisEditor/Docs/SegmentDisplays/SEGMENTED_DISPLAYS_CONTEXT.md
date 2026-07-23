# Oasis Segmented Displays — Context and Architecture

## Scope

This planning set covers conventional segmented displays in Oasis Editor and Oasis Player:

- 7-segment numeric displays
- 16-segment / alphanumeric displays
- decimal points
- comma tails where supported
- emulator-driven per-segment state
- emissive rendering in Unity

The first implementation phase is deliberately limited to 7-segment displays. The shared foundation must support a later 16-segment phase without duplicating loading, placement, materials, update routing, or mesh lifetime management.

## Repository

```text
johnparker007/Oasis
```

Relevant projects:

```text
WindowsNetProjects/OasisEditor
UnityProjects/OasisPlayer
```

## Existing authoring models

The current Face model already distinguishes `FaceSevenSegmentDisplayElement` and `FaceAlphaDisplayElement`.

Seven-segment authoring currently includes:

```text
OnColorHex
OffColorHex
ShowDecimalPoint
```

Alpha-display authoring currently includes:

```text
SegmentDisplayType
OnColorHex
OffColorHex
ShowDecimalPoint
ShowCommaTail
IsReversed
```

Both inherit the normal Face element identity, bounds, visibility, and machine-link fields. Runtime export already emits separate collections for seven-segment and alpha displays.

Evolve the current format directly. There is no backwards-compatibility requirement.

## Architectural decision

Render segmented displays from generated vector geometry and a shared emissive shader.

Use:

```text
shared canonical normalized segment geometry
        ↓
Editor preview and Unity mesh generation
        ↓
one shared segmented-display shader/material
        ↓
per-digit MaterialPropertyBlock state
```

Do not use:

- one texture per segment;
- an Editor-rendered bitmap atlas as the primary segment representation;
- font glyph rendering;
- separate materials for every digit or segment;
- duplicated 7-segment and 16-segment runtime systems.

## Canonical geometry

Define each topology using normalized 2D shapes in a renderer-independent representation.

Conceptually:

```csharp
SegmentDisplayGeometryDefinition
{
    SegmentDisplayTopology Topology;
    IReadOnlyList<SegmentShapeDefinition> Segments;
}

SegmentShapeDefinition
{
    int SegmentIndex;
    string SegmentName;
    IReadOnlyList<NormalizedPoint> Polygon;
}
```

Prefer a normalized local design space:

```text
X: 0..1
Y: 0..1
```

The geometry source must define stable segment names, indices, polygons, punctuation geometry, winding, and bounds. The same numbering must be used by Editor drawing, runtime export, Unity meshes, emulator state routing, and tests.

## Canonical 7-segment mapping

Use one explicit mapping unless the repository already contains an established authoritative mapping:

```text
bit 0 = A
bit 1 = B
bit 2 = C
bit 3 = D
bit 4 = E
bit 5 = F
bit 6 = G
bit 7 = decimal point
```

If an existing backend mapping differs, centralize one conversion layer. Do not scatter mapping logic among renderers.

## Canonical 16-segment mapping

The 16-segment phase must define one explicit named and indexed topology after inspecting existing decoder/backend conventions.

Prefer direct segment masks from emulator output. Character-to-segment decoding is secondary and should only be used when a backend supplies characters rather than masks.

## Definition versus state

Keep display definition separate from runtime state.

Definition data includes:

- stable object ID;
- machine display reference;
- topology/type;
- digit count or deterministic digit-layout data;
- Face bounds;
- on color;
- off color;
- decimal/comma options;
- reversal where applicable.

State data includes:

- active segment mask per digit;
- brightness/intensity where available.

The serialized runtime manifest contains definition data, not continuously changing state.

## Face bounds and sizing

Segment displays are visual components. The authored Face rectangle should define their mounted bounds.

The runtime renderer should:

1. resolve the Cabinet Face target;
2. map the Face rectangle using existing placement conventions;
3. divide the bounds into digit cells;
4. fit normalized geometry into each cell;
5. preserve intended spacing and aspect behavior.

Do not create a Cabinet-level physical display specification in this phase.

## Unity rendering design

### Initial object model

Use one renderer/GameObject per digit initially.

Each digit uses:

- one generated mesh containing all segment polygons;
- one shared segmented-display material;
- `MaterialPropertyBlock` for per-digit values;
- no unique material instance.

Do not introduce structured buffers or whole-display GPU batching until profiling demonstrates a need.

### Mesh data

Store segment index in a shader-readable vertex channel such as UV1/UV2 or vertex color. Choose a representation compatible with the current render pipeline and easy to test.

The shader must identify segments without texture lookups.

### Shader inputs

Support:

```text
active segment mask
on color
off color
active emission intensity
inactive emission intensity
optional global brightness
```

Active segments output HDR emission suitable for bloom. Inactive segments remain subtly visible through the authored off color.

### Geometry depth

Start with flat front-face geometry. Small extrusion or bevel work is a later visual enhancement and must not block the first vertical slice.

## Shared runtime components

Prefer a structure similar to:

```text
RuntimeSegmentDisplayDefinition
RuntimeSegmentDisplayGeometry
RuntimeSegmentDisplayMeshFactory
RuntimeSegmentDisplayRenderer
RuntimeSegmentDisplayStateRouter
RuntimeSegmentDisplayShader
```

Topology-specific code should be limited to geometry, names/indices, and optional character mapping.

Shared code owns loading, placement, lifecycle, materials, property blocks, visibility, brightness, mesh caching, routing, and diagnostics.

## Mesh caching

Cache generated digit meshes by geometry-affecting inputs only, for example:

```text
topology
decimal-point enabled
comma-tail enabled
geometry profile/version
```

Color, brightness, object ID, and active mask must not create new meshes.

## Runtime state routing

Trace the existing emulator/runtime update pipeline before adding new event types.

The intended route is:

```text
emulator backend update
    → machine display reference
    → runtime display instance
    → digit index + segment mask + brightness
    → MaterialPropertyBlock update
```

Reuse incremental events where available. Do not poll or rebuild meshes when state changes.

## Editor geometry reuse

Inspect how Oasis Editor currently draws 7-segment and alpha displays. Extract or centralize the geometry mathematics so Editor preview and Unity use equivalent canonical definitions.

Because Editor and Player are separate projects/frameworks, sharing may use a neutral data definition, generated platform-specific code/data, or strongly tested equivalent definitions. Prefer one canonical source or generated data over manually maintained duplicate polygon lists.

Do not export rasterized segment textures merely to avoid sharing geometry.

## Schema policy

If serialized shape changes:

- increment the current Face runtime schema version;
- increment Machine runtime schema only if its shape changes;
- update Editor writer and Player reader together;
- update fixtures and tests;
- support only the latest schema;
- remove superseded readers and parsing branches;
- do not add migration or fallback infrastructure.

## Scope sequence

### Phase 1

Shared segmented-display foundation plus complete 7-segment export/render/update path.

### Phase 2

16-segment/alphanumeric topology using the Phase 1 foundation.

### Later

- dot matrix;
- richer VFD behavior;
- glass/filter layers;
- manufacturer-specific geometry profiles;
- batching only if profiling requires it.

## Non-goals

This planning set does not include:

- reel work;
- dot-matrix rendering;
- general text rendering;
- arbitrary user-authored segment polygons;
- texture-per-segment systems;
- backwards-compatible schema loaders;
- speculative batching architecture;
- redesigning emulator backend abstractions.
