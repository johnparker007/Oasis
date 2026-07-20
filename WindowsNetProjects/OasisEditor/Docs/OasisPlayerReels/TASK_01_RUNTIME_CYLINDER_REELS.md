# Task 01 — Runtime Cylinder Reels

## Objective

Implement the first conventional mechanical reel rendering path in Oasis Player.

Use a generated open cylinder mesh with the long reel-band image mapped once around its outside surface. Display reel movement by rotating the mesh around its axle.

This replaces the 2D scrolling-strip visual only in Oasis Player runtime. The Oasis Editor 2D preview remains unchanged.

Read first:

- `Docs/OasisPlayerReels/RUNTIME_REELS_CONTEXT.md`
- current Face runtime export/loading code
- `OasisEditor/Rendering/ReelElementRenderer.cs`
- `OasisEditor/MameReelRuntimeAdapter.cs`
- `OasisEditor/MachineRuntimeBuildService.cs`
- `OasisPlayer/RuntimeBuild/RuntimeBuildManifests.cs`

## Mandatory schema policy

This is an early personal project with no released runtime format and no users requiring backwards compatibility.

When reel support changes a runtime manifest:

1. change the current schema/model directly
2. increment the current schema version if the serialized shape changes
3. update Editor export and Player loading together
4. make the loader accept only the new current version
5. update all fixtures and tests to the new format
6. delete obsolete loaders, DTOs, migrations, fallbacks and compatibility tests

Do not implement old-schema support. Do not keep multiple versions alive. Do not add compatibility code “just in case.” Old generated runtime builds may stop loading and should be regenerated with the current Editor.

Before coding, inspect the current branch for compatibility code introduced by an earlier attempt. Remove any such code rather than building on it.

## Required investigation

Trace and report:

1. how conventional reels are represented in Panel2D and Face data
2. how reel strip assets are copied and referenced
3. how reel machine references are resolved
4. how cabinet targets or mounting transforms identify physical reels
5. where runtime reel position updates enter Player
6. which 96-step reversal and offset calculations can be shared
7. what schema compatibility code, if any, must be deleted

Do not invent a parallel reel model before checking the current export and target systems.

## Editor/runtime export

Export the information needed to instantiate conventional reels:

- stable reel object ID
- machine reel reference
- packaged reel-band texture
- stop count
- authored reversal semantic
- authored band offset
- target or mounting reference
- physical width and radius/diameter

Use one current format only. Keep authored semantic values unchanged. Unity-specific baseline and direction conversion belongs in Player.

## Player manifest and loading

Add strongly named runtime reel manifest models rather than overloading unrelated generic entries.

Resolve paths through the existing contained-path safety rules.

Reject or clearly warn on malformed current-format entries such as:

- missing texture
- invalid machine reference
- non-positive dimensions
- invalid stop count
- missing target

Do not attempt to reinterpret malformed data as an older schema.

## Generated mesh

Create one reusable reel mesh factory.

Generate only the curved outer cylinder surface initially:

- configurable radial segment count
- configurable width and radius
- outward-facing normals
- correct triangle winding
- UVs mapping the band exactly once around the circumference
- width mapped across the other texture axis
- consistently positioned seam
- no end caps
- shared/cached meshes for matching dimensions

Use a sensible default radial resolution and document the trade-off.

## Material

Create runtime-owned URP-compatible reel materials.

Use the reel-band image as base colour. Do not reuse the Face lamp shader. Configure wrapping and filtering so the seam does not show a clamp artefact.

## Placement

Attach each reel to its cabinet target or mounting transform.

Do not derive permanent world-space placement from Panel2D pixel coordinates. Centralize Unity axis correction, base rotation and scale conversion. Do not modify the cabinet root or GLB mesh.

## Position application

Drive reels from the canonical 96-position-per-revolution semantics.

Use one helper to convert effective reel position to Unity local rotation:

```csharp
var wrapped = PositiveModulo(effectivePosition, 96f);
var normalized = wrapped / 96f;
var angle = baselineDegrees + directionSign * normalized * 360f;
```

Determine `baselineDegrees` and `directionSign` through explicit Editor/Player comparison. Do not compensate by changing exported `BandOffset` or reversal values.

Apply position changes without recreating meshes or materials.

## First milestone

The first milestone must:

1. load a current-format machine containing conventional reels
2. instantiate one cylinder per reel target
3. apply the correct reel-band texture
4. set arbitrary positions from `0..95`
5. visually match the Editor at the same effective position

After static alignment works, connect the available runtime state/event source. A temporary debug control is acceptable only while isolated and must be removed after connection.

## Tests

### Mesh tests

Verify:

- expected vertex and triangle counts
- no end-cap triangles
- UV range and continuity
- outward normals
- bounds match configured dimensions
- triangles face outward

### Position tests

Verify:

- positions `0`, `24`, `48`, `72`, `95`
- wrapping for `96`, negative and over-range values
- baseline offset
- direction sign
- reversal
- band offset

### Manifest tests

Verify only the current schema:

- current-format round-trip serialization
- exact current schema/version validation
- contained texture path resolution
- rejection of invalid dimensions and stops
- rejection of obsolete schema versions

Do not add tests proving that old versions still load.

## Manual validation

Use an asymmetric diagnostic strip with every stop labelled. Compare Editor and Player at:

```text
0
1
8
24
32
48
64
72
88
95
```

Also verify:

- seam joining
- forward direction
- reversed reels
- band offsets
- different textures and dimensions
- lighting in bright and dark scenes
- no regressions to Faces or lamps

## Constraints

Keep:

- current Editor 2D preview
- 96-position semantics
- current machine reel references
- Face orientation and lamp pipeline
- runtime-owned Unity materials
- generated/runtime-loaded cabinet architecture

Do not:

- retain backwards-compatible schema handling
- create legacy DTOs or migration classes
- support multiple runtime manifest versions
- add fallback parsing for obsolete builds
- create a Blender-authored reel mesh requirement
- modify the GLB cabinet mesh
- rotate the cabinet root
- encode Unity corrections into exported values
- couple reels to the Face lamp shader
- implement disc or flip reels
- add motor physics before static alignment is correct
- duplicate reversal/offset logic without a clear reason

## Cleanup

Remove temporary debug controls after the production state path is connected.

Remove compatibility code introduced by prior attempts. Do not leave two competing reel renderers, schema models, or loading paths in Player.

## Deliverable

Implement the current-format conventional reel slice and report:

- current data flow discovered
- obsolete compatibility code removed
- final schema/version change
- mesh axis and UV convention
- seam location
- position conversion formula
- baseline and direction mapping
- files changed
- tests added and run
- manual comparison results
- remaining limitations
