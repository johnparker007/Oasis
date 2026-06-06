# Face System Architecture Plan

This document defines the next major Oasis Editor workstream: introducing a first-class Face document system for modelling the physical/front presentation of a fruit machine.

The Face system should build on the current Panel2D, runtime state, MAME input, Skia rendering, and Play View architecture rather than replacing them.

## Summary

Current Panel2D work gives Oasis:

- imported MFME layout data;
- lamps;
- reels;
- alpha/segment displays;
- input definitions;
- MAME runtime integration;
- 2D Edit View;
- 2D Play View;
- shared Skia renderer;
- runtime-state driven visuals.

The Face system should represent the physical/presentation side of the fruit machine:

- glass artwork;
- lamp mask;
- lamp tray;
- physical button locations;
- reel windows;
- display windows;
- depths/materials;
- future Unity renderer data.

Recommended user-facing name:

```text
Face
```

Recommended internal direction:

```text
Face Document
    contains layers/assemblies
```

`Assembly` can remain an internal concept, but the primary user-facing document should be `Face`, `Machine Face`, or similar.

## Core Design Rule

Do not duplicate runtime objects.

A Face document should reference machine/runtime objects defined elsewhere, rather than copying them.

Preferred:

```text
Machine Runtime Object
    LampId = Lamp17

Panel2D Element
    references Lamp17

Face Lamp Window
    references Lamp17
```

Avoid:

```text
Panel2D contains Lamp17
Face contains copied Lamp17
```

Duplicating runtime objects will create synchronization issues.

## Relationship To Panel2D

Panel2D should continue to be the logical/electrical/import authoring document.

Panel2D is for:

- MFME import;
- lamp/input/display mapping;
- 2D editing;
- emulation debugging;
- logical runtime state visualization.

Face is for:

- physical/presentation modelling;
- glass artwork;
- mask/tray/reel/display/button placement;
- material/depth data;
- future Unity rendering.

Recommended project structure:

```text
Project
    Panel2D documents
    Face documents
    Input Map
    Runtime object definitions/references
    Assets
```

## Runtime State Direction

Before deep Face implementation, Codex should verify that runtime state is sufficiently renderer-independent.

Desired architecture:

```text
MAME Runtime
    -> Machine Runtime State
        -> Panel2D Edit View
        -> Panel2D Play View
        -> Face Edit View
        -> Face Play View
```

Runtime state should not be owned by Panel2D rendering.

Examples of renderer-independent state:

```text
Lamp 17 = On
Reel 2 = Position 83
Alpha Display A = cell masks / text
Seven Segment Display B = masks
Button 5 = Down
```

If the current `PanelRuntimeState` is too Panel2D-specific, Codex should introduce a light abstraction/rename/refactor path toward:

```text
MachineRuntimeState
```

or equivalent.

Do not perform a giant runtime rewrite unless necessary. Make the smallest clean change that allows Face views to consume the same runtime state as Panel2D views.

## Runtime Object References

Codex should inspect existing IDs used by:

- lamps;
- reels;
- alpha displays;
- seven-segment displays;
- inputs/buttons;
- Panel2D elements;
- imported MFME components.

If IDs are already stable enough, use them.

If not, introduce a small reference layer.

Suggested types:

```text
MachineObjectReference
MachineLampReference
MachineReelReference
MachineDisplayReference
MachineInputReference
```

A Face document should store references to these logical/runtime objects.

## Face Document Model

Initial Face document can be simple.

Suggested high-level model:

```text
FaceDocument
    Id
    Name
    SourcePanel2DDocumentId optional
    Layers[]
    Elements[]
```

Suggested layer concepts:

```text
GlassArtworkLayer
LampMaskLayer
LampTrayLayer
ReelWindowLayer
DisplayWindowLayer
ButtonLayer
```

Suggested element concepts:

```text
FaceArtworkElement
FaceLampWindowElement
FaceReelWindowElement
FaceDisplayWindowElement
FaceButtonElement
FaceMaskCutoutElement
```

Initial fields:

```text
ObjectId
Name
Kind
X
Y
Width
Height
ZDepth optional
MaterialId optional
LinkedMachineObjectId optional
LinkedPanel2DElementId optional
IsVisible
IsLocked
```

Keep the initial schema small and evolvable.

## Face File Extension

Suggested extension:

```text
.face
```

Alternative if collision/ambiguity is a concern:

```text
.oasisface
```

Prefer `.face` only if existing project/document naming conventions support short extensions cleanly.

Codex should inspect current document storage conventions before choosing.

## Face Edit View Direction

Do not start with a full 3D editor.

Start with a 2D layered Face Edit View.

Purpose:

- define areas/cutouts/windows;
- link them to lamps/reels/displays/buttons;
- edit 2D positions/bounds;
- preview layered structure;
- eventually feed 3D preview/export.

Initial Face Edit View can use existing Skia/document-space interaction patterns from Panel2D Edit View.

It should support:

- pan/zoom;
- selection;
- move/resize;
- layers visible/locked;
- inspector editing;
- document save/load.

Do not build full 3D modelling tools initially.

## Face Play View Direction

Face should mirror the current Panel2D Edit/Play split:

```text
Panel2D Document
    -> Panel2D Edit View
    -> Panel2D Play View

Face Document
    -> Face Edit View
    -> Face Play View
```

Face Play View should be:

- read-only/playable;
- live runtime-state driven;
- clickable only for machine inputs/buttons;
- keyboard focused for mapped input shortcuts;
- non-editing;
- eventually 3D rendered.

Initial Face Play View may be 2D layered or pseudo-3D. It does not need Unity immediately.

Important rule:

```text
Face Play View does not own emulator state.
```

It observes the same runtime state and uses the same input command services as Panel2D Play View.

Mouse/keyboard routing should reuse the existing input system:

```text
Face button hit
    -> InputDefinition
    -> MAME input command
```

Keyboard shortcuts should behave like Panel2D Play View:

```text
Face Play View focused
    -> key down/up
    -> mapped input down/up
```

## Renderer Abstraction

Do not couple Face document format to Unity.

Introduce a renderer abstraction before any Unity-specific work.

Suggested:

```text
IFaceRenderer
Face2DRenderer
FacePreviewRenderer
```

Future implementations can include:

```text
HelixFaceRenderer
UnityFaceRenderer
```

Initial implementation can be simple and 2D.

The Face document should be renderer-neutral.

## Unity Renderer Future

The external Unity renderer should eventually consume Face documents, not Panel2D documents directly.

Expected future path:

```text
Face Document
    -> Unity renderer import/export/runtime
```

This allows Unity to understand:

- glass artwork;
- lamp tray depth;
- masks;
- reel/display/window geometry;
- physical button locations;
- materials.

Do not implement Unity integration in the first Face phase.

## Recommended Phase Sequence

### Phase 0 - Inventory / Readiness Check

Before implementing Face, inspect and document:

- current document type system;
- project/document storage system;
- Panel2D document model;
- current runtime state model;
- input definition/model system;
- Panel2D Edit/Play View architecture;
- Skia renderer abstractions;
- document save/load behavior;
- inspector/document routing.

Deliverable:

```text
FaceSystem.Inventory.md
```

This phase should answer:

- where should Face document type plug in?
- is runtime state renderer-independent enough?
- are runtime object IDs stable enough?
- can Face views reuse Panel2D pan/zoom/selection services?
- what minimal refactor is needed before Face implementation?

### Phase 1 - Runtime State / Reference Cleanup

Only if Phase 0 finds it necessary.

Goals:

- make runtime state consumable by Face and Panel2D;
- avoid Panel2D-specific naming leaking too deeply;
- introduce/confirm stable machine object references;
- preserve existing Panel2D behavior.

Do not do a giant rewrite.

### Phase 2 - Face Document Type Skeleton

Add:

- new document type enum/member;
- Face document model;
- create/open/save/load support;
- basic document tab support;
- placeholder editor surface.

Initial Face document can contain no elements or a minimal element list.

### Phase 3 - Face Element Model

Add initial Face elements:

- artwork rectangle;
- lamp window;
- display window;
- reel window;
- button hit area.

Include references to runtime objects and/or Panel2D elements.

### Phase 4 - Face Edit View 2D Layered MVP

Add a simple 2D Face Edit View:

- Skia render surface;
- pan/zoom;
- selection;
- move/resize;
- layer visibility/lock basics;
- inspector integration where practical.

Do not build full 3D.

### Phase 5 - Face Play View MVP

Add Face Play View:

- read-only;
- same runtime state as Panel2D;
- button/input hit testing;
- keyboard shortcut routing;
- live lamps/displays/reels if model supports them.

Initial rendering may be 2D/pseudo-3D.

### Phase 6 - Face Generation From Panel2D Selection

Add workflow to create Face elements from selected Panel2D areas/elements.

Examples:

- selected Panel2D lamp elements -> Face lamp windows;
- selected reel elements -> Face reel windows;
- selected display elements -> Face display windows;
- selected input-linked lamps/buttons -> Face button hit areas.

This can be simple and incremental.

### Phase 7 - 3D Preview Investigation

Only after Face document/edit/play MVPs are stable.

Explore:

- simple in-editor pseudo-3D;
- HelixToolkit or similar;
- Unity-hosted/external preview;
- export path to Unity renderer project.

## Tests

Add non-WPF tests where practical:

- Face document serialization/deserialization;
- Face element reference validation;
- runtime object reference resolution;
- Face element hit testing;
- Face selection/move/resize math;
- Face Play View input target resolution;
- migration/default behavior for projects without Face documents;
- generation from Panel2D selection.

Avoid heavy visual/pixel-perfect tests.

## Manual Verification Expectations

After Phase 0:

- inventory clearly identifies required refactors;
- no major behavior changes unless explicitly part of cleanup.

After Phase 2:

- Face document can be created/opened/saved;
- existing Panel2D workflows still work.

After Phase 4:

- Face Edit View opens;
- pan/zoom works;
- selection/move/resize basics work.

After Phase 5:

- Face Play View opens;
- MAME runtime state appears;
- clickable buttons send inputs;
- keyboard shortcuts work when focused.

## Out Of Scope Initially

Do not implement initially:

- full Unity renderer integration;
- full 3D modelling editor;
- complex material editor;
- physics;
- lighting authoring;
- complete machine/cabinet modelling;
- HLSL/GPU-specific renderer;
- wholesale project file format redesign.

## Important Guidance For Codex

Work one phase at a time.

Do not start implementation beyond the current phase unless instructed.

For the first Codex pass, only complete Phase 0 inventory/readiness and recommend minimal refactors.

John will test/review after each phase before continuing.
