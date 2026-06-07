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

The Face system should represent the physical/presentation side of the fruit machine. The long-term physical model is no longer treated as `Artwork + Lamp Windows`; it is:

```text
FaceArtworkLayer
FaceMaskLayer
Runtime Objects
    Lamps
    Buttons
    Displays
    Reels
Future Tray Geometry
```

In this model:

- artwork is the printed artwork layer;
- mask is the single opaque printed layer behind the artwork, aligned to the same face-sized source region;
- runtime objects are the machine-controlled lamps, buttons, displays, and reels;
- tray geometry is a future representation of the physical lamp tray/light containment behind the mask.

Important: the mask layer is one face-sized layer aligned to the artwork. It is not a persisted collection of independent lamp mask images. Per-lamp extraction may be used internally during generation, but persisted Face documents should store one aligned `FaceMaskLayer` plus provenance/extraction metadata.

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
- Face models, including artwork elements, runtime-linked lamp/reel/display/button elements, and an aligned Face mask layer model;
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
    SourceRegion optional
    FaceMaskLayer optional
    Layers[]
    Elements[]
```

Layer concepts:

```text
FaceArtworkLayer
FaceMaskLayer
RuntimeObjectLayers
    Lamps
    Buttons
    Displays
    Reels
FutureTrayGeometry
```

Element concepts:

```text
FaceArtworkElement
FaceLampWindowElement
FaceReelWindowElement
FaceDisplayWindowElement
FaceButtonElement
FaceMaskLayer metadata (single aligned mask, not per-lamp elements)
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
- Face models cover the current MVP surface, including artwork, an aligned Face mask layer, lamps, reels, displays, and button hit areas;
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

### Phase 9B - Alpha Display MVP - Complete

Completed at MVP level.

Outcome:

- `FaceAlphaDisplayElement` is now the dedicated Face element for alpha display visuals;
- generated Face documents create alpha display Face elements from contained Panel2D alpha displays;
- alpha Face elements serialize/deserialize with machine-object references, provenance-only Panel2D element links, display type, color, decimal-point, comma-tail, and reversal metadata;
- Face Edit View renders alpha display elements from `MachineRuntimeState`;
- Face Play View renders alpha display elements from `MachineRuntimeState`;
- MAME VFD/segment output updates Face alpha displays live through `MachineObjectReference.AlphaDisplay`;
- existing lamp, seven-segment, and input behavior remain unchanged.

Runtime rule:

```text
FaceAlphaDisplayElement
    -> MachineObjectReference
        -> MachineRuntimeState
```

Do not use `LinkedPanel2DElementId` for Face Play View runtime behavior. Retain it only for provenance, generation workflows, and editor workflows.

### Phase 9C - Reel Display MVP - Complete

Completed at MVP level.

Outcome:

- `FaceReelDisplayElement` is now the dedicated Face element for reel display/window visuals;
- generated Face documents create reel display Face elements from contained Panel2D reels;
- reel display Face elements serialize/deserialize with machine-object references, provenance-only Panel2D element links, reel strip asset path, stops, visible scale, band offset, and reversal metadata;
- Face Edit View renders reel display elements from `MachineRuntimeState`;
- Face Play View renders reel display elements from `MachineRuntimeState`;
- MAME reel output updates Face reel displays live through `MachineObjectReference.Reel`;
- Face reel displays apply the same MVP platform/stop internal reel offsets used by Panel2D reel rendering so Impact, MPU4, Scorpion4, and other stop-count-specific adjustments stay aligned between Panel2D and Face views;
- existing lamp, input, seven-segment display, and alpha display behavior remain unchanged.

Notes:

- reel animation fidelity remains intentionally out of scope for the MVP;
- runtime plumbing correctness is prioritized over presentation polish.

Runtime rule:

```text
FaceReelDisplayElement
    -> MachineObjectReference
        -> MachineRuntimeState
```

Do not use `LinkedPanel2DElementId` for Face Play View runtime behavior. Retain it only for provenance, generation workflows, and editor workflows.


### Phase 9D - Runtime Display Consolidation - Complete

Completed as a focused cleanup, review, consolidation, and documentation phase.

Outcome:

- Face runtime display resolution now has one shared helper for mapping Face runtime display elements back to `MachineObjectReference` values for visual invalidation;
- reel, seven-segment, and alpha MAME adapters use the shared Face reference-index helper instead of open-coded per-display object-id matching where invalidating Face visuals;
- `FaceRuntimeStateResolver` uses a single typed machine-reference validation path for lamps, reels, seven-segment displays, and alpha displays;
- regression coverage verifies that Face runtime display indexing groups machine-reference-linked elements and ignores provenance-only `LinkedPanel2DElementId` links;
- no new runtime display types, visual-fidelity effects, regeneration behavior, 3D preview, or Unity integration were added.

Runtime rule preserved:

```text
Face runtime display element
    -> LinkedMachineObjectReference
        -> MachineRuntimeState
            -> shared renderer primitive / input dispatch path
```

`LinkedPanel2DElementId` remains provenance/editor/regeneration metadata only. Runtime resolution and invalidation should not fall back to Panel2D element IDs.

### Phase 10 - Face Regeneration MVP - Complete

Completed as a workflow/data-management MVP.

Outcome:

- added a user-facing **Regenerate Face** command for open Face documents that still carry regeneration metadata;
- regeneration locates the original open Panel2D source document through `SourcePanel2DDocumentId` and requires a valid `SourceRegion`;
- regeneration re-runs the existing Panel2D-to-Face generation path rather than adding separate visual-fidelity logic;
- generated Face artwork, lamp windows, buttons, seven-segment displays, alpha displays, and reel displays are rebuilt from the current Panel2D source region;
- generated elements are correlated using regeneration metadata, primarily `LinkedPanel2DElementId` plus Face element kind;
- existing generated Face element object IDs and `LinkedMachineObjectReference` values are preserved when a regenerated match exists, so runtime identity survives regeneration;
- manual Face-only elements without regeneration keys are appended back to the regenerated document so they are not destroyed unnecessarily;
- stale generated elements whose provenance no longer appears in the regenerated source set are removed without introducing advanced conflict-resolution UI;
- Face document saving now preserves document-level `SourcePanel2DDocumentId` and `SourceRegion` metadata.

Regeneration contract:

```text
FaceDocument
    SourcePanel2DDocumentId
    SourceRegion
    Elements[]
        LinkedPanel2DElementId
        LinkedMachineObjectReference
```

Runtime rule preserved:

```text
Face Play View
    -> Face element LinkedMachineObjectReference
        -> MachineRuntimeState
```

`LinkedPanel2DElementId` is used only to correlate generated Face content with Panel2D source content during regeneration. It remains provenance/editor/regeneration metadata and is not used for Face Play View runtime state resolution.

Manual verification steps:

1. Open a project with a Panel2D document containing artwork plus lamps, buttons, seven-segment displays, alpha displays, and reels.
2. Open the source Panel2D document and use **File -> Generate Face From Region...** to create a Face from a valid source region.
3. Add or move a manual Face-only lamp window or other Face element that does not have `LinkedPanel2DElementId` metadata.
4. Change the source Panel2D element geometry/properties inside the same source region.
5. Select the generated Face document and run **File -> Regenerate Face**.
6. Verify generated artwork, lamps, buttons, seven-segment displays, alpha displays, and reels update to match the source Panel2D region.
7. Verify matched regenerated elements keep their existing `LinkedMachineObjectReference` values.
8. Verify the manual Face-only element remains present after regeneration.
9. Run the existing Face Play View and confirm lamp/display/reel/button behavior is unchanged.
10. Re-open the Panel2D document and confirm existing Panel2D edit/play behavior is unchanged.

Next recommended phase:

- Phase 10.5 is complete. Phase 11A should start with Face Mask Layer Extraction MVP, focused on creating one persisted face-sized mask layer aligned to the artwork/source region while preserving the regeneration metadata contract and the `LinkedMachineObjectReference` -> `MachineRuntimeState` runtime contract.


### Phase 10.5 - Face Generation / Regeneration UX - Complete

Completed as a small workflow/discoverability phase before visual-fidelity work.

Outcome:

- Face document provenance is visible in the Inspector when a Face document is selected without a specific Face element selected, including source Panel2D document metadata, source region, generated element count, and last regenerated timestamp;
- generated and regenerated Face documents persist `LastRegeneratedAtUtc` metadata so the user can see when the source region was last replayed;
- the File menu now exposes both **Regenerate Face** and **Open Source Panel2D**, making the regeneration workflow and source-location workflow discoverable from the active Face document;
- **Regenerate Face** remains visible/enabled for Faces with regeneration metadata even when the source Panel2D is not currently open, so execution can report a clear missing-source diagnostic instead of silently hiding the workflow;
- Face validation diagnostics now report missing source Panel2D documents, invalid/missing source regions, missing artwork/reel assets, missing machine references, and mismatched machine-reference kinds through the existing Output log path where appropriate;
- the Face Inspector surfaces a machine-reference warning count for the selected Face document as a lightweight diagnostic summary.

Regeneration/source UX contract:

```text
Face document selected
    -> Inspector shows provenance and diagnostics summary
    -> File > Open Source Panel2D activates the open source tab when available
    -> File > Regenerate Face replays SourcePanel2DDocumentId + SourceRegion
    -> Output log reports missing-source / missing-asset / missing-reference diagnostics
```

Important boundaries preserved:

- no visual-fidelity changes were added;
- no 3D preview behavior was added;
- no Unity integration behavior was added;
- `LinkedMachineObjectReference` remains the Face runtime identity path, while `LinkedPanel2DElementId` remains provenance/regeneration metadata only.

Manual verification steps:

1. Open a project with a Panel2D source document and a generated Face document.
2. Select the Face document with no Face element selected and confirm the Inspector shows Source Panel2D Document, Source Region, Generated Element Count, Last Regenerated, and workflow command guidance.
3. Run **File -> Open Source Panel2D** and confirm the source Panel2D tab is activated when it is open.
4. Close or do not open the source Panel2D, select the Face, run **File -> Regenerate Face**, and confirm the Output log reports that the source Panel2D could not be located.
5. Create a Face with a missing artwork/reel asset path or remove a runtime-linked element's machine reference, then regenerate or invoke the source-location workflow and confirm Output log diagnostics identify the missing data.

Next recommended phase:

- Phase 11A should be **Face Mask Layer Extraction MVP**, focused on generating and persisting one aligned Face mask layer before visual-fidelity work. Tray geometry remains deferred, and per-lamp extraction is only an implementation detail.

### Phase 11A - Face Mask Layer Extraction MVP - Complete

Purpose:

- generate a single monochrome `FaceMaskLayer` asset aligned to the Face artwork/source region;
- persist mask-layer metadata such as `SourcePanel2DDocumentId`, `SourceRegion`, extraction threshold, generated UTC timestamp, asset path, and useful per-lamp contribution metadata;
- preserve enough extraction metadata for future tray generation without making per-lamp extraction a runtime dependency.

Important boundaries:

- mask extraction precedes visual-fidelity work;
- the persisted model is one face-sized mask layer, not one mask asset per lamp;
- per-lamp extraction is an implementation detail used to build the union mask;
- tray geometry is intentionally deferred;
- do not implement glow, bloom, blur, emission rendering, runtime mask rendering, tray extraction, tray simulation, light leakage simulation, 3D preview, or Unity integration in Phase 11A.

Extraction direction:

```text
For each source lamp participating in Face generation:
    background/artwork pixels
    lamp-on pixels
    brightness delta
    threshold
    binary contribution mask
    union/max composite into the single face-sized FaceMaskLayer
```

The mask answers: **where can light escape?** Future tray geometry answers: **which bulb is responsible for the light?**

### Phase 11B - Face Mask Layer System - Complete

Completed as a tooling/metadata phase that makes the generated `FaceMaskLayer` a first-class Face asset without changing runtime rendering behavior.

Outcome:

- Face hierarchy now exposes the mask layer under a Layers group whenever a Face document contains `FaceMaskLayer` metadata;
- selecting the mask layer shows Inspector metadata for asset path, dimensions, source region, extraction threshold, generated timestamp, contribution count, source Panel2D document, workflow commands, and future renderer-consumption guidance;
- selecting the Face document also summarizes mask-layer metadata so masks are visible even before selecting the layer node;
- File menu validation can be run explicitly for the active Face document and reports diagnostics through the existing Output log;
- validation now covers missing mask metadata, missing mask asset paths/assets, invalid or mismatched mask dimensions, unreadable mask assets, and missing/incomplete contribution metadata;
- regeneration remains the supported UX for replacing generated mask layers in this phase and continues to replay source Panel2D metadata through the existing Face regeneration workflow.

Important boundaries preserved:

- no lamp glow, bloom, blur, emission rendering, runtime mask rendering, tray extraction/simulation, light leakage simulation, 3D preview, or Unity integration was added;
- Face Play View runtime state still resolves through `LinkedMachineObjectReference` -> `MachineRuntimeState`;
- mask contribution metadata remains provenance/regeneration/tray-preparation data, not a runtime identity contract.

Future renderer consumption contract:

```text
Face renderer
    -> load FaceDocument.FaceMaskLayer.AssetPath
    -> verify mask dimensions match FaceMaskLayer.Width/Height and the Face source region/artwork alignment
    -> sample the single aligned monochrome mask as an opacity/light-escape map in face/document space
    -> use Face runtime elements and their LinkedMachineObjectReference values for lamp/display/reel/button runtime state
    -> do not treat FaceMaskLayer.Contributions as per-lamp runtime masks
```

Renderers may use `FaceMaskLayer.Contributions` later for diagnostics, authoring overlays, or tray-generation preparation, but not to decide which runtime lamp is currently on. That responsibility remains with runtime elements/tray geometry and machine-object references.

Manual verification steps:

1. Open a project with a generated Face document that contains a mask layer.
2. Confirm the Hierarchy shows **Layers > Face Mask** and selecting it updates the Inspector.
3. Confirm the Inspector shows asset path, dimensions, source region, threshold, generated timestamp, contribution count, and renderer-consumption guidance.
4. Run **File > Validate Face** and confirm mask diagnostics are logged for missing assets, dimension mismatches, or missing contribution metadata.
5. Run **File > Regenerate Face** and confirm the mask layer metadata/assets are regenerated while existing Face Play View behavior remains unchanged.

Next recommended phase:

- Phase 11C should be **Lamp Visual Fidelity**, focused on mask-aware lamp presentation and renderer-side quality improvements while preserving the `MachineObjectReference` -> `MachineRuntimeState` runtime contract.

### Phase 11C - Lamp Visual Fidelity - In Progress

Phase 11C is being split into small renderer-only fidelity increments so runtime architecture remains unchanged.

#### Phase 11C1 - Mask-Aware Lamp Rendering - Complete

Purpose:

- consume the persisted `FaceMaskLayer` during Face Play View rendering;
- preserve the existing `FaceLampWindowElement.LinkedMachineObjectReference` -> `MachineRuntimeState` runtime-state contract;
- improve lamp physical accuracy by allowing only mask-open pixels to receive lamp illumination.

Implemented renderer consumption contract:

```text
Face Play View
    -> Face2DRenderer.Render(FaceDocumentModel, MachineRuntimeState, viewport)
        -> draw FaceArtworkElement artwork
        -> load FaceDocument.FaceMaskLayer.AssetPath as one aligned monochrome mask
        -> keep the mask layer invisible as a standalone layer
        -> draw FaceLampWindowElement illumination into an offscreen layer
        -> apply the mask luminance as the lamp layer alpha/light-escape map
        -> draw displays
        -> draw buttons
```

Runtime lamp rendering now consumes only:

- `FaceMaskLayer` for the aligned light-escape map;
- `FaceLampWindowElement` for lamp window placement and machine-object reference;
- `MachineRuntimeState` for lamp intensity.

Important boundaries preserved:

- per-lamp `FaceMaskLayer.Contributions` metadata remains generation/provenance/tray-preparation data and is not used for runtime lamp decisions;
- Panel2D rendering remains unchanged;
- Face Edit View mask hierarchy/Inspector behavior remains editable and inspectable;
- no bloom, post-processing, advanced glow, emission effects, tray simulation, light leakage simulation, 3D preview, or Unity integration was added.

Recommended next visual-fidelity phase:

- Phase 11C2 should be **Basic Lamp Falloff and Color Tuning**, limited to renderer-side quality improvements such as per-window radial falloff, simple color/intensity tuning, and optional authoring-safe diagnostics. It should continue to use the single `FaceMaskLayer` plus runtime elements and must still avoid bloom/post-processing, tray simulation, light leakage simulation, Unity integration, and any dependency on per-lamp extraction metadata.

#### Phase 11C2 - Basic Lamp Falloff and Color Tuning - Future

Goals:

- improve the simple solid-fill lamp illumination with low-risk renderer-side falloff/color tuning;
- keep runtime identity/state unchanged;
- keep all tray, light-leakage, bloom, post-processing, 3D, and Unity work deferred.

### Phase 11D - Display/Reel Visual Fidelity - Future

Goals:

- display and reel presentation improvements after the mask-layer foundation;
- improved window/material treatment for displays and reels;
- no runtime identity or document ownership changes unless separately planned.

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


## Runtime Display Architecture Summary

The current Face runtime-display architecture has three deliberately separate responsibilities.

### 1. Runtime identity and state

`MachineObjectReference` is the identity contract for runtime-linked visuals. Lamps, reels, seven-segment displays, alpha displays, and inputs/buttons are represented by typed machine references, while `MachineRuntimeState` stores renderer-neutral values such as lamp intensity, raw reel position, segment masks, segment brightness, and input state. Panel2D element IDs may still exist in state for legacy Panel2D rendering compatibility, but Face runtime behavior must resolve through machine references.

Reel state has a specific split:

- MAME adapters write the machine-reference reel value as a wrapped raw machine position in the 96-position runtime domain;
- Face reel rendering resolves a visual-effective position from that raw value using the Face reel element's stops, reversal, band offset, and the current platform;
- Panel2D continues to keep its legacy object-id keyed effective reel values for existing Panel2D behavior.

This split keeps Face from depending on Panel2D element IDs while preserving Panel2D compatibility.

### 2. Renderer-facing resolution

`FaceRuntimeStateResolver` is the Face renderer-facing adapter for runtime values. It validates that each Face element's `LinkedMachineObjectReference` has the expected kind, then reads the corresponding value from `MachineRuntimeState`. The resolver covers lamps, reels, seven-segment displays, and alpha displays. Buttons use the Face input-target resolver and shared play input router because their runtime behavior is command/input dispatch rather than visual display state.

Face display renderers reuse existing Skia primitives where possible:

- Face reels call the same reel-strip rendering primitive used by Panel2D reels;
- Face seven-segment displays call the same seven-segment rendering primitive used by Panel2D;
- Face alpha displays call the same alpha/segment rendering primitive used by Panel2D;
- Face lamps and buttons remain Face-specific simple MVP shapes until a later visual-fidelity phase explicitly changes presentation.

### 3. Update and invalidation fanout

MAME adapters update `MachineRuntimeState` on the UI thread, then notify the visual surfaces that need repainting. Panel2D invalidation still uses Panel2D object IDs. Face invalidation uses Face element object IDs discovered from machine references through the shared Face runtime display reference index. This keeps invalidation consistent across reels, seven-segment displays, and alpha displays and prevents accidental dependency on `LinkedPanel2DElementId`.

Current taxonomy:

| Runtime category | Panel2D visual | Face visual | Runtime reference | Runtime/display notes |
| --- | --- | --- | --- | --- |
| Lamp | `PanelElementKind.Lamp` | `FaceLampWindowElement` | `MachineObjectKind.Lamp` | Resolver reads lamp intensity from `MachineRuntimeState`. |
| Button/input | input-linked Panel2D visual plus input definition | `FaceButtonElement` | `MachineObjectKind.Input` / `MachineInputReference` | Uses shared play input dispatch; not a display renderer. |
| Reel | `PanelElementKind.Reel` | `FaceReelDisplayElement` | `MachineObjectKind.Reel` | Machine state is raw 96-step position; Face derives visual-effective position from Face metadata and platform. |
| Seven segment | `PanelElementKind.SevenSegment` | `FaceSevenSegmentDisplayElement` | `MachineObjectKind.SevenSegmentDisplay` | Resolver reads one mask/brightness cell from `MachineRuntimeState`. |
| Alpha display | `PanelElementKind.Alpha` | `FaceAlphaDisplayElement` | `MachineObjectKind.AlphaDisplay` | Resolver reads 16 mask/brightness cells from `MachineRuntimeState`; display geometry remains visual metadata. |

### Consolidation guidance

Future work should continue consolidating around these seams instead of adding per-display special cases:

- add runtime-linked Face display invalidation through the shared reference index;
- add renderer-facing runtime reads through `FaceRuntimeStateResolver`;
- keep serialization/generation metadata on the Face element models, but keep runtime identity in `LinkedMachineObjectReference`;
- keep `LinkedPanel2DElementId` as provenance only, especially for Phase 10 regeneration;
- avoid changing visual fidelity, animation behavior, or renderer ownership during architecture/cleanup phases.

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
- Phase 9B: complete - alpha displays serialize, generate, and render from `MachineRuntimeState` through machine-object references;
- Phase 9C: complete - reel displays serialize, generate, and render from `MachineRuntimeState` through machine-object references, with correctness prioritized over animation fidelity;
- Phase 9D: complete - runtime display resolution and invalidation are consolidated/documented without adding display types or visual-fidelity behavior;
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

## Manual Verification Steps - Phase 9D Runtime Display Consolidation

1. Open an Oasis project containing a Panel2D document with lamps, buttons/inputs, reels, seven-segment displays, and alpha displays.
2. Open or generate a Face document that contains Face lamp windows, buttons, reel displays, seven-segment displays, and alpha displays linked by machine-object references.
3. Start MAME for the machine and confirm each Face display updates live from runtime state: lamps change intensity, reels move, seven-segment masks update, and alpha/VFD masks and brightness update.
4. Confirm the corresponding Panel2D displays continue updating exactly as before.
5. Confirm Face buttons still dispatch pointer down/up through the shared play input path and that keyboard shortcuts still work in Face Play View.
6. Save, close, and reopen the Face document and confirm runtime-linked display elements still contain their machine references.
7. Inspect Face runtime-linked elements and confirm `LinkedPanel2DElementId` is present only as provenance/source metadata; deleting or changing provenance-only links should not be required for live runtime display updates when machine references are valid.
8. Confirm no new display visual effects, Face Regeneration behavior, 3D preview, or Unity integration behavior appears in this phase.

## Manual Verification Steps - Phase 9C Reel Display MVP

1. Open an Oasis project containing a Panel2D document with at least one reel element that has a valid display/reel number.
2. Generate a Face document from a region that fully contains the Panel2D reel.
3. Confirm the generated Face hierarchy includes a Reel Displays group and a generated reel display element.
4. Save the Face document, close it, reopen it, and confirm the reel display persists with its machine reference (`reel:<number>`) and provenance-only `LinkedPanel2DElementId`.
5. Open the Face document in Face Edit View and confirm the reel window renders as either the configured reel strip image or the MVP placeholder strip, with the same platform/stop offset alignment as the source Panel2D reel.
6. Open the same Face document in Face Play View and confirm the same reel display renders.
7. Start MAME for the machine and confirm reel output changes move/update the Face reel display live and remains visually aligned with the corresponding Panel2D reel position for the selected fruit-machine platform and reel stop count.
8. Confirm lamps, inputs/buttons, seven-segment displays, and alpha displays still update exactly as they did before Phase 9C.
9. Inspect a generated reel display and confirm runtime behavior still resolves through `MachineObjectReference.Reel` and `MachineRuntimeState`; `LinkedPanel2DElementId` should only identify the source Panel2D element for provenance/editor workflows.

## Runtime Display Consolidation Status Before Face Regeneration

Phase 9D completed the low-risk consolidation that should happen before Face Regeneration. Completed items:

- Face renderer-facing runtime display resolution is centralized in `FaceRuntimeStateResolver` for lamps, reels, seven-segment displays, and alpha displays;
- Face visual invalidation by machine reference is centralized through a lightweight reference-index helper used by reel and segment MAME adapters;
- the reel runtime contract is documented: machine-reference reel positions are raw wrapped 96-position values, and Face elements derive their visual-effective position from element metadata plus platform;
- regression coverage verifies that the Face reference-index path ignores provenance-only `LinkedPanel2DElementId` links.

Remaining recommendations for Phase 10 and later:

- keep Face Regeneration correlation based on provenance metadata, but preserve runtime identity through `LinkedMachineObjectReference`;
- consider centralizing Face display metadata serialization/generation patterns only if Phase 10 touches the same storage/generation fields repeatedly;
- keep visual fidelity, 3D preview, and Unity-facing changes out of Phase 10 unless a later plan explicitly requests them.
