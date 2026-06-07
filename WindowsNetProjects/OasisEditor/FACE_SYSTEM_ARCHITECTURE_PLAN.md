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

## Current Implementation State

The Face system is no longer only a pre-implementation proposal. The project now has implemented foundations for:

- `MachineRuntimeState` as the shared runtime-state container used by Panel2D and Face rendering/play workflows;
- machine-object reference types and resolution services;
- persisted Face documents and Face document storage;
- Face document creation/open/save support;
- Face element models, including lamp, reel, display, button, mask/cutout, and artwork elements;
- a 2D Face Edit View with document-space rendering and editing workflows;
- Face generation from Panel2D regions/selections;
- Face artwork import/rendering support;
- a 2D Face Play View path that renders Face documents against runtime state.

This plan should now be read as an architecture record plus forward roadmap. Phases 0 through 6 are complete at MVP level unless a later section explicitly calls out hardening or follow-up work.

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

Runtime state is now represented by `MachineRuntimeState` and should remain renderer-independent.

Current architecture:

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

Do not move emulator state ownership into Panel2D, Face Edit View, Face Play View, or any renderer. Views and renderers should consume `MachineRuntimeState` snapshots/values through explicit runtime-object references.

Future runtime-state changes should remain small and behavior-preserving unless a separate implementation plan explicitly approves a larger rewrite.

## Runtime Object References

The project now has a small machine-object reference layer for runtime-facing identities.

Reference coverage should include:

- lamps;
- reels;
- alpha displays;
- seven-segment displays;
- inputs/buttons;
- imported MFME components.

Panel2D element IDs may be stored as editor/source provenance, but they are not the runtime identity contract for Face Play View.

Reference types include:

```text
MachineObjectReference
MachineLampReference
MachineReelReference
MachineDisplayReference
MachineInputReference
```

A Face document should store references to these logical/runtime objects.

## Face Document Model

Face documents should remain small, versionable, and evolvable.

High-level model:

```text
FaceDocument
    Id
    Name
    SourcePanel2DDocumentId optional
    Layers[]
    Elements[]
```

Layer concepts:

```text
GlassArtworkLayer
LampMaskLayer
LampTrayLayer
ReelWindowLayer
DisplayWindowLayer
ButtonLayer
```

Element concepts:

```text
FaceArtworkElement
FaceLampWindowElement
FaceReelWindowElement
FaceDisplayWindowElement
FaceButtonElement
FaceMaskCutoutElement
```

Core fields:

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

`LinkedMachineObjectId` / typed machine-object references are the runtime identity path. `LinkedPanel2DElementId` is retained only as a provenance/source-authoring field for import, generation, and editor workflows.

Keep the schema small and evolvable.

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

Important rules:

```text
Face Play View does not own emulator state.
Face Play View runtime behavior must resolve through MachineObjectReference and MachineRuntimeState.
Face Play View must not depend on LinkedPanel2DElementId for runtime behavior.
LinkedPanel2DElementId exists only for provenance, generation, and editor workflows.
```

Face Play View observes the same runtime state and uses the same input command services as Panel2D Play View.

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

### Phase 0 - Inventory / Readiness Check - Complete

Completed by the Face system inventory/readiness document.

Outcome:

- documented the current document type system;
- documented project/document storage behavior;
- documented Panel2D model and rendering architecture;
- documented runtime state and input-routing readiness;
- identified the minimal refactor path toward shared runtime state and machine-object references.

Deliverable:

```text
FaceSystem.Inventory.md
```

### Phase 1 - Runtime State / Reference Cleanup - Complete

Completed at MVP level.

Outcome:

- `MachineRuntimeState` is the shared runtime-state container;
- runtime state is consumable outside Panel2D-specific rendering;
- machine-object references and resolver services exist;
- Panel2D behavior remains supported.

Continuing rule:

- runtime behavior should resolve through machine-object references and `MachineRuntimeState`, not through view/editor provenance fields.

### Phase 2 - Face Document Type Skeleton - Complete

Completed at MVP level.

Outcome:

- Face document model exists;
- Face document storage/serialization exists;
- Face documents can be created/opened/saved through the workspace/document flow;
- Face document tabs/surfaces are integrated with the editor shell.

### Phase 3 - Face Element Model - Complete

Completed at MVP level.

Outcome:

- Face elements exist for artwork, lamp windows, reel windows, display windows, button hit areas, and mask/cutout-style geometry;
- elements can retain editor/source provenance;
- elements can carry runtime-object references for play/runtime behavior.

### Phase 4 - Face Edit View 2D Layered MVP - Complete

Completed at MVP level.

Outcome:

- 2D Face Edit View exists;
- Face elements render in document space;
- selection/editing workflows exist;
- Face artwork can be shown in the editor;
- the implementation remains renderer-neutral and does not require Unity.

### Phase 5 - Face Play View MVP - Complete

Completed at MVP level.

Outcome:

- Face Play View exists as a read-only/play surface;
- Face Play View renders Face documents using shared runtime state;
- live runtime visuals are resolved from `MachineRuntimeState` through machine-object references;
- provenance-only Panel2D links are not the runtime behavior contract.

Follow-up input work is now tracked in Phase 7 and Phase 8.

### Phase 6 - Face Generation From Panel2D Selection - Complete

Completed at MVP level.

Outcome:

- Face documents/elements can be generated from Panel2D regions/selections;
- generated Face elements can include artwork and converted lamp windows;
- generated elements may retain `LinkedPanel2DElementId` as provenance for editor/generation workflows;
- generated runtime behavior must still resolve through machine-object references and `MachineRuntimeState`.

### Phase 7 - Face Input System MVP - Future

Goal:

- harden Face button/input behavior as a first-class Face runtime feature.

Planned work:

- confirm Face button hit testing resolves to machine input references;
- ensure pointer down/up/release-all behavior matches Panel2D Play View semantics;
- support disabled/hidden/locked runtime input behavior consistently;
- add non-WPF tests for Face input target resolution and hit testing;
- verify Face input behavior does not depend on `LinkedPanel2DElementId`.

### Phase 8 - Keyboard Input Routing - Future

Goal:

- make keyboard shortcuts and focus behavior consistent across Panel2D Play View and Face Play View.

Planned work:

- route focused Face Play View key down/up events through the shared play input router;
- normalize shortcut matching with the existing input map behavior;
- ensure release-all behavior when focus is lost or play mode closes;
- add tests for keyboard routing where practical.

### Phase 9 - Runtime Displays MVP - Future

Goal:

- complete runtime-driven display rendering for Face Play View.

Planned work:

- render alpha/segment display windows from `MachineRuntimeState`;
- render reel windows from `MachineRuntimeState`;
- validate display/reel reference resolution through machine-object references;
- add serialization/defaulting tests for display and reel Face elements;
- keep display runtime behavior independent of `LinkedPanel2DElementId`.

### Phase 10 - Visual Fidelity Layer - Future

Goal:

- improve 2D/pseudo-3D visual quality without coupling the Face document format to a specific renderer.

Planned work:

- refine artwork compositing, lamp mask/tray visuals, opacity, glow, and depth cues;
- add material/depth metadata only where it is renderer-neutral;
- improve editor/play visual parity;
- avoid pixel-perfect tests unless a stable renderer contract exists.

### Phase 11 - 3D Preview Investigation - Future

Goal:

- investigate practical 3D preview options after the Face 2D/edit/play workflows are stable.

Explore:

- in-editor pseudo-3D beyond the current 2D layered view;
- HelixToolkit or similar WPF-compatible preview technology;
- Unity-hosted or external preview approaches;
- export/import constraints for a future renderer pipeline.

This phase is investigative. Do not commit to Unity-specific document fields during this phase.

### Phase 12 - Unity Renderer Integration Planning - Future

Goal:

- plan, but not yet implement, a Unity renderer integration path.

Planned work:

- define the Face document data Unity would consume;
- identify required asset packaging and runtime-state bridge boundaries;
- define how machine-object references map to renderer-side runtime updates;
- document editor/export responsibilities versus renderer responsibilities;
- produce an implementation plan before any Unity-specific runtime/editor code is added.

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

Completed MVP checks that should continue to be regression-tested:

- Face document can be created/opened/saved;
- existing Panel2D workflows still work;
- Face Edit View opens and renders Face elements/artwork;
- Face Edit View supports basic document-space editing workflows;
- Face Play View opens and renders against `MachineRuntimeState`;
- generated Face documents retain provenance without making provenance the runtime identity path.

Future phase verification should focus on:

- Phase 7: Face pointer/button input resolves through machine-object references;
- Phase 8: keyboard shortcuts work when Face Play View is focused and release cleanly when focus is lost;
- Phase 9: reels and displays render from `MachineRuntimeState` through machine-object references;
- Phase 10: visual-fidelity improvements do not change document/runtime identity contracts;
- Phase 11: 3D preview investigation does not introduce renderer-specific document coupling;
- Phase 12: Unity integration planning clearly separates editor/export responsibilities from renderer responsibilities.

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

Work one future phase at a time.

Do not start implementation beyond the current requested phase unless instructed.

Preserve the completed Face MVP architecture when adding future phases: runtime behavior flows through machine-object references and `MachineRuntimeState`; `LinkedPanel2DElementId` remains provenance/editor/generation data only.

John will test/review after each phase before continuing.
