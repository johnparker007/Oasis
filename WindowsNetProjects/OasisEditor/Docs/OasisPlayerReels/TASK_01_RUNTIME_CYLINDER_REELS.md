# Task 01 — Runtime Cylinder Reels

## Objective

Implement the first conventional mechanical reel rendering path in Oasis Player.

Use a generated open cylinder mesh with the long reel-band image mapped once around its outside surface. Display reel movement by rotating the mesh around its axle.

This should replace the 2D scrolling-strip visual only in Oasis Player runtime. The Oasis Editor 2D preview remains unchanged.

Read first:

- `Docs/OasisPlayerReels/RUNTIME_REELS_CONTEXT.md`
- current Face runtime export/loading code
- `OasisEditor/Rendering/ReelElementRenderer.cs`
- `OasisEditor/MameReelRuntimeAdapter.cs`
- `OasisEditor/MachineRuntimeBuildService.cs`
- `OasisPlayer/RuntimeBuild/RuntimeBuildManifests.cs`

## Required investigation

Before implementation, trace and report:

1. how conventional reel elements are represented in Panel2D and Face data
2. how reel strip assets are copied and referenced today
3. how reel machine references are resolved
4. how cabinet targets or mounting transforms can identify each physical reel
5. where runtime reel position updates should enter Oasis Player
6. which existing 96-step reversal and offset calculations can be shared rather than duplicated

Do not begin by inventing a parallel reel data model without checking the current export and target systems.

## Architecture

Implement a focused pipeline with these responsibilities:

### Editor/runtime export

Export the information required to instantiate a conventional reel in Player, including:

- stable reel object ID
- machine reel reference
- packaged reel-band texture
- stop count
- authored reversal semantic
- authored band offset
- target or mounting reference
- physical width and radius/diameter

Increment runtime manifest schema versions where required and update all loader validation and tests consistently.

Keep authored semantic values unchanged. Unity-specific baseline and direction conversion belongs in Player.

### Player manifest/load model

Add strongly named runtime reel manifest models rather than overloading generic Face entries with unrelated fields.

Resolve all paths through the existing contained-path safety rules.

Reject or clearly warn on malformed entries such as:

- missing texture
- invalid machine reference
- non-positive dimensions
- invalid stop count
- missing target

One bad reel should not silently corrupt unrelated runtime components.

### Generated mesh

Create one reusable reel mesh factory.

Generate only the curved outer cylinder surface initially.

Requirements:

- configurable radial segment count
- configurable width and radius
- outward-facing normals
- correct triangle winding for the chosen material culling
- UVs covering the reel-band texture exactly once around the circumference
- width mapped across the other texture axis
- seam positioned consistently
- no end caps
- no duplicated implementation per reel instance

Use a sensible default radial resolution, then document the trade-off. The mesh should appear circular at normal cabinet viewing distances without excessive vertices.

Cache or share meshes where dimensions and segment count are identical.

### Material

Create runtime-owned reel materials.

Use a URP-compatible lit material and the reel-band image as base colour.

Do not reuse the Face lamp shader.

Configure texture wrapping/filtering so the circumference seam does not show a clamp artefact.

### Placement

Attach each generated reel to the correct cabinet target or mounting transform.

Do not derive permanent world-space placement directly from Panel2D pixel coordinates.

Centralize any Unity axis correction, base rotation and scale conversion.

Do not rotate or modify the cabinet root to make reels fit.

### Position application

Drive the reel from the existing canonical 96-position-per-revolution semantics.

Use one helper to convert effective reel position to Unity local rotation.

Conceptually:

```csharp
var wrapped = PositiveModulo(effectivePosition, 96f);
var normalized = wrapped / 96f;
var angle = baselineDegrees + directionSign * normalized * 360f;
```

Determine `baselineDegrees` and `directionSign` through explicit comparison with the Editor preview and document why they are required.

Do not compensate by changing exported `BandOffset` or reversal values.

Apply reel position changes without recreating meshes or materials.

## First milestone

The first milestone is a static/runtime-debug path that can:

1. load a machine containing conventional reels
2. instantiate one cylinder per reel target
3. apply the correct reel-band texture
4. set arbitrary test positions from `0..95`
5. visually match the symbol shown by the Editor at the same effective position

After that works, connect the existing runtime state/event source available in Player. Do not block basic visual verification on full emulator porting.

A temporary debug position control is acceptable only if it is clearly isolated and removed once the real runtime state path is connected.

## Tests

Add focused automated tests where practical.

### Mesh tests

Verify:

- expected vertex and triangle counts
- no end-cap triangles
- UV range and continuity
- outward normals
- bounds match configured width and radius
- triangles face outward

### Position conversion tests

Verify:

- `0`, `24`, `48`, `72`, `95` positions
- wrapping for `96`, negative values and values above one revolution
- baseline offset
- direction sign
- authored reversal
- band offset

### Manifest tests

Verify:

- round-trip serialization
- schema validation
- contained texture path resolution
- rejection of invalid dimensions/stops
- compatibility handling is explicit rather than accidental

## Manual validation

Use an asymmetric diagnostic reel strip with every stop labelled.

Compare Editor and Player at minimum at:

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

- first and last symbols join correctly at the seam
- forward movement matches Editor direction
- reversed reels match Editor
- band offsets match Editor
- multiple reels can use different textures and dimensions
- no visible rear seam from normal camera positions
- lighting behaves correctly in bright and dark scenes
- Face rendering and lamp rendering are unchanged

## Constraints

Keep:

- current Editor 2D reel preview
- 96-position runtime semantics
- current machine reel references
- current Face orientation and lamp pipeline
- runtime-owned Unity materials
- generated/runtime-loaded cabinet architecture

Do not:

- create a Blender-authored reel mesh requirement
- modify the GLB cabinet mesh
- rotate the cabinet root
- encode Unity correction into exported semantic values
- couple reels to the Face lamp shader
- implement disc or flip reels in this task
- add motor physics or animation smoothing before static alignment is correct
- duplicate reversal/offset logic across Editor and Player without a clear reason

## Cleanup

Remove temporary debug controls and dead code once the production state path is connected.

Do not leave two competing reel-rendering implementations in Player.

## Deliverable

Implement the complete initial conventional runtime reel slice and report:

- current data flow discovered
- manifest/schema changes
- mesh convention and axis
- UV convention and seam location
- position conversion formula
- baseline and direction mapping
- files changed
- tests added and run
- manual comparison results
- remaining limitations for the next reel task
