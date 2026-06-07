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

This plan should now be read as an architecture record plus forward roadmap. Phases 0 through 8 are complete at MVP level unless a later section explicitly calls out hardening or follow-up work.

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
Face Play View runtime behavior must resolve through:
    MachineObjectReference
        -> MachineRuntimeState
Face Play View must not depend on LinkedPanel2DElementId for runtime behavior.
LinkedPanel2DElementId exists only for provenance, generation, and editor workflows.
```

`LinkedPanel2DElementId` exists only for:

- provenance;
- generation workflows;
- editor workflows.

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

### Phase 3 - Face Edit View MVP - Complete

Completed at MVP level.

Outcome:

- 2D Face Edit View exists;
- Face elements render in document space;
- selection/editing workflows exist;
- Face element models cover the current MVP surface, including lamps, reels, displays, button hit areas, mask/cutout-style geometry, and artwork;
- elements can retain editor/source provenance;
- elements can carry runtime-object references for play/runtime behavior;
- the implementation remains renderer-neutral and does not require Unity.

### Phase 4 - Face Generation MVP - Complete

Completed at MVP level.

Outcome:

- Face documents/elements can be generated from Panel2D regions/selections;
- generated Face elements can include converted runtime-linked elements where source data is available;
- generated elements may retain `LinkedPanel2DElementId` as provenance for editor/generation workflows;
- generated runtime behavior must still resolve through machine-object references and `MachineRuntimeState`.

### Phase 5 - Face Artwork MVP - Complete

Completed at MVP level.

Outcome:

- Face artwork can be imported and persisted;
- Face artwork elements render in Face Edit View and Face Play View;
- artwork participates in the layered Face document model without owning runtime state;
- artwork remains renderer-neutral and suitable for future fidelity/export work.

### Phase 6 - Face Play View MVP - Complete

Completed at MVP level.

Outcome:

- Face Play View exists as a read-only/play surface;
- Face Play View renders Face documents using shared runtime state;
- live runtime visuals are resolved from `MachineRuntimeState` through machine-object references;
- provenance-only Panel2D links are not the runtime behavior contract.

### Phase 7 - Face Input System MVP - Complete

Completed at MVP level.

Outcome:

- `FaceButtonElement` is persisted as a first-class Face element;
- generated Face documents can create button elements from Panel2D-linked input definitions where source data is available;
- Face button hit testing resolves through `MachineInputReference`/`MachineObjectReference.Input`;
- Face Play View pointer down/up uses the shared play input dispatch path and MAME command service;
- Face runtime input behavior does not depend on `LinkedPanel2DElementId`, which remains provenance/editor metadata.

### Phase 8 - Keyboard Input Routing - Complete

Completed at MVP level.

Outcome:

- Play View input routing now has a document-neutral dispatch facade for keyboard input, Panel2D pointer targets, and Face machine-input targets;
- focused Face Play View key down/up events route through the shared play input router;
- shortcut matching continues to use the existing input map normalization behavior;
- active play inputs are released when Play View focus is lost or the view closes;
- existing Panel2D keyboard behavior is preserved while Face keyboard routing uses the same public dispatch surface;
- tests cover the shared dispatch facade across Panel2D pointer, Face pointer, keyboard down/up, and release-all behavior.

### Phase 9A - Seven Segment Display MVP - Complete

Completed at MVP level.

Outcome:

- `FaceSevenSegmentDisplayElement` is now the dedicated Face element for seven-segment display visuals;
- generated Face documents create seven-segment Face elements from contained Panel2D seven-segment displays;
- seven-segment Face elements serialize/deserialize with machine-object references, provenance-only Panel2D element links, and MVP display color metadata;
- Face Edit View renders seven-segment display elements from `MachineRuntimeState`;
- Face Play View renders seven-segment display elements from `MachineRuntimeState`;
- MAME digit output updates Face seven-segment displays live through `MachineObjectReference.SevenSegmentDisplay`;
- existing lamp and input behavior remain unchanged.

Runtime rule:

```text
FaceSevenSegmentDisplayElement
    -> MachineObjectReference
        -> MachineRuntimeState
```

Do not use `LinkedPanel2DElementId` for Face Play View runtime behavior. Retain it only for provenance, generation workflows, and editor workflows.

### Phase 9B - Alpha Display MVP - Future

Goals:

- add `FaceAlphaDisplayElement` as the dedicated Face element for alpha display visuals;
- generate Face alpha display elements from Panel2D alpha displays;
- update Face alpha display visuals through `MachineRuntimeState`;
- render alpha displays in Face Edit View;
- render alpha displays in Face Play View.

Runtime rule:

```text
FaceAlphaDisplayElement
    -> MachineObjectReference
        -> MachineRuntimeState
```

Do not use `LinkedPanel2DElementId` for Face Play View runtime behavior. Retain it only for provenance, generation workflows, and editor workflows.

### Phase 9C - Reel Display MVP - Future

Goals:

- add `FaceReelDisplayElement` as the dedicated Face element for reel display/window visuals;
- generate Face reel display elements from Panel2D reels;
- update Face reel visuals through `MachineRuntimeState`;
- render reel displays in Face Edit View;
- render reel displays in Face Play View.

Notes:

- reel animation fidelity is not required initially;
- focus on correctness and runtime plumbing before presentation polish.

Runtime rule:

```text
FaceReelDisplayElement
    -> MachineObjectReference
        -> MachineRuntimeState
```

Do not use `LinkedPanel2DElementId` for Face Play View runtime behavior. Retain it only for provenance, generation workflows, and editor workflows.

### Phase 10 - Face Regeneration - Future

Goals:

- regenerate Face content from source Panel2D data;
- preserve manual Face edits where practical;
- use existing provenance metadata:
  - `SourcePanel2DDocumentId`;
  - `SourceRegion`;
  - `LinkedPanel2DElementId`;
  - `MachineObjectReference`.

Regeneration may use `LinkedPanel2DElementId` and other provenance fields to correlate generated content with source Panel2D content, but regenerated Face Play View runtime behavior must still resolve through `MachineObjectReference` and `MachineRuntimeState`.

### Phase 11 - Visual Fidelity Layer - Future

Goals:

- lamp glow;
- artwork blending;
- masks;
- lamp trays;
- presentation improvements.

Important:

- no runtime architecture changes in this phase;
- rendering quality only;
- visual-fidelity improvements must not change the `MachineObjectReference` -> `MachineRuntimeState` runtime contract.

### Phase 12 - 3D Preview Investigation - Future

Goals:

- in-editor 3D preview exploration;
- HelixToolkit investigation;
- lightweight preview renderer investigation.

This phase is investigative. Do not commit to Unity-specific document fields or runtime architecture changes during this phase.

### Phase 13 - Unity Renderer Integration Planning - Future

Goals:

- define Face export/import contract;
- define Unity runtime consumption model;
- define artwork/material/depth requirements;
- define synchronization strategy.

Planned output should clearly separate editor/export responsibilities from Unity renderer responsibilities and preserve machine-object identity as the synchronization contract.

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

- Phase 9A: complete - seven-segment displays serialize, generate, and render from `MachineRuntimeState` through machine-object references;
- Phase 9B: alpha displays generate and render from `MachineRuntimeState` through machine-object references;
- Phase 9C: reel displays generate and render from `MachineRuntimeState` through machine-object references, with correctness prioritized over animation fidelity;
- Phase 10: Face regeneration uses provenance metadata while preserving runtime identity through machine-object references;
- Phase 11: visual-fidelity improvements do not change document/runtime identity contracts;
- Phase 12: 3D preview investigation does not introduce renderer-specific document coupling;
- Phase 13: Unity integration planning clearly separates editor/export responsibilities from renderer responsibilities.

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
