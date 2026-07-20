# Oasis Player Runtime Reels — Context

## Scope

This work spans:

- `WindowsNetProjects/OasisEditor`
- `UnityProjects/OasisPlayer`
- shared runtime build formats in the `johnparker007/Oasis` repository

The next runtime-rendered component after Face lamps is the conventional mechanical reel.

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

Generate it deterministically at runtime or through a shared mesh factory.

## Axis and UV convention

Choose one explicit local convention and centralize it.

Recommended:

- cylinder axle along local X
- reel width along local X
- rotation around local X
- circumference parameter mapped to texture V
- reel width mapped to texture U

The implementation must verify the actual orientation against the imported cabinet and Face target transforms rather than assuming Unity primitive-cylinder conventions.

The UV seam should be placed on the hidden rear side of the reel where practical.

The full reel-band texture must map exactly once around the circumference.

## Runtime state semantics

Do not redefine reel position semantics in Unity.

The Player should consume one canonical normalized or 96-step effective reel position produced from the same rules as the Editor:

- 96 positions per revolution
- platform reversal
- per-reel `IsReversed`
- platform offset
- per-reel `BandOffset`

Avoid independently reimplementing these rules in several Player call sites.

A normalized runtime value should use:

```text
normalized = wrappedPosition / 96
```

The mesh rotation should then be derived through one centralized Editor/runtime-to-Unity conversion helper, including any required baseline offset and sign reversal discovered during visual testing.

Do not modify exported authored values merely to compensate for Unity axis or winding conventions.

## Runtime build data

The existing Face runtime manifest already contains reel element bounds and machine references, but it does not yet contain enough information to render a 3D reel.

The runtime export will need to resolve and package at least:

- reel object ID
- machine reel reference
- reel-band texture path
- stop count
- visible scale where still meaningful
- authored band offset
- reversed flag or a canonical pre-resolved direction semantic
- placement/target information linking the reel to the cabinet
- physical reel width
- physical reel radius or diameter
- optional visible-window or masking information

Prefer using cabinet-authored reel targets or named mounting transforms where available. Do not position reels from 2D panel pixel bounds directly in world space.

If cabinet reel targets do not yet exist, the first implementation may use a clearly documented temporary placement path, but it must not become the permanent data model.

## Material and lighting

Use a runtime-owned reel material compatible with URP.

Initial requirements:

- base-colour reel-band texture
- normal ambient/main/additional lighting
- no lamp-state shader coupling
- no bloom-specific behaviour
- no shader variants unless demonstrably necessary

The reel-band texture should normally be sampled as an sRGB colour texture.

Use texture wrapping appropriate for the cylinder circumference. The seam must not produce a visible clamp line.

## Motion

The first implementation may apply the latest position directly each frame or event.

Keep the state path compatible with later interpolation, but do not invent acceleration, inertia, bounce or motor simulation during this task.

The initial correctness target is exact symbol alignment with the Editor for stable positions.

## Non-goals

Do not implement in the first reel task:

- disc reels
- flip reels
- reel lamps
- physical reel stops or detents
- motor sound
- acceleration/deceleration simulation
- blur shaders
- symbol-specific geometry
- reel casing, hub or spindle artwork
- Editor 3D reel preview redesign
- emulator backend porting

## Validation artwork

Use a deliberately asymmetric reel strip with:

- numbered symbols
- obvious top/bottom orientation
- a marked texture seam
- distinct colours per stop
- direction arrows

This is required to detect baseline, reversal, UV and band-offset errors quickly.
