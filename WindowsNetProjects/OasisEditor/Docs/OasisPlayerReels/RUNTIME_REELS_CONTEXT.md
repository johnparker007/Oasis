# Oasis Player Runtime Reels — Context

## Scope

This work spans:

- `WindowsNetProjects/OasisEditor`
- `UnityProjects/OasisPlayer`
- shared runtime build formats in the `johnparker007/Oasis` repository

The next runtime-rendered component after Face lamps is the conventional mechanical reel.

## Project maturity and schema policy

Oasis Editor and Oasis Player are an extremely early personal project. There are no external users, released machine packages, stable public file formats, or deployed Player versions that require backwards compatibility.

For this work:

- treat the current repository state as the only supported version
- update Editor export and Player loading together
- increment a schema version only when the current format genuinely changes
- make the current loader accept only the current schema/version
- update fixtures and tests to the current format
- delete superseded schema models, loaders, migration code, fallbacks and compatibility tests

Do not add:

- legacy DTOs
- old-version parsing
- migration layers
- compatibility branches
- dual read/write formats
- fallback defaults intended to load obsolete builds
- tests whose sole purpose is preserving obsolete schemas

A schema version is a strict current-format identifier, not a promise to support earlier versions. Breaking old generated runtime builds is acceptable; regenerate them with the current Editor.

## Existing 2D reel implementation

The Oasis Editor currently renders reels as a vertically scrolling 2D strip image inside a clipped rectangle.

Key implementation:

- `OasisEditor/Rendering/ReelElementRenderer.cs`
- `OasisEditor/MameReelRuntimeAdapter.cs`
- `PanelElementModel` reel fields in `Panel2DDocumentModel.cs`

Current reel data includes:

- `ObjectId`
- machine reel reference
- strip image `AssetPath`
- `Stops`
- `VisibleScale`
- `BandOffset`
- `IsReversed`
- panel-space bounds

The runtime position convention is based on 96 positions per revolution.

The Editor currently:

1. receives a raw reel position from the emulation backend
2. wraps it to `0..95`
3. applies platform reversal XOR per-reel reversal
4. applies platform and authored band offsets
5. stores the resulting effective position
6. scrolls a vertically arranged reel strip image through a clipped 2D window

The existing 2D preview should remain unchanged during the first Unity runtime reel implementation.

## Proposed Unity rendering model

Use a generated hollow/open cylinder mesh, following the minimal approach proven in the previous 3D fruit-machine project.

The reel-band image is a long, narrow vertical texture containing the symbols in sequence. Map it around the outside curved surface of the cylinder, then rotate the reel around its axle to display the requested position.

The generated mesh should initially contain only the curved outer surface:

- no end caps
- no hub
- no axle
- no physical symbol geometry
- no separate mesh asset authored in Blender

Generate it deterministically through a shared mesh factory.

## Axis and UV convention

Choose one explicit local convention and centralize it.

Recommended:

- cylinder axle along local X
- reel width along local X
- rotation around local X
- circumference mapped to texture V
- reel width mapped to texture U

Verify the actual orientation against imported cabinet and target transforms. Place the UV seam consistently, preferably on the hidden rear side. Map the full reel-band texture exactly once around the circumference.

## Runtime state semantics

Do not redefine reel position semantics in Unity.

The Player should consume one canonical effective reel position based on:

- 96 positions per revolution
- platform reversal
- per-reel `IsReversed`
- platform offset
- per-reel `BandOffset`

Avoid independently reimplementing these rules in several Player call sites.

```text
normalized = wrappedPosition / 96
```

Derive mesh rotation through one centralized runtime-to-Unity conversion helper, including any required baseline and sign correction found during visual testing.

Do not modify exported authored values to compensate for Unity conventions.

## Runtime build data

The current Face runtime manifest contains reel element bounds and machine references, but not enough information to render a 3D reel.

The current runtime format should be changed directly to include the required reel data. Update the Editor exporter, Player loader, fixtures and tests in the same change. Do not preserve the previous format.

Required data includes:

- reel object ID
- machine reel reference
- reel-band texture path
- stop count
- authored band offset
- reversed flag or canonical direction semantic
- placement/target information
- physical reel width
- physical reel radius or diameter

Prefer cabinet-authored reel targets or named mounting transforms. Do not use 2D panel pixel bounds as the permanent world-space placement model.

## Material and lighting

Use a runtime-owned URP-compatible reel material with:

- reel-band texture as base colour
- ambient, main and additional lighting
- no lamp-state shader coupling
- no bloom-specific behaviour
- no unnecessary shader variants

The reel-band texture should be sampled as sRGB. Configure wrapping and filtering so the circumference seam does not show a clamp artefact.

## Motion

The first implementation may apply the latest position directly. Keep the state path suitable for later interpolation, but do not add acceleration, inertia, bounce or motor simulation yet.

The initial correctness target is exact symbol alignment with the Editor at stable positions.

## Non-goals

Do not implement:

- disc reels
- flip reels
- reel lamps
- physical detents
- motor sound
- acceleration/deceleration simulation
- blur shaders
- symbol geometry
- hubs or spindles
- Editor 3D reel preview redesign
- emulator backend porting
- backwards-compatible runtime schemas

## Validation artwork

Use a deliberately asymmetric reel strip with:

- numbered symbols
- obvious top/bottom orientation
- a marked seam
- distinct colours per stop
- direction arrows

This is required to detect baseline, reversal, UV and band-offset errors quickly.
